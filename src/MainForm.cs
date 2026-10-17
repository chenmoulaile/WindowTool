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
                  HK_ALPHA_DEC = 10, HK_ALPHA_INC = 11, HK_VRAND = 12, HK_PLAY = 13;

        public static bool DesignRender;

        MacSidebar sidebar;
        Panel pageHost;
        MacToast toast;
        readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
        Page current;
        string currentPageKey = "";

        Settings cfg;
        public Settings Cfg { get { return cfg; } }

        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        bool startupHidden, reallyExit, balloonShown, loading;

        Timer tmr, tmrBg, tmrAuto;
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
            SelectPage(cfg.Str("page", "window"), true);

            if (DesignRender) return;
            RegisterHotkeys();
            tmr.Start();
            tmrBg.Start();
            tmrAuto.Start();
        }

        // ---------------- 主题 ----------------

        void InitTheme()
        {
            Theme.SetDark(ResolveDark());
            Theme.SetHue((AccentHue)Math.Max(0, Math.Min(6, cfg.Int("hue", 0))));
            Theme.Radius = cfg.Int("radius", 10);
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

            toast = new MacToast();
            Controls.Add(toast);

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
            AddSidebarItem("general", "通用", "外观与热键", Glyph.Gear, Color.FromArgb(142, 142, 147));

            Register(new WindowPage(this));
            Register(new BackgroundPage(this));
            Register(new MusicPage(this));
            Register(new EnvPage(this));
            Register(new GeneralPage(this));
        }

        void Register(Page p)
        {
            pages[p.Key] = p;
            p.Dock = DockStyle.Fill;
            pageHost.Controls.Add(p);
            p.Build();
            p.Visible = false;
        }

        Size PageArea()
        {
            if (pageHost == null) return Theme.Sz(640, 520);
            return pageHost.DisplayRectangle.Size;
        }

        void LayoutShell()
        {
            if (sidebar == null || pageHost == null) return;
            int sbW = Theme.S(230);
            int band = TitleBand;
            sidebar.SetBounds(0, 0, sbW, ClientSize.Height);

            int miniH = miniVisible ? MiniHeight : 0;
            pageHost.SetBounds(sbW, band, ClientSize.Width - sbW, ClientSize.Height - band - miniH);
            pageHost.Padding = new Padding(Theme.S(22), Theme.S(6), Theme.S(22), Theme.S(14));

            if (mini != null) mini.SetBounds(sbW, ClientSize.Height - miniH, ClientSize.Width - sbW, miniH);

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
            int x = Theme.S(230) + Theme.S(22);
            MacUi.LeftText(g, current.Title, Theme.FontTitle,
                new RectangleF(x, Theme.S(11), ClientSize.Width - x - Theme.S(24), Theme.S(24)), Theme.Text);
        }

        internal void SelectPage(string key, bool force)
        {
            Page p;
            if (!pages.TryGetValue(key, out p)) { key = "window"; p = pages["window"]; }
            if (current == p && !force) return;
            if (current != null) current.OnHide();
            current = p;
            currentPageKey = key;
            foreach (var kv in pages) kv.Value.Visible = kv.Value == p;
            int idx = 0;
            for (int i = 0; i < sidebar.Items.Count; i++) if (sidebar.Items[i].Key == key) idx = i;
            if (sidebar.SelectedIndex != idx) sidebar.SelectedIndex = idx;
            LayoutShell();
            p.OnShow();
            p.Tick();          // 立刻回填内容，不等 500ms 定时器
            if (!loading) Save();
            Invalidate();
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

        public void RefreshWindows()
        {
            IntPtr keep = selected;
            winList.Clear();
            Native.EnumWindows((h, lp) =>
            {
                if (!Native.IsWindowVisible(h)) return true;
                int len = Native.GetWindowTextLength(h);
                if (len <= 0) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == selfPid) return true;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;
                var sbT = new StringBuilder(len + 2);
                Native.GetWindowText(h, sbT, sbT.Capacity);
                var sbC = new StringBuilder(256);
                Native.GetClassName(h, sbC, 256);
                string cls = sbC.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true;
                winList.Add(new WinEntry { Hwnd = h, Title = sbT.ToString(), Cls = cls, Proc = ProcName(pid), Icon = IconFor(h) });
                return true;
            }, IntPtr.Zero);

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

        static readonly Dictionary<uint, string> procCache = new Dictionary<uint, string>();
        static readonly Dictionary<uint, Image> iconCache = new Dictionary<uint, Image>();

        static string ProcName(uint pid)
        {
            string s;
            if (procCache.TryGetValue(pid, out s)) return s;
            try { using (var p = System.Diagnostics.Process.GetProcessById((int)pid)) s = p.ProcessName; }
            catch { s = ""; }
            procCache[pid] = s;
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
                {
                    var ic = System.Drawing.Icon.ExtractAssociatedIcon(p.MainModule.FileName);
                    if (ic != null) img = ic.ToBitmap();
                }
            }
            catch { }
            iconCache[pid] = img;
            return img;
        }

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
            Native.GetWindowText(h, sbT, 512);
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
            foreach (var w in winList) if (w.Hwnd == h) return w;
            return null;
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
            ApplyBgCore(playlistTarget, () => new BgWindow(playlistTarget, path, fit), meta);
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
                if (current != null) current.Tick();
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
            trayMenu = new ContextMenuStrip();
            RebuildTrayMenu();

            tray = new NotifyIcon();
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { tray.Icon = SystemIcons.Application; }
            tray.Text = "OI 工具箱";
            tray.ContextMenuStrip = trayMenu;
            tray.Visible = !DesignRender;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
            tray.DoubleClick += (s, e) => ShowMain();
        }

        void RebuildTrayMenu()
        {
            trayMenu.Items.Clear();
            trayMenu.Items.Add("显示主界面", null, (s, e) => ShowMain());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("选择图片背景", null, (s, e) => TrayApplyBg(true));
            trayMenu.Items.Add("选择视频背景", null, (s, e) => TrayApplyBg(false));
            trayMenu.Items.Add("取消背景", null, (s, e) => TrayBgOff());
            trayMenu.Items.Add(new ToolStripSeparator());

            var pinned = Manager.PinnedSnapshot();
            if (pinned.Length > 0)
            {
                foreach (var h in pinned)
                {
                    IntPtr hh = h;
                    string t = TitleOf(hh);
                    trayMenu.Items.Add("取消「" + Theme.Ellipsis(t, Theme.FontUi, Theme.S(200)) + "」",
                        null, (s, e) => { Manager.CancelOp2(hh); Toast("已还原一个置顶窗口。", ToastKind.Good); });
                }
                trayMenu.Items.Add(new ToolStripSeparator());
            }

            if (music != null && music.HasSong)
            {
                trayMenu.Items.Add(music.Playing ? "暂停" : "播放", null, (s, e) => music.Toggle());
                trayMenu.Items.Add("下一首", null, (s, e) => music.Next(true));
                trayMenu.Items.Add(new ToolStripSeparator());
            }

            var exit = new ToolStripMenuItem("退出");
            exit.Click += (s, e) => { reallyExit = true; Close(); };
            trayMenu.Items.Add(exit);
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
            try { Manager.CancelAll(); } catch (Exception ex) { Program.Log(ex); }
            try { Manager.RestoreAll(); } catch (Exception ex) { Program.Log(ex); }
            base.OnFormClosed(e);
        }
    }
}
