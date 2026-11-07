using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Codex2077
{
    public static class VerifyUi
    {
        private static int checks;
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }
        private static T Control<T>(Panel panel, string name) where T : FrameworkElement { return (T)panel.Window.FindName(name); }

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verification");
            Directory.CreateDirectory(output);
            var fixture = Path.Combine(output, "language-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixture);
            app.Startup += (s, e) =>
            {
                try
                {
                    var panel = new Panel(false, fixture);
                    Assert(Localization.Language == Localization.Chinese, "First run must default to Chinese");
                    Assert(!File.Exists(Path.Combine(fixture, "settings.json")), "Opening the panel must not write settings");
                    var demo = new Snapshot {
                        Headline = "2077 主题已开启",
                        Detail = "红黑面板、青色高亮和额度状态条已注入当前窗口。点击关闭可恢复原来的外观。",
                        Connection = "已连接 · 仅本机", Theme = "2077 主题已开启",
                        Version = "Microsoft Store · 正式版", NodeVersion = "Node.js 22+",
                        Endpoint = "127.0.0.1:9341",
                        Technical = "主题状态\r\n{\"installed\":true,\"demo\":true}",
                        Connected = true, Installed = true, CanRemove = true
                    };
                    panel.Present(demo);
                    panel.LogAt(new DateTime(2026, 1, 1, 12, 0, 0), "2077 主题已开启，实际窗口状态已确认。");
                    panel.LogAt(new DateTime(2026, 1, 1, 12, 0, 1), "主题文件校验通过 · Node.js 22+");
                    Capture(panel, 880, 780, Path.Combine(output, "panel-demo-zh.png"));
                    Assert(!Control<Button>(panel, "OnButton").IsEnabled && Control<Button>(panel, "OffButton").IsEnabled, "Enabled-state controls inconsistent");
                    Control<ComboBox>(panel, "LanguagePicker").SelectedIndex = 1;
                    Assert(Localization.Language == Localization.English, "Language picker did not change language");
                    Assert(Control<TextBlock>(panel, "Headline").Text == "2077 theme enabled", "Snapshot headline did not update immediately");
                    Assert(Control<TextBox>(panel, "LogBox").Text.Contains("Theme files verified"), "Existing log entries did not translate");
                    Assert(Control<TextBox>(panel, "DiagnosticBox").Text.Contains("Theme status"), "Diagnostic headings did not translate");
                    Assert(Control<TextBox>(panel, "DiagnosticBox").Text.Contains("{\"installed\":true,\"demo\":true}"), "Raw diagnostics were altered");
                    Assert(Control<Button>(panel, "OffButton").IsEnabled && !Control<Button>(panel, "OnButton").IsEnabled, "Language switch changed theme actions");
                    Capture(panel, 880, 780, Path.Combine(output, "panel-demo-en.png"));
                    Capture(panel, 720, 640, Path.Combine(output, "panel-demo-en-compact.png"));
                    Assert(Control<ComboBox>(panel, "LanguagePicker").ActualWidth >= 100, "Language picker was clipped");
                    Assert(Control<Button>(panel, "OnButton").ActualWidth > 150, "English enable button was clipped");
                    Assert(Control<TextBlock>(panel, "Headline").ActualWidth > 300, "English heading has insufficient space");

                    Localization.Initialize(fixture);
                    Assert(Localization.Language == Localization.English, "Language choice did not survive reload");
                    File.WriteAllText(Path.Combine(fixture, "settings.json"), "{\"language\":\"en\",\"futureSetting\":123}");
                    Assert(Localization.SelectLanguage(Localization.Chinese), "Could not save Chinese preference");
                    Assert(File.ReadAllText(Path.Combine(fixture, "settings.json")).Contains("futureSetting"), "Saving language removed unrelated settings");
                    panel.ChangeLanguage(Localization.Chinese);
                    Assert(Control<TextBlock>(panel, "Headline").Text == "2077 主题已开启", "Switching back lost the canonical headline");
                    Assert(Control<TextBox>(panel, "LogBox").Text.Contains("主题文件校验通过"), "Switching back lost canonical log messages");
                    Assert(Control<TextBox>(panel, "LogBox").Text.Split('\n').Length == 2, "Switching language duplicated log entries");

                    typeof(Panel).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
                    typeof(Panel).GetField("busyTitle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, "开启主题");
                    panel.ChangeLanguage(Localization.English);
                    Assert(Control<TextBlock>(panel, "StatusTag").Text == "Working", "Busy status did not translate");
                    Assert(Control<TextBlock>(panel, "Detail").Text == "Enable theme. Waiting for the result…", "Busy explanation did not translate");
                    Assert(!Control<Button>(panel, "OffButton").IsEnabled && Control<ComboBox>(panel, "LanguagePicker").IsEnabled, "Language switch did not preserve busy guards");
                    typeof(Panel).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, false);
                    typeof(Panel).GetField("busyTitle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, null);
                    typeof(Panel).GetField("operationError", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, "主题文件校验失败，请重新获取完整主题资源。");
                    panel.ChangeLanguage(Localization.Chinese);
                    Assert(Control<TextBlock>(panel, "Detail").Text == "主题文件校验失败，请重新获取完整主题资源。", "Operation failure was lost on language switch");
                    panel.ChangeLanguage(Localization.English);
                    Assert(Control<TextBlock>(panel, "Detail").Text == "Theme file verification failed. Download the complete theme resources again.", "Operation failure did not translate");

                    panel.Present(new Snapshot { Headline = "尚未连接 Codex", CanLaunch = true });
                    Assert(!Control<Button>(panel, "OnButton").IsEnabled && !Control<Button>(panel, "OffButton").IsEnabled, "Disconnected state enabled theme mutation");
                    Assert(Control<Button>(panel, "LaunchButton").IsEnabled, "Launch availability changed");
                    panel.Present(new Snapshot { Connection = "Codex 已运行 · 尚未连接" });
                    Assert(!Control<Button>(panel, "LaunchButton").IsEnabled, "Running app enabled launch");
                    panel.ChangeLanguage(Localization.English);
                    typeof(Panel).GetMethod("ShowError", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { new InvalidOperationException("操作超时，请刷新状态后重试。") });
                    Assert(Control<TextBlock>(panel, "Detail").Text == "The operation timed out. Refresh and try again.", "Error detail did not translate");
                    panel.ChangeLanguage(Localization.Chinese);
                    Assert(Control<TextBlock>(panel, "Detail").Text == "操作超时，请刷新状态后重试。", "Error did not translate back");

                    File.WriteAllText(Path.Combine(fixture, "settings.json"), "invalid json");
                    Localization.Initialize(fixture);
                    Assert(Localization.Language == Localization.Chinese && Localization.PreferenceNotice.Length > 0, "Invalid settings must fall back gracefully");
                    File.WriteAllText(Path.Combine(fixture, "settings.json"), "{\"language\":\"unknown\"}");
                    Localization.Initialize(fixture);
                    Assert(Localization.Language == Localization.Chinese, "Unknown language must default to Chinese");
                    File.WriteAllText(Path.Combine(fixture, "settings.json"), "{\"language\":\"zh-CN\"}");
                    File.SetAttributes(Path.Combine(fixture, "settings.json"), FileAttributes.ReadOnly);
                    panel.ChangeLanguage(Localization.English);
                    Assert(Localization.Language == Localization.English && Localization.PreferenceNotice.Length > 0, "Read-only settings must allow a session-only language change");
                    Assert(Control<TextBlock>(panel, "LanguageNotice").Visibility == Visibility.Visible && Control<TextBlock>(panel, "LanguageNotice").Text.Contains("could not be saved"), "Read-only fallback must be visible and localized");
                    Assert(File.ReadAllText(Path.Combine(fixture, "settings.json")).Contains("zh-CN"), "Read-only preference was overwritten");
                    File.SetAttributes(Path.Combine(fixture, "settings.json"), FileAttributes.Normal);
                    Assert(Localization.Text(@"D:\主题状态\未找到\file.txt") == @"D:\主题状态\未找到\file.txt", "Local paths must remain unchanged");
                    Assert(Localization.Text(@"    at helper (D:\主题状态\未找到\file.js:10)") == @"    at helper (D:\主题状态\未找到\file.js:10)", "Paths embedded in stack traces must remain unchanged");
                    Assert(Localization.Text("{\"主题状态\":\"已开启\"}") == "{\"主题状态\":\"已开启\"}", "JSON data must remain unchanged");
                    Assert(new TargetInfo { Id = "demotarget", Title = "主题状态 · demotarg" }.ToString() == "主题状态 · demotarg", "User window titles must remain unchanged");
                    Assert(new TargetInfo { Id = "demotarget", HasDefaultTitle = true }.ToString() == "Codex main window · demotarg", "Default target label did not translate");

                    // Every owned Chinese backend literal must have an English rendering.
                    foreach (string line in File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backend.cs")))
                    foreach (Match match in Regex.Matches(line, "\"(?:[^\"\\\\]|\\\\.)*\""))
                    {
                        if (!Regex.IsMatch(match.Value, @"[\p{IsCJKUnifiedIdeographs}]")) continue;
                        string literal = Regex.Unescape(match.Value.Substring(1, match.Value.Length - 2));
                        if (Regex.IsMatch(literal, @"[\p{IsCJKUnifiedIdeographs}]"))
                            Assert(!Regex.IsMatch(Localization.Text(literal), @"[\p{IsCJKUnifiedIdeographs}]"), "Missing backend translation: " + literal);
                    }
                    File.WriteAllText(Path.Combine(output, "ui-check.txt"), "PASS: " + checks + " isolated checks. Chinese/English controls, immediate language switching of synthetic state/log/errors, diagnostics, portable preference reload, corrupt/unknown/read-only settings, action guards, and normal/compact rendering.\r\nScreenshots use synthetic demo data only. No live Codex inspection, theme injection, launch, or restart performed.");
                    Environment.ExitCode = 0;
                }
                catch (Exception error) { File.WriteAllText(Path.Combine(output, "ui-check.txt"), error.ToString()); Environment.ExitCode = 1; }
                finally
                {
                    // Delete only files created in this unique, verified verification subdirectory.
                    if (Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(output) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string file in Directory.GetFiles(fixture)) { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); }
                        Directory.Delete(fixture);
                    }
                    app.Shutdown();
                }
            };
            app.Run();
        }

        private static void Capture(Panel panel, int width, int height, string path)
        {
            var content = (FrameworkElement)panel.Window.Content;
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var context = background.RenderOpen()) context.DrawRectangle(panel.Window.Background, null, new Rect(0, 0, width, height));
            bitmap.Render(background);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
