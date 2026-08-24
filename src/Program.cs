using System;
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
                Native.PostMessage(Native.HWND_BROADCAST, WM_ALREADY_RUNNING, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Report(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);
            var disp = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            disp.UnhandledException += (s, e) =>
            {
                Log(e.Exception);
                e.Handled = true;
            };
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
