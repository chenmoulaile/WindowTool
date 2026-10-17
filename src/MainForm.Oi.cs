using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WindowTool
{
    /// OI 工具箱的 UI 回调出口（可能来自后台线程，内部自行切回 UI 线程）。
    sealed class OiUi
    {
        readonly Action<string> status;
        readonly Action<int> progress;

        public OiUi(Action<string> status, Action<int> progress)
        {
            this.status = status;
            this.progress = progress;
        }

        public void Say(string s)
        {
            var a = status;
            if (a != null) a(s);
        }

        public void Bar(int pct)
        {
            var a = progress;
            if (a != null) a(pct);
        }
    }

    sealed partial class MainForm
    {
        public OiUi OiUi;

        void OiSay(string s)
        {
            if (OiUi != null) OiUi.Say(s);
        }

        void OiBar(int pct)
        {
            if (OiUi != null) OiUi.Bar(pct);
        }

        public void ExitApp()
        {
            reallyExit = true;
            Close();
        }

        public void OpenSettings()
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) File.WriteAllText(p, "");
                Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            }
            catch (Exception ex) { Program.Log(ex); ToastBad("无法打开设置文件：" + ex.Message); }
        }

        // ---------------- 下载与安装 ----------------

        void StartDownload(string url, string name, Action<string> afterDone)
        {
            if (OiTools.Busy) { OiSay("已有下载在进行中，请稍候…"); return; }
            string dest;
            try { dest = Path.Combine(OiTools.DownloadsDir(), Path.GetFileName(new Uri(url).LocalPath)); }
            catch { dest = Path.Combine(OiTools.DownloadsDir(), name.Replace(" ", "") + ".exe"); }
            OiBar(0);
            OiSay("下载 " + name + " …");
            OiTools.Download(url, dest,
                (rec, tot) =>
                {
                    int pct = tot > 0 ? (int)(rec * 100 / tot) : 0;
                    OiBar(pct);
                    OiSay("下载 " + name + " … " + pct + "%");
                },
                () =>
                {
                    OiBar(100);
                    OiSay(name + " 下载完成：" + dest);
                    if (afterDone != null) afterDone(dest);
                },
                msg =>
                {
                    OiBar(0);
                    OiSay(name + " 下载失败：" + msg);
                    ToastBad(name + " 下载失败：" + msg);
                });
        }

        public void DlSublime()
        {
            OiSay("获取 Sublime Text 最新版本号…");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string ver = null;
                try { ver = OiTools.FetchSublimeVersion(); }
                catch (Exception ex) { Program.Log(ex); }
                try
                {
                    Ui(delegate
                    {
                        if (ver == null)
                        {
                            OiTools.OpenUrl("https://www.sublimetext.com/download");
                            OiSay("获取版本号失败，已打开官网下载页。");
                            return;
                        }
                        StartDownload("https://download.sublimetext.com/sublime_text_build_" + ver + "_x64_setup.exe",
                            "Sublime Text " + ver, dest => OiTools.RunInstaller(dest, "/SILENT"));
                    });
                }
                catch (Exception ex) { Program.Log(ex); }
            });
        }

        public void DlVscode()
        {
            StartDownload("https://update.code.visualstudio.com/latest/win32-x64-user/stable", "VS Code",
                dest => OiTools.RunInstaller(dest, "/VERYSILENT /NORESTART /MERGETASKS=!runcode"));
        }

        public void Open302()
        {
            OiTools.OpenUrl("https://www.dogfight360.com/blog/1484/");
            OiSay("已打开 SteamCommunity 302 官方页面，在页面内下载最新版。");
        }

        public void Cc(string browser)
        {
            OiSay("获取 Competitive Companion 最新版本…");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string tag = null;
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "Mozilla/5.0");
                        string json = wc.DownloadString("https://api.github.com/repos/jmerle/competitive-companion/releases/latest");
                        var m = System.Text.RegularExpressions.Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) tag = m.Groups[1].Value;
                    }
                }
                catch (Exception ex) { Program.Log(ex); }
                try
                {
                    Ui(delegate
                    {
                        if (tag == null)
                        {
                            OiTools.OpenUrl("https://github.com/jmerle/competitive-companion/releases");
                            OiSay("获取版本失败，已打开 GitHub Releases 页面，请手动下载 chrome.zip。");
                            return;
                        }
                        string url = "https://github.com/jmerle/competitive-companion/releases/download/" +
                            tag + "/competitive_companion-" + tag + "-chrome.zip";
                        StartDownload(url, "Competitive Companion " + tag, dest => ExtractCompanion(dest, browser));
                    });
                }
                catch (Exception ex) { Program.Log(ex); }
            });
        }

        void ExtractCompanion(string zipPath, string browser)
        {
            OiSay("解压扩展…");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string dir = Path.Combine(OiTools.DownloadsDir(), "CompetitiveCompanion");
                    if (Directory.Exists(dir)) { try { Directory.Delete(dir, true); } catch { } }
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, dir);
                    Ui(delegate
                    {
                        OiSay("已解压到 " + dir + "。" + (browser == "chrome"
                            ? "Chrome：chrome://extensions → 开「开发者模式」→「加载已解压的扩展程序」→ 选该文件夹"
                            : "Edge：edge://extensions → 开「开发人员模式」→「加载解压缩的扩展」→ 选该文件夹"));
                        ToastGood("Competitive Companion 已解压，按提示在浏览器里加载即可。");
                    });
                }
                catch (Exception ex)
                {
                    Ui(delegate { OiSay("解压失败：" + ex.Message); });
                }
            });
        }

        public void Msys2()
        {
            if (OiTools.Msys2Installed())
            {
                OiSay("MSYS2 已安装在 " + OiTools.MsysRoot + "，可直接安装工具链。");
                Toast("MSYS2 已安装。");
                return;
            }
            StartDownload("https://repo.msys2.org/distrib/msys2-x86_64-latest.exe", "MSYS2", dest =>
            {
                OiSay("正在静默安装 MSYS2 到 " + OiTools.MsysRoot + "（约 1-2 分钟）…");
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        int code = OiTools.InstallMsys2Silent(dest);
                        try
                        {
                            Ui(delegate
                            {
                                if (code == 0) { OiSay("MSYS2 安装完成！点「一键安装工具链」继续。"); ToastGood("MSYS2 安装完成。"); }
                                else { OiSay("MSYS2 安装程序退出码 " + code + "，可手动运行：" + dest); ToastWarn("MSYS2 安装未完成（退出码 " + code + "）"); }
                            });
                        }
                        catch (Exception ex) { Program.Log(ex); }
                    }
                    catch (Exception ex) { OiSay("MSYS2 安装失败：" + ex.Message); }
                });
            });
        }

        public void Toolchain()
        {
            if (!OiTools.Msys2Installed()) { ToastWarn("请先安装 MSYS2。"); OiSay("请先点「MSYS2」下载安装。"); return; }
            if (oiToolchainRunning) { Toast("工具链正在安装中…"); return; }
            oiToolchainRunning = true;
            OiSay("工具链安装中：同步 pacman 数据库（首次较慢）…");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var log = new StringBuilder();
                Action<string> on = line =>
                {
                    if (string.IsNullOrEmpty(line)) return;
                    lock (log) log.AppendLine(line);
                    string shown = line.Length > 90 ? line.Substring(0, 90) + "…" : line;
                    OiSay("工具链：" + shown);
                };
                try
                {
                    OiTools.RunBash("pacman -Syuu --noconfirm", on);
                    OiTools.RunBash("pacman -Syuu --noconfirm", on);
                    OiTools.RunBash("pacman -S --needed --noconfirm " +
                        "mingw-w64-ucrt-x86_64-clang " +
                        "mingw-w64-ucrt-x86_64-clang-tools-extra " +
                        "mingw-w64-ucrt-x86_64-gdb " +
                        "mingw-w64-ucrt-x86_64-make " +
                        "mingw-w64-ucrt-x86_64-gcc", on);
                    bool pathOk = OiTools.AddMsysToPath();
                    try
                    {
                        lock (log) File.WriteAllText(Path.Combine(Path.GetTempPath(), "WindowTool_oi.log"), log.ToString());
                    }
                    catch { }
                    Ui(delegate
                    {
                        oiToolchainRunning = false;
                        if (pathOk)
                        {
                            OiSay("工具链安装完成！clangd/g++ 已就位（ucrt64\\bin 已加入用户 PATH，重启 Sublime 后 LSP 可用）。");
                            ToastGood("工具链安装完成。");
                        }
                        else
                        {
                            OiSay("工具链安装完成，但加入 PATH 失败，请手动把 " + OiTools.MsysRoot + "\\ucrt64\\bin 加入 PATH。");
                            ToastWarn("工具链已装，但 PATH 写入失败");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Program.Log(ex);
                    try { Ui(delegate { oiToolchainRunning = false; OiSay("工具链安装失败：" + ex.Message); }); } catch { }
                }
            });
        }

        bool oiToolchainRunning;
    }
}
