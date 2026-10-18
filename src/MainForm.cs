using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    sealed class WinEntry
    {
        public IntPtr Hwnd;
        public string Title = "";
        public string Cls = "";
        public string Proc = "";
        public Image Icon;
        public override string ToString() { return Title + "   [" + Cls + "]"; }
    }

    sealed partial class MainForm : MacForm
    {
        const int WM_HOTKEY = 0x0312;
        const int HK_TOP = 1, HK_OP2 = 2, HK_UP = 3, HK_DOWN = 4, HK_CANCELONE = 5,
                  HK_CANCELALL = 6, HK_IMG = 7, HK_VID = 8, HK_BGOFF = 9,
                  HK_ALPHA_DEC = 10, HK_ALPHA_INC = 11, HK_VRAND = 12, HK_PLAY = 13,
                  HK_SNAP_L = 20, HK_SNAP_R = 21, HK_SNAP_T = 22, HK_SNAP_B = 23,
                  HK_SNAP_Q1 = 24, HK_SNAP_Q2 = 25, HK_SNAP_Q3 = 26, HK_SNAP_Q4 = 27,
                  HK_SNAP_MAX = 28, HK_SNAP_BACK = 29;

        public static bool DesignRender;

        MacSidebar sidebar;
        Panel pageHost;
        SnapLayer slide;
        MacToast toast;
        readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
        Page current;
        string currentPageKey = "";
        int titleDx;
        readonly object titleTag = new object();

        Settings cfg;
        public Settings Cfg { get { return cfg; } }

        NotifyIcon tray;
        MacMenu trayMenu;
        bool startupHidden, reallyExit, balloonShown, loading;

        Timer tmr, tmrBg, tmrAuto, trimT;
        MouseCapture capture;
        readonly uint selfPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        readonly Dictionary<IntPtr, BgWindow> bgForms = new Dictionary<IntPtr, BgWindow>();
        readonly Dictionary<IntPtr, BgMeta> bgMeta = new Dictionary<IntPtr, BgMeta>();
        readonly List<string[]> pendingRestore = new List<string[]>();
        int restoreTries;

        readonly List<WinEntry> winList = new List<WinEntry>();
        IntPtr selected = IntPtr.Zero;
        public event EventHandler TargetChanged;

        public sealed class BgMeta
        {
            public string Kind;
            public string Path;
            public byte Alpha;
            public int Fit;
        }

        public MainForm()
        {
            cfg = Settings.Load(SettingsPath());
            InitTheme();

            Text = "OI 工具箱";
            Font = Theme.FontUi;
            BackColor = Theme.Window;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = Theme.Sz(760, 520);
            ClientSize = Theme.Sz(cfg.Int("winW", 880), cfg.Int("winH", 616));

            capture = new MouseCapture();
            capture.Clicked += OnPickedPoint;

            BuildShell();
            StartMusic();
            BuildPages();
            BuildTimers();
            BuildTray();

            RefreshWindows();
            LoadPrefs();
            LoadPresets();
            SelectPage(cfg.Str("page", "window"), true);

            if (DesignRender) return;
            RegisterHotkeys();
            tmr.Start();
            tmrBg.Start();
            tmrAuto.Start();
            trimT.Start();
        }

        // ---------------- 主题 ----------------

        void InitTheme()
        {
            Theme.SetDark(ResolveDark());
            Theme.SetHue((AccentHue)Math.Max(0, Math.Min(6, cfg.Int("hue", 0))));
            Theme.Radius = cfg.Int("radius", 10);
            // 动画默认跟随 Windows 的"显示动画"设置，用户可在通用页单独关掉
            Motion.Enabled = cfg.Int("motion", Motion.SystemAllowsAnimation() ? 1 : 0) != 0;
        }

        public bool ResolveDark()
        {
            int mode = cfg.Int("darkMode", cfg.Bool("dark", false) ? 1 : 0);   // 0 亮 / 1 暗 / 2 跟随系统
            if (mode == 2) return SystemLightsDark();
            return mode == 1;
        }

        public static bool SystemLightsDark()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        public void ApplyTheme()
        {
            Theme.SetDark(ResolveDark());
            Theme.SetHue((AccentHue)Math.Max(0, Math.Min(6, cfg.Int("hue", 0))));
            Theme.Radius = cfg.Int("radius", 10);
            BackColor = Theme.Window;
            Theme.Restyle(this);
            ApplyChrome();
            LayoutShell();
            if (current != null) current.Relayout(PageArea());
            if (mini != null) mini.Invalidate();
            Invalidate();
        }

        // ---------------- 外壳布局 ----------------

        void BuildShell()
        {
            sidebar = new MacSidebar();
            sidebar.Header = "OI 工具箱";
            sidebar.SelectedIndexChanged += (s, e) =>
            {
                if (sidebar.Selected != null) SelectPage(sidebar.Selected.Key, false);
            };
            Controls.Add(sidebar);

            pageHost = new Panel();
            pageHost.BackColor = Theme.Window;
            Controls.Add(pageHost);

            slide = new SnapLayer();
            pageHost.Controls.Add(slide);

            toast = new MacToast();
            Controls.Add(toast);
            // 红绿灯必须压过侧栏：DrawToBitmap 按创建顺序合成子窗口，
            // 不显式置顶的话自绘截图里会被侧栏盖掉（实机 z-order 也一样不可靠）
            foreach (Control c in Controls) if (c is TrafficLights) c.BringToFront();

            BuildMiniBar();
            LayoutShell();
        }

        void AddSidebarItem(string key, string title, string subtitle, Glyph icon, Color tile)
        {
            var it = new SidebarItem { Key = key, Title = title, Subtitle = subtitle, Icon = icon, Tile = tile };
            sidebar.Items.Add(it);
        }

        void BuildPages()
        {
            AddSidebarItem("window", "窗口", "置顶与穿透", Glyph.Pin, Color.FromArgb(0, 122, 255));
            AddSidebarItem("bg", "背景", "图片与视频", Glyph.Picture, Color.FromArgb(175, 82, 222));
            AddSidebarItem("music", "音乐", "播放与歌词", Glyph.Music, Color.FromArgb(255, 45, 85));
            AddSidebarItem("env", "竞赛环境", "一键部署", Glyph.Box, Color.FromArgb(48, 176, 199));
            AddSidebarItem("layout", "布局方案", "一键摆位", Glyph.Layers, Color.FromArgb(88, 86, 214));
            AddSidebarItem("general", "通用", "外观与热键", Glyph.Gear, Color.FromArgb(142, 142, 147));

            Register(new WindowPage(this));
            Register(new BackgroundPage(this));
            Register(new MusicPage(this));
            Register(new EnvPage(this));
            Register(new LayoutPage(this));
            Register(new GeneralPage(this));
        }

        void Register(Page p)
        {
            pages[p.Key] = p;
            p.Dock = DockStyle.None;
            pageHost.Controls.Add(p);
            p.Build();
            p.Visible = false;
            p.Place(PageRect());
        }

        Rectangle PageRect()
        {
            if (pageHost == null) return new Rectangle(0, 0, Theme.S(640), Theme.S(520));
            return pageHost.ClientRectangle;
        }

        Size PageArea()
        {
            if (pageHost == null) return Theme.Sz(640, 520);
            return pageHost.DisplayRectangle.Size;
        }

        void LayoutShell()
        {
            if (sidebar == null || pageHost == null) return;
            int sbW = Theme.SidebarW;
            int band = TitleBand;
            sidebar.SetBounds(0, 0, sbW, ClientSize.Height);

            int miniH = miniVisible ? MiniHeight : 0;
            pageHost.SetBounds(sbW, band, ClientSize.Width - sbW, ClientSize.Height - band - miniH);
            pageHost.Padding = new Padding(Theme.S(22), Theme.S(6), Theme.S(22), Theme.S(14));

            if (mini != null) mini.SetBounds(sbW, ClientSize.Height - miniH, ClientSize.Width - sbW, miniH);

            foreach (var kv in pages) kv.Value.Place(PageRect());

            toast.BringToFront();
            toast.Location = new Point(pageHost.Left + (pageHost.Width - toast.Width) / 2, 0);

            if (current != null) current.Relayout(PageArea());
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutShell();
        }

        protected override void OnChromePaint(Graphics g)
        {
            if (current == null) return;
            int x = Theme.SidebarW + Theme.S(22) + titleDx;
            MacUi.LeftText(g, current.Title, Theme.FontTitle,
                new RectangleF(x, Theme.S(11), ClientSize.Width - x - Theme.S(24), Theme.S(24)), Theme.Text);
            // 侧栏不再放副标题，说明文字挪到这里，页面仍然说得出自己是干什么的
            var si = sidebar == null ? null : sidebar.Selected;
            if (si != null && !string.IsNullOrEmpty(si.Subtitle))
            {
                int tw = TextRenderer.MeasureText(current.Title, Theme.FontTitle, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                MacUi.LeftText(g, si.Subtitle, Theme.FontSmall,
                    new RectangleF(x + tw + Theme.S(10), Theme.S(15),
                        ClientSize.Width - x - tw - Theme.S(40), Theme.S(18)), Theme.TextFaint);
            }

        }

        internal void SelectPage(string key, bool force)
        {
            Page p;
            if (!pages.TryGetValue(key, out p)) { key = "window"; p = pages["window"]; }
            if (current == p && !force) return;
            int prev = sidebar == null ? 0 : sidebar.SelectedIndex;
            int idx = 0;
            for (int i = 0; i < sidebar.Items.Count; i++) if (sidebar.Items[i].Key == key) idx = i;
            Page old = current;

            // 转场靠位图合成：整页重绘 17.9ms/帧，逐帧移动必然掉帧；
            // 截两张图后每帧只 blit（0.7ms/张）。
            bool animate = !force && Motion.Enabled && !DesignRender && slide != null &&
                old != null && old != p && old.Visible;
            Bitmap oldSnap = animate ? SnapLayer.Grab(old) : null;

            if (old != null) old.OnHide();
            current = p;
            currentPageKey = key;
            foreach (var kv in pages) kv.Value.Visible = kv.Value == p;
            if (force) sidebar.SetSelectedSilent(idx);
            else if (sidebar.SelectedIndex != idx) sidebar.SelectedIndex = idx;
            LayoutShell();
            p.OnShow();
            p.Tick();          // 立刻回填内容，不等 500ms 定时器

            if (animate)
            {
                p.Update();
                slide.Play(PageRect(), oldSnap, SnapLayer.Grab(p), idx >= prev ? -1 : 1);
            }
            else if (oldSnap != null) oldSnap.Dispose();

            if (!loading) Save();
            if (force) titleDx = 0;
            else AnimateTitle(idx >= prev ? 1 : -1);
            Invalidate();
        }

        /// 页面内容瞬时切换（整页重绘 18ms，逐帧位移必然掉帧），
        /// 标题带跟着滑一下：只脏 1760x88 的一条，单帧约 1ms。
        void AnimateTitle(int dir)
        {
            if (!Motion.Enabled) { titleDx = 0; Invalidate(TitleBandRect()); return; }
            int from = Theme.S(16) * dir;
            titleDx = from;
            Motion.To(titleTag, 250, Ease.Out, delegate(float t)
            {
                titleDx = (int)Math.Round(from * (1f - t));
                Invalidate(TitleBandRect());
            });
        }

        Rectangle TitleBandRect()
        {
            return new Rectangle(Theme.SidebarW, 0, Math.Max(1, ClientSize.Width - Theme.SidebarW), TitleBand);
        }

        // ---------------- 提示 ----------------

        public void Toast(string msg) { Toast(msg, ToastKind.Info); }
        public void ToastGood(string msg) { Toast(msg, ToastKind.Good); }
        public void ToastWarn(string msg) { Toast(msg, ToastKind.Warn); }
        public void ToastBad(string msg) { Toast(msg, ToastKind.Bad); }

        public void Toast(string msg, ToastKind k)
        {
            if (DesignRender || toast == null) return;
            try
            {
                toast.Show(msg, k, Math.Max(Theme.S(220), pageHost.Width - Theme.S(40)));
                toast.Location = new Point(pageHost.Left + (pageHost.Width - toast.Width) / 2, 0);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ---------------- 设置持久化 ----------------

        static string SettingsPath()
        {
            return Path.Combine(Application.StartupPath, "settings.txt");
        }

        public void Save()
        {
            if (loading) return;
            cfg.Set("winW", (int)(ClientSize.Width / Theme.Scale));
            cfg.Set("winH", (int)(ClientSize.Height / Theme.Scale));
            cfg.Set("page", currentPageKey);
            SaveMusic();
            cfg.Save();
        }

        void LoadPrefs()
        {
            loading = true;
            if (cfg.Bool("silentStart", false)) startupHidden = true;
            loading = false;
        }

        // ---------------- 目标窗口服务 ----------------

        public IntPtr Target { get { return selected; } }

        public bool Ok(IntPtr h) { return h != IntPtr.Zero && Native.IsWindow(h); }

        // 复用缓冲区：EnumWindows 是 UI 线程同步回调，重复刷新时不必每次新建字符串构造器
        static readonly StringBuilder sbTitle = new StringBuilder(512);
        static readonly StringBuilder sbClass = new StringBuilder(256);
        readonly List<WinEntry> refreshScratch = new List<WinEntry>();

        public void RefreshWindows()
        {
            IntPtr keep = selected;
            refreshScratch.Clear();
            sbTitle.EnsureCapacity(512);
            Native.EnumWindows((h, lp) =>
            {
                if (!Native.IsWindowVisible(h)) return true;
                int len = Native.GetWindowTextLength(h);
                if (len <= 0) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == selfPid) return true;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;

                sbTitle.Length = 0;
                Native.GetWindowText(h, sbTitle, sbTitle.Capacity);
                sbClass.Length = 0;
                Native.GetClassName(h, sbClass, sbClass.Capacity);
                string cls = sbClass.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true;
                string title = sbTitle.ToString();

                // 标题与类名没变就复用原对象，避免每次刷新重建整张列表
                WinEntry e;
                if (!byHwnd.TryGetValue(h, out e) || e.Title != title || e.Cls != cls)
                {
                    e = new WinEntry { Hwnd = h, Title = title, Cls = cls, Proc = ProcName(pid), Icon = IconFor(h) };
                    byHwnd[h] = e;
                }
                refreshScratch.Add(e);
                return true;
            }, IntPtr.Zero);

            winList.Clear();
            winList.AddRange(refreshScratch);

            if (byHwnd.Count > winList.Count + 32)
            {
                var dead = new List<IntPtr>(byHwnd.Count);
                foreach (var kv in byHwnd) if (!Native.IsWindow(kv.Key)) dead.Add(kv.Key);
                foreach (var d in dead) byHwnd.Remove(d);
            }

            bool found = false;
            foreach (var w in winList) if (w.Hwnd == keep) found = true;
            if (!found)
            {
                IntPtr fg = Native.GetForegroundWindow();
                foreach (var w in winList) if (w.Hwnd == fg) { selected = fg; found = true; break; }
            }
            if (!found) selected = winList.Count > 0 ? winList[0].Hwnd : IntPtr.Zero;
            RaiseTarget();
        }

        // 缓存必须有界：图标按 PID 缓存且原先从不清理，进程退出后位图还留着，
        // 长时间挂机会一直涨。图标也统一缩到 32px 再存（原始可能是 256px，一张就 256KB）。
        const int MaxCachedProcs = 192;
        const int MaxCachedIcons = 48;
        static readonly Dictionary<uint, string> procCache = new Dictionary<uint, string>();
        static readonly Queue<uint> procOrder = new Queue<uint>();
        static readonly Dictionary<uint, Image> iconCache = new Dictionary<uint, Image>();
        static readonly Queue<uint> iconOrder = new Queue<uint>();

        static string ProcName(uint pid)
        {
            string s;
            if (procCache.TryGetValue(pid, out s)) return s;
            try { using (var p = System.Diagnostics.Process.GetProcessById((int)pid)) s = p.ProcessName; }
            catch { s = ""; }
            procCache[pid] = s;
            procOrder.Enqueue(pid);
            while (procOrder.Count > MaxCachedProcs) procCache.Remove(procOrder.Dequeue());
            return s;
        }

        static Image IconFor(IntPtr h)
        {
            uint pid; Native.GetWindowThreadProcessId(h, out pid);
            Image img;
            if (iconCache.TryGetValue(pid, out img)) return img;
            img = null;
            try
            {
                using (var p = System.Diagnostics.Process.GetProcessById((int)pid))
                using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(p.MainModule.FileName))
                {
                    if (ic != null)
                    {
                        var small = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(ic.ToBitmap(), 0, 0, 32, 32);
                        }
                        img = small;
                    }
                }
            }
            catch { }
            iconCache[pid] = img;
            iconOrder.Enqueue(pid);
            while (iconOrder.Count > MaxCachedIcons)
            {
                uint dead = iconOrder.Dequeue();
                Image old;
                if (iconCache.TryGetValue(dead, out old))
                {
                    iconCache.Remove(dead);
                    if (old != null) old.Dispose();
                }
            }
            return img;
        }

        /// 把工作集交还给系统。隐藏到托盘后调用，驻留后台时内存占用能明显降下来。
        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);
                Native.SetProcessWorkingSetSize(Native.GetCurrentProcess(), -1, -1);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// 按 HWND 复用条目，避免每次刷新都重建整张列表和重新取图标
        readonly Dictionary<IntPtr, WinEntry> byHwnd = new Dictionary<IntPtr, WinEntry>();

        public void SetTarget(IntPtr h)
        {
            if (selected == h) return;
            selected = h;
            RaiseTarget();
        }

        void RaiseTarget()
        {
            var h = TargetChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void AddPicked(IntPtr h)
        {
            var sbT = new StringBuilder(512);
            Native.GetWindowText(h, sbT, sbT.Capacity);
            var sbC = new StringBuilder(256);
            Native.GetClassName(h, sbC, 256);
            uint pid; Native.GetWindowThreadProcessId(h, out pid);
            winList.Insert(0, new WinEntry
            {
                Hwnd = h,
                Title = sbT.Length == 0 ? "(无标题)" : sbT.ToString(),
                Cls = sbC.ToString(),
                Proc = ProcName(pid),
                Icon = IconFor(h)
            });
            selected = h;
            RaiseTarget();
        }

        public WinEntry Entry(IntPtr h)
        {
            WinEntry e;
            return byHwnd.TryGetValue(h, out e) ? e : null;
        }

        public List<WinEntry> WindowList { get { return winList; } }

        public void BeginPick() { OnPick(null, EventArgs.Empty); }

        public DisplayFit Fit
        {
            get { return CurrentFit(); }
            set
            {
                cfg.Set("fitMode", (int)value);
                Save();
                ReapplyFit();
            }
        }

        void ReapplyFit()
        {
            if (videoFiles == null || videoIdx < 0 || !Ok(playlistTarget) || !bgForms.ContainsKey(playlistTarget)) return;
            string path = videoFiles[videoIdx];
            BgMeta old;
            bgMeta.TryGetValue(playlistTarget, out old);
            var meta = new BgMeta
            {
                Kind = "vid",
                Path = path,
                Alpha = old != null ? old.Alpha : BgAlpha(),
                Fit = (int)CurrentFit()
            };
            var fit = CurrentFit();
            ApplyBgCore(playlistTarget, () => new VideoBgWindow(playlistTarget, path, fit), meta);
        }

        public void SetBgAlpha(int v)
        {
            cfg.Set("bgAlpha", Math.Max(60, Math.Min(250, v)));
            Save();
            foreach (var kv in new Dictionary<IntPtr, BgMeta>(bgMeta))
            {
                kv.Value.Alpha = (byte)Math.Max(60, Math.Min(250, v));
                if (Ok(kv.Key)) Manager.ApplyBg(kv.Key, kv.Value.Alpha);
            }
        }

        public void SetRadius(int v)
        {
            cfg.Set("radius", Math.Max(0, Math.Min(20, v)));
            Theme.Radius = cfg.Int("radius", 10);
            Save();
            ApplyTheme();
        }

        /// 从后台线程安全地回到 UI 线程。句柄尚未创建（静默启动/正在关闭）时丢弃并记日志，
        /// 而不是抛 InvalidOperationException。
        public void Ui(Action a)
        {
            if (a == null) return;
            try
            {
                // 静默启动时窗口可能从未创建句柄，此时丢弃 UI 回调是正常路径，不记崩溃日志
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(a);
                else a();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void OnPick(object sender, EventArgs e)
        {
            capture.Start();
            if (capture.Active)
            {
                Cursor = Cursors.Cross;
                Toast("点击任意窗口以选中，按 Esc 取消。", ToastKind.Info);
            }
        }

        void OnPickedPoint(Point pt)
        {
            Cursor = Cursors.Default;
            IntPtr h = Native.WindowFromPoint(new Native.POINT { X = pt.X, Y = pt.Y });
            if (h != IntPtr.Zero) h = Native.GetAncestor(h, Native.GA_ROOT);
            if (h == IntPtr.Zero) return;
            uint pid; Native.GetWindowThreadProcessId(h, out pid);
            if (pid == selfPid) { Toast("不能选择本程序自身。", ToastKind.Warn); return; }
            RefreshWindows();
            if (Entry(h) == null) AddPicked(h);
            else SetTarget(h);
            Toast("已选中：" + (Entry(h) == null ? "" : Entry(h).Title), ToastKind.Good);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && capture != null && capture.Active)
            {
                capture.Stop();
                Cursor = Cursors.Default;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- 定时器 ----------------

        void BuildTimers()
        {
            tmr = new Timer { Interval = 500 };
            tmr.Tick += OnTick;
            tmrBg = new Timer { Interval = 60 };
            tmrBg.Tick += OnBgTick;
            tmrAuto = new Timer { Interval = 1000 };
            tmrAuto.Tick += OnSecondTick;
            trimT = new Timer { Interval = 4000 };
            trimT.Tick += (sn, ev) => { trimT.Stop(); TrimWorkingSet(); };
        }

        void OnTick(object sender, EventArgs e)
        {
            try
            {
                Manager.Cleanup();
                if (selected != IntPtr.Zero && !Native.IsWindow(selected))
                {
                    selected = IntPtr.Zero;
                    RaiseTarget();
                }
                TryRestoreBackgrounds();
                foreach (var kv in pages) { }
                // 动画期间让路：页面同步要遍历控件与窗口状态，和补间抢同一帧会掉帧
                if (!Motion.Busy && current != null) current.Tick();
                if (mini != null && miniVisible) mini.Repaint();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void OnSecondTick(object sender, EventArgs e)
        {
            try { AutoSwitchVideo(); }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ---------------- 托盘 ----------------

        void BuildTray()
        {
            trayMenu = new MacMenu();
            trayMenu.OnRefresh = RebuildTrayMenu;
            RebuildTrayMenu();

            tray = new NotifyIcon();
            tray.Icon = AppIcon();
            tray.Text = "OI 工具箱";
            tray.ContextMenuStrip = trayMenu;
            tray.Visible = !DesignRender;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
            tray.DoubleClick += (s, e) => ShowMain();
        }

        /// 按当前 DPI 的托盘尺寸取图标里最接近的一帧，避免系统把 256 帧硬缩成糊的。
        /// 只能从内嵌资源流里取：new Icon(exe 路径, w, h) 解析不出 csc 写进去的组图标。
        static Icon AppIcon()
        {
            int px = Math.Max(16, Theme.S(16));
            if (px > 128) px = 128;     // GDI+ 读不到 256 帧（ICO 里用 0 表示）
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var st = asm.GetManifestResourceStream("WindowTool.tray.ico"))
                    if (st != null) return new Icon(st, px, px);
            }
            catch (Exception ex) { Program.Log(ex); }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            return SystemIcons.Application;
        }

        void RebuildTrayMenu()
        {
            var m = trayMenu;
            m.Items.Clear();

            m.AddItem("显示主界面", Glyph.Window, (s, e) => ShowMain());

            if (music != null && music.HasSong)
            {
                m.AddSep();
                m.AddItem(music.Playing ? "暂停播放" : "继续播放", music.Playing ? Glyph.Pause : Glyph.Play,
                    (s, e) => music.Toggle());
                m.AddItem("下一首", Glyph.Next, (s, e) => music.Next(true));
            }

            m.AddSep();
            m.AddItem("选择图片背景", Glyph.Picture, (s, e) => TrayApplyBg(true));
            m.AddItem("选择视频背景", Glyph.Film, (s, e) => TrayApplyBg(false));
            var off = m.AddItem("关闭桌面背景", Glyph.Close, (s, e) => TrayBgOff());
            off.Enabled = bgForms.Count > 0;

            var pinned = Manager.PinnedSnapshot();
            if (pinned.Length > 0)
            {
                m.AddSep();
                int shown = Math.Min(pinned.Length, 5);
                for (int i = 0; i < shown; i++)
                {
                    IntPtr hh = pinned[i];
                    string t = Theme.Ellipsis(TitleOf(hh), Theme.FontUi, Theme.S(210));
                    m.AddItem("取消置顶「" + t + "」", Glyph.PinSlash,
                        (s, e) => { Manager.CancelOp2(hh); Toast("已还原一个置顶窗口。", ToastKind.Good); });
                }
                if (pinned.Length > shown)
                {
                    m.AddItem("全部取消置顶（" + pinned.Length + " 个）", Glyph.PinSlash,
                        (s, e) =>
                        {
                            int n = Manager.CancelAll();
                            Toast("已还原 " + n + " 个置顶窗口。", ToastKind.Good);
                        });
                }
            }

            m.AddSep();
            bool dark = Theme.IsDark;
            int mode = cfg.Int("darkMode", dark ? 1 : 0);
            if (mode == 2) dark = SystemLightsDark();
            var dm = m.AddItem("深色模式", Glyph.Moon, (s, e) => SetTrayDark(!Theme.IsDark));
            dm.Flag = dark;
            var ar = m.AddItem("开机自动运行", Glyph.Bolt, (s, e) => ToggleAutoRun());
            ar.Flag = AutoRunOn();

            m.AddSep();
            m.AddItem("退出", Glyph.Power, MacTone.Danger, (s, e) => { reallyExit = true; Close(); });
        }

        void SetTrayDark(bool on)
        {
            cfg.Set("darkMode", on ? 1 : 0);
            cfg.Set("dark", on);
            ApplyTheme();
            Save();
        }

        public bool AutoRunOn()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    return k != null && k.GetValue("WindowTool") != null;
            }
            catch { }
            return false;
        }

        public void ToggleAutoRun()
        {
            SetAutoRun(!AutoRunOn(), true);
        }

        /// 通用页与托盘菜单共用：on 为 false 时删除 Run 项。
        public void SetAutoRun(bool on, bool notify)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) { ToastBad("无法打开注册表启动项。"); return; }
                    if (on) k.SetValue("WindowTool", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("WindowTool", false);
                }
                if (notify) ToastGood(on ? "已开启开机自动运行。" : "已关闭开机自动运行。");
            }
            catch (Exception ex) { Program.Log(ex); ToastBad("设置失败：" + ex.Message); }
        }

        static string TitleOf(IntPtr h)
        {
            var sb = new StringBuilder(512);
            Native.GetWindowText(h, sb, sb.Capacity);
            return sb.Length == 0 ? "(无标题)" : sb.ToString();
        }

        IntPtr MenuTarget()
        {
            IntPtr t = HotTarget();
            if (!Ok(t)) { Balloon("请先在「窗口」页选择一个目标窗口。"); return IntPtr.Zero; }
            return t;
        }

        void Balloon(string text)
        {
            if (tray == null) return;
            try
            {
                tray.BalloonTipTitle = "OI 工具箱";
                tray.BalloonTipText = text;
                tray.ShowBalloonTip(1600);
            }
            catch { }
        }

        void TrayApplyBg(bool image)
        {
            IntPtr t = MenuTarget();
            if (!Ok(t)) return;
            if (image) PickImageAndApply(t); else PickVideoAndApply(t);
        }

        void TrayBgOff()
        {
            IntPtr t = MenuTarget();
            if (!Ok(t)) return;
            bool had = Manager.ClearBg(t);
            if (had) { CloseBgForm(t); videoFiles = null; playlistTarget = IntPtr.Zero; }
            Balloon(had ? "已取消背景并还原窗口样式。" : "该窗口当前没有背景。");
        }

        void ShowMain()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startupHidden)
            {
                startupHidden = false;
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Save();
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (trimT != null) trimT.Start();     // 收进托盘后把内存交还系统
                if (!balloonShown)
                {
                    balloonShown = true;
                    Balloon("已隐藏到托盘，所有功能继续生效。单击托盘图标可再次打开。");
                }
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (tmr != null) tmr.Stop();
            if (tmrBg != null) tmrBg.Stop();
            if (tmrAuto != null) tmrAuto.Stop();
            if (trimT != null) { trimT.Stop(); trimT.Dispose(); trimT = null; }
            UnregisterHotkeys();
            if (capture != null) capture.Dispose();
            StopMusic();
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
            foreach (var kv in new Dictionary<IntPtr, BgWindow>(bgForms))
            {
                Manager.ClearBg(kv.Key);
                CloseBgForm(kv.Key);
            }
            try { Manager.DisableAllBlurs(); } catch (Exception ex) { Program.Log(ex); }
            try { Manager.CancelAll(); } catch (Exception ex) { Program.Log(ex); }
            try { Manager.RestoreAll(); } catch (Exception ex) { Program.Log(ex); }
            base.OnFormClosed(e);
        }
    }
}
