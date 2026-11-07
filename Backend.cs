using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Codex2077
{
    public sealed class TargetInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool HasDefaultTitle { get; set; }
        public override string ToString()
        {
            // Real window titles belong to the user; only our fallback label is translated.
            return HasDefaultTitle ? Localization.Text("Codex 主窗口") + " · " + Id.Substring(0, Math.Min(8, Id.Length)) : Title;
        }
    }

    public sealed class Snapshot
    {
        public string Headline { get; set; }
        public string Detail { get; set; }
        public string Connection { get; set; }
        public string Theme { get; set; }
        public string Version { get; set; }
        public string NodeVersion { get; set; }
        public string Endpoint { get; set; }
        public string Technical { get; set; }
        public bool Connected { get; set; }
        public bool Installed { get; set; }
        public bool CanLaunch { get; set; }
        public bool CanApply { get; set; }
        public bool CanRemove { get; set; }
        public IList<TargetInfo> Targets { get; set; }
        public Snapshot()
        {
            Headline = "正在检查"; Detail = ""; Connection = "未连接";
            Theme = "尚未确认"; Version = "未找到"; NodeVersion = "未找到";
            Endpoint = "127.0.0.1:9341"; Technical = "";
            Targets = new List<TargetInfo>();
        }
    }

    public static class Backend
    {
        const int Port = 9341;
        static readonly SemaphoreSlim Operation = new SemaphoreSlim(1, 1);
        static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string Script = Path.Combine(Root, "Theme", "scripts", "theme.mjs");

        sealed class Install
        {
            public string Executable;
            public string Version;
            public bool Beta;
        }

        sealed class NodeRuntime
        {
            public string Path;
            public string Version;
        }

        sealed class ProcessResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }

        sealed class PortState
        {
            public bool Listening;
            public string Error;
            public Install Owner;
        }

        sealed class RunningState
        {
            public bool Running;
            public bool Unknown;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandle(IntPtr file, StringBuilder path, uint length, uint flags);

        public static async Task<Snapshot> InspectAsync(string targetId)
        {
            await Operation.WaitAsync();
            try { return await InspectCoreAsync(targetId); }
            finally { Operation.Release(); }
        }

        static async Task<Snapshot> InspectCoreAsync(string targetId)
        {
            var state = new Snapshot();
            try
            {
                var installs = ReadInstalls();
                var primary = installs.FirstOrDefault(i => !i.Beta) ?? installs.FirstOrDefault();
                if (primary == null)
                {
                    state.Headline = "未找到 Codex";
                    state.Detail = "请先安装并打开一次 Microsoft Store 版 Codex / ChatGPT，再刷新状态。";
                    return state;
                }
                state.Version = primary.Version + (primary.Beta ? " · Beta" : " · 正式版");
                state.Technical = "应用位置\r\n" + primary.Executable;
                NodeRuntime node = null;
                try
                {
                    node = await FindNodeAsync();
                    state.NodeVersion = node.Version;
                    state.Technical += "\r\n\r\nNode\r\n" + node.Path;
                }
                catch (Exception ex) { state.Technical += "\r\n\r\n" + ex.Message; }

                var port = await InspectPortAsync(installs);
                if (port.Error != null)
                {
                    state.Headline = "连接需要处理";
                    state.Detail = port.Error;
                    state.Connection = "连接验证失败";
                    return state;
                }
                if (!port.Listening)
                {
                    var running = ReadRunning(installs);
                    state.Headline = running.Running ? "等待主题连接" : "尚未连接 Codex";
                    state.Detail = running.Running
                        ? "Codex 已运行，但主题连接尚未开启。请完成当前工作并从应用菜单退出 Codex，然后点击“启动主题连接”。"
                        : "点击“启动主题连接”打开 Codex，连接成功后即可开启 2077 主题。";
                    if (running.Unknown)
                        state.Detail = "无法确认 Codex 是否仍在运行。请从应用菜单退出 Codex，再刷新状态。";
                    state.CanLaunch = !running.Running && !running.Unknown && node != null;
                    state.Connection = running.Running ? "Codex 已运行 · 尚未连接" : "等待启动";
                    if (node == null)
                    {
                        state.Headline = "需要 Node.js 22 或更新版本";
                        state.Detail = "安装 Node.js 后重新打开此面板；诊断详情中可查看检测结果。";
                    }
                    return state;
                }

                var targets = await ReadTargetsAsync();
                if (port.Owner != null)
                {
                    state.Version = port.Owner.Version + (port.Owner.Beta ? " · Beta" : " · 正式版");
                    state.Technical += "\r\n\r\n连接所属应用\r\n" + port.Owner.Executable;
                }
                state.Targets = targets;
                state.Connected = true;
                state.Connection = "已连接 · 仅本机";
                var selected = SelectTarget(targets, targetId);
                if (selected == null)
                {
                    state.Headline = targets.Count == 0 ? "等待 Codex 主窗口" : "请选择一个 Codex 窗口";
                    state.Detail = targets.Count == 0
                        ? "连接已就绪，等待 Codex 主窗口加载后请刷新。"
                        : "检测到多个主窗口。选中要换肤的窗口后，面板会读取该窗口的实际主题状态。";
                    return state;
                }
                if (node == null)
                {
                    state.Headline = "需要 Node.js 22 或更新版本";
                    state.Detail = "连接已就绪，安装 Node.js 后重新打开面板即可控制主题。";
                    return state;
                }
                var status = await ThemeCommandAsync("status", selected.Id, node);
                state.Installed = Bool(status, "installed");
                var baseTheme = Text(status, "baseTheme");
                state.Theme = state.Installed ? "2077 主题已开启" : "2077 主题已关闭";
                state.CanRemove = state.Installed;
                state.CanApply = !state.Installed && (baseTheme.Length == 0 || baseTheme == "deep-dive-protocol");
                state.Headline = state.Installed ? "2077 主题已开启" : "可以开启 2077 主题";
                state.Detail = state.Installed
                    ? "红黑面板、青色高亮和额度状态条已注入当前窗口。点击关闭可恢复原来的外观。"
                    : "主题连接正常。点击开启，即可把当前窗口切换为深潜协议风格。";
                if (!state.Installed && !state.CanApply)
                {
                    state.Headline = "其他主题正在使用中";
                    state.Detail = "请先从原来的换肤工具关闭现有主题，然后刷新状态。";
                }
                state.Technical += "\r\n\r\n窗口 ID\r\n" + selected.Id
                    + "\r\n\r\n主题状态\r\n" + Json().Serialize(status);
            }
            catch (Exception ex)
            {
                state.Headline = "暂时无法读取状态";
                state.Detail = FriendlyError(ex.Message);
                state.Connection = "状态未确认";
                state.Theme = "尚未确认";
                state.Connected = false;
                state.Installed = false;
                state.CanLaunch = state.CanApply = state.CanRemove = false;
                state.Technical += "\r\n\r\n" + ex.Message;
            }
            return state;
        }

        public static Task<string> ApplyAsync(string targetId) { return ChangeAsync("apply", targetId); }
        public static Task<string> RemoveAsync(string targetId) { return ChangeAsync("remove", targetId); }

        static async Task<string> ChangeAsync(string action, string targetId)
        {
            await Operation.WaitAsync();
            try
            {
                var installs = ReadInstalls();
                var port = await InspectPortAsync(installs);
                if (port.Error != null) throw new InvalidOperationException(port.Error);
                if (!port.Listening) throw new InvalidOperationException("主题连接已断开，请刷新状态并重新连接 Codex。");
                var targets = await ReadTargetsAsync();
                var selected = SelectTarget(targets, targetId);
                if (selected == null) throw new InvalidOperationException("请选择一个可用的 Codex 主窗口，再操作主题开关。");
                var node = await FindNodeAsync();
                await ThemeCommandAsync(action, selected.Id, node);
                // Read back the renderer state; command completion alone is not proof.
                port = await InspectPortAsync(installs);
                if (!port.Listening || port.Error != null)
                    throw new InvalidOperationException("操作后连接中断，主题最终状态未确认。请重新连接并刷新。" );
                var verified = await ThemeCommandAsync("status", selected.Id, node);
                if (Bool(verified, "installed") != (action == "apply"))
                    throw new InvalidOperationException("主题操作已执行，但实际状态未通过确认。请刷新查看当前状态。");
                return action == "apply" ? "2077 主题已开启，实际窗口状态已确认。" : "2077 主题已关闭，实际窗口状态已确认。";
            }
            catch (Exception ex) { throw new InvalidOperationException(FriendlyError(ex.Message) + "\r\n\r\n" + ex.Message, ex); }
            finally { Operation.Release(); }
        }

        public static async Task<string> CheckAsync()
        {
            await Operation.WaitAsync();
            try
            {
                var node = await FindNodeAsync();
                var result = await ThemeCommandAsync("check", null, node);
                if (!Bool(result, "ok")) throw new InvalidOperationException("主题文件校验没有通过。");
                return "主题文件校验通过 · " + node.Version + "\r\nSHA-256  " + Text(result, "sha");
            }
            finally { Operation.Release(); }
        }

        public static async Task<string> LaunchAsync()
        {
            await Operation.WaitAsync();
            try
            {
                var installs = ReadInstalls();
                var install = installs.FirstOrDefault(i => !i.Beta) ?? installs.FirstOrDefault();
                if (install == null) throw new InvalidOperationException("未找到可启动的 Microsoft Store 版 Codex。");
                await FindNodeAsync();
                var port = await InspectPortAsync(installs);
                if (port.Error != null) throw new InvalidOperationException(port.Error);
                if (port.Listening) return "主题连接已就绪，请刷新并选择窗口。";
                var running = ReadRunning(installs);
                if (running.Running || running.Unknown)
                    throw new InvalidOperationException("请先完成当前工作，从应用菜单退出 Codex，然后再次点击“启动主题连接”。");
                var info = new ProcessStartInfo(install.Executable, "--remote-debugging-address=127.0.0.1 --remote-debugging-port=9341");
                info.UseShellExecute = false;
                info.WorkingDirectory = Path.GetDirectoryName(install.Executable);
                using (var started = Process.Start(info))
                {
                    if (started == null) throw new InvalidOperationException("Windows 没有返回 Codex 启动结果。");
                }
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(750);
                    port = await InspectPortAsync(installs);
                    if (port.Error != null) throw new InvalidOperationException(port.Error);
                    if (port.Listening)
                    {
                        try
                        {
                            if ((await ReadTargetsAsync()).Count > 0)
                                return "Codex 主题连接已就绪，可以开启 2077 主题。";
                        }
                        catch (WebException) { }
                    }
                }
                throw new InvalidOperationException("Codex 已尝试启动，但 30 秒内没有开放主题连接。此版本可能限制调试入口。请查看 Codex 是否已经打开，并刷新重试；诊断详情中可查看版本信息。");
            }
            finally { Operation.Release(); }
        }

        static List<Install> ReadInstalls()
        {
            const string keyPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            var result = new List<Install>();
            using (var packages = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                if (packages == null) return result;
                foreach (string name in packages.GetSubKeyNames())
                {
                    if (!Regex.IsMatch(name, @"^OpenAI\.Codex(?:Beta)?_[\d.]+_x64__2p2nqsd0c76g0$")) continue;
                    using (var package = packages.OpenSubKey(name))
                    {
                        var root = package == null ? null : package.GetValue("PackageRootFolder") as string;
                        if (String.IsNullOrEmpty(root)) continue;
                        var expectedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps", name);
                        if (!SamePath(root, expectedRoot)) continue;
                        try
                        {
                            var manifest = XDocument.Load(Path.Combine(root, "AppxManifest.xml"));
                            var app = manifest.Descendants().FirstOrDefault(e => e.Name.LocalName == "Application" &&
                                new[] { @"app\Codex.exe", @"app\ChatGPT.exe" }.Contains(((string)e.Attribute("Executable") ?? "").Replace('/', '\\'), StringComparer.OrdinalIgnoreCase));
                            if (app == null) continue;
                            var exe = Path.Combine(root, ((string)app.Attribute("Executable")).Replace('/', '\\'));
                            if (!File.Exists(exe)) continue;
                            result.Add(new Install { Executable = exe, Version = name.Split('_')[1], Beta = name.StartsWith("OpenAI.CodexBeta_", StringComparison.Ordinal) });
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            return result.OrderByDescending(i => new Version(i.Version)).ToList();
        }

        static bool SamePath(string first, string second)
        {
            return String.Equals(CanonicalPath(first), CanonicalPath(second), StringComparison.OrdinalIgnoreCase);
        }

        static string CanonicalPath(string path)
        {
            string full = Path.GetFullPath(path).TrimEnd('\\');
            var file = CreateFile(full, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            if (file == new IntPtr(-1)) return full;
            try
            {
                var text = new StringBuilder(32768);
                uint size = GetFinalPathNameByHandle(file, text, (uint)text.Capacity, 0);
                if (size == 0 || size >= text.Capacity) return full;
                string resolved = text.ToString();
                if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + resolved.Substring(8);
                if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) return resolved.Substring(4);
                return resolved;
            }
            finally { CloseHandle(file); }
        }

        static string ImagePath(int processId)
        {
            var handle = OpenProcess(0x1000, false, processId);
            if (handle == IntPtr.Zero) return null;
            try
            {
                var text = new StringBuilder(32768);
                int size = text.Capacity;
                return QueryFullProcessImageName(handle, 0, text, ref size) ? text.ToString() : null;
            }
            finally { CloseHandle(handle); }
        }

        static RunningState ReadRunning(IList<Install> installs)
        {
            var state = new RunningState();
            foreach (string name in new[] { "Codex", "ChatGPT" })
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        var image = ImagePath(process.Id);
                        if (image == null)
                        {
                            try { if (!process.HasExited) state.Unknown = true; }
                            catch (InvalidOperationException) { }
                            continue;
                        }
                        if (installs.Any(i => SamePath(image, i.Executable))) state.Running = true;
                    }
                }
            }
            return state;
        }

        static async Task<PortState> InspectPortAsync(IList<Install> installs)
        {
            var result = await RunAsync(Path.Combine(Environment.SystemDirectory, "netstat.exe"), "-ano -p tcp", 8000);
            if (result.ExitCode != 0) throw new InvalidOperationException("无法读取本机连接状态。" + result.Error);
            var state = new PortState();
            foreach (string line in result.Output.Split('\n'))
            {
                var match = Regex.Match(line, @"^\s*TCP\s+(\S+)\s+(\S+)\s+\S+\s+(\d+)\s*$", RegexOptions.IgnoreCase);
                if (!match.Success || !match.Groups[1].Value.EndsWith(":" + Port, StringComparison.Ordinal)) continue;
                if (match.Groups[2].Value != "0.0.0.0:0" && match.Groups[2].Value != "[::]:0") continue;
                state.Listening = true;
                string local = match.Groups[1].Value;
                if (!IsLoopbackEndpoint(local))
                {
                    state.Error = "9341 端口接受外部连接。请关闭该调试入口，再使用面板的本机连接方式启动。";
                    return state;
                }
                var image = ImagePath(Int32.Parse(match.Groups[3].Value));
                if (image == null || !installs.Any(i => SamePath(i.Executable, image)))
                {
                    state.Error = "9341 端口被其他程序占用，或无法确认所属程序。请关闭占用程序后刷新。";
                    return state;
                }
                state.Owner = installs.First(i => SamePath(i.Executable, image));
            }
            // netstat -p tcp excludes IPv6 on Windows; verify that table as well.
            var ipv6 = await RunAsync(Path.Combine(Environment.SystemDirectory, "netstat.exe"), "-ano -p tcpv6", 8000);
            if (ipv6.ExitCode != 0) throw new InvalidOperationException("无法读取本机 IPv6 连接状态。" + ipv6.Error);
            foreach (string line in ipv6.Output.Split('\n'))
            {
                var match = Regex.Match(line, @"^\s*TCP\s+(\S+)\s+\[::\]:0\s+\S+\s+(\d+)\s*$", RegexOptions.IgnoreCase);
                if (!match.Success || !match.Groups[1].Value.EndsWith(":" + Port, StringComparison.Ordinal)) continue;
                if (!state.Listening)
                    state.Error = "主题入口只监听 IPv6，尚未开放本面板使用的 127.0.0.1 连接。请从应用菜单退出 Codex，再重新启动主题连接。";
                if (!IsLoopbackEndpoint(match.Groups[1].Value))
                {
                    state.Error = "9341 端口接受外部 IPv6 连接。请关闭该调试入口，再使用面板的本机连接方式启动。";
                    return state;
                }
                var image = ImagePath(Int32.Parse(match.Groups[2].Value));
                if (image == null || !installs.Any(i => SamePath(i.Executable, image)))
                {
                    state.Error = "9341 端口被其他程序占用，或无法确认所属程序。请关闭占用程序后刷新。";
                    return state;
                }
            }
            return state;
        }

        static bool IsLoopbackEndpoint(string endpoint)
        {
            return endpoint == "127.0.0.1:" + Port || endpoint == "[::1]:" + Port;
        }

        static async Task<IList<TargetInfo>> ReadTargetsAsync()
        {
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:9341/json/list");
            request.Proxy = null;
            request.AllowAutoRedirect = false;
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            string content = await Task.Run(delegate
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("主题连接返回了意外的 HTTP 状态。");
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        var buffer = new char[1024 * 1024 + 1];
                        int used = 0, read;
                        while (used < buffer.Length && (read = reader.Read(buffer, used, buffer.Length - used)) > 0) used += read;
                        if (used > 1024 * 1024) throw new InvalidOperationException("主题窗口列表过大。");
                        return new string(buffer, 0, used);
                    }
                }
            });
            return ParseTargets(content);
        }

        static IList<TargetInfo> ParseTargets(string content)
        {
            var raw = Json().DeserializeObject(content) as object[];
            if (raw == null) throw new InvalidOperationException("主题连接返回了无效的窗口列表。");
            var result = new List<TargetInfo>();
            foreach (object item in raw)
            {
                var target = item as Dictionary<string, object>;
                if (target == null || Text(target, "type") != "page") continue;
                Uri url;
                if (!Uri.TryCreate(Text(target, "url"), UriKind.Absolute, out url)) continue;
                if (url.Scheme != "app" || url.Host != "-" || url.AbsolutePath != "/index.html" || url.Fragment.Length != 0 || url.UserInfo.Length != 0 || !url.IsDefaultPort) continue;
                var query = System.Web.HttpUtility.ParseQueryString(url.Query);
                var routes = query.GetValues("initialRoute");
                if (routes != null && routes.Length > 1) continue;
                var route = query["initialRoute"] ?? "";
                if (Regex.IsMatch(route, @"^/(avatar-overlay|detached-window|spotlight)(?:[/?#]|$)", RegexOptions.IgnoreCase)) continue;
                var id = Text(target, "id");
                if (!Regex.IsMatch(id, @"^[A-Za-z0-9._-]{1,128}$")) continue;
                string title = Text(target, "title");
                bool hasDefaultTitle = title.Length == 0;
                if (title.Length == 0) title = "Codex 主窗口";
                if (title.Length > 100) title = title.Substring(0, 100) + "…";
                result.Add(new TargetInfo { Id = id, Title = title + " · " + id.Substring(0, Math.Min(8, id.Length)), HasDefaultTitle = hasDefaultTitle });
            }
            return result;
        }

        static TargetInfo SelectTarget(IList<TargetInfo> targets, string targetId)
        {
            if (!String.IsNullOrEmpty(targetId)) return targets.FirstOrDefault(t => t.Id == targetId);
            return targets.Count == 1 ? targets[0] : null;
        }

        static async Task<NodeRuntime> FindNodeAsync()
        {
            var candidates = new List<string>();
            candidates.Add(Path.Combine(Root, "Runtime", "node.exe"));
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                if (String.IsNullOrWhiteSpace(directory)) continue;
                try { candidates.Add(Path.Combine(directory.Trim('"'), "node.exe")); }
                catch (ArgumentException) { }
            }
            foreach (string keyName in new[] { @"SOFTWARE\Node.js", @"SOFTWARE\WOW6432Node\Node.js" })
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyName))
                {
                    var installPath = key == null ? null : key.GetValue("InstallPath") as string;
                    if (!String.IsNullOrEmpty(installPath)) candidates.Add(Path.Combine(installPath, "node.exe"));
                }
            }
            var errors = new List<string>();
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(candidate)) continue;
                var result = await RunAsync(candidate, "--version", 6000);
                var version = result.Output.Trim();
                var match = Regex.Match(version, @"^v(\d+)\.\d+\.\d+");
                if (result.ExitCode == 0 && match.Success && Int32.Parse(match.Groups[1].Value) >= 22)
                    return new NodeRuntime { Path = candidate, Version = version };
                errors.Add(candidate + " · " + version + " " + result.Error.Trim());
            }
            throw new InvalidOperationException("需要 Node.js 22 或更新版本。" + (errors.Count == 0 ? "" : "\r\n" + String.Join("\r\n", errors)));
        }

        static async Task<Dictionary<string, object>> ThemeCommandAsync(string action, string targetId, NodeRuntime node)
        {
            if (!File.Exists(Script)) throw new InvalidOperationException("主题程序文件缺失，请保留完整的 Codex2077 文件夹。");
            string arguments = Quote(Script) + " " + action + " --port=9341";
            if (!String.IsNullOrEmpty(targetId))
            {
                if (!Regex.IsMatch(targetId, @"^[A-Za-z0-9._-]{1,128}$")) throw new InvalidOperationException("窗口标识无效，请刷新状态。");
                arguments += " --target-id=" + targetId;
            }
            var result = await RunAsync(node.Path, arguments, 24000);
            if (result.ExitCode != 0) throw new InvalidOperationException(result.Error.Trim() + "\r\n" + result.Output.Trim());
            var parsed = Json().DeserializeObject(result.Output) as Dictionary<string, object>;
            if (parsed == null) throw new InvalidOperationException("主题程序返回了无效状态。" + result.Output);
            return parsed;
        }

        static async Task<ProcessResult> RunAsync(string executable, string arguments, int timeout)
        {
            var start = new ProcessStartInfo(executable, arguments);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            using (var process = new Process { StartInfo = start })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var exited = Task.Run(delegate { process.WaitForExit(); });
                if (await Task.WhenAny(exited, Task.Delay(timeout)) != exited)
                {
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    await exited;
                    throw new TimeoutException("操作超时，请刷新状态后重试。");
                }
                return new ProcessResult { ExitCode = process.ExitCode, Output = await output, Error = await error };
            }
        }

        static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }; }
        static string Text(Dictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
        }
        static bool Bool(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || !(value is bool))
                throw new InvalidOperationException("主题程序返回的 " + key + " 状态缺失或类型无效，最终状态未确认。");
            return (bool)value;
        }
        static string FriendlyError(string error)
        {
            if (error.Contains("checksum mismatch")) return "主题文件校验失败，请重新获取完整主题资源。";
            if (error.Contains("Another skin is active")) return "其他主题正在使用中，请先从原工具关闭后重试。";
            if (error.Contains("Native controls not found")) return "当前 Codex 界面与这版主题不兼容，主题尚未应用。";
            if (error.Contains("Control layout changed")) return "主题影响了原生按钮布局，已自动恢复原外观。";
            if (error.Contains("unique Codex main renderer")) return "窗口已经变化，请刷新并重新选择 Codex 主窗口。";
            if (error.Contains("fetch failed") || error.Contains("ECONNREFUSED")) return "主题连接已断开，请刷新状态并重新连接。";
            if (error.Contains("CDP") && error.Contains("timed out")) return "Codex 响应超时，请稍后刷新状态。";
            var firstLine = error.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return String.IsNullOrEmpty(firstLine) ? "操作未完成，请查看诊断详情。" : firstLine;
        }
    }
}
