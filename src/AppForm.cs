using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    public enum ToastKind { Info, Good, Warn, Bad }

    sealed class WinEntry
    {
        public IntPtr Hwnd;
        public string Title = "";
        public string Cls = "";
        public string Proc = "";
        public Image Icon;
        public override string ToString() { return Title; }
    }

    /// <summary>
    /// 主窗体外壳：暗色现代侧栏布局（图标导航栏 + 内容页），
    /// 常规 − □ × 窗口按钮，置顶看门狗，托盘常驻。
    /// </summary>
    sealed partial class AppForm : Form
    {
        const int WM_HOTKEY = 0x0312;
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                  HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        const int HK_TOP = 1, HK_OP2 = 2, HK_UP = 3, HK_DOWN = 4, HK_CANCELONE = 5,
                  HK_CANCELALL = 6, HK_IMG = 7, HK_BGOFF = 9,
                  HK_ALPHA_DEC = 10, HK_ALPHA_INC = 11, HK_MONNEXT = 14,
                  HK_SNAP_L = 20, HK_SNAP_R = 21, HK_SNAP_T = 22, HK_SNAP_B = 23,
                  HK_SNAP_Q1 = 24, HK_SNAP_Q2 = 25, HK_SNAP_Q3 = 26, HK_SNAP_Q4 = 27,
                  HK_SNAP_MAX = 28, HK_SNAP_BACK = 29;

        Settings cfg;
        public Settings Cfg { get { return cfg; } }

        /// 全局单例引用（无 UI 上下文的子控件用）
        public static AppForm Instance;

        // 外壳
        TitleBar titleBar;
        NavRail nav;
        Panel pageHost;
        ToastHost toast;
        readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
        Page current;
        string currentPageKey = "";

        // 托盘与定时器
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        Timer tmrUi;      // 500ms：页面状态回填
        Timer tmrGuard;   // 1200ms：置顶守护 + 自动置顶规则
        Timer tmrBg;      // 60ms：背景窗跟随
        Timer tmrSec;     // 1000ms：背景恢复扫描
        Timer tmrTrim;    // 一次性：启动后收紧工作集
        Timer tmrEnum;    // 2500ms：触发后台窗口枚举
        MouseCapture capture;
        readonly uint selfPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        bool startupHidden, reallyExit, balloonShown, loading;

        // 置顶守护：前台变化事件钩子（比轮询更跟手）
        Native.WinEventProc fgProc;
        IntPtr fgHook;

        // 窗口列表（后台线程枚举 → UI 线程换装）
        readonly List<WinEntry> winList = new List<WinEntry>();
        readonly Dictionary<IntPtr, WinEntry> byHwnd = new Dictionary<IntPtr, WinEntry>();
        readonly object enumLock = new object();
        volatile bool enumBusy;
        IntPtr selected = IntPtr.Zero;
        public event EventHandler TargetChanged;

        // 背景
        readonly Dictionary<IntPtr, BgWindow> bgForms = new Dictionary<IntPtr, BgWindow>();
        readonly Dictionary<IntPtr, BgMeta> bgMeta = new Dictionary<IntPtr, BgMeta>();
        readonly List<string[]> pendingRestore = new List<string[]>();
        int restoreTries;

        // 自动置顶规则
        readonly List<string> autoRules = new List<string>();
        readonly HashSet<IntPtr> autoPinned = new HashSet<IntPtr>();

        public sealed class BgMeta
        {
            public string Kind;
            public string Path;
            public byte Alpha;
            public int Fit;
        }

        public AppForm()
        {
            Instance = this;
            cfg = Settings.Load(SettingsPath());

            Text = "OI 窗口工具";
            Font = Theme.FontBody;
            BackColor = Theme.Shell;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            MinimumSize = Theme.Sz(720, 520);
            ClientSize = Theme.Sz(cfg.Int("winW", 900), cfg.Int("winH", 620));

            ApplyThemeFromCfg();

            capture = new MouseCapture();
            capture.Clicked += OnPickedPoint;

            BuildShell();
            BuildPages();
            BuildTimers();
            BuildTray();
            InstallForegroundHook();

            LoadAutoRules();
            LoadPresets();
            SelectPage(cfg.Str("page", "window"), true);

            RefreshWindowsSync();   // 启动时同步刷一次，列表不空窗
            LoadPrefs();

            RegisterHotkeys();
            tmrUi.Start();
            tmrGuard.Start();
            tmrBg.Start();
            tmrSec.Start();
            tmrEnum.Start();
            tmrTrim.Start();
        }

        // ==================================================================
        // 主题
        // ==================================================================

        void ApplyThemeFromCfg()
        {
            Theme.SetDark(ResolveDark());
            Theme.SetHue((AccentHue)Math.Max(0, Math.Min(4, cfg.Int("hue", 0))));
        }

        public bool ResolveDark()
        {
            int mode = cfg.Int("darkMode", 1);   // 默认暗色
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
                    if (k == null) return true;
                    object v = k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return true; }
        }

        public void ApplyTheme()
        {
            ApplyThemeFromCfg();
            BackColor = Theme.Shell;
            Theme.RestyleAll(this);
            LayoutShell();
            foreach (var kv in pages) kv.Value.Relayout(kv.Value.Width, kv.Value.Height);
            Invalidate(true);
        }

        // ==================================================================
        // 外壳布局
        // ==================================================================

        void BuildShell()
        {
            titleBar = new TitleBar();
            titleBar.Title = "OI 窗口工具";
            titleBar.MinClick += () => WindowState = FormWindowState.Minimized;
            titleBar.MaxClick += ToggleMax;
            titleBar.CloseClick += HideToTray;
            Controls.Add(titleBar);

            nav = new NavRail();
            nav.SelectionChanged += (s, e) =>
            {
                if (nav.Selected != null) SelectPage(nav.Selected.Key, false);
            };
            Controls.Add(nav);

            pageHost = new Panel();
            pageHost.BackColor = Theme.Shell;
            UiUtil.DoubleBuffer(pageHost);
            Controls.Add(pageHost);

            toast = new ToastHost();
            Controls.Add(toast);

            LayoutShell();
        }

        void ToggleMax()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
            titleBar.Maximized = WindowState == FormWindowState.Maximized;
            LayoutShell();
        }

        void BuildPages()
        {
            nav.Items.Add(new NavItem { Key = "window", Title = "窗口", Icon = Glyph.Pin });
            nav.Items.Add(new NavItem { Key = "bg", Title = "背景", Icon = Glyph.Picture });
            nav.Items.Add(new NavItem { Key = "layout", Title = "布局", Icon = Glyph.Layers });
            nav.Items.Add(new NavItem { Key = "settings", Title = "设置", Icon = Glyph.Gear });

            Register(new WindowPage(this));
            Register(new BgPage(this));
            Register(new LayoutPage(this));
            Register(new SettingsPage(this));
        }

        void Register(Page p)
        {
            pages[p.Key] = p;
            pageHost.Controls.Add(p);
            p.Visible = false;
            p.Build();
            p.Place(pageHost.DisplayRectangle);
        }

        void LayoutShell()
        {
            if (nav == null || pageHost == null || titleBar == null) return;
            int railW = Theme.S(64);
            titleBar.SetBounds(0, 0, ClientSize.Width, Theme.S(38));
            nav.SetBounds(0, Theme.S(38), railW, ClientSize.Height - Theme.S(38));
            pageHost.SetBounds(railW, Theme.S(38), ClientSize.Width - railW, ClientSize.Height - Theme.S(38));
            pageHost.Padding = new Padding(Theme.S(18), Theme.S(10), Theme.S(18), Theme.S(14));
            foreach (var kv in pages) kv.Value.Place(pageHost.DisplayRectangle);
            toast.Location = new Point(pageHost.Left + (pageHost.Width - toast.Width) / 2, pageHost.Top + Theme.S(6));
            toast.BringToFront();
            if (current != null) current.Relayout(current.Width, current.Height);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (WindowState == FormWindowState.Maximized)
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            LayoutShell();
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
            for (int i = 0; i < nav.Items.Count; i++) if (nav.Items[i].Key == key) idx = i;
            nav.SelectedIndex = idx;
            LayoutShell();
            p.OnShow();
            p.Tick();
            if (!loading) Save();
        }

        // ==================================================================
        // 提示
        // ==================================================================

        public void Toast(string msg, ToastKind kind)
        {
            if (toast == null || !IsHandleCreated) return;
            Color tone = Theme.TextDim;
            if (kind == ToastKind.Good) tone = Theme.Good;
            else if (kind == ToastKind.Warn) tone = Theme.Warn;
            else if (kind == ToastKind.Bad) tone = Theme.Bad;
            try
            {
                toast.Show(msg, tone, Math.Max(Theme.S(260), pageHost.Width - Theme.S(80)));
                toast.Location = new Point(pageHost.Left + (pageHost.Width - toast.Width) / 2, pageHost.Top + Theme.S(6));
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void ToastGood(string msg) { Toast(msg, ToastKind.Good); }
        public void ToastWarn(string msg) { Toast(msg, ToastKind.Warn); }
        public void ToastBad(string msg) { Toast(msg, ToastKind.Bad); }

        // ==================================================================
        // 设置持久化
        // ==================================================================

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
            cfg.Save();
        }

        void LoadPrefs()
        {
            loading = true;
            if (cfg.Bool("silentStart", false)) startupHidden = true;
            loading = false;
        }

        // ==================================================================
        // 目标窗口
        // ==================================================================

        public IntPtr Target { get { return selected; } }

        public bool Ok(IntPtr h) { return h != IntPtr.Zero && Native.IsWindow(h); }

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

        public WinEntry Entry(IntPtr h)
        {
            WinEntry e;
            lock (enumLock) return byHwnd.TryGetValue(h, out e) ? e : null;
        }

        public List<WinEntry> WindowList { get { return winList; } }

        /// 读取窗口列表前先拿这把锁（后台枚举线程也会换装列表）
        public object EnumLock { get { return enumLock; } }

        public void RefreshWindowsSync()
        {
            // 启动/点选后立即同步枚举一次（不等待后台周期）
            var list = EnumerateWorker();
            ApplyEnumResult(list);
        }

        void ApplyEnumResult(List<WinEntry> list)
        {
            lock (enumLock)
            {
                winList.Clear();
                winList.AddRange(list);
                foreach (var e in list) byHwnd[e.Hwnd] = e;
                if (byHwnd.Count > winList.Count + 32)
                {
                    var dead = new List<IntPtr>(byHwnd.Count);
                    foreach (var kv in byHwnd) if (!Native.IsWindow(kv.Key)) dead.Add(kv.Key);
                    foreach (var d in dead) byHwnd.Remove(d);
                }
            }

            bool found = false;
            foreach (var w in winList) if (w.Hwnd == selected) { found = true; break; }
            if (!found)
            {
                IntPtr fg = Native.GetForegroundWindow();
                foreach (var w in winList) if (w.Hwnd == fg) { selected = fg; found = true; break; }
            }
            if (!found) selected = winList.Count > 0 ? winList[0].Hwnd : IntPtr.Zero;
            RaiseTarget();
            if (current != null) current.Tick();
        }

        /// 后台线程窗口枚举：标题取不到（挂死窗口）也不再冻结 UI。
        List<WinEntry> EnumerateWorker()
        {
            var scratch = new List<WinEntry>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr lp)
            {
                if (!Native.IsWindowVisible(h)) return true;
                int len = Native.GetWindowTextLength(h);
                if (len <= 0) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == selfPid) return true;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;

                var sbT = new StringBuilder(512);
                Native.GetWindowText(h, sbT, 512);
                var sbC = new StringBuilder(256);
                Native.GetClassName(h, sbC, 256);
                string cls = sbC.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true;

                WinEntry cached;
                lock (enumLock)
                {
                    if (byHwnd.TryGetValue(h, out cached) && cached.Title == sbT.ToString() && cached.Cls == cls)
                    {
                        scratch.Add(cached);
                        return true;
                    }
                }

                var e = new WinEntry
                {
                    Hwnd = h,
                    Title = sbT.ToString(),
                    Cls = cls,
                    Proc = ProcName(pid),
                    Icon = IconFor(pid)
                };
                scratch.Add(e);
                return true;
            }, IntPtr.Zero);
            return scratch;
        }

        void AsyncRefreshWindows()
        {
            if (enumBusy) return;
            enumBusy = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                List<WinEntry> list = null;
                try { list = EnumerateWorker(); }
                catch (Exception ex) { Program.Log(ex); }
                enumBusy = false;
                if (list != null) Ui(delegate { ApplyEnumResult(list); });
            });
        }

        /// 从后台线程安全地回到 UI 线程。
        public void Ui(Action a)
        {
            if (a == null) return;
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(a);
                else a();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        // 进程名 / 图标缓存（有界，防挂机膨胀）
        const int MaxCachedProcs = 192;
        const int MaxCachedIcons = 48;
        static readonly Dictionary<uint, string> procCache = new Dictionary<uint, string>();
        static readonly Queue<uint> procOrder = new Queue<uint>();
        static readonly Dictionary<uint, Image> iconCache = new Dictionary<uint, Image>();
        static readonly Queue<uint> iconOrder = new Queue<uint>();

        static string ProcName(uint pid)
        {
            lock (procCache)
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
        }

        static Image IconFor(uint pid)
        {
            lock (iconCache)
            {
                Image img;
                if (iconCache.TryGetValue(pid, out img)) return img;
                img = null;
                try
                {
                    using (var p = System.Diagnostics.Process.GetProcessById((int)pid))
                    {
                        string path = null;
                        try { path = p.MainModule.FileName; }
                        catch { }
                        if (path != null)
                        {
                            using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(path))
                            {
                                if (ic != null)
                                {
                                    var small = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                    using (var g = Graphics.FromImage(small))
                                    {
                                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                        g.DrawImage(ic.ToBitmap(), 0, 0, 32, 32);
                                    }
                                    img = small;
                                }
                            }
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
        }

        // ==================================================================
        // 点选窗口
        // ==================================================================

        public void BeginPick()
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
            RefreshWindowsSync();
            if (Entry(h) == null)
            {
                var sbT = new StringBuilder(512);
                Native.GetWindowText(h, sbT, 512);
                var sbC = new StringBuilder(256);
                Native.GetClassName(h, sbC, 256);
                var e = new WinEntry
                {
                    Hwnd = h,
                    Title = sbT.Length == 0 ? "(无标题)" : sbT.ToString(),
                    Cls = sbC.ToString(),
                    Proc = ProcName(pid),
                    Icon = IconFor(pid)
                };
                lock (enumLock) { winList.Insert(0, e); byHwnd[h] = e; }
            }
            SetTarget(h);
            Toast("已选中：" + TitleOf(h), ToastKind.Good);
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

        // ==================================================================
        // 置顶守护
        // ==================================================================

        void InstallForegroundHook()
        {
            try
            {
                fgProc = OnForegroundEvent;
                fgHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, fgProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void OnForegroundEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            // 前台一变就补一遍置顶：对抗「自己反复置顶」的程序几乎无延迟
            Ui(delegate
            {
                try
                {
                    Manager.GuardScan();
                    ApplyAutoRules(false);
                }
                catch (Exception ex) { Program.Log(ex); }
            });
        }

        int lastRescueToast;

        void OnGuardTick(object sender, EventArgs e)
        {
            try
            {
                Manager.Cleanup();
                int rescued = Manager.GuardScan();
                if (rescued > 0 && GuardEnabled && Environment.TickCount - lastRescueToast > 10000)
                {
                    lastRescueToast = Environment.TickCount;
                    ToastWarn("已守护置顶：补回 " + rescued + " 个被压下的窗口。");
                }
                ApplyAutoRules(true);
                if (selected != IntPtr.Zero && !Native.IsWindow(selected))
                {
                    selected = IntPtr.Zero;
                    RaiseTarget();
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public bool GuardEnabled
        {
            get { return cfg.Bool("guard", true); }
            set { cfg.Set("guard", value); Save(); }
        }

        // ==================================================================
        // 定时器
        // ==================================================================

        void BuildTimers()
        {
            tmrUi = new Timer { Interval = 500 };
            tmrUi.Tick += OnUiTick;

            tmrGuard = new Timer { Interval = 1200 };
            tmrGuard.Tick += OnGuardTick;

            tmrBg = new Timer { Interval = 60 };
            tmrBg.Tick += OnBgTick;

            tmrSec = new Timer { Interval = 1000 };
            tmrSec.Tick += OnSecondTick;

            tmrEnum = new Timer { Interval = 2500 };
            tmrEnum.Tick += (s, e) => AsyncRefreshWindows();

            tmrTrim = new Timer { Interval = 4000 };
            tmrTrim.Tick += (s, e) => { tmrTrim.Stop(); TrimWorkingSet(); };
        }

        void OnUiTick(object sender, EventArgs e)
        {
            try { if (current != null) current.Tick(); }
            catch (Exception ex) { Program.Log(ex); }
        }

        void OnSecondTick(object sender, EventArgs e)
        {
            try { TryRestoreBackgrounds(); }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// 把工作集交还给系统。
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

        // ==================================================================
        // 窗口过程：无边框缩放 + 热键 + 二次启动唤醒
        // ==================================================================

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    int edge = Theme.S(6);
                    int x = m.LParam.ToInt32() & 0xFFFF;
                    int y = m.LParam.ToInt32() >> 16;
                    var p = PointToClient(new Point(x, y));
                    bool l = p.X <= edge, r = p.X >= ClientSize.Width - edge;
                    bool t = p.Y <= edge, b = p.Y >= ClientSize.Height - edge;
                    if (t && l) m.Result = (IntPtr)HTTOPLEFT;
                    else if (t && r) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (b && l) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (b && r) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (t) m.Result = (IntPtr)HTTOP;
                    else if (b) m.Result = (IntPtr)HTBOTTOM;
                    else if (l) m.Result = (IntPtr)HTLEFT;
                    else if (r) m.Result = (IntPtr)HTRIGHT;
                }
                return;
            }
            if (m.Msg == Native.WM_SETTINGCHANGE)
            {
                if (cfg != null && cfg.Int("darkMode", 1) == 2) ApplyTheme();
                return;
            }
            if (m.Msg == WM_HOTKEY) { DispatchHotkey(m.WParam.ToInt32()); return; }
            if (Program.WM_ALREADY_RUNNING != 0 && m.Msg == Program.WM_ALREADY_RUNNING)
            {
                try { BeginInvoke(new Action(ActivateSelf)); } catch { }
                return;
            }
            base.WndProc(ref m);
        }

        void ActivateSelf()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Native.SetForegroundWindow(Handle);
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

        void HideToTray()
        {
            Hide();
            if (tmrTrim != null) tmrTrim.Start();
            if (!balloonShown)
            {
                balloonShown = true;
                Balloon("已隐藏到托盘，所有功能继续生效。单击托盘图标可再次打开。");
            }
        }

        // ==================================================================
        // 生命周期收尾
        // ==================================================================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Save();
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (tmrUi != null) tmrUi.Stop();
            if (tmrGuard != null) tmrGuard.Stop();
            if (tmrBg != null) tmrBg.Stop();
            if (tmrSec != null) tmrSec.Stop();
            if (tmrEnum != null) tmrEnum.Stop();
            if (tmrTrim != null) { tmrTrim.Stop(); tmrTrim.Dispose(); tmrTrim = null; }
            UnregisterHotkeys();
            if (capture != null) capture.Dispose();
            if (fgHook != IntPtr.Zero)
            {
                try { Native.UnhookWinEvent(fgHook); } catch { }
                fgHook = IntPtr.Zero;
            }
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
