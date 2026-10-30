using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>
    /// 背景页：图片背景的选择、显示模式、透明度、背景记忆。
    /// 视频背景与 WPF 依赖已在 v2.0 移除。
    /// </summary>
    sealed class BgPage : Page
    {
        UiCard applyCard;
        UiButton pickBtn;
        UiButton offBtn;
        UiSegmented fitSeg;
        UiSlider alphaSlider;
        UiToggle memToggle;
        LabelEx targetHint;

        UiCard listCard;
        UiButton refreshListBtn;
        readonly List<BgRow> rows = new List<BgRow>();

        public BgPage(AppForm app)
            : base(app, "bg", "窗口背景")
        {
        }

        public override void Build()
        {
            applyCard = new UiCard { Title = "应用到当前目标窗口" };
            targetHint = new LabelEx { Font = Theme.FontSmall, TextColor = Theme.TextDim };
            applyCard.Controls.Add(targetHint);

            pickBtn = new UiButton { Label = "选择图片…", Icon = Glyph.Picture, Kind = UiButtonKind.Primary };
            pickBtn.Click += (s, e) =>
            {
                if (!App.Ok(App.Target)) { App.ToastWarn("请先在「窗口」页选择目标窗口。"); return; }
                App.PickImageAndApply(App.Target);
                RebuildRows();
            };
            applyCard.Controls.Add(pickBtn);

            offBtn = new UiButton { Label = "取消背景", Icon = Glyph.Close, Kind = UiButtonKind.Normal };
            offBtn.Click += (s, e) => { App.ClearBgOn(App.Target); RebuildRows(); };
            applyCard.Controls.Add(offBtn);

            fitSeg = new UiSegmented { Items = new[] { "填充裁剪", "完整显示", "变形铺满" } };
            fitSeg.SelectedIndex = (int)App.Fit;
            fitSeg.Changed += (s, e) => { App.Fit = (DisplayFit)fitSeg.SelectedIndex; };
            applyCard.Controls.Add(fitSeg);

            alphaSlider = new UiSlider { Label = "背景态透明度", Min = 60, Max = 250 };
            alphaSlider.Value = App.BgAlpha();
            alphaSlider.Changed += (s, e) => App.SetBgAlpha(alphaSlider.Value);
            applyCard.Controls.Add(alphaSlider);

            memToggle = new UiToggle { Label = "记住各窗口背景", Hint = "下次启动自动恢复同名进程+同标题窗口的背景" };
            memToggle.Checked = App.BgMemory;
            memToggle.Toggled += (s, e) => App.BgMemory = memToggle.Checked;
            applyCard.Controls.Add(memToggle);
            Controls.Add(applyCard);

            listCard = new UiCard { Title = "当前生效的背景" };
            refreshListBtn = new UiButton { Label = "刷新", Icon = Glyph.Play, Kind = UiButtonKind.Normal, Small = true };
            refreshListBtn.Click += (s, e) => RebuildRows();
            listCard.Controls.Add(refreshListBtn);
            Controls.Add(listCard);

            empty = new LabelEx { Font = Theme.FontSmall, TextColor = Theme.TextFaint, Text2 = "（当前没有窗口使用背景）" };
            listCard.Controls.Add(empty);

            App.TargetChanged += (s, e) => SyncTarget();
            SyncTarget();
            RebuildRows();
        }

        void SyncTarget()
        {
            IntPtr t = App.Target;
            if (App.Ok(t))
            {
                var e = App.Entry(t);
                targetHint.Text2 = "目标：" + AppForm.TitleOf(t) + (e != null ? "（" + e.Proc + "）" : "");
            }
            else targetHint.Text2 = "尚未选择目标窗口（在「窗口」页选择）";
            targetHint.Invalidate();
        }

        void RebuildRows()
        {
            foreach (var r in rows)
            {
                listCard.Controls.Remove(r);
                r.Dispose();
            }
            rows.Clear();

            var snap = App.BgSnapshot();
            int y = listCard.HeaderH + Theme.S(34);
            foreach (var kv in snap)
            {
                var row = new BgRow(kv.Key, kv.Value);
                row.Removed += delegate { RebuildRows(); };
                listCard.Controls.Add(row);
                rows.Add(row);
                y += row.Height + Theme.S(4);
            }

            empty.Visible = rows.Count == 0;
            Relayout(Width, Height);
        }

        LabelEx empty;

        public override void OnShow()
        {
            SyncTarget();
            RebuildRows();
            alphaSlider.Value = App.BgAlpha();
            fitSeg.SelectedIndex = (int)App.Fit;
            memToggle.Checked = App.BgMemory;
        }

        public override void Tick()
        {
            if (!Visible) return;
            SyncTarget();
            foreach (var r in rows) r.RefreshState();
        }

        public override void Relayout(int w, int h)
        {
            if (applyCard == null) return;
            int cw = Math.Min(w - Theme.S(8), Theme.S(560));
            int x = Math.Max(0, (w - cw) / 2);

            applyCard.SetBounds(x, 0, cw, Theme.S(224));
            targetHint.SetBounds(Theme.S(14), applyCard.HeaderH + Theme.S(2), cw - Theme.S(28), Theme.S(16));

            pickBtn.SetBounds(Theme.S(14), applyCard.HeaderH + Theme.S(24), (cw - Theme.S(32)) / 2, Theme.S(30));
            offBtn.SetBounds(Theme.S(14) + (cw - Theme.S(32)) / 2 + Theme.S(4), applyCard.HeaderH + Theme.S(24), (cw - Theme.S(32)) / 2, Theme.S(30));

            fitSeg.SetBounds(Theme.S(14), applyCard.HeaderH + Theme.S(62), cw - Theme.S(28), Theme.S(26));
            alphaSlider.SetBounds(Theme.S(14), applyCard.HeaderH + Theme.S(96), cw - Theme.S(28), Theme.S(38));
            memToggle.SetBounds(Theme.S(14), applyCard.HeaderH + Theme.S(142), cw - Theme.S(28), Theme.S(36));

            // 列表卡片（高度按内容，超出由页面滚动）
            int ly = Theme.S(224) + Theme.S(14);
            int rowsH = 0;
            foreach (var r in rows) rowsH += r.Height + Theme.S(4);
            int listH = Math.Max(Theme.S(110), rowsH + listCard.HeaderH + Theme.S(46));
            listCard.SetBounds(x, ly, cw, listH);
            refreshListBtn.SetBounds(cw - Theme.S(80), listCard.HeaderH - Theme.S(26), Theme.S(66), Theme.S(22));

            empty.SetBounds(Theme.S(14), listCard.HeaderH + Theme.S(8), cw - Theme.S(28), Theme.S(18));

            int yy = listCard.HeaderH + Theme.S(34);
            foreach (var r in rows)
            {
                r.SetBounds(Theme.S(10), yy, cw - Theme.S(20), r.Height);
                yy += r.Height + Theme.S(4);
            }
        }

        /// 一行：窗口标题 + 路径 + 移除按钮
        sealed class BgRow : Control
        {
            readonly IntPtr hwnd;
            readonly AppForm.BgMeta meta;
            UiButton del;
            LabelEx titleL, pathL;
            public event Action Removed;

            public BgRow(IntPtr h, AppForm.BgMeta m)
            {
                hwnd = h;
                meta = m;
                Height = Theme.S(40);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.InputBg;

                titleL = new LabelEx { Font = Theme.FontBody, TextColor = Theme.Text };
                Controls.Add(titleL);
                pathL = new LabelEx { Font = Theme.FontSmall, TextColor = Theme.TextDim };
                Controls.Add(pathL);
                del = new UiButton { Label = "移除", Icon = Glyph.Trash, Kind = UiButtonKind.Quiet, Small = true };
                del.Click += delegate
                {
                    AppInst.ClearBgOn(hwnd);
                    var r = Removed;
                    if (r != null) r();
                };
                Controls.Add(del);
                RefreshState();
            }

            static AppForm AppInst { get { return AppForm.Instance; } }

            public void RefreshState()
            {
                titleL.Text2 = Native.IsWindow(hwnd)
                    ? Theme.Ellipsis(AppForm.TitleOf(hwnd), Theme.FontBody, Width - Theme.S(90))
                    : "(窗口已关闭)";
                pathL.Text2 = meta.Path;
                titleL.Invalidate();
                pathL.Invalidate();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                int dw = Theme.S(70);
                del.SetBounds(Width - dw - Theme.S(4), (Height - Theme.S(24)) / 2, dw, Theme.S(24));
                titleL.SetBounds(Theme.S(10), Theme.S(2), Width - dw - Theme.S(16), Theme.S(20));
                pathL.SetBounds(Theme.S(10), Theme.S(20), Width - dw - Theme.S(16), Theme.S(16));
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
