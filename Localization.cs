using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Codex2077
{
    // Keep state and log messages in their canonical form, then render at the UI boundary.
    // This makes a language change immediate, without querying or changing the Codex window.
    public static class Localization
    {
        public const string Chinese = "zh-CN";
        public const string English = "en";
        public static string Language { get; private set; }
        public static string PreferenceNotice { get; private set; }
        private static string directory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly Dictionary<string, string> EnglishText = new Dictionary<string, string>
        {
            { "2077 主题控制台", "2077 Theme Console" },
            { "深潜协议 · Codex 桌面外观", "Deep Dive Protocol · Codex desktop theme" },
            { "语言", "Language" },
            { "选择界面语言", "Choose interface language" },
            { "刷新状态", "Refresh" },
            { "刷新当前连接和主题状态", "Refresh connection and theme status" },
            { "主题保留在当前 Codex 窗口，重启后可在这里重新开启。", "The theme stays in the current Codex window. Enable it here again after restarting Codex." },
            { "正在读取状态", "Reading status" },
            { "检测中", "Checking" },
            { "检查 Codex 连接、主题状态和本机运行环境。", "Checking the Codex connection, theme status, and local runtime." },
            { "开启 2077 主题", "Enable 2077 theme" },
            { "开启主题", "Enable theme" },
            { "关闭主题", "Disable theme" },
            { "启动主题连接", "Start connection" },
            { "选择要操作的 Codex 窗口", "Choose the Codex window to customize" },
            { "要操作的 Codex 窗口", "Codex window to customize" },
            { "Codex 连接", "Codex connection" },
            { "应用版本", "App version" },
            { "主题状态", "Theme status" },
            { "运行环境", "Runtime" },
            { "运行记录", "Activity log" },
            { "检查主题文件", "Check theme files" },
            { "操作时间和运行结果", "Operation times and results" },
            { "查看诊断详情", "Diagnostic details" },
            { "连接地址及错误详情", "Connection address and error details" },
            { "已开启", "Enabled" },
            { "已关闭", "Disabled" },
            { "已连接", "Connected" },
            { "待连接", "Disconnected" },
            { "处理中", "Working" },
            { "{0}，正在等待实际结果……", "{0}. Waiting for the result…" },
            { "未完成", "Incomplete" },
            { "暂时无法确认状态", "Status unavailable" },
            { "读取失败", "Read failed" },
            { "2077 主题控制台已经打开，请从任务栏切回。", "2077 Theme Console is already open. Switch to it from the taskbar." },
            { "操作未完成", "Operation incomplete" },
            { "2077 主题控制台无法启动", "Unable to start 2077 Theme Console" },
            { "语言设置无法读取，已使用中文。", "Language settings could not be read. Chinese was selected by default." },
            { "无法保存语言设置；本次会话已切换，重新打开后可能恢复原语言。", "Language changed for this session. Settings could not be saved; the previous language may return next time." },
            { "正在检查", "Checking" },
            { "未连接", "Not connected" },
            { "尚未确认", "Not confirmed" },
            { "未找到", "Not found" },
            { "未找到 Codex", "Codex not found" },
            { "请先安装并打开一次 Microsoft Store 版 Codex / ChatGPT，再刷新状态。", "Install and open the Microsoft Store version of Codex / ChatGPT once, then refresh." },
            { "正式版", "Stable" },
            { "应用位置", "Application location" },
            { "连接需要处理", "Connection needs attention" },
            { "连接验证失败", "Connection verification failed" },
            { "等待主题连接", "Waiting for a theme connection" },
            { "尚未连接 Codex", "Codex is not connected" },
            { "Codex 已运行，但主题连接尚未开启。请完成当前工作并从应用菜单退出 Codex，然后点击“启动主题连接”。", "Codex is running without a theme connection. Finish your work and quit Codex from its app menu, then click Start connection." },
            { "点击“启动主题连接”打开 Codex，连接成功后即可开启 2077 主题。", "Click Start connection to open Codex. Once connected, you can enable the 2077 theme." },
            { "无法确认 Codex 是否仍在运行。请从应用菜单退出 Codex，再刷新状态。", "Unable to confirm whether Codex is still running. Quit Codex from its app menu, then refresh." },
            { "Codex 已运行 · 尚未连接", "Codex running · Not connected" },
            { "等待启动", "Waiting to start" },
            { "需要 Node.js 22 或更新版本", "Node.js 22 or later is required" },
            { "安装 Node.js 后重新打开此面板；诊断详情中可查看检测结果。", "Install Node.js, then reopen this console. See Diagnostic details for the detection results." },
            { "连接所属应用", "Connected application" },
            { "已连接 · 仅本机", "Connected · Local only" },
            { "等待 Codex 主窗口", "Waiting for the Codex main window" },
            { "请选择一个 Codex 窗口", "Choose a Codex window" },
            { "连接已就绪，等待 Codex 主窗口加载后请刷新。", "The connection is ready. Refresh once the Codex main window has loaded." },
            { "检测到多个主窗口。选中要换肤的窗口后，面板会读取该窗口的实际主题状态。", "Multiple main windows were found. Choose one to read its current theme status." },
            { "连接已就绪，安装 Node.js 后重新打开面板即可控制主题。", "The connection is ready. Install Node.js and reopen this console to control the theme." },
            { "2077 主题已开启", "2077 theme enabled" },
            { "2077 主题已关闭", "2077 theme disabled" },
            { "可以开启 2077 主题", "Ready to enable the 2077 theme" },
            { "红黑面板、青色高亮和额度状态条已注入当前窗口。点击关闭可恢复原来的外观。", "Red and black panels, cyan highlights, and quota indicators are active in this window. Disable the theme to restore its previous appearance." },
            { "主题连接正常。点击开启，即可把当前窗口切换为深潜协议风格。", "The theme connection is ready. Click Enable 2077 theme to apply Deep Dive Protocol to this window." },
            { "其他主题正在使用中", "Another theme is active" },
            { "请先从原来的换肤工具关闭现有主题，然后刷新状态。", "Disable the existing theme in the tool that applied it, then refresh." },
            { "窗口 ID", "Window ID" },
            { "暂时无法读取状态", "Unable to read status" },
            { "状态未确认", "Status not confirmed" },
            { "主题连接已断开，请刷新状态并重新连接 Codex。", "The theme connection was lost. Refresh and reconnect Codex." },
            { "请选择一个可用的 Codex 主窗口，再操作主题开关。", "Choose an available Codex main window before changing the theme." },
            { "操作后连接中断，主题最终状态未确认。请重新连接并刷新。", "The connection was lost after the operation. The final theme state is not confirmed. Reconnect and refresh." },
            { "主题操作已执行，但实际状态未通过确认。请刷新查看当前状态。", "The theme operation ran, but its final state could not be verified. Refresh to check the current state." },
            { "2077 主题已开启，实际窗口状态已确认。", "2077 theme enabled. The window state has been verified." },
            { "2077 主题已关闭，实际窗口状态已确认。", "2077 theme disabled. The window state has been verified." },
            { "主题文件校验没有通过。", "Theme file validation failed." },
            { "主题文件校验通过", "Theme files verified" },
            { "未找到可启动的 Microsoft Store 版 Codex。", "No launchable Microsoft Store version of Codex was found." },
            { "主题连接已就绪，请刷新并选择窗口。", "The theme connection is ready. Refresh and choose a window." },
            { "请先完成当前工作，从应用菜单退出 Codex，然后再次点击“启动主题连接”。", "Finish your work and quit Codex from its app menu, then click Start connection again." },
            { "Windows 没有返回 Codex 启动结果。", "Windows did not return a Codex launch result." },
            { "Codex 主题连接已就绪，可以开启 2077 主题。", "The Codex theme connection is ready. You can enable the 2077 theme." },
            { "Codex 已尝试启动，但 30 秒内没有开放主题连接。此版本可能限制调试入口。请查看 Codex 是否已经打开，并刷新重试；诊断详情中可查看版本信息。", "Codex was launched, but no theme connection became available within 30 seconds. This version may restrict debugging. Check whether Codex opened, then refresh. Version information is available in Diagnostic details." },
            { "无法读取本机连接状态。", "Unable to read local connection status. " },
            { "9341 端口接受外部连接。请关闭该调试入口，再使用面板的本机连接方式启动。", "Port 9341 accepts external connections. Close that debugging endpoint, then use this console to start a local connection." },
            { "9341 端口被其他程序占用，或无法确认所属程序。请关闭占用程序后刷新。", "Port 9341 belongs to another program, or its owner could not be verified. Close that program, then refresh." },
            { "无法读取本机 IPv6 连接状态。", "Unable to read local IPv6 connection status. " },
            { "主题入口只监听 IPv6，尚未开放本面板使用的 127.0.0.1 连接。请从应用菜单退出 Codex，再重新启动主题连接。", "The theme endpoint listens only on IPv6; this console requires 127.0.0.1. Quit Codex from its app menu and start the theme connection again." },
            { "9341 端口接受外部 IPv6 连接。请关闭该调试入口，再使用面板的本机连接方式启动。", "Port 9341 accepts external IPv6 connections. Close that debugging endpoint, then use this console to start a local connection." },
            { "主题连接返回了意外的 HTTP 状态。", "The theme connection returned an unexpected HTTP status." },
            { "主题窗口列表过大。", "The theme window list is too large." },
            { "主题连接返回了无效的窗口列表。", "The theme connection returned an invalid window list." },
            { "Codex 主窗口", "Codex main window" },
            { "需要 Node.js 22 或更新版本。", "Node.js 22 or later is required." },
            { "主题程序文件缺失，请保留完整的 Codex2077 文件夹。", "Theme program files are missing. Keep the complete Codex2077 folder together." },
            { "窗口标识无效，请刷新状态。", "The window identifier is invalid. Refresh the status." },
            { "主题程序返回了无效状态。", "The theme program returned an invalid status. " },
            { "操作超时，请刷新状态后重试。", "The operation timed out. Refresh and try again." },
            { "主题程序返回的 ", "The theme program returned a missing or invalid " },
            { " 状态缺失或类型无效，最终状态未确认。", " status. The final state is not confirmed." },
            { "主题文件校验失败，请重新获取完整主题资源。", "Theme file verification failed. Download the complete theme resources again." },
            { "其他主题正在使用中，请先从原工具关闭后重试。", "Another theme is active. Disable it in its original tool, then try again." },
            { "当前 Codex 界面与这版主题不兼容，主题尚未应用。", "This Codex interface is incompatible with the theme. The theme has not been applied." },
            { "主题影响了原生按钮布局，已自动恢复原外观。", "The theme changed the native button layout. The previous appearance was restored automatically." },
            { "窗口已经变化，请刷新并重新选择 Codex 主窗口。", "The window changed. Refresh and choose the Codex main window again." },
            { "主题连接已断开，请刷新状态并重新连接。", "The theme connection was lost. Refresh and reconnect." },
            { "Codex 响应超时，请稍后刷新状态。", "Codex took too long to respond. Refresh again shortly." },
            { "操作未完成，请查看诊断详情。", "The operation did not complete. See Diagnostic details." },
        };
        private static readonly Regex Phrases = new Regex(String.Join("|", EnglishText.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)), RegexOptions.Compiled);

        static Localization() { Language = Chinese; PreferenceNotice = ""; }

        public static string Text(string value)
        {
            if (value == null) value = "尚未确认";
            if (Language != English) return value;
            string translated;
            if (EnglishText.TryGetValue(value, out translated)) return translated;
            // Paths and serialized diagnostics are data, not translatable UI copy.
            return String.Join("\n", value.Split('\n').Select(line =>
                Regex.IsMatch(line, @"(?:[A-Za-z]:[\\/]|\\\\|file:|https?:)") || Regex.IsMatch(line.TrimStart(), @"^[\{\[]")
                    ? line : Phrases.Replace(line, match => EnglishText[match.Value])));
        }

        public static string Format(string template, params object[] values) { return String.Format(Text(template), values); }

        public static void Initialize(string portableDirectory)
        {
            directory = portableDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            Language = Chinese;
            PreferenceNotice = "";
            try
            {
                var settings = ReadSettings();
                object saved;
                if (settings.TryGetValue("language", out saved) && saved as string == English) Language = English;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException) && !(ex is System.Security.SecurityException)) throw;
                PreferenceNotice = "语言设置无法读取，已使用中文。";
            }
        }

        public static bool SelectLanguage(string language)
        {
            Language = language == English ? English : Chinese;
            string temporary = null;
            try
            {
                Dictionary<string, object> settings;
                try { settings = ReadSettings(); } catch (Exception ex)
                {
                    if (!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                    settings = new Dictionary<string, object>();
                }
                settings["language"] = Language;
                string path = Path.Combine(directory, "settings.json");
                temporary = Path.Combine(directory, "settings." + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(settings) + Environment.NewLine, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                PreferenceNotice = "";
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException) && !(ex is System.Security.SecurityException)) throw;
                PreferenceNotice = "无法保存语言设置；本次会话已切换，重新打开后可能恢复原语言。";
                return false;
            }
            finally
            {
                if (temporary != null) try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (System.Security.SecurityException) { }
            }
        }

        private static Dictionary<string, object> ReadSettings()
        {
            string path = Path.Combine(directory, "settings.json");
            if (!File.Exists(path)) return new Dictionary<string, object>();
            if (new FileInfo(path).Length > 65536) throw new InvalidOperationException("Settings file too large.");
            var settings = new JavaScriptSerializer { MaxJsonLength = 65536 }.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (settings == null) throw new InvalidOperationException("Invalid settings object.");
            return settings;
        }
    }
}
