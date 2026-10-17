using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;

namespace WindowTool
{
    // 只为验证窗口外壳（四周阴影/边框、红绿灯位置、最大化还原）而存在的探针进程。
    // 不走 Program.Main，所以不碰单实例互斥体，可以和他正在跑的实例共存。
    static class ChromeProbe
    {
        static void Report()
        {
            var me = System.Diagnostics.Process.GetCurrentProcess();
            Console.WriteLine(string.Format("MEM workingset={0:N0} KB  private={1:N0} KB  peak={2:N0} KB", me.WorkingSet64 / 1024.0, me.PrivateMemorySize64 / 1024.0, me.PeakWorkingSet64 / 1024.0));
            bool wpf = false, integ = false;
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = a.GetName().Name;
                if (n == "PresentationCore" || n == "PresentationFramework" || n == "WindowsBase") wpf = true;
                if (n == "WindowsFormsIntegration") integ = true;
            }
            Console.WriteLine("WPF loaded=" + wpf + "  ElementHost loaded=" + integ);
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name.IndexOf("Presentation", StringComparison.Ordinal) >= 0 || a.GetName().Name == "WindowsBase") Console.WriteLine("  loaded: " + a.GetName().Name);
        }

        [STAThread]
        static void Main(string[] argv)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.Scale = Math.Max(1f, Math.Min(4f, g.DpiX / 96f));
            }
            catch { }

            int holdMs = argv.Length > 0 ? int.Parse(argv[0]) : 3000;
            string shot = argv.Length > 1 ? argv[1] : "build/probe.png";

            var f = new MainForm();
            Report();
            Console.WriteLine("-- after ctor --");
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(140, 110);
            f.Text = "外壳探针";
            f.TopMost = true;

            var t = new Timer { Interval = holdMs };
            t.Tick += (s, e) =>
            {
                t.Stop();
                try
                {
                    using (var bmp = new Bitmap(f.Width + 60, f.Height + 60, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(bmp))
                            g.CopyFromScreen(f.Left - 30, f.Top - 30, 0, 0, bmp.Size);
                        bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    Console.WriteLine("PROBE shot saved " + shot + " bounds=" + f.Bounds);
                }
                catch (Exception ex) { Console.Error.WriteLine("PROBE capture failed: " + ex.Message); }
                Report();
                f.ExitApp();
            };
            f.Shown += (s, e) => t.Start();
            Application.Run(f);
        }
    }
}
