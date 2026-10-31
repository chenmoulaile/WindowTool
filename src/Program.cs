using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace WindowTool
{
    static class Program
    {
        const string MutexName = @"Local\WindowTool.SingleInstance.1";
        static Mutex single;
        public static int WM_ALREADY_RUNNING;

        [STAThread]
        static void Main()
        {
            Boot.Begin();
            try
            {
                WM_ALREADY_RUNNING = Native.RegisterWindowMessage("WindowTool.AlreadyRunning.1");
                Boot.Step("register-window-message ok");

                bool created;
                single = new Mutex(true, MutexName, out created);
                Boot.Step("mutex created=" + created);
                if (!created)
                {
                    // 已有实例：广播唤醒其主界面；短暂重试避免首个实例窗口尚未创建时消息丢失
                    Boot.Step("second instance: notifying the running one and exiting");
                    for (int i = 0; i < 20; i++)
                    {
                        Native.PostMessage(Native.HWND_BROADCAST, WM_ALREADY_RUNNING, IntPtr.Zero, IntPtr.Zero);
                        Thread.Sleep(50);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Boot.Step("visual-styles ok");

                // 系统DPI → 全局缩放（清单已声明 dpiAware，高分屏下按真实DPI布局，不再被系统拉伸模糊）
                try
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                        Theme.Scale = Math.Max(1f, Math.Min(4f, g.DpiX / 96f));
                }
                catch { Theme.Scale = 1f; }
                Boot.Step("dpi scale=" + Theme.Scale.ToString("0.##"));

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Report(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Fatal(e.ExceptionObject as Exception, "未处理异常");
                // v2.0 起不再引用任何 WPF 程序集（视频背景已移除），常驻内存大幅下降。
                Boot.Step("constructing main window");
                Application.Run(new AppForm());
                Boot.Step("message loop exited normally");
            }
            catch (Exception ex)
            {
                Fatal(ex, "启动失败");
            }
        }

        static void Report(Exception ex)
        {
            Log(ex);
            Boot.Step("ui-exception: " + (ex == null ? "?" : ex.GetType().Name));
            try { MessageBox.Show("发生错误（已写入日志，不影响继续使用）：\r\n" + (ex == null ? "" : ex.Message)); } catch { }
        }

        static void Fatal(Exception ex, string title)
        {
            if (ex == null) return;
            Log(ex);
            Boot.Step("FATAL " + ex.GetType().Name + ": " + ex.Message);
            string msg = "OI 窗口工具 " + title + "：\r\n\r\n" +
                         ex.GetType().Name + ": " + ex.Message + "\r\n\r\n" +
                         "日志位置：\r\n" + Boot.LogPathHint + "\r\n\r\n" +
                         "把该日志发给开发者即可定位问题。";
            try { Native.MessageBox(IntPtr.Zero, msg, "OI 窗口工具", Native.MB_ICONERROR); } catch { }
        }

        public static void Log(Exception ex)
        {
            if (ex == null) return;
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "WindowTool_crash.log"),
                    "\r\n[" + DateTime.Now + "] " + ex.GetType().Name + ": " + ex.Message + "\r\n" +
                    (ex.StackTrace ?? "") + "\r\n");
            }
            catch { }
        }
    }

    /// <summary>
    /// v2.0.2 诊断日志：启动的每个阶段都写入 exe 旁的 WindowTool.log（无写权限则退回 %TEMP%）。
    /// 目的：用户机器上"双击没反应"时能留下证据链——
    ///   有日志 → 代码跑到哪一步一目了然；完全无日志 → 进程根本没启动（安全软件拦截/文件被隔离）。
    /// 全部写入都在 try/catch 中，日志本身绝不会导致启动失败。
    /// </summary>
    static class Boot
    {
        static string path;
        static int t0;

        public static string LogPathHint { get { return path ?? "(日志不可用)"; } }

        public static void Begin()
        {
            t0 = Environment.TickCount;
            // exe 所在目录优先（便携场景，日志跟着程序走）；只读目录退回 %TEMP%
            try
            {
                string p = Path.Combine(Application.StartupPath, "WindowTool.log");
                File.AppendAllText(p, "");
                path = p;
            }
            catch { path = null; }
            if (path == null)
            {
                try { path = Path.Combine(Path.GetTempPath(), "WindowTool.log"); } catch { path = null; }
            }
            try
            {
                File.AppendAllText(path,
                    "\r\n==== OI 窗口工具 v" + Ver + " 启动 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====\r\n" +
                    "exe: " + Loc + "\r\n" +
                    "os: " + Environment.OSVersion + "  64bit进程: " + Environment.Is64BitProcess + "\r\n" +
                    "clr: " + Environment.Version + "\r\n");
            }
            catch { }
        }

        public static void Step(string what)
        {
            if (path == null) return;
            try { File.AppendAllText(path, (Environment.TickCount - t0) + "ms  " + what + "\r\n"); } catch { }
        }

        static string Ver
        {
            get { try { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); } catch { return "?"; } }
        }

        static string Loc
        {
            get { try { return Assembly.GetExecutingAssembly().Location; } catch { return "?"; } }
        }
    }
}
