using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WindowTool
{
    // 开发期工具：离屏渲染界面为 PNG 供人工核对。
    // 用 SW_SHOWNOACTIVATE 显示到屏幕外，控件真实绘制但不抢焦点、不可见。
    static class RenderHarness
    {
        const int SW_HIDE = 0;
        const int SW_SHOWNOACTIVATE = 4;

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        static void Main(string[] argv)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string dir = argv.Length > 0 ? argv[0] : ".";
            Directory.CreateDirectory(dir);
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.Scale = Math.Max(1f, Math.Min(4f, g.DpiX / 96f));
            }
            catch { }

            SelfCheck();
            PresetCheck();

            if (argv.Length > 1 && argv[1] == "smoke") { Smoke(); return; }

            MainForm.DesignRender = true;
            string[] keys = { "window", "bg", "music", "layout", "env", "general" };
            foreach (string mode in new[] { "light", "dark" })
            {
                WriteSettings(mode == "dark" ? "darkMode=1" : "darkMode=0");
                foreach (string key in keys)
                {
                    var f = new MainForm();
                    f.SelectPage(key, true);
                    for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(15); }
                    Shot(f, Path.Combine(dir, "ui_" + mode + "_" + key + ".png"), 1);
                }
            }

            WriteSettings("darkMode=0");

            var l = new DesktopLyricsForm();
            l.SetLine("中间一段很好听 副歌进来了");
            Shot(l, Path.Combine(dir, "ui_lyrics.png"), 2);

            var t = new TaskbarLyricsForm();
            t.SetLine("正在播放  -  某首歌");
            Shot(t, Path.Combine(dir, "ui_taskbar_lyrics.png"), 2);

            // 迷你播放条：不启动引擎，只把状态摆出来验证排版
            WriteSettings("darkMode=0");
            var mp = new MainForm();
            var tr = new Track { Source = "本地", LocalPath = "D:\\fake.mp3", Title = "夜曲", Artist = "周杰伦" };
            mp.Music.Songs.Add(tr);
            mp.Music.Index = 0;
            Force(mp, "miniVisible", true);
            Call(mp, "LayoutShell");
            var miniField = typeof(MainForm).GetField("mini", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var miniCtl = miniField.GetValue(mp) as Control;
            miniCtl.Visible = true;
            Call(mp, "RefreshMiniBar");
            for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(15); }
            for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(15); }
            Shot(mp, Path.Combine(dir, "ui_mini.png"), 1);

            Console.WriteLine("RENDER DONE scale=" + Theme.Scale + " dir=" + dir);
        }

        // 无窗口冒烟测试：走真实启动路径（托盘、热键注册、全部定时器），但从不 Show 窗口
        static void Smoke()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "WindowTool_crash.log");
            long before = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
            var f = new MainForm();
            f.CreateControl();
            for (int i = 0; i < 90; i++)
            {
                Application.DoEvents();
                Thread.Sleep(20);
                if (i == 30) f.SelectPage("music", true);
                if (i == 50) f.SelectPage("env", true);
                if (i == 70) f.SelectPage("window", true);
            }
            Console.WriteLine("SMOKE pages switched, timers ran. hotkeyFails=" + f.HotkeyFails.Count);
            f.Close();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(20); }
            long after = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
            Console.WriteLine("SMOKE crashlog bytes " + before + " -> " + after);
            if (after > before)
            {
                string all = File.ReadAllText(logPath);
                Console.Error.WriteLine("SMOKE NEW CRASH ENTRIES:\n" + all.Substring((int)before));            }
            Console.WriteLine(after > before ? "SMOKE FAIL" : "SMOKE OK");
        }

        static void Force(object o, string field, object val)
        {
            var f = o.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) { Console.Error.WriteLine("no field " + field); return; }
            f.SetValue(o, val);
        }

        static void Call(object o, string method)
        {
            var m = o.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (m == null) { Console.Error.WriteLine("no method " + method); return; }
            m.Invoke(o, null);
        }

        // 纯逻辑自检：歌词解析与二分定位
        static void SelfCheck()
        {
            var lines = Lrc.Parse("[00:12.30]第一句\n[00:15.00]第二句\n[01:05.5]第三句\n[ti:test]\n");
            Console.WriteLine("LRC lines=" + lines.Count);
            Console.WriteLine("at 0:13 -> " + Lrc.IndexAt(lines, TimeSpan.FromSeconds(13)));
            Console.WriteLine("at 0:16 -> " + Lrc.IndexAt(lines, TimeSpan.FromSeconds(16)));
            Console.WriteLine("at 1:10 -> " + Lrc.IndexAt(lines, TimeSpan.FromSeconds(70)));
            Console.WriteLine("at 0:01 -> " + Lrc.IndexAt(lines, TimeSpan.FromSeconds(1)));
        }

        // 走真实的 AddPreset / SavePresets / LoadPresets 往返；不建窗口、不建托盘
        static void PresetCheck()
        {
            MainForm.DesignRender = true;
            WriteSettings("darkMode=0");
            var f = new MainForm();

            var p = new Preset { Name = "刷题 A|B" };
            p.Items.Add(new PresetEntry
            {
                Proc = "sublime_text", Title = "std.cpp | 模拟赛",
                X = 10, Y = 20, W = 800, H = 600, HasRect = true, Top = true, Alpha = 200
            });
            p.Items.Add(new PresetEntry
            {
                Proc = "chrome", Title = "OJ",
                X = 0, Y = 0, W = 1024, H = 768, HasRect = true, Top = false, Alpha = 255
            });

            f.AddPreset(p);
            f.Dispose();

            // 重新构造：强制走 settings.txt 的磁盘往返，而不是复用内存里的 cfg
            var f2 = new MainForm();
            Console.WriteLine("CHK count=" + f2.Presets.Count);
            if (f2.Presets.Count == 1)
            {
                var g = f2.Presets[0];
                Console.WriteLine("CHK name=[" + g.Name + "] items=" + g.Items.Count);
                foreach (var e in g.Items)
                    Console.WriteLine("  e proc=[" + e.Proc + "] title=[" + e.Title + "] " +
                        e.X + "," + e.Y + " " + e.W + "x" + e.H +
                        " hasRect=" + e.HasRect + " top=" + e.Top + " alpha=" + e.Alpha);
            }
            f2.DeletePreset(f2.Presets.Count > 0 ? f2.Presets[0] : p);
            f2.Dispose();

            var f3 = new MainForm();
            Console.WriteLine("CHK afterDelete=" + f3.Presets.Count);
            f3.Dispose();
        }

        static void WriteSettings(string extra)
        {
            string s = "bgAlpha=240\nswitchMin=0\nswitchSec=0\nfitMode=0\nradius=10\n" +
                "silentStart=0\nbgMem=1\n" +
                extra + "\nbgCount=0\n";
            File.WriteAllText(Path.Combine(Application.StartupPath, "settings.txt"), s);
        }

        static void Shot(Control c, string outPath, int zoom)
        {
            var f = c as Form;
            if (f != null)
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-8000, -8000);
                f.ShowInTaskbar = false;
            }
            c.CreateControl();
            if (f != null) ShowWindow(f.Handle, SW_SHOWNOACTIVATE);
            for (int i = 0; i < 25; i++) { Application.DoEvents(); Thread.Sleep(20); }

            int w = Math.Max(1, c.Width), h = Math.Max(1, c.Height);
            using (var sub = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                c.DrawToBitmap(sub, new Rectangle(0, 0, w, h));
                if (zoom == 1)
                {
                    sub.Save(outPath, ImageFormat.Png);
                }
                else
                {
                    using (var bmp = new Bitmap(w * zoom, h * zoom, PixelFormat.Format32bppArgb))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.FromArgb(105, 110, 120));
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.DrawImage(sub, new Rectangle(0, 0, w * zoom, h * zoom));
                        bmp.Save(outPath, ImageFormat.Png);
                    }
                }
            }
            if (f != null) ShowWindow(f.Handle, SW_HIDE);
            c.Dispose();
        }
    }
}
