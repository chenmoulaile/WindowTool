using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
            MotionCheck();
            IconSheet(dir);
            PresetCheck();

            if (argv.Length > 1 && argv[1] == "smoke") { Smoke(); return; }
            if (argv.Length > 1 && argv[1] == "perf") { Perf(); return; }
            MenuShot(dir);

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

        // 冒烟测试：DesignRender=true 走完整构造（含托盘菜单构建、定时器字段、页面切换），
        // 但不显示窗口、不创建可见托盘图标、不注册全局热键——那三样会打扰正在使用电脑的用户。
        static void Smoke()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "WindowTool_crash.log");
            long before = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
            MainForm.DesignRender = true;
            var f = new MainForm();
            f.CreateControl();
            StartTimers(f);
            FeatureCheck(f);
            for (int i = 0; i < 90; i++)
            {
                Application.DoEvents();
                Thread.Sleep(20);
                if (i == 30) f.SelectPage("music", false);
                if (i == 50) f.SelectPage("env", false);
                if (i == 70) f.SelectPage("window", false);
            }
            Console.WriteLine("SMOKE pages switched (instant + title glide), timers ran.");
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

        static string Touch(string name)
        {
            string p = Path.Combine(Path.GetTempPath(), name);
            File.WriteAllText(p, "0");
            return p;
        }

        // 帧成本体检：页面/侧栏整块重绘耗时 + 真动画的帧间隔分布
        static void Perf()
        {
            MainForm.DesignRender = true;
            var f = new MainForm();
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-8000, -8000);
            f.ShowInTaskbar = false;
            f.CreateControl();
            ShowWindow(f.Handle, SW_SHOWNOACTIVATE);
            for (int i = 0; i < 12; i++) { Application.DoEvents(); Thread.Sleep(20); }

            var bf = BindingFlags.NonPublic | BindingFlags.Instance;
            var page = typeof(MainForm).GetField("current", bf).GetValue(f) as Page;
            var side = typeof(MainForm).GetField("sidebar", bf).GetValue(f) as Control;
            Console.WriteLine("PERF form=" + f.Width + "x" + f.Height + " page=" + page.Width + "x" + page.Height +
                " sidebar=" + side.Width + "x" + side.Height + " scale=" + Theme.Scale + " motion=" + Motion.Enabled);

            foreach (Control c in f.Controls)
            {
                var tl = c as TrafficLights;
                if (tl == null) continue;
                using (var one = new Bitmap(tl.Width, tl.Height))
                {
                    tl.DrawToBitmap(one, new Rectangle(0, 0, tl.Width, tl.Height));
                    var mid = one.GetPixel(tl.Width / 2, tl.Height / 2);
                    Console.WriteLine("PERF lights alone=" + mid + " handle=" + tl.IsHandleCreated +
                        " created=" + tl.Created + " bounds=" + tl.Bounds);
                }
            }
            var z = new System.Text.StringBuilder();
            foreach (Control c in f.Controls)
                z.Append(c.GetType().Name + "@" + c.Bounds + " vis=" + c.Visible + " | ");
            Console.WriteLine("PERF zorder " + z);
            Cost("page", page);
            Cost("sidebar", side);
            Theme.ShadowQuality = 0;
            Console.WriteLine("PERF page paint (no shadow) = " + Best(PaintFull(page)).ToString("0.0") + "ms");
            Theme.ShadowQuality = 100;
            Console.WriteLine("PERF page paint (full shadow) = " + Best(PaintFull(page)).ToString("0.0") + "ms");
            BlitTest(page.Width, page.Height);
            RowBlitTest(page.Width, Theme.S(46), 11);
            var list = new MacList();
            list.SetBounds(0, 0, page.Width, Theme.S(300));
            list.CreateControl();
            for (int i = 0; i < 30; i++) list.Items.Add(new MacListItem("2026 CSP-S MoniSai day" + i + ".cpp")
            { Subtitle = "sublime_text · PX_WINDOW_CLASS" });
            Console.WriteLine("PERF list full paint=" + Best(PaintFull(list)).ToString("0.0") + "ms rows=" + list.Items.Count);

            var rowBand = new Rectangle(0, Theme.S(84) + Theme.S(52) * 2 - Theme.S(6),
                side.Width, Theme.S(52) * 2 + Theme.S(16));
            Console.WriteLine("PERF sidebar 2-rows paint=" + PaintClip(side, rowBand).ToString("0.0") + "ms");

            // 真消息循环里量：Idle 泵 + 墙钟进度
            int steps = 0; long span = 0;
            var probe = new object();
            var marks = new List<long>();
            var sw = Stopwatch.StartNew();
            Motion.To(probe, 300, Ease.Linear, delegate(float t) { steps++; marks.Add(sw.ElapsedMilliseconds); });
            var shut = new System.Windows.Forms.Timer { Interval = 380 };
            shut.Tick += delegate { shut.Stop(); span = sw.ElapsedMilliseconds; Application.ExitThread(); };
            shut.Start();
            Application.Run();
            shut.Dispose();
            Motion.Cancel(probe);
            marks.Sort();
            var gaps = new List<long>();
            for (int i = 1; i < marks.Count; i++) gaps.Add(marks[i] - marks[i - 1]);
            gaps.Sort();
            Console.WriteLine("PERF motion real-loop steps=" + steps + " span=" + span + "ms" +
                (gaps.Count > 0 ? " p50=" + gaps[gaps.Count / 2] + "ms max=" + gaps[gaps.Count - 1] + "ms" : ""));

            var rw = typeof(MainForm).GetMethod("RefreshWindows", BindingFlags.Public | bf);
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++) rw.Invoke(f, null);
            Console.WriteLine("PERF RefreshWindows avg=" + (sw2.ElapsedMilliseconds / 5.0).ToString("0.0") + "ms");

            // 转场合成层：截图成本 + 每帧 blit 帧数
            double grab = Best(delegate
            {
                var t0 = Stopwatch.StartNew();
                var b = SnapLayer.Grab(page);
                double ms = t0.Elapsed.TotalMilliseconds;
                if (b != null) b.Dispose();
                return ms;
            });
            Console.WriteLine("PERF grab page = " + grab.ToString("0.0") + "ms (一次性)");
            var sl = new SnapLayer();
            sl.Parent = page.Parent;
            sl.CreateControl();
            int paints = 0;
            sl.Paint += delegate { paints++; };
            sl.Play(page.Bounds, SnapLayer.Grab(page), SnapLayer.Grab(page), -1);
            var shut2 = new System.Windows.Forms.Timer { Interval = 400 };
            shut2.Tick += delegate { shut2.Stop(); Application.ExitThread(); };
            shut2.Start();
            Application.Run();
            Console.WriteLine("PERF transition blit frames=" + paints + " in 250ms (60fps 应有约 15 帧)");
            sl.Dispose();

            var mm = new MacMenu();
            mm.AddItem("显示主界面", Glyph.Window, null);
            mm.AddSep();
            mm.AddItem("退出", Glyph.Power, MacTone.Danger, null);
            mm.CreateControl();
            int cw = mm.ClientSize.Width - mm.Padding.Horizontal;
            Console.WriteLine("PERF menu " + mm.Width + "x" + mm.Height +
                " itemW=" + mm.Items[0].Width + "," + mm.Items[2].Width +
                " contentW=" + cw + " aligned=" + (mm.Items[0].Width == mm.Items[2].Width && mm.Items[0].Width <= cw));
            mm.Dispose();

            // 侧栏胶囊滑动：真实补间 + 只脏两行
            var sw3 = Stopwatch.StartNew();
            var sb = typeof(MainForm).GetField("sidebar", bf).GetValue(f) as MacSidebar;
            sb.SelectedIndex = 3;
            int lastInv = 0;
            for (int i = 0; i < 40; i++)
            {
                Application.DoEvents();
                Thread.Sleep(4);
                if (i == 6) sidebarPaintSample = TimePaint(sb);
            }
            Console.WriteLine("PERF sidebar glide total=" + sw3.ElapsedMilliseconds + "ms samplePaint=" + sidebarPaintSample +
                "ms (只脏两行时应远小于整栏)");
            sb.SelectedIndex = 0;

            ShowWindow(f.Handle, SW_HIDE);
            f.Dispose();
        }

        static long sidebarPaintSample;

        static long TimePaint(Control c)
        {
            var sw = Stopwatch.StartNew();
            c.Invalidate();
            c.Update();
            return sw.ElapsedMilliseconds;
        }

        // 取多次里的最好值：DrawToBitmap 的噪声（GC/首次建 DC）比真实绘制还大
        static void Cost(string label, Control c)
        {
            if (c == null) return;
            using (var bmp = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height)))
            {
                var best = double.MaxValue;
                for (int i = 0; i < 12; i++)
                {
                    var sw = Stopwatch.StartNew();
                    c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                    double ms = sw.Elapsed.TotalMilliseconds;
                    if (ms < best) best = ms;
                }
                Console.WriteLine("PERF " + label + " paint=" + best.ToString("0.0") + "ms");
            }
        }

        // 只画某一矩形区域（模拟局部重绘），直接调用 OnPaint
        static double Best(Func<double> f)
        {
            double best = double.MaxValue;
            for (int i = 0; i < 12; i++) { double v = f(); if (v < best) best = v; }
            return best;
        }

        // 位图必须复用：每次新建 1.4MP 位图会让"绘制耗时"里混进 14ms 的分配开销
        static Func<double> PaintFull(Control c)
        {
            var reuse = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height));
            return delegate
            {
                var sw = Stopwatch.StartNew();
                c.DrawToBitmap(reuse, new Rectangle(0, 0, c.Width, c.Height));
                return sw.Elapsed.TotalMilliseconds;
            };
        }

        /// 整页位图带偏移 blit：这是"用合成做动画"的成本上限
        static void BlitTest(int w, int h)
        {
            using (var src = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var dst = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(System.Drawing.Color.FromArgb(180, 90, 120, 200));
                double ms = Best(delegate
                {
                    var sw = Stopwatch.StartNew();
                    g.DrawImage(src, new Rectangle(6, 4, w, h), new Rectangle(0, 0, w, h),
                        GraphicsUnit.Pixel);
                    return sw.Elapsed.TotalMilliseconds;
                });
                Console.WriteLine("PERF blit " + w + "x" + h + " = " + ms.ToString("0.0") + "ms/frame");
            }
        }

        /// 逐行位图 blit（列表滚动的目标做法）
        static void RowBlitTest(int w, int rowH, int rows)
        {
            var band = new List<Bitmap>();
            try
            {
                for (int i = 0; i < rows; i++)
                {
                    var b = new Bitmap(w, rowH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(b)) g.Clear(System.Drawing.Color.FromArgb(200, 120, 140, 220));
                    band.Add(b);
                }
                using (var dst = new Bitmap(w, rowH * rows, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                using (var g = Graphics.FromImage(dst))
                {
                    double ms = Best(delegate
                    {
                        var sw = Stopwatch.StartNew();
                        for (int i = 0; i < band.Count; i++) g.DrawImageUnscaled(band[i], 0, i * rowH - 7);
                        return sw.Elapsed.TotalMilliseconds;
                    });
                    Console.WriteLine("PERF rowblit " + rows + "x(" + w + "x" + rowH + ") = " + ms.ToString("0.0") + "ms/frame");
                }
            }
            finally { foreach (var b in band) b.Dispose(); }
        }

        static double PaintClip(Control c, Rectangle clip)
        {
            var m = c.GetType().GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return -1;
            using (var bmp = new Bitmap(clip.Width, clip.Height))
            using (var g = Graphics.FromImage(bmp))
            {
                g.TranslateTransform(-clip.X, -clip.Y);
                var args = new PaintEventArgs(g, clip);
                m.Invoke(c, new object[] { args });
                double best = double.MaxValue;
                for (int i = 0; i < 12; i++)
                {
                    var sw = Stopwatch.StartNew();
                    m.Invoke(c, new object[] { args });
                    double ms = sw.Elapsed.TotalMilliseconds;
                    if (ms < best) best = ms;
                }
                return best;
            }
        }

        static void StartTimers(MainForm f)
        {
            var fi = typeof(MainForm).GetField("tmr", BindingFlags.NonPublic | BindingFlags.Instance);
            var fb = typeof(MainForm).GetField("tmrBg", BindingFlags.NonPublic | BindingFlags.Instance);
            var fa = typeof(MainForm).GetField("tmrAuto", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var m in new[] { fi, fb, fa })
            {
                if (m == null) continue;
                var t = m.GetValue(f) as System.Windows.Forms.Timer;
                if (t != null) { t.Interval = 120; t.Start(); }
            }
        }

        // v1.13 那批功能的离线核对：几何、状态往返、设置导入导出、主题热切
        static void FeatureCheck(MainForm f)
        {
            var wa = new Rectangle(0, 0, 1920, 1040);
            var l = Manager.SnapRect(SnapKind.Left, wa, Rectangle.Empty, false);
            var r = Manager.SnapRect(SnapKind.Right, wa, Rectangle.Empty, false);
            Console.WriteLine("SNAP left=" + l + " right=" + r + " tile=" + (l.Right == r.X && l.Width + r.Width == wa.Width));
            var tl = Manager.SnapRect(SnapKind.TopLeft, wa, Rectangle.Empty, false);
            var br = Manager.SnapRect(SnapKind.BottomRight, wa, Rectangle.Empty, false);
            Console.WriteLine("SNAP topleft=" + tl + " bottomright=" + br);
            var odd = Manager.SnapRect(SnapKind.Left, new Rectangle(0, 0, 1011, 1011), Rectangle.Empty, false);
            var oddR = Manager.SnapRect(SnapKind.Right, new Rectangle(0, 0, 1011, 1011), Rectangle.Empty, false);
            Console.WriteLine("SNAP odd-width cover=" + (odd.Width + oddR.Width));
            var mx = Manager.SnapRect(SnapKind.Max, wa, Rectangle.Empty, false);
            Console.WriteLine("SNAP max==workarea:" + (mx == wa));
            var ct = Manager.SnapRect(SnapKind.Center, wa, new Rectangle(-500, -500, 800, 600), true);
            Console.WriteLine("SNAP center=" + ct + " centered=" + (ct.X + ct.Width / 2 == 960 && ct.Y + ct.Height / 2 == 520));

            // 播放列表与进度的磁盘往返：只认真实存在的本地文件，网易条目不落盘
            string p1 = Touch("wt_a - 夜曲.mp3"), p2 = Touch("wt_b.mp3");
            var cfg = new Settings();
            f.Music.Songs.Clear();
            f.Music.Songs.Add(new Track { Source = "本地", LocalPath = p1, Title = "A", Artist = "x" });
            f.Music.Songs.Add(new Track { Source = "本地", LocalPath = p2, Title = "B", Artist = "y" });
            f.Music.Songs.Add(new Track { Source = "网易", NetId = "12345", Title = "C", Artist = "z" });
            f.Music.Index = 0;
            f.Music.RememberState(cfg);
            var f2 = new MainForm();
            f2.Music.RestoreState(cfg);
            Console.WriteLine("PLIST saved=" + cfg.Int("plCount", -1) + " restored=" + f2.Music.Songs.Count
                + " at=" + f2.Music.Index
                + " first=[" + (f2.Music.Songs.Count > 0 ? f2.Music.Songs[0].Artist + " / " + f2.Music.Songs[0].Title : "?") + "]"
                + " want=2/2 (网易条目应被丢弃, 文件名要拆出歌手)");
            f2.Dispose();
            try { File.Delete(p1); File.Delete(p2); } catch { }

            // 设置导出 / 导入：导出返回目标路径，导入后值必须回得来
            string tmp = Path.Combine(Path.GetTempPath(), "wt_export_check.txt");
            f.Cfg.Set("radius", 13);
            string settingsPath = Path.Combine(Application.StartupPath, "settings.txt");
            f.Cfg.Set("hue", 2);
            string got = f.ExportSettings(tmp);
            bool copied = got == tmp && File.Exists(tmp) && new FileInfo(tmp).Length > 0;
            File.AppendAllText(settingsPath, "radius=4" + (char)10 + "hue=0" + (char)10);
            bool imp = f.ImportSettings(tmp);
            var back = Settings.Load(settingsPath);
            Console.WriteLine("EXPORT ok=" + copied + " IMPORT ok=" + imp
                + " radius=" + back.Int("radius", -1) + " hue=" + back.Int("hue", -1)
                + " want=True/True/13/2");
            try { File.Delete(tmp); } catch { }

            // 主题热切（等价于收到 WM_SETTINGCHANGE 后的重建）
            try
            {
                Theme.SetDark(true); f.ApplyTheme();
                Theme.SetDark(false); f.ApplyTheme();
                Console.WriteLine("THEME live switch ok, dark=" + Theme.IsDark);
            }
            catch (Exception ex) { Console.WriteLine("THEME FAIL " + ex.Message); }

            // 托盘菜单重建：条目、图标、勾选态都得在打开前刷新
            var menuField = typeof(MainForm).GetField("trayMenu", BindingFlags.NonPublic | BindingFlags.Instance);
            var mm = menuField.GetValue(f) as MacMenu;
            var rebuild = typeof(MainForm).GetMethod("RebuildTrayMenu", BindingFlags.NonPublic | BindingFlags.Instance);
            rebuild.Invoke(f, null);
            int withIcon = 0, rows = 0;
            foreach (ToolStripItem it in mm.Items)
            {
                var mi = it as MacMenuItem;
                if (mi == null || mi.Line) continue;
                rows++;
                if (mi.Width > 40 && mi.Height > 20) withIcon++;
            }
            Console.WriteLine("TRAYMENU items=" + mm.Items.Count + " rows=" + rows + " sized=" + withIcon);
            mm.Dispose();
            f2 = null;

            // 毛玻璃：无效句柄必须安静地返回 false，不能抛
            Console.WriteLine("BLUR invalid-hwnd apply=" + Manager.ApplyBlur(new IntPtr(12345), true)
                + " isBlurred=" + Manager.IsBlurred(new IntPtr(12345))
                + " snapInvalid=" + Manager.Snap(new IntPtr(12345), SnapKind.Left));
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

        // 动画引擎自检：曲线端点、补间替换、时钟收尾
        static void MotionCheck()
        {
            Console.WriteLine("MOTION curve out(0)=" + Motion.Curve(Ease.Out, 0f) +
                " out(1)=" + Motion.Curve(Ease.Out, 1f) +
                " inOut(.5)=" + Motion.Curve(Ease.InOut, 0.5f) +
                " spring(1)=" + Motion.Curve(Ease.Spring, 1f) +
                " clamp(-1)=" + Motion.Curve(Ease.Out, -1f) +
                " clamp(9)=" + Motion.Curve(Ease.Out, 9f));

            Motion.Enabled = true;
            int hits = 0;
            float last = -1f;
            bool done = false;
            object tag = new object();
            Motion.To(tag, 90, Ease.Out, delegate(float t) { hits++; last = t; }, delegate { done = true; });
            for (int i = 0; i < 40 && !done; i++) { Application.DoEvents(); Thread.Sleep(10); }
            Console.WriteLine("MOTION tween hits=" + hits + " last=" + last + " done=" + done);

            // 同一 tag 必须互相顶替，否则快速切页时两条补间会抢同一个属性
            int a = 0, b = 0;
            object t2 = new object();
            Motion.To(t2, 400, Ease.Linear, delegate(float t) { a++; });
            Motion.To(t2, 400, Ease.Linear, delegate(float t) { b++; });
            for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(20); }
            Motion.StopAll();
            Console.WriteLine("MOTION replace old=" + a + " new=" + b + " want=1/1 (旧补间只保留它自己的起始帧)");

            Motion.Enabled = false;
            bool instant = false;
            Motion.To(new object(), 400, Ease.Linear, delegate(float t) { instant = t >= 1f; });
            Console.WriteLine("MOTION disabled->instant=" + instant);
            Console.WriteLine("MOTION systemAnimations=" + Motion.SystemAllowsAnimation());
        }

        // 图标族总览：同一套网格下逐个核对，避免"看着像但小尺寸糊成一团"
        static void IconSheet(string dir)
        {
            Array vals = Enum.GetValues(typeof(Glyph));
            int cols = 8, cell = 64;
            int rows = (vals.Length + cols - 1) / cols;
            int w = cols * cell, h = rows * cell * 2 + 8;
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.SetDark(false);
                for (int i = 0; i < vals.Length; i++)
                {
                    Glyph k = (Glyph)vals.GetValue(i);
                    int c = i % cols, r = i / cols;
                    var lightRect = new Rectangle(c * cell, r * cell * 2, cell, cell * 2);
                    g.DrawRectangle(Pens.Gainsboro, lightRect);
                    var box = new RectangleF(c * cell + 14, r * cell * 2 + 10, cell - 28, cell - 28);
                    Icons.Draw(g, k, box, Color.FromArgb(255, 40, 40, 46));
                    var small = new RectangleF(c * cell + 26, r * cell * 2 + cell + 4, 20, 20);
                    Icons.Draw(g, k, small, Color.FromArgb(255, 40, 40, 46));
                }
                bmp.Save(Path.Combine(dir, "icons_light.png"), ImageFormat.Png);

                using (var g2 = Graphics.FromImage(bmp))
                {
                    Theme.SetDark(true);
                    g2.Clear(Color.FromArgb(255, 30, 30, 32));
                    for (int i = 0; i < vals.Length; i++)
                    {
                        Glyph k = (Glyph)vals.GetValue(i);
                        int c = i % cols, r = i / cols;
                        var box = new RectangleF(c * cell + 14, r * cell * 2 + 10, cell - 28, cell - 28);
                        Icons.Draw(g2, k, box, Color.FromArgb(255, 235, 235, 240));
                        var small = new RectangleF(c * cell + 26, r * cell * 2 + cell + 4, 20, 20);
                        Icons.Draw(g2, k, small, Color.FromArgb(255, 235, 235, 240));
                    }
                }
                bmp.Save(Path.Combine(dir, "icons_dark.png"), ImageFormat.Png);
                Theme.SetDark(false);
                Console.WriteLine("ICONS sheet glyphs=" + vals.Length);
            }
        }

        // 托盘菜单：只建句柄不 Show，绘制进位图核对配色/图标/间距
        static void MenuShot(string dir)
        {
            try
            {
                foreach (string mode in new[] { "light", "dark" })
                {
                    Theme.SetDark(mode == "dark");
                    var m = new MacMenu();
                    m.AddItem("显示主界面", Glyph.Window, null);
                    m.AddSep();
                    m.AddItem("继续播放", Glyph.Play, null);
                    m.AddItem("下一首", Glyph.Next, null);
                    m.AddSep();
                    m.AddItem("选择图片背景", Glyph.Picture, null);
                    m.AddItem("选择视频背景", Glyph.Film, null);
                    var off = m.AddItem("关闭桌面背景", Glyph.Close, null);
                    off.Enabled = false;
                    m.AddSep();
                    m.AddItem("取消置顶「std.cpp - Sublime Text」", Glyph.PinSlash, null);
                    m.AddItem("全部取消置顶（7 个）", Glyph.PinSlash, null);
                    m.AddSep();
                    var dm = m.AddItem("深色模式", Glyph.Moon, null); dm.Flag = mode == "dark";
                    var ar = m.AddItem("开机自动运行", Glyph.Bolt, null); ar.Flag = true;
                    m.AddSep();
                    m.AddItem("退出", Glyph.Power, MacTone.Danger, null);

                    // 强制一条进入悬停态：Select() 不会触发 MouseEnter，只能直接推 fade 值
                    int pick = mode == "dark" ? m.Items.Count - 1 : 9;
                    m.CreateControl();
                    m.PerformLayout();
                    var picked = m.Items[pick];
                    var ff = picked.GetType().GetField("fade", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (ff != null)
                    {
                        var hv = ff.GetValue(picked);
                        hv.GetType().GetField("Value").SetValue(hv, 1f);
                        picked.Invalidate();
                    }
                    else Console.WriteLine("MENU 找不到 fade 字段，悬停胶囊未验证");
                    for (int i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(20); }
                    int w = m.Width, h = m.Height;
                    if (w < 40 || h < 40) { Console.WriteLine("MENU size=" + w + "x" + h + " (太小，跳过)"); return; }
                    var dbg = new System.Text.StringBuilder();
                    foreach (ToolStripItem it in m.Items)
                        dbg.Append(it.GetType().Name[8] + ":" + it.Bounds.X + "," + it.Bounds.Y + " " + it.Bounds.Width + "x" + it.Bounds.Height + "  ");
                    Console.WriteLine("MENU bounds " + dbg);
                    using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                    {
                        m.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                        bmp.Save(Path.Combine(dir, "menu_" + mode + ".png"), ImageFormat.Png);
                        Console.WriteLine("MENU " + mode + " " + w + "x" + h + " items=" + m.Items.Count);
                    }
                    m.Dispose();
                }
                Theme.SetDark(false);
            }
            catch (Exception ex) { Console.WriteLine("MENU FAIL " + ex.GetType().Name + ": " + ex.Message); }
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
                // DrawToBitmap 按子控件索引正序合成，而 z-order 是索引 0 在最前：
                // 红绿灯会被侧栏盖掉。按真实 z-order 倒序补画一遍最前的控件。
                if (f != null)
                    for (int zi = f.Controls.Count - 1; zi >= 0; zi--)
                    {
                        var kid = f.Controls[zi];
                        if (!kid.Visible || kid is Form) continue;
                        if (kid.Bounds.Width < 2 || kid.Bounds.Height < 2) continue;
                        try { kid.DrawToBitmap(sub, kid.Bounds); } catch { }
                    }
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
