using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace WindowTool
{
    /// OI 工具箱：一键下载与工具链部署（网络/进程操作在后台线程，UI 回调由调用方切换）
    static class OiTools
    {
        public const string MsysRoot = @"C:\msys64";

        static OiTools()
        {
            try
            {
                // .NET 4.0 默认不含 TLS1.2，手动开启
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)3072 |   // TLS 1.2
                    (SecurityProtocolType)768 |    // TLS 1.1
                    SecurityProtocolType.Tls;
            }
            catch { }
        }

        public static bool Busy { get; private set; }

        public static string DownloadsDir()
        {
            try
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(d)) return d;
            }
            catch { }
            return Path.GetTempPath();
        }

        // 异步下载；progress/done/error 可能在后台线程触发
        public static void Download(string url, string dest, Action<long, long> progress, Action done, Action<string> error)
        {
            Busy = true;
            try
            {
                var wc = new WebClient();
                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                wc.DownloadProgressChanged += (s, e) =>
                {
                    var p = progress;
                    if (p != null) p(e.BytesReceived, e.TotalBytesToReceive);
                };
                wc.DownloadFileCompleted += (s, e) =>
                {
                    Busy = false;
                    if (e.Error != null) { var err = error; if (err != null) err(e.Error.Message); }
                    else { var d = done; if (d != null) d(); }
                };
                wc.DownloadFileAsync(new Uri(url), dest);
            }
            catch (Exception ex)
            {
                Busy = false;
                var err = error;
                if (err != null) err(ex.Message);
            }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Program.Log(ex); }
        }

        public static void RunInstaller(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args ?? "") { UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public static bool Msys2Installed()
        {
            return File.Exists(Path.Combine(MsysRoot, "usr", "bin", "bash.exe"));
        }

        // 静默安装 MSYS2（Qt 安装器参数），阻塞至退出；返回退出码
        public static int InstallMsys2Silent(string installerExe)
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerExe,
                Arguments = "install --root " + MsysRoot + " --confirm-command",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // 后台执行 bash -lc，onLine 逐行回调；阻塞至退出
        public static int RunBash(string command, Action<string> onLine)
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(MsysRoot, "usr", "bin", "bash.exe"),
                Arguments = "-lc \"" + command + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            try { psi.EnvironmentVariables["MSYSTEM"] = "UCRT64"; } catch { }
            using (var p = Process.Start(psi))
            {
                p.OutputDataReceived += (s, e) => { if (onLine != null && e.Data != null) onLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (onLine != null && e.Data != null) onLine(e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // 把 ucrt64\bin 加入当前用户 PATH（供 Sublime LSP 找到 clangd）
        public static bool AddMsysToPath()
        {
            try
            {
                string dir = Path.Combine(MsysRoot, "ucrt64", "bin");
                if (!Directory.Exists(dir)) return false;
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", true))
                {
                    if (k == null) return false;
                    string cur = (k.GetValue("Path") as string) ?? "";
                    if ((";" + cur.ToLowerInvariant() + ";").Contains(";" + dir.ToLowerInvariant() + ";")) return true;
                    string neu = cur.TrimEnd(';');
                    neu = (neu.Length > 0 ? neu + ";" : "") + dir;
                    k.SetValue("Path", neu);
                    return true;
                }
            }
            catch (Exception ex) { Program.Log(ex); return false; }
        }

        // Sublime Text 官方 latest 版本号（如 4192）
        public static string FetchSublimeVersion()
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "Mozilla/5.0");
                string s = wc.DownloadString("https://download.sublimetext.com/latest/stable");
                s = (s ?? "").Trim();
                if (s.Length == 0 || s.Length > 6) throw new Exception("版本号异常: " + s);
                return s;
            }
        }
    }
}
