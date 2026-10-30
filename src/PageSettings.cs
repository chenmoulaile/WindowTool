using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>
    /// 设置页：主题 / 强调色 / 置顶守护 / 自动置顶规则 / 启动 / 热键 / 数据。
    /// </summary>
    sealed class SettingsPage : Page
    {
        UiCard lookCard;
        UiSegmented themeSeg;
        readonly List<Swatch> swatches = new List<Swatch>();
        LabelEx hueName;

        UiCard guardCard;
        UiToggle guardToggle;
        LabelEx guardStat;

        UiCard ruleCard;
        readonly List<RuleRow> ruleRows = new List<RuleRow>();
        UiButton addRuleBtn;
        LabelEx ruleEmpty;

        UiCard startCard;
        UiToggle autoRunToggle;
        UiToggle silentToggle;

        UiCard keyCard;
        LabelEx keyText;
        LabelEx hotFail;

        UiCard dataCard;
        UiButton exportBtn, importBtn, resetBtn;
        LabelEx aboutL;

        public SettingsPage(AppForm app)
            : base(app, "settings", "设置")
        {
        }

        public override void Build()
        {
            // ---------------- 外观 ----------------
            lookCard = new UiCard { Title = "外观" };

            var themeL = new LabelEx { Font = Theme.FontBody, TextColor = Theme.Text, Text2 = "主题" };
            lookCard.Controls.Add(themeL);
            themeSeg = new UiSegmented { Items = new[] { "浅色", "深色", "跟随系统" } };
            themeSeg.SelectedIndex = Math.Max(0, Math.Min(2, App.Cfg.Int("darkMode", 1)));
            themeSeg.Changed += (s, e) =>
            {
                App.Cfg.Set("darkMode", themeSeg.SelectedIndex);
                App.Save();
                App.ApplyTheme();
                RebuildSwatches();
            };
            lookCard.Controls.Add(themeSeg);

            var hueL = new LabelEx { Font = Theme.FontBody, TextColor = Theme.Text, Text2 = "强调色" };
            lookCard.Controls.Add(hueL);
            hueName = new LabelEx { Font = Theme.FontSmall, TextColor = Theme.TextDim };
            lookCard.Controls.Add(hueName);
            RebuildSwatches();
            Controls.Add(lookCard);

            // ---------------- 置顶守护 ----------------
            guardCard = new UiCard { Title = "置顶守护" };
            guardToggle = new UiToggle
            {
                Label = "启用置顶守护",
                Hint = "被其他程序压下去时自动顶回来；前台切换瞬间即时补防"
            };
            guardToggle.Checked = App.GuardEnabled;
            guardToggle.Toggled += (s, e) => { App.GuardEnabled = guardToggle.Checked; };
            guardCard.Controls.Add(guardToggle);

            guardStat = new LabelEx { Font = Theme.FontSmall, TextColor = Theme.TextDim };
            guardCard.Controls.Add(guardStat);

            var adminHint = new LabelEx
            {
                Font = Theme.FontSmall,
                TextColor = Theme.TextDim,
                Text2 = "提示：对高权限窗口（如任务管理器、部分杀软）置顶无效时，\n请右键本程序选择「以管理员身份运行」。",
                Multiline = true
            };
            guardCard.Controls.Add(adminHint);
            Controls.Add(guardCard);

            // ---------------- 自动置顶规则 ----------------
            ruleCard = new UiCard { Title = "自动置顶规则（按进程名）" };
            addRuleBtn = new UiButton { Label = "添加当前目标进程", Icon = Glyph.Plus, Kind = UiButtonKind.Normal, Small = true };
            addRuleBtn.Click += (s, e) =>
            {
                var en = App.Entry(App.Target);
                if (en == null || en.Proc.Length == 0) { App.ToastWarn("请先在「窗口」页选择目标窗口。"); return; }
                if (App.AutoRules.Contains(en.Proc)) { App.ToastWarn("规则里已有 " + en.Proc); return; }
                App.AddAutoRule(en.Proc);
                App.ToastGood("已添加规则：" + en.Proc + "（新窗口出现时自动置顶）");
                RebuildRules();
            };
            ruleCard.Controls.Add(addRuleBtn);
            ruleEmpty = new LabelEx
            {
                Font = Theme.FontSmall,
                TextColor = Theme.TextFaint,
                Text2 = "（没有规则。例如把 wemeetapp 加进来，腾讯会议一打开就会自动置顶。）",
                Multiline = true
            };
            ruleCard.Controls.Add(ruleEmpty);
            RebuildRules();
            Controls.Add(ruleCard);

            // ---------------- 启动 ----------------
            startCard = new UiCard { Title = "启动" };
            autoRunToggle = new UiToggle { Label = "开机自动运行", Hint = "写入当前用户注册表 Run 项" };
            autoRunToggle.Checked = App.AutoRunOn();
            autoRunToggle.Toggled += (s, e) => App.SetAutoRun(autoRunToggle.Checked, true);
            startCard.Controls.Add(autoRunToggle);

            silentToggle = new UiToggle { Label = "静默启动", Hint = "双击后只在托盘出现，不弹窗口" };
            silentToggle.Checked = App.Cfg.Bool("silentStart", false);
            silentToggle.Toggled += (s, e) =>
            {
                App.Cfg.Set("silentStart", silentToggle.Checked);
                App.Save();
            };
            startCard.Controls.Add(silentToggle);
            Controls.Add(startCard);

            // ---------------- 热键 ----------------
            keyCard = new UiCard { Title = "全局热键（Ctrl+Alt+…）" };
            hotFail = new LabelEx
            {
                Font = Theme.FontSmall,
                TextColor = Theme.Warn,
                Multiline = true,
                Text2 = ""
            };
            keyCard.Controls.Add(hotFail);
            keyText = new LabelEx
            {
                Font = Theme.FontMono,
                TextColor = Theme.TextDim,
                Multiline = true,
                Text2 =
                    " 1  置顶切换              6  取消全部穿透置顶\n" +
                    " 2  穿透置顶/再按取消      7  图片背景\n" +
                    " 3  上移一层              0  取消背景\n" +
                    " 4  下移一层              N  移到下一显示器\n" +
                    " 5  取消该窗口穿透        - / =  透明度 -15 / +15\n" +
                    "\n" +
                    " Ctrl+Alt+Shift + ← → ↑ ↓   左/右/上/下半屏\n" +
                    " Ctrl+Alt+Shift + Q W E R   四象限    M 铺满  B 还原\n" +
                    "\n" +
                    " 热键作用于当前聚焦的窗口；焦点在本工具上时作用于列表中选中的窗口。"
            };
            keyCard.Controls.Add(keyText);
            Controls.Add(keyCard);

            // ---------------- 数据 ----------------
            dataCard = new UiCard { Title = "设置数据" };
            exportBtn = new UiButton { Label = "导出设置", Icon = Glyph.Layers, Kind = UiButtonKind.Normal, Small = true };
            exportBtn.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "导出设置";
                    dlg.Filter = "设置文件|*.txt";
                    dlg.FileName = "WindowTool_settings.txt";
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    try { File.Copy(App.Cfg.Path, dlg.FileName, true); App.ToastGood("已导出到：" + dlg.FileName); }
                    catch (Exception ex) { App.ToastBad("导出失败：" + ex.Message); }
                }
            };
            dataCard.Controls.Add(exportBtn);

            importBtn = new UiButton { Label = "导入设置", Icon = Glyph.Keyboard, Kind = UiButtonKind.Normal, Small = true };
            importBtn.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "导入设置";
                    dlg.Filter = "设置文件|*.txt|所有文件|*.*";
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    try
                    {
                        File.Copy(dlg.FileName, App.Cfg.Path, true);
                        App.ToastGood("已导入，重启本程序后生效。");
                    }
                    catch (Exception ex) { App.ToastBad("导入失败：" + ex.Message); }
                }
            };
            dataCard.Controls.Add(importBtn);

            resetBtn = new UiButton { Label = "恢复默认并重启", Icon = Glyph.Power, Kind = UiButtonKind.Danger, Small = true };
            resetBtn.Click += (s, e) =>
            {
                try { File.Delete(App.Cfg.Path); } catch { }
                Application.Restart();
            };
            dataCard.Controls.Add(resetBtn);

            aboutL = new LabelEx
            {
                Font = Theme.FontSmall,
                TextColor = Theme.TextFaint,
                Text2 = "OI 窗口工具 v2.0 · 窗口置顶 / 穿透 / 背景 / 布局 · MIT License"
            };
            dataCard.Controls.Add(aboutL);
            Controls.Add(dataCard);
        }

        void RebuildSwatches()
        {
            foreach (var sw in swatches)
            {
                lookCard.Controls.Remove(sw);
                sw.Dispose();
            }
            swatches.Clear();
            string[] names = { "石墨蓝", "青瓷", "鸢尾紫", "赤陶", "苔绿" };
            for (int i = 0; i < 5; i++)
            {
                var hue = (AccentHue)i;
                var sw = new Swatch(hue);
                sw.Picked += delegate
                {
                    App.Cfg.Set("hue", (int)hue);
                    App.Save();
                    App.ApplyTheme();
                    hueName.Text2 = names[i];
                    hueName.Invalidate();
                    foreach (var s2 in swatches) s2.Invalidate();
                };
                lookCard.Controls.Add(sw);
                swatches.Add(sw);
            }
            hueName.Text2 = names[Math.Max(0, Math.Min(4, App.Cfg.Int("hue", 0)))];
        }

        void RebuildRules()
        {
            foreach (var r in ruleRows)
            {
                ruleCard.Controls.Remove(r);
                r.Dispose();
            }
            ruleRows.Clear();

            foreach (string rule in App.AutoRules.ToArray())
            {
                var row = new RuleRow(rule);
                row.Removed += RebuildRules;
                ruleCard.Controls.Add(row);
                ruleRows.Add(row);
            }
            ruleEmpty.Visible = ruleRows.Count == 0;
            Relayout(Width, Height);
        }

        public override void OnShow()
        {
            themeSeg.SelectedIndex = Math.Max(0, Math.Min(2, App.Cfg.Int("darkMode", 1)));
            guardToggle.Checked = App.GuardEnabled;
            autoRunToggle.Checked = App.AutoRunOn();
            silentToggle.Checked = App.Cfg.Bool("silentStart", false);
            RebuildSwatches();
            RebuildRules();
        }

        public override void Tick()
        {
            if (!Visible) return;
            guardStat.Text2 = "本次运行已补回 " + Manager.GuardRescues + " 次被压下的置顶";
            guardStat.Invalidate();
            var fails = App.HotkeyFails;
            string t = fails.Count == 0 ? "" : "注册失败的热键：" + string.Join("、", fails.ToArray()) + "（可能被其他程序占用）";
            if (t != hotFail.Text2)
            {
                hotFail.Text2 = t;
                hotFail.Invalidate();
            }
        }

        public override void Relayout(int w, int h)
        {
            if (lookCard == null) return;
            int cw = Math.Min(w - Theme.S(8), Theme.S(560));
            int x = Math.Max(0, (w - cw) / 2);
            int y = 0;

            lookCard.SetBounds(x, y, cw, Theme.S(150));
            // 主题行（标题带下方）
            var themeL = FindLabel(lookCard, "主题");
            if (themeL != null) themeL.SetBounds(Theme.S(14), lookCard.HeaderH + Theme.S(4), Theme.S(60), Theme.S(18));
            themeSeg.SetBounds(Theme.S(84), lookCard.HeaderH + Theme.S(2), Theme.S(210), Theme.S(24));
            var hueL = FindLabel(lookCard, "强调色");
            if (hueL != null) hueL.SetBounds(Theme.S(14), lookCard.HeaderH + Theme.S(40), Theme.S(60), Theme.S(18));
            hueName.SetBounds(Theme.S(320), lookCard.HeaderH + Theme.S(44), Theme.S(140), Theme.S(18));
            int sx = Theme.S(84);
            foreach (var sw in swatches)
            {
                sw.SetBounds(sx, lookCard.HeaderH + Theme.S(38), Theme.S(30), Theme.S(30));
                sx += Theme.S(38);
            }
            y += Theme.S(150) + Theme.S(12);

            guardCard.SetBounds(x, y, cw, Theme.S(150));
            guardToggle.SetBounds(Theme.S(12), guardCard.HeaderH + Theme.S(2), cw - Theme.S(24), Theme.S(36));
            guardStat.SetBounds(Theme.S(12), guardCard.HeaderH + Theme.S(44), cw - Theme.S(24), Theme.S(18));
            var adminHint = FindAdminHint(guardCard);
            if (adminHint != null) adminHint.SetBounds(Theme.S(12), guardCard.HeaderH + Theme.S(68), cw - Theme.S(24), Theme.S(34));
            y += Theme.S(150) + Theme.S(12);

            int rulesH = Theme.S(120) + ruleRows.Count * (Theme.S(30) + Theme.S(4));
            ruleCard.SetBounds(x, y, cw, rulesH);
            addRuleBtn.SetBounds(Theme.S(12), ruleCard.HeaderH + Theme.S(2), Theme.S(180), Theme.S(26));
            ruleEmpty.SetBounds(Theme.S(12), ruleCard.HeaderH + Theme.S(34), cw - Theme.S(24), Theme.S(34));
            int ry = ruleCard.HeaderH + Theme.S(34);
            foreach (var r in ruleRows)
            {
                r.SetBounds(Theme.S(12), ry, cw - Theme.S(24), Theme.S(28));
                ry += Theme.S(30);
            }
            y += rulesH + Theme.S(12);

            startCard.SetBounds(x, y, cw, Theme.S(124));
            autoRunToggle.SetBounds(Theme.S(12), startCard.HeaderH + Theme.S(2), cw - Theme.S(24), Theme.S(36));
            silentToggle.SetBounds(Theme.S(12), startCard.HeaderH + Theme.S(42), cw - Theme.S(24), Theme.S(36));
            y += Theme.S(124) + Theme.S(12);

            keyCard.SetBounds(x, y, cw, Theme.S(200));
            keyText.SetBounds(Theme.S(14), keyCard.HeaderH, cw - Theme.S(28), Theme.S(132));
            hotFail.SetBounds(Theme.S(14), keyCard.HeaderH + Theme.S(134), cw - Theme.S(28), Theme.S(30));
            y += Theme.S(200) + Theme.S(12);

            dataCard.SetBounds(x, y, cw, Theme.S(110));
            int bw = (cw - Theme.S(28) - Theme.S(8)) / 3;
            exportBtn.SetBounds(Theme.S(14), dataCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            importBtn.SetBounds(Theme.S(14) + bw + Theme.S(4), dataCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            resetBtn.SetBounds(Theme.S(14) + (bw + Theme.S(4)) * 2, dataCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            aboutL.SetBounds(Theme.S(14), dataCard.HeaderH + Theme.S(40), cw - Theme.S(28), Theme.S(18));
        }

        static LabelEx FindAdminHint(UiCard card)
        {
            foreach (Control c in card.Controls)
            {
                var l = c as LabelEx;
                if (l != null && l.Multiline && l.Text2.IndexOf("管理员") >= 0) return l;
            }
            return null;
        }

        static LabelEx FindLabel(UiCard card, string text)
        {
            foreach (Control c in card.Controls)
            {
                var l = c as LabelEx;
                if (l != null && l.Text2 == text) return l;
            }
            return null;
        }

        /// 色块
        sealed class Swatch : Control
        {
            readonly AccentHue hue;
            public event Action Picked;

            public Swatch(AccentHue h)
            {
                hue = h;
                Size = Theme.Sz(30, 30);
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                var p = Picked;
                if (p != null) p();
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // 取填充变体：通过 Theme.Hue 临时切换读取（只读色值，不改全局状态的做法：
                // 直接复制 AccentFill 逻辑里的色值）
                Color c = FillOf(hue);
                bool sel = Theme.Hue == hue;
                var r = ClientRectangle; r.Width--; r.Height--;
                using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
                using (var pen = new Pen(sel ? Theme.Text : Theme.Border, sel ? 2f : 1f))
                    g.DrawEllipse(pen, r);
            }

            static Color FillOf(AccentHue h)
            {
                switch (h)
                {
                    case AccentHue.Jade: return Color.FromArgb(0x2F, 0x9E, 0x77);
                    case AccentHue.Violet: return Color.FromArgb(0x7C, 0x6C, 0xE0);
                    case AccentHue.Terracotta: return Color.FromArgb(0xD1, 0x5C, 0x4B);
                    case AccentHue.Moss: return Color.FromArgb(0x71, 0x8D, 0x3F);
                    default: return Color.FromArgb(0x37, 0x74, 0xD9);
                }
            }
        }

        /// 规则行
        sealed class RuleRow : Control
        {
            readonly string proc;
            LabelEx nameL;
            UiButton delBtn;
            public event Action Removed;

            public RuleRow(string p)
            {
                proc = p;
                Height = Theme.S(28);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                nameL = new LabelEx { Font = Theme.FontMono, TextColor = Theme.Text, Text2 = p };
                Controls.Add(nameL);
                delBtn = new UiButton { Label = "删除", Icon = Glyph.Trash, Kind = UiButtonKind.Quiet, Small = true };
                delBtn.Click += delegate
                {
                    AppForm.Instance.RemoveAutoRule(proc);
                    var h = Removed;
                    if (h != null) h();
                };
                Controls.Add(delBtn);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                delBtn.SetBounds(Width - Theme.S(74), 0, Theme.S(74), Height);
                nameL.SetBounds(Theme.S(10), 0, Width - Theme.S(84), Height);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var r = ClientRectangle; r.Width--; r.Height--;
                Theme.FillRound(g, r, Theme.InputBg, Theme.S(6));
                using (var pen = new Pen(Theme.Border)) Theme.RoundRect(g, r, Theme.S(6));
            }
        }
    }
}
