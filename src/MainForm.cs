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
        GroupBox grpOps, grpBg, grpUU, grpOI;
        NumericUpDown numBgAlpha, numMin, numSec, numRadius;
        CheckBox chkUU, chkDark, chkLag;
        ComboBox cmbVidMode;
        TextBox txtKwProc, txtKwTitle;
        Button btnSublime, btnVscode, btn302, btnCcChrome, btnCcEdge, btnMsys2, btnToolchain;
        ProgressBar pbOi;
        Label lblOi;
        bool oiToolchainRunning;
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
            Text = "OI 工具箱";
            Font = Theme.FontUi;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = Theme.Sz(524, 652);

            lblSel = new Label();
            lblSel.Location = Theme.P(16, 14);
            lblSel.Size = Theme.Sz(200, 26);
            Theme.StyleLabel(lblSel, Theme.TextDim);
            lblSel.Text = "当前窗口：(未选择)";

            chkAutoRun = new CheckBox();
            chkAutoRun.Text = "开机启动";
            chkAutoRun.Location = Theme.P(220, 10);
            chkAutoRun.Size = Theme.Sz(74, 24);
            Theme.StyleCheck(chkAutoRun);
            chkAutoRun.CheckedChanged += (s, e) => { SetAutoRun(chkAutoRun.Checked); SaveSettings(); };

            chkSilent = new CheckBox();
            chkSilent.Text = "静默";
            chkSilent.Location = Theme.P(296, 10);
            chkSilent.Size = Theme.Sz(52, 24);
            Theme.StyleCheck(chkSilent);
            chkSilent.CheckedChanged += (s, e) => SaveSettings();

            chkDark = new CheckBox();
            chkDark.Text = "暗色";
            chkDark.Location = Theme.P(350, 10);
            chkDark.Size = Theme.Sz(52, 24);
            Theme.StyleCheck(chkDark);
            chkDark.CheckedChanged += OnDarkToggle;

            Label lblRadTip = new Label();
            lblRadTip.Text = "圆角:";
            lblRadTip.Location = Theme.P(406, 14);
            lblRadTip.Size = Theme.Sz(34, 18);
            Theme.StyleLabel(lblRadTip, Theme.TextDim);

            numRadius = new NumericUpDown();
            numRadius.Minimum = 0;
            numRadius.Maximum = 20;
            numRadius.Value = 10;
            numRadius.Location = Theme.P(442, 11);
            numRadius.Size = Theme.Sz(44, 23);
            Theme.StyleNum(numRadius);
            numRadius.ValueChanged += OnRadiusChanged;

            Controls.Add(lblRadTip);
            Controls.Add(chkAutoRun);
            Controls.Add(chkDark);

            comboWins = new ComboBox();
            comboWins.Location = Theme.P(12, 44);
            comboWins.Size = Theme.Sz(300, 24);
            comboWins.DropDownStyle = ComboBoxStyle.DropDownList;
            Theme.StyleCombo(comboWins);

            btnRefresh = NewBtn("刷新", Theme.P(318, 43), Theme.Sz(64, 25), OnRefresh, false);
            btnPick = NewBtn("点选窗口", Theme.P(388, 43), Theme.Sz(108, 25), OnPick, true);

            grpOps = new DarkGroupBox();
            grpOps.Text = "窗口操作";
            grpOps.Location = Theme.P(12, 80);
            grpOps.Size = Theme.Sz(500, 140);
            grpOps.ForeColor = Theme.Text;

            btnTopOnly = GNewBtn(grpOps, "仅置顶(①切换)", Theme.P(14, 24), Theme.Sz(104, 27), OnTopOnly, true);
            btnUnTop = GNewBtn(grpOps, "取消置顶", Theme.P(124, 24), Theme.Sz(88, 27), OnUnTop, false);
            btnOp2 = GNewBtn(grpOps, "②穿透置顶/取消", Theme.P(218, 24), Theme.Sz(126, 27), OnOp2, true);
            btnCancelOp2 = GNewBtn(grpOps, "取消该窗口②", Theme.P(350, 24), Theme.Sz(122, 27), OnCancelOp2, false);
            btnCancelAll = GNewBtn(grpOps, "取消所有②", Theme.P(14, 58), Theme.Sz(104, 27), OnCancelAll, false);
            btnUp = GNewBtn(grpOps, "上移一层", Theme.P(124, 58), Theme.Sz(84, 27), OnLayerUp, false);
            btnDown = GNewBtn(grpOps, "下移一层", Theme.P(214, 58), Theme.Sz(84, 27), OnLayerDown, false);
            btnBottom = GNewBtn(grpOps, "移到最底", Theme.P(304, 58), Theme.Sz(84, 27), OnLayerBottom, false);

            Label lblAlpha = new Label();
            lblAlpha.Text = "透明度:";
            lblAlpha.Location = Theme.P(16, 102);
            lblAlpha.Size = Theme.Sz(56, 20);
            Theme.StyleLabel(lblAlpha, Theme.TextDim);

            trkAlpha = new DarkSlider();
            trkAlpha.Minimum = 40;
            trkAlpha.Maximum = 255;
            trkAlpha.Value = 255;
            trkAlpha.Location = Theme.P(74, 94);
            trkAlpha.Size = Theme.Sz(230, 32);
            trkAlpha.ValueChanged += OnAlphaChanged;

            lblAlphaVal = new Label();
            lblAlphaVal.Text = "255";
            lblAlphaVal.Location = Theme.P(310, 102);
            lblAlphaVal.Size = Theme.Sz(60, 20);
            Theme.StyleLabel(lblAlphaVal, Theme.Accent);

            Label lblAlphaTip = new Label();
            lblAlphaTip.Text = "(拖动即时生效，作用于所选窗口)";
            lblAlphaTip.Location = Theme.P(368, 102);
            lblAlphaTip.Size = Theme.Sz(126, 34);
            Theme.StyleLabel(lblAlphaTip, Theme.TextDim);

            grpOps.Controls.Add(lblAlpha);
            grpOps.Controls.Add(trkAlpha);
            grpOps.Controls.Add(lblAlphaVal);
            grpOps.Controls.Add(lblAlphaTip);

            grpBg = new DarkGroupBox();
            grpBg.Text = "背景";
            grpBg.Location = Theme.P(12, 228);
            grpBg.Size = Theme.Sz(500, 146);
            grpBg.ForeColor = Theme.Text;

            btnImgBg = GNewBtn(grpBg, "应用图片背景(⑦)", Theme.P(14, 22), Theme.Sz(136, 27), OnImageBg, false);
            btnVidBg = GNewBtn(grpBg, "应用视频背景(⑧)", Theme.P(158, 22), Theme.Sz(136, 27), OnVideoBg, false);
            numBgAlpha = new NumericUpDown();
            numBgAlpha.Minimum = 60;
            numBgAlpha.Maximum = 250;
            numBgAlpha.Value = 160;
            numBgAlpha.Location = Theme.P(304, 25);
            numBgAlpha.Size = Theme.Sz(54, 23);
            Theme.StyleNum(numBgAlpha);
            numBgAlpha.ValueChanged += (s, e) => SaveSettings();
            Label lblBgNum = new Label();
            lblBgNum.Text = "背景时透明度";
            lblBgNum.Location = Theme.P(362, 28);
            lblBgNum.Size = Theme.Sz(96, 18);
            Theme.StyleLabel(lblBgNum, Theme.TextDim);

            btnFolderPick = GNewBtn(grpBg, "选择文件夹轮播", Theme.P(14, 52), Theme.Sz(112, 27), OnFolderPick, false);
            cmbVidMode = new ComboBox();
            cmbVidMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbVidMode.Items.AddRange(new object[] { "填充裁剪", "完整显示", "变形铺满" });
            cmbVidMode.SelectedIndex = 0;
            cmbVidMode.Location = Theme.P(130, 54);
            cmbVidMode.Size = Theme.Sz(104, 23);
            Theme.StyleCombo(cmbVidMode);
            cmbVidMode.SelectedIndexChanged += OnFitChanged;
            btnBgOff = GNewBtn(grpBg, "取消背景", Theme.P(238, 52), Theme.Sz(76, 27), OnBgOff, false);
            numMin = new NumericUpDown();
            numMin.Minimum = 0;
            numMin.Maximum = 120;
            numMin.Value = 0;
            numMin.Location = Theme.P(320, 55);
            numMin.Size = Theme.Sz(34, 23);
            Theme.StyleNum(numMin);
            numMin.ValueChanged += (s, e) => SaveSettings();
            Label lblFen = new Label();
            lblFen.Text = "分";
            lblFen.Location = Theme.P(356, 58);
            lblFen.Size = Theme.Sz(18, 18);
            Theme.StyleLabel(lblFen, Theme.TextDim);
            numSec = new NumericUpDown();
            numSec.Minimum = 0;
            numSec.Maximum = 59;
            numSec.Value = 0;
            numSec.Location = Theme.P(374, 55);
            numSec.Size = Theme.Sz(34, 23);
            Theme.StyleNum(numSec);
            numSec.ValueChanged += (s, e) => SaveSettings();
            lblMinTip = new Label();
            lblMinTip.Text = "秒自切(0关)";
            lblMinTip.Location = Theme.P(410, 58);
            lblMinTip.Size = Theme.Sz(78, 18);
            Theme.StyleLabel(lblMinTip, Theme.TextDim);

            chkBgMem = new CheckBox();
            chkBgMem.Text = "记住各窗口背景";
            chkBgMem.Location = Theme.P(14, 84);
            chkBgMem.Size = Theme.Sz(128, 22);
            Theme.StyleCheck(chkBgMem);
            chkBgMem.CheckedChanged += (s, e) => SaveSettings();

            lblBgHint = new Label();
            lblBgHint.Text = "";
            lblBgHint.Location = Theme.P(148, 88);
            lblBgHint.Size = Theme.Sz(338, 20);
            Theme.StyleLabel(lblBgHint, Theme.Green);

            grpBg.Controls.Add(numBgAlpha);
            grpBg.Controls.Add(lblBgNum);
            grpBg.Controls.Add(cmbVidMode);
            grpBg.Controls.Add(numMin);
            grpBg.Controls.Add(lblFen);
            grpBg.Controls.Add(numSec);
            grpBg.Controls.Add(lblMinTip);
            grpBg.Controls.Add(chkBgMem);
            grpBg.Controls.Add(lblBgHint);

            grpUU = new DarkGroupBox();
            grpUU.Text = "UU远程被控提醒(⑨)";
            grpUU.Location = Theme.P(12, 382);
            grpUU.Size = Theme.Sz(500, 84);
            grpUU.ForeColor = Theme.Text;

            chkUU = new CheckBox();
            chkUU.Text = "启用";
            chkUU.Location = Theme.P(14, 24);
            chkUU.Size = Theme.Sz(58, 22);
            Theme.StyleCheck(chkUU);
            chkUU.CheckedChanged += OnUuToggle;

            Label l1 = new Label(); l1.Text = "进程名含:"; l1.Location = Theme.P(80, 27); l1.Size = Theme.Sz(66, 18);
            Theme.StyleLabel(l1, Theme.TextDim);
            // 默认同时匹配 UU远程(GameViewer) 的进程名与 exe 路径，逗号分隔多个关键词
            txtKwProc = new TextBox(); txtKwProc.Text = "uu,gameviewer"; txtKwProc.Location = Theme.P(148, 24); txtKwProc.Size = Theme.Sz(86, 22);
            Theme.StyleText(txtKwProc);
            Label l2 = new Label(); l2.Text = "窗口标题含:"; l2.Location = Theme.P(244, 27); l2.Size = Theme.Sz(74, 18);
            Theme.StyleLabel(l2, Theme.TextDim);
            txtKwTitle = new TextBox(); txtKwTitle.Text = "远程"; txtKwTitle.Location = Theme.P(320, 24); txtKwTitle.Size = Theme.Sz(76, 22);
            Theme.StyleText(txtKwTitle);
            btnTestAlert = GNewBtn(grpUU, "测试弹窗", Theme.P(406, 22), Theme.Sz(84, 25), OnUuTest, false);

            chkLag = new CheckBox();
            chkLag.Text = "卡顿也提醒";
            chkLag.Location = Theme.P(14, 54);
            chkLag.Size = Theme.Sz(100, 22);
            Theme.StyleCheck(chkLag);
            chkLag.CheckedChanged += (s, e) => { UuConfigureLive(); SaveSettings(); };

            lblUUStatus = new Label();
            lblUUStatus.Text = "提示：匹配进程名或exe完整路径(如 GameViewerServer)；被控时鼠标旁持续提醒。";
            lblUUStatus.Location = Theme.P(118, 57);
            lblUUStatus.Size = Theme.Sz(370, 18);
            Theme.StyleLabel(lblUUStatus, Theme.TextDim);

            grpUU.Controls.Add(l1); grpUU.Controls.Add(txtKwProc); grpUU.Controls.Add(l2); grpUU.Controls.Add(txtKwTitle);
            grpUU.Controls.Add(chkLag);
            grpUU.Controls.Add(lblUUStatus);

            grpOI = new DarkGroupBox();
            grpOI.Text = "OI 工具箱（一键下载部署环境）";
            grpOI.Location = Theme.P(12, 474);
            grpOI.Size = Theme.Sz(500, 132);
            grpOI.ForeColor = Theme.Text;

            btnSublime = GNewBtn(grpOI, "Sublime Text", Theme.P(14, 22), Theme.Sz(118, 27), OnDlSublime, false);
            btnVscode = GNewBtn(grpOI, "VS Code", Theme.P(138, 22), Theme.Sz(96, 27), OnDlVscode, false);
            btn302 = GNewBtn(grpOI, "SteamCommunity 302", Theme.P(240, 22), Theme.Sz(150, 27), OnOpen302, false);

            btnCcChrome = GNewBtn(grpOI, "Competitive Companion (Chrome)", Theme.P(14, 52), Theme.Sz(232, 27), OnCcChrome, false);
            btnCcEdge = GNewBtn(grpOI, "Competitive Companion (Edge)", Theme.P(252, 52), Theme.Sz(232, 27), OnCcEdge, false);

            btnMsys2 = GNewBtn(grpOI, "MSYS2", Theme.P(14, 82), Theme.Sz(118, 27), OnMsys2, false);
            btnToolchain = GNewBtn(grpOI, "安装 clang/clangd 工具链", Theme.P(138, 82), Theme.Sz(196, 27), OnToolchain, false);
            pbOi = new ProgressBar();
            pbOi.Location = Theme.P(340, 86);
            pbOi.Size = Theme.Sz(146, 18);
            lblOi = new Label();
            lblOi.Location = Theme.P(14, 112);
            lblOi.Size = Theme.Sz(472, 16);
            Theme.StyleLabel(lblOi, Theme.TextDim);
            lblOi.Text = "编辑器 / 刷题插件 / MSYS2(clang+clangd+g++，UCRT64，自动加入 PATH，供 Sublime LSP 使用)";

            grpOI.Controls.Add(pbOi);
            grpOI.Controls.Add(lblOi);

            lblKeys = new Label();
            lblKeys.Location = Theme.P(12, 612);
            lblKeys.Size = Theme.Sz(500, 34);
            Theme.StyleLabel(lblKeys, Theme.TextDim);
            lblKeys.Tag = "keys";
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
            Controls.Add(grpOI);
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
            Theme.Restyle(this);
            SaveSettings();
        }

        void ReapplyRadii()
        {
            Theme.Restyle(this);
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
            if (d.TryGetValue("switchSec", out v) && decimal.TryParse(v, out x))
                numSec.Value = ClampDec(x, numSec.Minimum, numSec.Maximum);
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
            // 旧版默认值只有"uu"，匹配不到 GameViewerServer，自动升级默认关键词
            if (txtKwProc.Text.Trim().Equals("uu", StringComparison.OrdinalIgnoreCase))
                txtKwProc.Text = "uu,gameviewer";
            if (d.TryGetValue("uuTitle", out v)) txtKwTitle.Text = v;
            chkSilent.Checked = d.TryGetValue("silentStart", out v) && v == "1";
            startupHidden = chkSilent.Checked;
            chkDark.Checked = d.TryGetValue("dark", out v) && v == "1";
            if (Theme.IsDark != chkDark.Checked) Theme.SetDark(chkDark.Checked);
            BackColor = Theme.Bg;
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
            chkLag.Checked = d.TryGetValue("uuLag", out v) && v == "1";
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
                sb.AppendLine("switchSec=" + numSec.Value);
                sb.AppendLine("fitMode=" + cmbVidMode.SelectedIndex);
                sb.AppendLine("radius=" + (int)numRadius.Value);
                sb.AppendLine("uuProc=" + txtKwProc.Text);
                sb.AppendLine("uuTitle=" + txtKwTitle.Text);
                sb.AppendLine("uuEnabled=" + (chkUU.Checked ? "1" : "0"));
                sb.AppendLine("uuLag=" + (chkLag.Checked ? "1" : "0"));
                sb.AppendLine("silentStart=" + (chkSilent.Checked ? "1" : "0"));
                sb.AppendLine("bgMem=" + (chkBgMem.Checked ? "1" : "0"));
                sb.AppendLine("dark=" + (chkDark.Checked ? "1" : "0"));

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
            menu.Items.Add("选择图片背景", null, (s, e) => TrayApplyBg(true));
            menu.Items.Add("选择视频背景", null, (s, e) => TrayApplyBg(false));
            menu.Items.Add("取消背景", null, (s, e) => TrayBgOff());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { reallyExit = true; Close(); });

            tray = new NotifyIcon();
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { tray.Icon = SystemIcons.Application; }
            tray.Text = "OI 工具箱";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            // 只响应左键：右键交给托盘小菜单，不再弹主界面
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
            tray.DoubleClick += (s, e) => ShowMain();
        }

        IntPtr RequireMenuTarget()
        {
            IntPtr t = HotTarget();
            if (!Ok(t))
            {
                Balloon("请先打开主界面选择一个目标窗口。");
                return IntPtr.Zero;
            }
            return t;
        }

        void Balloon(string text)
        {
            if (tray == null) return;
            try
            {
                tray.BalloonTipTitle = "OI 工具箱";
                tray.BalloonTipText = text;
                tray.ShowBalloonTip(1500);
            }
            catch { }
        }

        void TrayApplyBg(bool image)
        {
            IntPtr t = RequireMenuTarget();
            if (!Ok(t)) return;
            if (image) PickImageAndApply(t); else PickVideoAndApply(t);
        }

        void TrayBgOff()
        {
            IntPtr t = RequireMenuTarget();
            if (!Ok(t)) return;
            bool had = Manager.ClearBg(t);
            if (had)
            {
                CloseBgForm(t);
                videoFiles = null;
                playlistTarget = IntPtr.Zero;
                UpdateButtonStates();
            }
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
                        tray.BalloonTipTitle = "OI 工具箱";
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
            if (Program.WM_ALREADY_RUNNING != 0 && m.Msg == Program.WM_ALREADY_RUNNING)
            {
                // 又一个本程序实例启动了：已互斥退出，这里把已有主界面拉起来
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
            // 必须用自绘 DarkButton：原生 Button 的悬停是系统灰且无圆角
            var b = new DarkButton();
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
            // Win11: 系统级圆角窗口（Win10 上调用失败会被忽略）
            int pref = 2;
            Native.DwmSetWindowAttribute(Handle, 33, ref pref, 4);
            UpdateTitleBarTheme();
        }

        void UpdateTitleBarTheme()
        {
            int dark = Theme.IsDark ? 1 : 0;
            Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
            Native.DwmSetWindowAttribute(Handle, 19, ref dark, 4);
        }

        void OnDarkToggle(object sender, EventArgs e)
        {
            Theme.SetDark(chkDark.Checked);
            BackColor = Theme.Bg;
            UpdateTitleBarTheme();
            Theme.Restyle(this);
            SaveSettings();
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
                int total = (int)numMin.Value * 60 + (int)numSec.Value;
                if (total <= 0) return;
                if (!Ok(playlistTarget)) return;
                if ((DateTime.Now - lastSwitch).TotalSeconds >= total)
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

        void UuConfigureLive()
        {
            UuMonitor.Configure(txtKwProc.Text, txtKwTitle.Text, chkLag.Checked);
        }

        void OnUuToggle(object sender, EventArgs e)
        {
            UuConfigureLive();
            if (chkUU.Checked) UuMonitor.Start(); else UuMonitor.Stop();
            SaveSettings();
        }

        void OnUuKeywords(object sender, EventArgs e)
        {
            if (chkUU.Checked) UuConfigureLive();
            SaveSettings();
        }

        void OnUuTest(object sender, EventArgs e) { UuMonitor.TestOnce(); }

        // ---------------- OI 工具箱 ----------------

        void OiStatus(string s)
        {
            try { lblOi.Text = s; } catch { }
        }

        void OiStatusSafe(string s)
        {
            try { BeginInvoke((Action)delegate { OiStatus(s); }); } catch { }
        }

        void StartDownload(string url, string name, Action<string> afterDone)
        {
            if (OiTools.Busy) { OiStatus("已有下载在进行中，请稍候…"); return; }
            string dest;
            try { dest = Path.Combine(OiTools.DownloadsDir(), Path.GetFileName(new Uri(url).LocalPath)); }
            catch { dest = Path.Combine(OiTools.DownloadsDir(), name.Replace(" ", "") + ".exe"); }
            try
            {
                pbOi.Value = 0;
            }
            catch { }
            OiStatus("下载 " + name + " …");
            OiTools.Download(url, dest,
                (rec, tot) =>
                {
                    int pct = tot > 0 ? (int)(rec * 100 / tot) : 0;
                    try { BeginInvoke((Action)delegate { try { pbOi.Value = pct; } catch { } OiStatus("下载 " + name + " … " + pct + "%"); }); } catch { }
                },
                () =>
                {
                    try { BeginInvoke((Action)delegate { try { pbOi.Value = 100; } catch { } }); } catch { }
                    OiStatusSafe(name + " 下载完成：" + dest);
                    if (afterDone != null) afterDone(dest);
                },
                msg => OiStatusSafe(name + " 下载失败：" + msg + "（可尝试关闭代理或手动打开官网）"));
        }

        void OnDlSublime(object sender, EventArgs e)
        {
            OiStatus("获取 Sublime Text 最新版本号…");
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string ver = null;
                try { ver = OiTools.FetchSublimeVersion(); }
                catch (Exception ex) { Program.Log(ex); }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (ver == null)
                        {
                            OiTools.OpenUrl("https://www.sublimetext.com/download");
                            OiStatus("获取版本号失败，已打开官网下载页。");
                            return;
                        }
                        StartDownload(
                            "https://download.sublimetext.com/sublime_text_build_" + ver + "_x64_setup.exe",
                            "Sublime Text " + ver,
                            dest => OiTools.RunInstaller(dest, "/SILENT"));
                    });
                }
                catch (Exception ex) { Program.Log(ex); }
            });
        }

        void OnDlVscode(object sender, EventArgs e)
        {
            StartDownload("https://update.code.visualstudio.com/latest/win32-x64-user/stable", "VS Code",
                dest => OiTools.RunInstaller(dest, "/VERYSILENT /NORESTART /MERGETASKS=!runcode"));
        }

        void OnOpen302(object sender, EventArgs e)
        {
            OiTools.OpenUrl("https://www.dogfight360.com/blog/1484/");
            OiStatus("已打开 SteamCommunity 302 官方页面，在页面内下载最新版。");
        }

        void OnCcChrome(object sender, EventArgs e)
        {
            OiTools.OpenUrl("https://chromewebstore.google.com/detail/cjnmckjndlmiambhedcpomfbpkeodmdj");
            OiStatus("已打开 Chrome 商店页面，点「添加至 Chrome」安装 Competitive Companion。");
        }

        void OnCcEdge(object sender, EventArgs e)
        {
            OiTools.OpenUrl("https://microsoftedge.microsoft.com/addons/search/competitive%20companion");
            OiStatus("已打开 Edge 商店搜索页，选择 Competitive Companion 点「获取」安装。");
        }

        void OnMsys2(object sender, EventArgs e)
        {
            if (OiTools.Msys2Installed())
            {
                OiStatus("MSYS2 已安装在 " + OiTools.MsysRoot + "，可直接安装工具链。");
                return;
            }
            StartDownload("https://repo.msys2.org/distrib/msys2-x86_64-latest.exe", "MSYS2", dest =>
            {
                OiStatusSafe("正在静默安装 MSYS2 到 " + OiTools.MsysRoot + "（约1-2分钟）…");
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        int code = OiTools.InstallMsys2Silent(dest);
                        OiStatusSafe(code == 0
                            ? "MSYS2 安装完成！点「安装 clang/clangd 工具链」继续。"
                            : "MSYS2 安装程序退出码 " + code + "，可手动运行：" + dest);
                    }
                    catch (Exception ex) { OiStatusSafe("MSYS2 安装失败：" + ex.Message); }
                });
            });
        }

        void OnToolchain(object sender, EventArgs e)
        {
            if (!OiTools.Msys2Installed()) { OiStatus("请先点「MSYS2」下载安装。"); return; }
            if (oiToolchainRunning) { OiStatus("工具链正在安装中…"); return; }
            oiToolchainRunning = true;
            OiStatus("工具链安装中：同步 pacman 数据库（首次较慢）…");
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var log = new StringBuilder();
                    Action<string> on = line =>
                    {
                        if (string.IsNullOrEmpty(line)) return;
                        lock (log) { log.AppendLine(line); }
                        string shown = line.Length > 90 ? line.Substring(0, 90) + "…" : line;
                        OiStatusSafe("工具链: " + shown);
                    };
                    // 首次运行可能要求更新核心包（退出码126），跑两次
                    OiTools.RunBash("pacman -Syuu --noconfirm", on);
                    OiTools.RunBash("pacman -Syuu --noconfirm", on);
                    OiTools.RunBash("pacman -S --needed --noconfirm " +
                        "mingw-w64-ucrt-x86_64-clang " +
                        "mingw-w64-ucrt-x86_64-clang-tools-extra " +  // clangd
                        "mingw-w64-ucrt-x86_64-gdb " +
                        "mingw-w64-ucrt-x86_64-make " +
                        "mingw-w64-ucrt-x86_64-gcc", on);
                    bool pathOk = OiTools.AddMsysToPath();
                    try
                    {
                        string logFile = Path.Combine(Path.GetTempPath(), "WindowTool_oi.log");
                        lock (log) { File.WriteAllText(logFile, log.ToString()); }
                    }
                    catch { }
                    BeginInvoke((Action)delegate
                    {
                        oiToolchainRunning = false;
                        OiStatus(pathOk
                            ? "工具链安装完成！clangd/g++ 已就位（UCRT64\\bin 已加入用户 PATH，重启 Sublime 后 LSP 可用）。"
                            : "工具链安装完成，但加入 PATH 失败，请手动把 " + OiTools.MsysRoot + "\\ucrt64\\bin 加入 PATH。");
                    });
                }
                catch (Exception ex)
                {
                    Program.Log(ex);
                    BeginInvoke((Action)delegate
                    {
                        oiToolchainRunning = false;
                        OiStatus("工具链安装失败：" + ex.Message);
                    });
                }
            });
        }

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
            // 退出自动还原：取消所有②、还原置顶/透明度/背景等一切修改
            try { Manager.CancelAll(); } catch { }
            try { Manager.RestoreAll(); } catch { }
            base.OnFormClosed(e);
        }
    }
}
