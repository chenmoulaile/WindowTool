using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    sealed class WinEntry
    {
        public IntPtr Hwnd;
        public string Title;
        public string Cls;
        public override string ToString() { return Title + "   [" + Cls + "]"; }
    }

    sealed class MainForm : Form
    {
        const int WM_HOTKEY = 0x0312;
        const int HK_TOP = 1;
        const int HK_OP2 = 2;
        const int HK_UP = 3;
        const int HK_DOWN = 4;
        const int HK_CANCELONE = 5;
        const int HK_CANCELALL = 6;
        const int HK_IMG = 7;
        const int HK_VID = 8;
        const int HK_BGOFF = 9;
        const int HK_ALPHA_DEC = 10;
        const int HK_ALPHA_INC = 11;
        const int HK_VRAND = 12;

        ComboBox comboWins;
        Button btnRefresh, btnPick, btnTopOnly, btnUnTop, btnOp2, btnCancelOp2, btnCancelAll, btnUp, btnDown, btnBottom, btnImgBg, btnVidBg, btnBgOff, btnTestAlert, btnFolderPick;
        DarkSlider trkAlpha;
        Label lblSel, lblAlphaVal, lblBgHint, lblUUStatus, lblKeys, lblMinTip;
        GroupBox grpOps, grpBg, grpUU;
        NumericUpDown numBgAlpha, numMin, numRadius;
        CheckBox chkUU;
        ComboBox cmbVidMode;
        TextBox txtKwProc, txtKwTitle;
        Timer tmr, tmrBg, tmrAuto;
        MouseCapture capture;
        readonly Dictionary<IntPtr, BgWindow> bgForms = new Dictionary<IntPtr, BgWindow>();
        readonly uint selfPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        bool suppressAlpha;

        readonly Random rng = new Random();
        string[] videoFiles;
        int videoIdx = -1;
        IntPtr playlistTarget;
        DateTime lastSwitch = DateTime.MinValue;

        NotifyIcon tray;
        bool startupHidden;
        bool reallyExit;
        bool balloonShown;
        bool loading;
        CheckBox chkSilent, chkAutoRun, chkBgMem;
        Dictionary<IntPtr, BgMeta> bgMeta = new Dictionary<IntPtr, BgMeta>();
        List<string[]> pendingRestore = new List<string[]>();
        int restoreTries;

        sealed class BgMeta
        {
            public string Kind;
            public string Path;
            public byte Alpha;
            public int Fit;
        }

        public MainForm()
        {
            Text = "窗口置顶助手";
            Font = Theme.FontUi;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(524, 514);

            lblSel = new Label();
            lblSel.Location = new Point(16, 14);
            lblSel.Size = new Size(246, 26);
            lblSel.ForeColor = Theme.TextDim;
            lblSel.Text = "当前窗口：(未选择)";

            chkAutoRun = new CheckBox();
            chkAutoRun.Text = "开机启动";
            chkAutoRun.Location = new Point(264, 10);
            chkAutoRun.Size = new Size(74, 24);
            Theme.StyleCheck(chkAutoRun);
            chkAutoRun.CheckedChanged += (s, e) => { SetAutoRun(chkAutoRun.Checked); SaveSettings(); };

            chkSilent = new CheckBox();
            chkSilent.Text = "静默";
            chkSilent.Location = new Point(340, 10);
            chkSilent.Size = new Size(52, 24);
            Theme.StyleCheck(chkSilent);
            chkSilent.CheckedChanged += (s, e) => SaveSettings();

            Label lblRadTip = new Label();
            lblRadTip.Text = "圆角:";
            lblRadTip.Location = new Point(396, 14);
            lblRadTip.Size = new Size(34, 18);
            Theme.StyleLabel(lblRadTip, Theme.TextDim);

            numRadius = new NumericUpDown();
            numRadius.Minimum = 0;
            numRadius.Maximum = 20;
            numRadius.Value = 10;
            numRadius.Location = new Point(432, 11);
            numRadius.Size = new Size(44, 23);
            Theme.StyleNum(numRadius);
            numRadius.ValueChanged += OnRadiusChanged;

            Controls.Add(lblRadTip);
            Controls.Add(chkAutoRun);

            comboWins = new ComboBox();
            comboWins.Location = new Point(12, 44);
            comboWins.Size = new Size(300, 24);
            comboWins.DropDownStyle = ComboBoxStyle.DropDownList;
            Theme.StyleCombo(comboWins);

            btnRefresh = NewBtn("刷新", new Point(318, 43), new Size(64, 25), OnRefresh, false);
            btnPick = NewBtn("点选窗口", new Point(388, 43), new Size(108, 25), OnPick, true);

            grpOps = new DarkGroupBox();
            grpOps.Text = "窗口操作";
            grpOps.Location = new Point(12, 82);
            grpOps.Size = new Size(500, 142);
            grpOps.ForeColor = Theme.Text;

            btnTopOnly = GNewBtn(grpOps, "仅置顶(①切换)", new Point(14, 24), new Size(104, 27), OnTopOnly, true);
            btnUnTop = GNewBtn(grpOps, "取消置顶", new Point(124, 24), new Size(88, 27), OnUnTop, false);
            btnOp2 = GNewBtn(grpOps, "②穿透置顶/取消", new Point(218, 24), new Size(126, 27), OnOp2, true);
            btnCancelOp2 = GNewBtn(grpOps, "取消该窗口②", new Point(350, 24), new Size(122, 27), OnCancelOp2, false);
            btnCancelAll = GNewBtn(grpOps, "取消所有②", new Point(14, 58), new Size(104, 27), OnCancelAll, false);
            btnUp = GNewBtn(grpOps, "上移一层", new Point(124, 58), new Size(84, 27), OnLayerUp, false);
            btnDown = GNewBtn(grpOps, "下移一层", new Point(214, 58), new Size(84, 27), OnLayerDown, false);
            btnBottom = GNewBtn(grpOps, "移到最底", new Point(304, 58), new Size(84, 27), OnLayerBottom, false);

            Label lblAlpha = new Label();
            lblAlpha.Text = "透明度:";
            lblAlpha.Location = new Point(16, 102);
            lblAlpha.Size = new Size(56, 20);
            Theme.StyleLabel(lblAlpha, Theme.TextDim);

            trkAlpha = new DarkSlider();
            trkAlpha.Minimum = 40;
            trkAlpha.Maximum = 255;
            trkAlpha.Value = 255;
            trkAlpha.Location = new Point(74, 94);
            trkAlpha.Size = new Size(230, 32);
            trkAlpha.ValueChanged += OnAlphaChanged;

            lblAlphaVal = new Label();
            lblAlphaVal.Text = "255";
            lblAlphaVal.Location = new Point(310, 102);
            lblAlphaVal.Size = new Size(60, 20);
            Theme.StyleLabel(lblAlphaVal, Theme.Accent);

            Label lblAlphaTip = new Label();
            lblAlphaTip.Text = "(拖动即时生效，作用于所选窗口)";
            lblAlphaTip.Location = new Point(368, 102);
            lblAlphaTip.Size = new Size(126, 34);
            Theme.StyleLabel(lblAlphaTip, Theme.TextDim);

            grpOps.Controls.Add(lblAlpha);
            grpOps.Controls.Add(trkAlpha);
            grpOps.Controls.Add(lblAlphaVal);
            grpOps.Controls.Add(lblAlphaTip);

            btnBlur = GNewBtn(grpOps, "毛玻璃 开/关", new Point(14, 130), new Size(110, 27), OnBlur, false);
            Label lblBlurTip = new Label();
            lblBlurTip.Text = "窗口美化：亚克力/模糊效果（部分系统版本可能不支持）";
            lblBlurTip.Location = new Point(132, 135);
            lblBlurTip.Size = new Size(360, 18);
            Theme.StyleLabel(lblBlurTip, Theme.TextDim);
            grpOps.Controls.Add(lblBlurTip);

            grpBg = new DarkGroupBox();
            grpBg.Text = "背景";
            grpBg.Location = new Point(12, 262);
            grpBg.Size = new Size(500, 146);
            grpBg.ForeColor = Theme.Text;

            btnImgBg = GNewBtn(grpBg, "应用图片背景(⑦)", new Point(14, 22), new Size(136, 27), OnImageBg, false);
            btnVidBg = GNewBtn(grpBg, "应用视频背景(⑧)", new Point(158, 22), new Size(136, 27), OnVideoBg, false);
            numBgAlpha = new NumericUpDown();
            numBgAlpha.Minimum = 60;
            numBgAlpha.Maximum = 250;
            numBgAlpha.Value = 160;
            numBgAlpha.Location = new Point(304, 25);
            numBgAlpha.Size = new Size(54, 23);
            Theme.StyleNum(numBgAlpha);
            numBgAlpha.ValueChanged += (s, e) => SaveSettings();
            Label lblBgNum = new Label();
            lblBgNum.Text = "背景时透明度";
            lblBgNum.Location = new Point(362, 28);
            lblBgNum.Size = new Size(96, 18);
            Theme.StyleLabel(lblBgNum, Theme.TextDim);

            btnFolderPick = GNewBtn(grpBg, "选择文件夹轮播", new Point(14, 52), new Size(118, 27), OnFolderPick, false);
            cmbVidMode = new ComboBox();
            cmbVidMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbVidMode.Items.AddRange(new object[] { "填充裁剪", "完整显示", "变形铺满" });
            cmbVidMode.SelectedIndex = 0;
            cmbVidMode.Location = new Point(138, 54);
            cmbVidMode.Size = new Size(110, 23);
            Theme.StyleCombo(cmbVidMode);
            cmbVidMode.SelectedIndexChanged += OnFitChanged;
            btnBgOff = GNewBtn(grpBg, "取消背景", new Point(254, 52), new Size(84, 27), OnBgOff, false);
            numMin = new NumericUpDown();
            numMin.Minimum = 0;
            numMin.Maximum = 120;
            numMin.Value = 0;
            numMin.Location = new Point(344, 55);
            numMin.Size = new Size(42, 23);
            Theme.StyleNum(numMin);
            numMin.ValueChanged += (s, e) => SaveSettings();
            lblMinTip = new Label();
            lblMinTip.Text = "分自切(0关)";
            lblMinTip.Location = new Point(390, 58);
            lblMinTip.Size = new Size(100, 18);
            Theme.StyleLabel(lblMinTip, Theme.TextDim);

            chkBgMem = new CheckBox();
            chkBgMem.Text = "记住各窗口背景";
            chkBgMem.Location = new Point(14, 84);
            chkBgMem.Size = new Size(128, 22);
            Theme.StyleCheck(chkBgMem);
            chkBgMem.CheckedChanged += (s, e) => SaveSettings();

            lblBgHint = new Label();
            lblBgHint.Text = "";
            lblBgHint.Location = new Point(148, 88);
            lblBgHint.Size = new Size(338, 20);
            Theme.StyleLabel(lblBgHint, Theme.Green);

            grpBg.Controls.Add(numBgAlpha);
            grpBg.Controls.Add(lblBgNum);
            grpBg.Controls.Add(cmbVidMode);
            grpBg.Controls.Add(numMin);
            grpBg.Controls.Add(lblMinTip);
            grpBg.Controls.Add(chkBgMem);
            grpBg.Controls.Add(lblBgHint);

            grpUU = new DarkGroupBox();
            grpUU.Text = "UU远程被控提醒(⑨)";
            grpUU.Location = new Point(12, 416);
            grpUU.Size = new Size(500, 84);
            grpUU.ForeColor = Theme.Text;

            chkUU = new CheckBox();
            chkUU.Text = "启用";
            chkUU.Location = new Point(14, 24);
            chkUU.Size = new Size(58, 22);
            Theme.StyleCheck(chkUU);
            chkUU.CheckedChanged += OnUuToggle;

            Label l1 = new Label(); l1.Text = "进程名含:"; l1.Location = new Point(80, 27); l1.Size = new Size(66, 18);
            Theme.StyleLabel(l1, Theme.TextDim);
            txtKwProc = new TextBox(); txtKwProc.Text = "uu"; txtKwProc.Location = new Point(148, 24); txtKwProc.Size = new Size(86, 22);
            Theme.StyleText(txtKwProc);
            Label l2 = new Label(); l2.Text = "窗口标题含:"; l2.Location = new Point(244, 27); l2.Size = new Size(74, 18);
            Theme.StyleLabel(l2, Theme.TextDim);
            txtKwTitle = new TextBox(); txtKwTitle.Text = "远程"; txtKwTitle.Location = new Point(320, 24); txtKwTitle.Size = new Size(76, 22);
            Theme.StyleText(txtKwTitle);
            btnTestAlert = GNewBtn(grpUU, "测试弹窗", new Point(406, 22), new Size(84, 25), OnUuTest, false);

            lblUUStatus = new Label();
            lblUUStatus.Text = "提示：检测为尽力匹配，可修改关键词；被控时鼠标旁显示“你已被控制”。";
            lblUUStatus.Location = new Point(14, 54);
            lblUUStatus.Size = new Size(476, 18);
            Theme.StyleLabel(lblUUStatus, Theme.TextDim);

            grpUU.Controls.Add(l1); grpUU.Controls.Add(txtKwProc); grpUU.Controls.Add(l2); grpUU.Controls.Add(txtKwTitle);
            grpUU.Controls.Add(lblUUStatus);

            lblKeys = new Label();
            lblKeys.Location = new Point(12, 506);
            lblKeys.Size = new Size(500, 34);
            lblKeys.ForeColor = Color.FromArgb(124, 134, 168);
            lblKeys.Font = new Font("微软雅黑", 8.25f);
            lblKeys.Text = "全局热键(Ctrl+Alt+): 1置顶切换 2穿透置顶 3上移层 4下移层 5取消本窗② 6取消全部② 7图片背景 8视频背景 0取消背景 R随机切视频 ←→调透明度";

            Controls.Add(lblSel);
            Controls.Add(chkSilent);
            Controls.Add(numRadius);
            Controls.Add(comboWins);
            Controls.Add(btnRefresh);
            Controls.Add(btnPick);
            Controls.Add(grpOps);
            Controls.Add(grpBg);
            Controls.Add(grpUU);
            Controls.Add(lblKeys);

            capture = new MouseCapture();
            capture.Clicked += OnPickedPoint;

            tmr = new Timer();
            tmr.Interval = 500;
            tmr.Tick += OnTick;

            tmrBg = new Timer();
            tmrBg.Interval = 60;
            tmrBg.Tick += OnBgTick;

            tmrAuto = new Timer();
            tmrAuto.Interval = 1000;
            tmrAuto.Tick += OnAutoSwitchTick;

            comboWins.SelectedIndexChanged += OnComboSel;
            txtKwProc.TextChanged += OnUuKeywords;
            txtKwTitle.TextChanged += OnUuKeywords;
            UuMonitor.StatusChanged += msg => { lblUUStatus.Text = msg; };

            RefreshList();
            RegisterHotkeys();
            UpdateButtonStates();
            BuildTray();
            LoadSettings();
            ReapplyRadii();
            tmr.Start();
            tmrBg.Start();
            tmrAuto.Start();
        }

        void OnRadiusChanged(object sender, EventArgs e)
        {
            Theme.Radius = (int)numRadius.Value;
            ReapplyRadii();
            SaveSettings();
        }

        void ReapplyRadii()
        {
            ApplyRadiiRecursive(Controls);
            InvalidateAllGroups(this);
        }

        static void InvalidateAllGroups(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is DarkGroupBox || c is DarkSlider) c.Invalidate();
                InvalidateAllGroups(c);
            }
        }

        static void ApplyRadiiRecursive(Control.ControlCollection cc)
        {
            foreach (Control c in cc)
            {
                if (c is Button) Theme.ApplyRegion(c);
                else if (c is TextBox || c is NumericUpDown || c is ComboBox) Theme.ApplyRegion(c);
                ApplyRadiiRecursive(c.Controls);
            }
        }

        string SettingsPath()
        {
            return Path.Combine(Application.StartupPath, "settings.txt");
        }

        static decimal ClampDec(decimal v, decimal min, decimal max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "WindowTool";

        bool GetAutoRun()
        {
            try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false)) return k != null && k.GetValue(AppName) != null; }
            catch { return false; }
        }

        void SetAutoRun(bool on)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(AppName, false);
                }
                Hint(on ? "已开启开机自启动。" : "已关闭开机自启动。");
            }
            catch (Exception ex) { Program.Log(ex); Hint("开机启动设置失败：" + ex.Message); }
        }

        static string Esc(string s) { return (s ?? "").Replace("|", "｜").Replace("\r", "").Replace("\n", ""); }

        void LoadSettings()
        {
            loading = true;
            var d = new Dictionary<string, string>();
            try
            {
                foreach (var line in File.ReadAllLines(SettingsPath()))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
            catch { }

            string v;
            decimal x;
            if (d.TryGetValue("bgAlpha", out v) && decimal.TryParse(v, out x))
                numBgAlpha.Value = ClampDec(x, numBgAlpha.Minimum, numBgAlpha.Maximum);
            if (d.TryGetValue("switchMin", out v) && decimal.TryParse(v, out x))
                numMin.Value = ClampDec(x, numMin.Minimum, numMin.Maximum);
            if (d.TryGetValue("fitMode", out v))
            {
                int fi;
                if (int.TryParse(v, out fi) && fi >= 0 && fi < cmbVidMode.Items.Count)
                    cmbVidMode.SelectedIndex = fi;
            }
            if (d.TryGetValue("radius", out v))
            {
                int ri;
                if (int.TryParse(v, out ri)) numRadius.Value = ClampDec(ri, numRadius.Minimum, numRadius.Maximum);
            }
            if (d.TryGetValue("uuProc", out v)) txtKwProc.Text = v;
            if (d.TryGetValue("uuTitle", out v)) txtKwTitle.Text = v;
            chkSilent.Checked = d.TryGetValue("silentStart", out v) && v == "1";
            startupHidden = chkSilent.Checked;
            chkAutoRun.Checked = GetAutoRun();
            chkBgMem.Checked = !d.TryGetValue("bgMem", out v) || v == "1";

            pendingRestore.Clear();
            int n = 0;
            if (d.TryGetValue("bgCount", out v)) int.TryParse(v, out n);
            for (int i = 0; i < n; i++)
            {
                if (!d.TryGetValue("bg" + i, out v)) continue;
                var parts = v.Split('|');
                if (parts.Length >= 6) pendingRestore.Add(parts);
            }

            chkUU.Checked = d.TryGetValue("uuEnabled", out v) && v == "1";
            loading = false;
        }

        void SaveSettings()
        {
            if (loading) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("bgAlpha=" + numBgAlpha.Value);
                sb.AppendLine("switchMin=" + numMin.Value);
                sb.AppendLine("fitMode=" + cmbVidMode.SelectedIndex);
                sb.AppendLine("radius=" + (int)numRadius.Value);
                sb.AppendLine("uuProc=" + txtKwProc.Text);
                sb.AppendLine("uuTitle=" + txtKwTitle.Text);
                sb.AppendLine("uuEnabled=" + (chkUU.Checked ? "1" : "0"));
                sb.AppendLine("silentStart=" + (chkSilent.Checked ? "1" : "0"));
                sb.AppendLine("bgMem=" + (chkBgMem.Checked ? "1" : "0"));

                var lines = new List<string>();
                if (chkBgMem.Checked)
                {
                    if (bgMeta.Count > 0)
                    {
                        foreach (var kv in bgForms)
                        {
                            BgMeta meta;
                            if (!bgMeta.TryGetValue(kv.Key, out meta)) continue;
                            uint pid; Native.GetWindowThreadProcessId(kv.Key, out pid);
                            string proc = "";
                            try { using (var pr = System.Diagnostics.Process.GetProcessById((int)pid)) proc = pr.ProcessName; }
                            catch { }
                            var sbT = new StringBuilder(512);
                            Native.GetWindowText(kv.Key, sbT, 512);
                            lines.Add(Esc(proc) + "|" + Esc(sbT.ToString()) + "|" + meta.Kind + "|" + Esc(meta.Path) + "|" + meta.Alpha + "|" + meta.Fit);
                        }
                    }
                    else
                    {
                        foreach (var ent in pendingRestore) lines.Add(string.Join("|", ent));
                    }
                }
                sb.AppendLine("bgCount=" + lines.Count);
                for (int i = 0; i < lines.Count; i++)
                    sb.AppendLine("bg" + i + "=" + lines[i]);

                File.WriteAllText(SettingsPath(), sb.ToString());
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        IntPtr FindWindowMatch(string proc, string title)
        {
            if (string.IsNullOrEmpty(title)) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            var cache = new Dictionary<uint, string>();
            Native.EnumWindows((h, lp) =>
            {
                if (found != IntPtr.Zero || !Native.IsWindowVisible(h)) return true;
                int len = Native.GetWindowTextLength(h);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 2);
                Native.GetWindowText(h, sb, sb.Capacity);
                if (sb.ToString() != title) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                string pn;
                if (!cache.TryGetValue(pid, out pn))
                {
                    try { using (var pr = System.Diagnostics.Process.GetProcessById((int)pid)) pn = pr.ProcessName; }
                    catch { pn = ""; }
                    cache[pid] = pn;
                }
                if (pn == proc) found = h;
                return found == IntPtr.Zero;
            }, IntPtr.Zero);
            return found;
        }

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, (s, e) => ShowMain());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { reallyExit = true; Close(); });

            tray = new NotifyIcon();
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { tray.Icon = SystemIcons.Application; }
            tray.Text = "窗口置顶助手";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.Click += (s, e) => ShowMain();
            tray.DoubleClick += (s, e) => ShowMain();
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
            SaveSettings();
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!balloonShown)
                {
                    balloonShown = true;
                    try
                    {
                        tray.BalloonTipTitle = "窗口置顶助手";
                        tray.BalloonTipText = "程序已隐藏到托盘，所有功能继续生效。单击托盘图标可再次打开。";
                        tray.ShowBalloonTip(2000);
                    }
                    catch { }
                }
                return;
            }
            base.OnFormClosing(e);
        }

        void RegisterHotkeys()
        {
            uint m = Native.MOD_CONTROL | Native.MOD_ALT;
            var fails = new List<string>();
            if (!Native.RegisterHotKey(Handle, HK_TOP, m, (uint)'1')) fails.Add("1");
            if (!Native.RegisterHotKey(Handle, HK_OP2, m, (uint)'2')) fails.Add("2");
            if (!Native.RegisterHotKey(Handle, HK_UP, m, (uint)'3')) fails.Add("3");
            if (!Native.RegisterHotKey(Handle, HK_DOWN, m, (uint)'4')) fails.Add("4");
            if (!Native.RegisterHotKey(Handle, HK_CANCELONE, m, (uint)'5')) fails.Add("5");
            if (!Native.RegisterHotKey(Handle, HK_CANCELALL, m, (uint)'6')) fails.Add("6");
            if (!Native.RegisterHotKey(Handle, HK_IMG, m, (uint)'7')) fails.Add("7");
            if (!Native.RegisterHotKey(Handle, HK_VID, m, (uint)'8')) fails.Add("8");
            if (!Native.RegisterHotKey(Handle, HK_BGOFF, m, (uint)'0')) fails.Add("0");
            if (!Native.RegisterHotKey(Handle, HK_VRAND, m, (uint)'R')) fails.Add("R");
            if (!Native.RegisterHotKey(Handle, HK_ALPHA_DEC, m, Native.VK_LEFT)) fails.Add("←");
            if (!Native.RegisterHotKey(Handle, HK_ALPHA_INC, m, Native.VK_RIGHT)) fails.Add("→");
            if (fails.Count > 0)
                lblKeys.Text += "　[注册失败: " + string.Join(" ", fails.ToArray()) + "]";
        }

        void UnregisterHotkeys()
        {
            for (int id = 1; id <= HK_VRAND; id++)
                Native.UnregisterHotKey(Handle, id);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                DispatchHotkey(m.WParam.ToInt32());
                return;
            }
            base.WndProc(ref m);
        }

        IntPtr HotTarget()
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != Handle)
            {
                uint pid;
                Native.GetWindowThreadProcessId(fg, out pid);
                if (pid != selfPid && Native.IsWindowVisible(fg))
                    return fg;
            }
            return Selected();
        }

        void DispatchHotkey(int id)
        {
            IntPtr t = HotTarget();
            switch (id)
            {
                case HK_TOP: DoSetTop(t, !Manager.IsTopmost(t)); break;
                case HK_OP2: DoApplyOp2(t); break;
                case HK_UP: DoLayerDir(t, true); break;
                case HK_DOWN: DoLayerDir(t, false); break;
                case HK_CANCELONE: DoCancelOp2Core(t); break;
                case HK_CANCELALL: DoCancelAllCore(); break;
                case HK_IMG:
                    if (Ok(t)) PickImageAndApply(t);
                    else Hint("热键：没有可用的目标窗口。");
                    break;
                case HK_VID:
                    if (Ok(t)) PickVideoAndApply(t);
                    else Hint("热键：没有可用的目标窗口。");
                    break;
                case HK_BGOFF: DoBgOffCore(t); break;
                case HK_VRAND: DoRandomVideo(); break;
                case HK_ALPHA_DEC: ChangeAlphaOn(t, -15); break;
                case HK_ALPHA_INC: ChangeAlphaOn(t, +15); break;
            }
        }

        Button NewBtn(string text, Point p, Size s, EventHandler h, bool primary)
        {
            var b = new Button();
            b.Text = text;
            b.Location = p;
            b.Size = s;
            b.Click += h;
            Theme.StyleButton(b, primary);
            return b;
        }

        Button GNewBtn(GroupBox g, string text, Point p, Size s, EventHandler h, bool primary)
        {
            var b = NewBtn(text, p, s, h, primary);
            g.Controls.Add(b);
            return b;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            Native.DwmSetWindowAttribute(Handle, 20, ref on, 4);
            Native.DwmSetWindowAttribute(Handle, 19, ref on, 4);
        }

        bool Ok(IntPtr h) { return h != IntPtr.Zero && Native.IsWindow(h); }

        void Hint(string s) { try { lblBgHint.Text = s; } catch { } }

        IntPtr Selected()
        {
            var e = comboWins.SelectedItem as WinEntry;
            return e == null ? IntPtr.Zero : e.Hwnd;
        }

        void SelectByHwnd(IntPtr h)
        {
            for (int i = 0; i < comboWins.Items.Count; i++)
            {
                var e = comboWins.Items[i] as WinEntry;
                if (e != null && e.Hwnd == h) { comboWins.SelectedIndex = i; return; }
            }
        }

        void UpdateSelLabel()
        {
            var e = comboWins.SelectedItem as WinEntry;
            if (e == null)
                lblSel.Text = "当前窗口：(未选择)";
            else
            {
                string t = e.Title;
                if (t.Length > 20) t = t.Substring(0, 20) + "…";
                lblSel.Text = "当前窗口：[" + e.Hwnd.ToInt64() + "] " + t;
            }
        }

        void UpdateButtonStates()
        {
            bool ok = Ok(Selected());
            btnTopOnly.Enabled = ok;
            btnUnTop.Enabled = ok && Manager.IsTopmost(Selected());
            btnOp2.Enabled = ok;
            btnCancelOp2.Enabled = ok && Manager.HasOp2(Selected());
            btnCancelAll.Enabled = Manager.PinnedCount > 0;
            btnUp.Enabled = ok;
            btnDown.Enabled = ok;
            btnBottom.Enabled = ok;
            trkAlpha.Enabled = ok;
            btnImgBg.Enabled = ok;
            btnVidBg.Enabled = ok;
            btnBgOff.Enabled = bgForms.Count > 0;
            btnBlur.Enabled = ok;

            var h = Selected();
            if (Ok(h))
            {
                int la = Manager.GetLastAlpha(h);
                if (trkAlpha.Value != la)
                {
                    suppressAlpha = true;
                    trkAlpha.Value = la;
                    suppressAlpha = false;
                    lblAlphaVal.Text = la.ToString();
                }
            }
        }

        void DoSetTop(IntPtr h, bool top)
        {
            if (!Ok(h)) return;
            Manager.SetTopmost(h, top);
            Hint(top ? "已置顶。" : "已取消置顶。");
            UpdateButtonStates();
        }

        void DoApplyOp2(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.HasOp2(h))
            {
                Manager.CancelOp2(h);
                Hint("该窗口已有②操作，本次已取消并还原样式（再按一次重新开启）。");
            }
            else
            {
                Manager.ApplyOp2(h);
                Hint("已执行②：置顶+穿透，透明度245。先应用的②始终在最上层。");
            }
            UpdateButtonStates();
        }

        void DoCancelOp2Core(IntPtr h)
        {
            if (!Ok(h)) return;
            Hint(Manager.CancelOp2(h) ? "已取消该窗口的②操作并还原样式。" : "该窗口当前没有②操作。");
            UpdateButtonStates();
        }

        void DoCancelAllCore()
        {
            int n = Manager.CancelAll();
            Hint("已取消全部②操作（" + n + " 个窗口）。");
            UpdateButtonStates();
        }

        DisplayFit CurrentFit()
        {
            int i = cmbVidMode == null ? 0 : cmbVidMode.SelectedIndex;
            if (i == 1) return DisplayFit.FitContain;
            if (i == 2) return DisplayFit.StretchFill;
            return DisplayFit.CoverFill;
        }

        void OnFitChanged(object sender, EventArgs e)
        {
            SaveSettings();
            if (videoFiles != null && videoIdx >= 0 && Ok(playlistTarget) && bgForms.ContainsKey(playlistTarget))
            {
                string path = videoFiles[videoIdx];
                BgMeta old;
                bgMeta.TryGetValue(playlistTarget, out old);
                var meta = new BgMeta
                {
                    Kind = "vid",
                    Path = path,
                    Alpha = old != null ? old.Alpha : (byte)numBgAlpha.Value,
                    Fit = (int)CurrentFit()
                };
                ApplyBgCore(playlistTarget, () => new BgWindow(playlistTarget, path, CurrentFit()), meta);
                Hint("显示模式已切换：" + cmbVidMode.Text);
            }
        }

        void DoLayerDir(IntPtr h, bool up)
        {
            if (!Ok(h)) return;
            if (!up && Manager.HasOp2(h)) { Hint("该窗口是②置顶窗口，需保持最上层。"); return; }
            bool moved = up ? Manager.MoveLayerUp(h) : Manager.MoveLayerDown(h);
            Hint(moved
                ? (up ? "已上移一层。" : "已下移一层。")
                : (up ? "已到达所在层级最上面。" : "已到达所在层级最下面。"));
        }

        void DoLayerBottom(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.HasOp2(h)) { Hint("该窗口是②置顶窗口，不能移到最底。"); return; }
            Manager.MoveLayerBottom(h);
        }

        void DoBgOffCore(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.ClearBg(h))
            {
                CloseBgForm(h);
                videoFiles = null;
                playlistTarget = IntPtr.Zero;
                Hint("已取消背景并还原窗口样式。");
            }
            else Hint("该窗口当前没有背景。");
            UpdateButtonStates();
        }

        void OnBlur(object sender, EventArgs e)
        {
            var h = Selected();
            if (!Ok(h)) return;
            bool on = !Manager.IsBlurred(h);
            if (Manager.ApplyBlur(h, on))
                Hint(on ? "已开启毛玻璃效果；若无变化可能是系统不支持该窗口。" : "已关闭毛玻璃效果。");
            else
                Hint("毛玻璃设置失败。");
        }

        void ChangeAlphaOn(IntPtr h, int delta)
        {
            if (!Ok(h)) return;
            int v = trkAlpha.Value + delta;
            if (v < trkAlpha.Minimum) v = trkAlpha.Minimum;
            if (v > trkAlpha.Maximum) v = trkAlpha.Maximum;
            suppressAlpha = true;
            trkAlpha.Value = v;
            suppressAlpha = false;
            lblAlphaVal.Text = v.ToString();
            Manager.ApplyOpacity(h, (byte)v);
        }

        void OnComboSel(object sender, EventArgs e)
        {
            UpdateSelLabel();
            UpdateButtonStates();
        }

        void OnRefresh(object sender, EventArgs e) { RefreshList(); }

        void OnPick(object sender, EventArgs e)
        {
            capture.Start();
            if (capture.Active)
            {
                Cursor = Cursors.Cross;
                lblSel.Text = "请点击目标窗口（Esc 取消）...";
                KeyPreview = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && capture.Active)
            {
                capture.Stop();
                Cursor = Cursors.Default;
                UpdateSelLabel();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void OnPickedPoint(Point pt)
        {
            Cursor = Cursors.Default;
            IntPtr h = Native.WindowFromPoint(new Native.POINT { X = pt.X, Y = pt.Y });
            if (h != IntPtr.Zero) h = Native.GetAncestor(h, Native.GA_ROOT);
            if (h != IntPtr.Zero)
            {
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == selfPid)
                {
                    UpdateSelLabel();
                    MessageBox.Show("不能选择本程序自身。");
                    return;
                }
                RefreshList();
                SelectByHwnd(h);
                if ((comboWins.SelectedItem as WinEntry) == null || ((WinEntry)comboWins.SelectedItem).Hwnd != h)
                {
                    AddHiddenEntry(h);
                }
                UpdateSelLabel();
            }
        }

        void AddHiddenEntry(IntPtr h)
        {
            var sbT = new StringBuilder(512);
            Native.GetWindowText(h, sbT, 512);
            var sbC = new StringBuilder(256);
            Native.GetClassName(h, sbC, 256);
            var en = new WinEntry { Hwnd = h, Title = string.IsNullOrEmpty(sbT.ToString()) ? "(无标题)" : sbT.ToString(), Cls = sbC.ToString() };
            int idx = comboWins.SelectedIndex;
            comboWins.Items.Insert(0, en);
            if (idx >= 0) comboWins.SelectedIndex = idx + 1; else comboWins.SelectedIndex = 0;
        }

        void RefreshList()
        {
            IntPtr keep = Selected();
            comboWins.Items.Clear();
            var list = new List<WinEntry>();
            Native.EnumWindows((h, lp) =>
            {
                if (!Native.IsWindowVisible(h)) return true;
                int len = Native.GetWindowTextLength(h);
                if (len <= 0) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == selfPid) return true;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;
                var sbT = new StringBuilder(len + 1);
                Native.GetWindowText(h, sbT, sbT.Capacity);
                var sbC = new StringBuilder(256);
                Native.GetClassName(h, sbC, 256);
                string cls = sbC.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true;
                list.Add(new WinEntry { Hwnd = h, Title = sbT.ToString(), Cls = cls });
                return true;
            }, IntPtr.Zero);
            foreach (var en in list) comboWins.Items.Add(en);
            if (keep != IntPtr.Zero && Native.IsWindow(keep)) SelectByHwnd(keep);
            else if (comboWins.Items.Count > 0) comboWins.SelectedIndex = 0;
            UpdateSelLabel();
        }

        void OnTopOnly(object sender, EventArgs e)
        {
            var h = Selected();
            DoSetTop(h, !Manager.IsTopmost(h));
        }
        void OnUnTop(object sender, EventArgs e) { DoSetTop(Selected(), false); }
        void OnOp2(object sender, EventArgs e) { DoApplyOp2(Selected()); }
        void OnCancelOp2(object sender, EventArgs e) { DoCancelOp2Core(Selected()); }
        void OnCancelAll(object sender, EventArgs e) { DoCancelAllCore(); }
        void OnLayerUp(object sender, EventArgs e) { DoLayerDir(Selected(), true); }
        void OnLayerDown(object sender, EventArgs e) { DoLayerDir(Selected(), false); }
        void OnLayerBottom(object sender, EventArgs e) { DoLayerBottom(Selected()); }

        void OnAlphaChanged(object sender, EventArgs e)
        {
            lblAlphaVal.Text = trkAlpha.Value.ToString();
            if (suppressAlpha) return;
            var h = Selected();
            if (Ok(h))
                Manager.ApplyOpacity(h, (byte)trkAlpha.Value);
        }

        void OnImageBg(object sender, EventArgs e)
        {
            var h = Selected();
            if (Ok(h)) PickImageAndApply(h);
        }

        void OnVideoBg(object sender, EventArgs e)
        {
            var h = Selected();
            if (Ok(h)) PickVideoAndApply(h);
        }

        void PickImageAndApply(IntPtr h)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择背景图片";
                dlg.Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Image img;
                try { img = Image.FromFile(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show("无法加载图片：" + ex.Message); return; }
                var fit = CurrentFit();
                var meta = new BgMeta { Kind = "img", Path = dlg.FileName, Alpha = (byte)numBgAlpha.Value, Fit = (int)fit };
                ApplyBgCore(h, () => new BgWindow(h, img, fit), meta);
                Hint("图片背景已应用（" + cmbVidMode.Text + "）。");
            }
        }

        void PickVideoAndApply(IntPtr h)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择背景视频";
                dlg.Filter = "视频|*.mp4;*.wmv;*.avi;*.mkv;*.mov;*.m4v|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string path = dlg.FileName;
                videoFiles = new[] { path };
                videoIdx = 0;
                playlistTarget = h;
                lastSwitch = DateTime.Now;
                ApplyBgCore(h, () => new BgWindow(h, path, CurrentFit()),
                    new BgMeta { Kind = "vid", Path = path, Alpha = (byte)numBgAlpha.Value, Fit = (int)CurrentFit() });
                Hint("视频循环播放中：" + Path.GetFileName(path) + "（" + cmbVidMode.Text + "）");
            }
        }

        void OnFolderPick(object sender, EventArgs e)
        {
            var h = Selected();
            if (!Ok(h)) { MessageBox.Show("请先选中一个目标窗口。"); return; }
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择视频文件夹（用于轮播背景）";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var exts = new[] { "*.mp4", "*.wmv", "*.avi", "*.mkv", "*.mov", "*.m4v" };
                    var found = new List<string>();
                    foreach (var pat in exts)
                        foreach (var f in Directory.GetFiles(dlg.SelectedPath, pat))
                            found.Add(f);
                    if (found.Count == 0) { Hint("该文件夹没有可用视频文件。"); return; }
                    videoFiles = found.ToArray();
                    videoIdx = -1;
                    playlistTarget = h;
                    ApplyRandomVideoCore(h, true);
                }
                catch (Exception ex) { Program.Log(ex); Hint("读取文件夹失败：" + ex.Message); }
            }
        }

        void ApplyRandomVideoCore(IntPtr h, bool force)
        {
            if (videoFiles == null || videoFiles.Length == 0 || !Ok(h)) return;
            int ni = videoIdx;
            if (videoFiles.Length > 1)
            {
                int guard = 0;
                do { ni = rng.Next(videoFiles.Length); guard++; }
                while (ni == videoIdx && guard < 20);
            }
            videoIdx = ni;
            try
            {
                string path = videoFiles[ni];
                lastSwitch = DateTime.Now;
                var fit = CurrentFit();
                ApplyBgCore(h, () => new BgWindow(h, path, fit),
                    new BgMeta { Kind = "vid", Path = path, Alpha = (byte)numBgAlpha.Value, Fit = (int)fit });
                Hint("视频背景 [" + (ni + 1) + "/" + videoFiles.Length + "]：" + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                Hint("切换视频失败：" + ex.Message);
            }
        }

        void DoRandomVideo()
        {
            if (videoFiles == null || videoFiles.Length == 0)
            {
                Hint("热键R：请先用「选择文件夹轮播」加载视频文件夹。");
                return;
            }
            IntPtr t = Ok(playlistTarget) && bgForms.ContainsKey(playlistTarget) ? playlistTarget : HotTarget();
            ApplyRandomVideoCore(t, true);
        }

        void OnAutoSwitchTick(object sender, EventArgs e)
        {
            try
            {
                if (videoFiles == null || videoFiles.Length <= 1) return;
                if ((int)numMin.Value <= 0) return;
                if (!Ok(playlistTarget)) return;
                if ((DateTime.Now - lastSwitch).TotalMinutes >= (double)numMin.Value)
                    ApplyRandomVideoCore(playlistTarget, true);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void ApplyBgCore(IntPtr h, Func<BgWindow> createBg, BgMeta meta)
        {
            CloseBgForm(h);
            byte a = meta.Alpha;
            Manager.ApplyBg(h, a);
            var bg = createBg();
            bgForms[h] = bg;
            bgMeta[h] = meta;
            Manager.SetBgHwnd(h, bg.Handle);
            bg.Follow();
            Hint("背景已应用：窗口透明度=" + a);
            UpdateButtonStates();
        }

        void CloseBgForm(IntPtr h)
        {
            bgMeta.Remove(h);
            BgWindow bg;
            if (bgForms.TryGetValue(h, out bg))
            {
                bgForms.Remove(h);
                try { bg.Hide(); } catch { }
                try { bg.Close(); } catch { }
            }
        }

        void OnBgOff(object sender, EventArgs e) { DoBgOffCore(Selected()); }

        void OnUuToggle(object sender, EventArgs e)
        {
            UuMonitor.Configure(txtKwProc.Text, txtKwTitle.Text);
            if (chkUU.Checked) UuMonitor.Start(); else UuMonitor.Stop();
            SaveSettings();
        }

        void OnUuKeywords(object sender, EventArgs e)
        {
            if (chkUU.Checked) UuMonitor.Configure(txtKwProc.Text, txtKwTitle.Text);
            SaveSettings();
        }

        void OnUuTest(object sender, EventArgs e) { UuMonitor.TestOnce(); }

        void OnBgTick(object sender, EventArgs e)
        {
            var dead = new List<IntPtr>();
            foreach (var kv in bgForms)
            {
                if (!Native.IsWindow(kv.Key))
                    dead.Add(kv.Key);
                else
                    kv.Value.Follow();
            }
            foreach (var k in dead)
            {
                CloseBgForm(k);
                Manager.ClearBg(k);
            }
            if (dead.Count > 0) UpdateButtonStates();
        }

        void OnTick(object sender, EventArgs e)
        {
            Manager.Cleanup();
            var h = Selected();
            if (h != IntPtr.Zero && !Native.IsWindow(h))
            {
                comboWins.SelectedIndex = -1;
                UpdateSelLabel();
            }
            UpdateButtonStates();

            if (chkBgMem != null && chkBgMem.Checked && pendingRestore.Count > 0)
            {
                restoreTries++;
                var done = new List<string[]>();
                foreach (var ent in pendingRestore)
                {
                    IntPtr wh = FindWindowMatch(ent[0], ent[1]);
                    if (wh == IntPtr.Zero) continue;
                    try
                    {
                        var meta = new BgMeta
                        {
                            Kind = ent[2],
                            Path = ent[3],
                            Alpha = (byte)int.Parse(ent[4]),
                            Fit = int.Parse(ent[5])
                        };
                        if (meta.Kind == "img")
                        {
                            var img = Image.FromFile(meta.Path);
                            ApplyBgCore(wh, () => new BgWindow(wh, img, (DisplayFit)meta.Fit), meta);
                        }
                        else
                        {
                            ApplyBgCore(wh, () => new BgWindow(wh, meta.Path, (DisplayFit)meta.Fit), meta);
                        }
                        Hint("已恢复窗口背景：" + ent[1]);
                    }
                    catch (Exception ex) { Program.Log(ex); }
                    done.Add(ent);
                }
                foreach (var d0 in done) pendingRestore.Remove(d0);
                if (restoreTries > 240) pendingRestore.Clear();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tmr.Stop();
            tmrBg.Stop();
            tmrAuto.Stop();
            UuMonitor.Stop();
            UnregisterHotkeys();
            capture.Dispose();
            Manager.DisableAllBlurs();
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
            base.OnFormClosed(e);
        }
    }
}
