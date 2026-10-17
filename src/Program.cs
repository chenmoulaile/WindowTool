using System;
using System.Drawing;
using System.IO;
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
            WM_ALREADY_RUNNING = Native.RegisterWindowMessage("WindowTool.AlreadyRunning.1");
            bool created;
            single = new Mutex(true, MutexName, out created);
            if (!created)
            {
                // 已有实例：广播唤醒其主界面；短暂重试避免首个实例窗口尚未创建时消息丢失
                for (int i = 0; i < 20; i++)
                {
                    Native.PostMessage(Native.HWND_BROADCAST, WM_ALREADY_RUNNING, IntPtr.Zero, IntPtr.Zero);
                    Thread.Sleep(50);
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 系统DPI → 全局缩放（清单已声明 dpiAware，高分屏下按真实DPI布局，不再被系统拉伸模糊）
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.Scale = Math.Max(1f, Math.Min(4f, g.DpiX / 96f));
            }
            catch { Theme.Scale = 1f; }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Report(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);
            // 刻意不订阅 WPF Dispatcher.UnhandledException：碰一下 Dispatcher.CurrentDispatcher
            // 就会把 WindowsBase/PresentationCore 拉进进程，白付十几 MB 常驻内存。
            // WPF 回调最终仍在 UI 线程抛出，上面的 ThreadException 已经能兜住。
            Application.Run(new MainForm());
        }

        static void Report(Exception ex)
        {
            Log(ex);
            try { MessageBox.Show("发生错误（已写入日志，不影响继续使用）：\r\n" + ex.Message); } catch { }
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
}
