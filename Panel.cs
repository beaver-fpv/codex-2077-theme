using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Codex2077
{
    public sealed class Panel
    {
        public readonly Window Window;
        private readonly DispatcherTimer timer;
        private readonly List<string> records = new List<string>();
        private Snapshot current;
        private bool busy;
        private bool fillingTargets;
        private bool closed;
        private string selectedTarget;
        private bool fillingLanguage;
        private string busyTitle;
        private string operationError;
        private Exception readError;

        public Panel(bool monitor) : this(monitor, null) { }

        public Panel(bool monitor, string settingsDirectory)
        {
            Localization.Initialize(settingsDirectory);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Panel.xaml"))
                Window = (Window)XamlReader.Load(stream);
            Control<ComboBox>("LanguagePicker").SelectionChanged += (s, e) =>
            {
                if (fillingLanguage) return;
                var item = Control<ComboBox>("LanguagePicker").SelectedItem as ComboBoxItem;
                if (item != null) ChangeLanguage((string)item.Tag);
            };
            Button("RefreshButton").Click += async (s, e) => await Refresh(true);
            Button("CheckButton").Click += async (s, e) => await Operate("检查主题文件", Backend.CheckAsync);
            Button("OnButton").Click += async (s, e) => await Operate("开启主题", () => Backend.ApplyAsync(selectedTarget));
            Button("OffButton").Click += async (s, e) => await Operate("关闭主题", () => Backend.RemoveAsync(selectedTarget));
            Button("LaunchButton").Click += async (s, e) => await Operate("启动主题连接", Backend.LaunchAsync);
            Control<ComboBox>("TargetPicker").SelectionChanged += async (s, e) =>
            {
                if (fillingTargets || busy) return;
                TargetInfo target = Control<ComboBox>("TargetPicker").SelectedItem as TargetInfo;
                selectedTarget = target == null ? null : target.Id;
                await Refresh(true);
            };
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            timer.Tick += async (s, e) => { if (!busy && Window.IsActive) await Refresh(false); };
            Window.Closed += (s, e) => { closed = true; timer.Stop(); };
            if (monitor) Window.Loaded += async (s, e) => { await Refresh(true); timer.Start(); };
            ApplyLanguage();
        }

        private T Control<T>(string name) where T : FrameworkElement { return (T)Window.FindName(name); }
        private Button Button(string name) { return Control<Button>(name); }
        private void Text(string name, string value) { Control<TextBlock>(name).Text = Localization.Text(value); }

        public void ChangeLanguage(string language)
        {
            Localization.SelectLanguage(language);
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Window.Title = Localization.Text("2077 主题控制台");
            Window.Language = System.Windows.Markup.XmlLanguage.GetLanguage(Localization.Language);
            foreach (var pair in new Dictionary<string, string> {
                {"TitleText", "2077 主题控制台"}, {"SubtitleText", "深潜协议 · Codex 桌面外观"},
                {"LanguageLabel", "语言"}, {"Footer", "主题保留在当前 Codex 窗口，重启后可在这里重新开启。"},
                {"TargetLabel", "选择要操作的 Codex 窗口"}, {"ConnectionLabel", "Codex 连接"},
                {"VersionLabel", "应用版本"}, {"ThemeLabel", "主题状态"}, {"RuntimeLabel", "运行环境"},
                {"LogLabel", "运行记录"} }) Text(pair.Key, pair.Value);
            foreach (var pair in new Dictionary<string, string> {
                {"RefreshButton", "刷新状态"}, {"OnButton", "开启 2077 主题"}, {"OffButton", "关闭主题"},
                {"LaunchButton", "启动主题连接"}, {"CheckButton", "检查主题文件"} })
                Button(pair.Key).Content = Localization.Text(pair.Value);
            foreach (var pair in new Dictionary<string, string> {
                {"RefreshButton", "刷新当前连接和主题状态"}, {"LanguagePicker", "选择界面语言"},
                {"TargetPicker", "要操作的 Codex 窗口"}, {"LogBox", "操作时间和运行结果"},
                {"DiagnosticBox", "连接地址及错误详情"} })
                System.Windows.Automation.AutomationProperties.SetName(Control<FrameworkElement>(pair.Key), Localization.Text(pair.Value));
            Control<Expander>("DiagnosticExpander").Header = Localization.Text("查看诊断详情");
            Text("LanguageNotice", Localization.PreferenceNotice);
            Control<TextBlock>("LanguageNotice").Visibility = String.IsNullOrEmpty(Localization.PreferenceNotice) ? Visibility.Collapsed : Visibility.Visible;
            fillingLanguage = true;
            Control<ComboBox>("LanguagePicker").SelectedIndex = Localization.Language == Localization.English ? 1 : 0;
            fillingLanguage = false;
            RenderState();
            RenderRecords();
        }

        public void Present(Snapshot state)
        {
            current = state;
            readError = null;
            operationError = null;
            RenderState();
        }

        private void RenderState()
        {
            var state = current ?? new Snapshot {
                Headline = "正在读取状态", Detail = "检查 Codex 连接、主题状态和本机运行环境。",
                Connection = "正在检查", Version = "正在检查", NodeVersion = "正在检查"
            };
            Text("Headline", state.Headline);
            Text("Detail", state.Detail);
            Text("ConnectionValue", state.Connection);
            Text("VersionValue", state.Version);
            Text("ThemeValue", state.Theme);
            Text("NodeValue", state.NodeVersion);
            Text("StatusTag", current == null ? "检测中" : state.Installed && state.Connected ? "已开启" : state.Connected ? "已连接" : "待连接");
            Control<TextBlock>("StatusTag").Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(state.Connected ? "#5DE4E1" : "#FCA9AD"));
            Control<TextBox>("DiagnosticBox").Text = (state.Endpoint ?? "") + Environment.NewLine + Localization.Text(state.Technical);
            fillingTargets = true;
            var picker = Control<ComboBox>("TargetPicker");
            if (state.Targets == null || !state.Targets.Any(t => t.Id == selectedTarget)) selectedTarget = null;
            picker.ItemsSource = null;
            picker.ItemsSource = state.Targets;
            picker.SelectedItem = state.Targets == null ? null : state.Targets.FirstOrDefault(t => t.Id == selectedTarget);
            Control<StackPanel>("TargetPanel").Visibility = state.Targets != null && state.Targets.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            fillingTargets = false;
            if (readError != null)
            {
                Text("Headline", "暂时无法确认状态");
                Text("StatusTag", "读取失败");
                Text("Detail", readError.Message);
                Text("ConnectionValue", "读取失败");
                Text("ThemeValue", "尚未确认");
                Control<TextBox>("DiagnosticBox").Text = Localization.Text(readError.ToString());
            }
            if (busy && busyTitle != null)
            {
                Text("StatusTag", "处理中");
                Text("Detail", Localization.Format("{0}，正在等待实际结果……", Localization.Text(busyTitle)));
            }
            if (operationError != null)
            {
                Text("Detail", operationError.Split(new[] { "\r\n\r\n" }, StringSplitOptions.None)[0]);
                Control<TextBox>("DiagnosticBox").Text += Environment.NewLine + Localization.Text(operationError);
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            Button("RefreshButton").IsEnabled = !busy;
            Button("CheckButton").IsEnabled = !busy;
            Button("OnButton").IsEnabled = !busy && current != null && current.CanApply;
            Button("OffButton").IsEnabled = !busy && current != null && current.CanRemove;
            Button("LaunchButton").IsEnabled = !busy && current != null && current.CanLaunch;
            Control<ComboBox>("TargetPicker").IsEnabled = !busy;
        }

        public void Log(string message)
        {
            LogAt(DateTime.Now, message);
        }

        internal void LogAt(DateTime time, string message)
        {
            records.Add(time.ToString("HH:mm:ss") + "  " + message);
            if (records.Count > 50) records.RemoveAt(0);
            RenderRecords();
        }

        private void RenderRecords()
        {
            var box = Control<TextBox>("LogBox");
            box.Text = String.Join(Environment.NewLine, records.Select(Localization.Text));
            box.ScrollToEnd();
        }

        private async Task Refresh(bool announce)
        {
            if (busy || closed) return;
            busy = true;
            UpdateButtons();
            try
            {
                Snapshot state = await Backend.InspectAsync(selectedTarget);
                if (closed) return;
                bool changed = current == null || state.Headline != current.Headline || state.Theme != current.Theme;
                Present(state);
                if (announce || changed) Log(state.Headline + " · " + state.Connection);
            }
            catch (Exception error) { if (!closed) ShowError(error); }
            finally { busy = false; if (!closed) UpdateButtons(); }
        }

        private async Task Operate(string title, Func<Task<string>> action)
        {
            if (busy || closed) return;
            busy = true;
            busyTitle = title;
            operationError = null;
            UpdateButtons();
            Text("StatusTag", "处理中");
            Text("Detail", Localization.Format("{0}，正在等待实际结果……", Localization.Text(title)));
            Log(title + "……");
            string errorText = null;
            try { string result = await action(); if (!closed) Log(result); }
            catch (Exception error) { errorText = error.Message; if (!closed) Log("未完成 · " + error.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()); }
            try
            {
                if (!closed)
                {
                    try { Present(await Backend.InspectAsync(selectedTarget)); }
                    catch (Exception error) { ShowError(error); }
                    if (errorText != null)
                    {
                        operationError = errorText;
                    }
                }
            }
            finally { busy = false; busyTitle = null; if (!closed) RenderState(); }
        }

        private void ShowError(Exception error)
        {
            current = null;
            readError = error;
            Log("读取失败 · " + error.Message);
            RenderState();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Localization.Initialize(null);
            bool created;
            using (var mutex = new Mutex(true, "Local\\Codex2077Panel", out created))
            {
                if (!created) { MessageBox.Show(Localization.Text("2077 主题控制台已经打开，请从任务栏切回。"), Localization.Text("2077 主题控制台")); return; }
                try
                {
                    var app = new Application();
                    app.DispatcherUnhandledException += (s, e) => { MessageBox.Show(Localization.Text(e.Exception.Message), Localization.Text("操作未完成")); e.Handled = true; };
                    app.Run(new Panel(true).Window);
                }
                catch (Exception error) { MessageBox.Show(Localization.Text(error.Message), Localization.Text("2077 主题控制台无法启动")); }
            }
        }
    }
}
