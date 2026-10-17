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
                f.ExitApp();
            };
            f.Shown += (s, e) => t.Start();
            Application.Run(f);
        }
    }
}
