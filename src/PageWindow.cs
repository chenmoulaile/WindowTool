using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>
    /// 窗口页：左侧可搜索窗口列表 + 右侧目标操作面板。
    /// </summary>
    sealed class WindowPage : Page
    {
        Panel right;          // 右列滚动容器
        UiSearch search;
        WinList list;
        UiButton pickBtn;
        UiButton refreshBtn;

        // 右侧操作面板
        UiCard targetCard;
        LabelEx targetTitle;
        LabelEx targetProc;

        UiCard pinCard;
        UiToggle topToggle;
        UiToggle op2Toggle;
        UiSlider alphaSlider;

        UiCard layerCard;
        UiButton upBtn, downBtn, bottomBtn;
        UiButton monBtn;

        UiCard snapCard;
        SnapGrid snapGrid;
        UiButton unsnapBtn;

        UiCard fxCard;
        UiToggle blurToggle;
        UiButton cancelAllBtn;

        public WindowPage(AppForm app)
            : base(app, "window", "窗口管理")
        {
        }

        void AddRight(Control c)
        {
            right.Controls.Add(c);
        }

        public override void Build()
        {
            right = new Panel();
            right.BackColor = Theme.Shell;
            right.AutoScroll = true;
            UiUtil.DoubleBuffer(right);
            Controls.Add(right);

            // ---------------- 左列 ----------------
            search = new UiSearch();
            search.TextChangedByUser += (s, e) => RefillList();
            Controls.Add(search);

            pickBtn = new UiButton { Label = "点选窗口", Icon = Glyph.Crosshair, Kind = UiButtonKind.Primary };
            pickBtn.Click += (s, e) => App.BeginPick();
            Controls.Add(pickBtn);

            refreshBtn = new UiButton { Label = "刷新", Icon = Glyph.Play, Kind = UiButtonKind.Normal };
            refreshBtn.Click += (s, e) => App.RefreshWindowsSync();
            Controls.Add(refreshBtn);

            list = new WinList { App = App };
            list.SelectedChanged += (s, e) => App.SetTarget(list.Selected);
            Controls.Add(list);

            // ---------------- 右列：目标信息 ----------------
            targetCard = new UiCard { Title = "当前目标" };
            targetCard.Controls.Add(targetTitle = new LabelEx
            {
                Font = Theme.FontBodyBold,
                TextColor = Theme.Text
            });
            targetCard.Controls.Add(targetProc = new LabelEx
            {
                Font = Theme.FontSmall,
                TextColor = Theme.TextDim
            });
            AddRight(targetCard);

            // ---------------- 置顶 ----------------
            pinCard = new UiCard { Title = "置顶与外观" };
            topToggle = new UiToggle { Label = "始终置顶", Hint = "被其他程序压下去会自动顶回来（守护）" };
            topToggle.Toggled += (s, e) =>
            {
                if (syncing) return;
                App.SetTop(App.Target, topToggle.Checked);
                SyncFromTarget();
            };
            pinCard.Controls.Add(topToggle);

            op2Toggle = new UiToggle { Label = "点击穿透（置顶）", Hint = "鼠标穿过窗口点到下层，透明度自动 245" };
            op2Toggle.Toggled += (s, e) =>
            {
                if (syncing) return;
                App.ToggleOp2(App.Target);
                SyncFromTarget();
            };
            pinCard.Controls.Add(op2Toggle);

            alphaSlider = new UiSlider { Label = "窗口透明度", Min = 40, Max = 255 };
            alphaSlider.Value = 255;
            alphaSlider.Changed += (s, e) => { if (!syncing) App.SetAlpha(App.Target, alphaSlider.Value); };
            alphaSlider.Committed += (s, e) => { };
            pinCard.Controls.Add(alphaSlider);
            AddRight(pinCard);

            // ---------------- 层级与显示器 ----------------
            layerCard = new UiCard { Title = "层级与显示器" };

            upBtn = new UiButton { Label = "上移一层", Icon = Glyph.LayerUp, Kind = UiButtonKind.Normal, Small = true };
            upBtn.Click += (s, e) => { App.LayerDir(App.Target, true); SyncFromTarget(); };
            layerCard.Controls.Add(upBtn);

            downBtn = new UiButton { Label = "下移一层", Icon = Glyph.LayerDown, Kind = UiButtonKind.Normal, Small = true };
            downBtn.Click += (s, e) => { App.LayerDir(App.Target, false); SyncFromTarget(); };
            layerCard.Controls.Add(downBtn);

            bottomBtn = new UiButton { Label = "移到最底", Icon = Glyph.LayerBottom, Kind = UiButtonKind.Normal, Small = true };
            bottomBtn.Click += (s, e) => { App.LayerBottom(App.Target); SyncFromTarget(); };
            layerCard.Controls.Add(bottomBtn);

            monBtn = new UiButton { Label = "下一显示器", Icon = Glyph.MonitorNext, Kind = UiButtonKind.Normal, Small = true };
            monBtn.Click += (s, e) => App.MoveToNextMonitor(App.Target);
            layerCard.Controls.Add(monBtn);
            AddRight(layerCard);

            // ---------------- 分屏吸附 ----------------
            snapCard = new UiCard { Title = "分屏吸附（Ctrl+Alt+Shift + 方向/QWER/M）" };
            snapGrid = new SnapGrid();
            snapGrid.SnapPicked += (kind) => { App.SnapTo(App.Target, kind); };
            snapCard.Controls.Add(snapGrid);

            unsnapBtn = new UiButton { Label = "还原吸附（B）", Icon = Glyph.Close, Kind = UiButtonKind.Normal, Small = true };
            unsnapBtn.Click += (s, e) => App.UnsnapTo(App.Target);
            snapCard.Controls.Add(unsnapBtn);
            AddRight(snapCard);

            // ---------------- 效果 ----------------
            fxCard = new UiCard { Title = "效果" };
            blurToggle = new UiToggle { Label = "毛玻璃", Hint = "亚克力/模糊，Win11 效果最好" };
            blurToggle.Toggled += (s, e) => { if (syncing) return; App.ToggleBlur(App.Target); SyncFromTarget(); };
            fxCard.Controls.Add(blurToggle);

            cancelAllBtn = new UiButton { Label = "还原全部穿透置顶", Icon = Glyph.PinOff, Kind = UiButtonKind.Danger, Small = true };
            cancelAllBtn.Click += (s, e) => { App.CancelAllOp2(); SyncFromTarget(); };
            fxCard.Controls.Add(cancelAllBtn);
            AddRight(fxCard);

            App.TargetChanged += (s, e) => SyncFromTarget();

            RefillList();
            SyncFromTarget();
        }

        public override void OnShow()
        {
            RefillList();
            SyncFromTarget();
        }

        bool syncing;

        void RefillList()
        {
            string q = search.Text2;
            var items = new List<WinEntry>();
            lock (App.EnumLock)
            {
                foreach (var w in App.WindowList)
                {
                    if (q.Length == 0 ||
                        (w.Title != null && w.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (w.Proc != null && w.Proc.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (w.Cls != null && w.Cls.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                        items.Add(w);
                }
            }
            list.Items.Clear();
            list.Items.AddRange(items.ToArray());
            if (App.Target != IntPtr.Zero) list.Select(App.Target);
            else if (items.Count > 0) list.Select(items[0].Hwnd);
            list.Invalidate();
        }

        void SyncFromTarget()
        {
            syncing = true;
            try { SyncFromTargetCore(); }
            finally { syncing = false; }
        }

        void SyncFromTargetCore()
        {
            IntPtr t = App.Target;
            bool ok = App.Ok(t);
            var e = ok ? App.Entry(t) : null;

            targetTitle.Text2 = ok ? Theme.Ellipsis(ManagerTitle(t), Theme.FontBodyBold, Width - Theme.S(60)) : "（未选择窗口）";
            targetProc.Text2 = ok ? (e != null ? (e.Proc + "  ·  " + e.Cls) : "") : "在左侧列表选择，或点「点选窗口」";

            topToggle.Checked = ok && Manager.IsTopmost(t);
            op2Toggle.Checked = ok && Manager.HasOp2(t);
            alphaSlider.Value = Manager.GetLastAlpha(t);
            blurToggle.Checked = ok && Manager.IsBlurred(t);

            bool can = ok;
            topToggle.Enabled = can;
            op2Toggle.Enabled = can;
            alphaSlider.Enabled = can;
            blurToggle.Enabled = can;
            upBtn.Enabled = can;
            downBtn.Enabled = can;
            bottomBtn.Enabled = can;
            monBtn.Enabled = can;
            unsnapBtn.Enabled = can && Manager.HasSnapBackup(t);

            targetTitle.Invalidate();
            targetProc.Invalidate();
        }

        static string ManagerTitle(IntPtr h) { return AppForm.TitleOf(h); }

        public override void Tick()
        {
            // 状态徽标与按钮可用性跟随真实窗口状态
            if (!Visible) return;
            SyncFromTarget();
            list.Invalidate();
        }

        public override void Relayout(int w, int h)
        {
            if (search == null) return;
            int pad = 0;
            int leftW = Math.Max(Theme.S(240), w * 38 / 100);
            int rightX = leftW + Theme.S(16);
            int rightW = w - rightX;

            // 左列
            search.SetBounds(pad, pad, leftW, Theme.S(30));
            pickBtn.SetBounds(pad, Theme.S(42), (leftW - Theme.S(8)) / 2, Theme.S(30));
            refreshBtn.SetBounds(pad + (leftW - Theme.S(8)) / 2 + Theme.S(8), Theme.S(42), (leftW - Theme.S(8)) / 2, Theme.S(30));
            list.SetBounds(pad, Theme.S(80), leftW, h - Theme.S(80));

            // 右列垂直排布（滚动容器内部，Y 从 0 起）
            right.SetBounds(rightX, 0, rightW, h);
            int y = 0;
            int cw = rightW - Theme.S(6);

            targetCard.SetBounds(0, y, cw, Theme.S(64));
            targetTitle.SetBounds(Theme.S(14), Theme.S(8), cw - Theme.S(28), Theme.S(22));
            targetProc.SetBounds(Theme.S(14), Theme.S(32), cw - Theme.S(28), Theme.S(18));
            y += Theme.S(64) + Theme.S(12);

            pinCard.SetBounds(0, y, cw, Theme.S(150));
            topToggle.SetBounds(Theme.S(12), pinCard.HeaderH + Theme.S(2), cw - Theme.S(24), Theme.S(34));
            op2Toggle.SetBounds(Theme.S(12), pinCard.HeaderH + Theme.S(40), cw - Theme.S(24), Theme.S(34));
            alphaSlider.SetBounds(Theme.S(12), pinCard.HeaderH + Theme.S(80), cw - Theme.S(24), Theme.S(38));
            y += Theme.S(150) + Theme.S(12);

            layerCard.SetBounds(0, y, cw, Theme.S(92));
            int bw = (cw - Theme.S(12) - Theme.S(24)) / 4;
            upBtn.SetBounds(Theme.S(12), layerCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            downBtn.SetBounds(Theme.S(12) + bw + Theme.S(4), layerCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            bottomBtn.SetBounds(Theme.S(12) + (bw + Theme.S(4)) * 2, layerCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            monBtn.SetBounds(Theme.S(12) + (bw + Theme.S(4)) * 3, layerCard.HeaderH + Theme.S(4), bw, Theme.S(28));
            y += Theme.S(92) + Theme.S(12);

            snapCard.SetBounds(0, y, cw, Theme.S(150));
            int grid = Theme.S(84);
            snapGrid.SetBounds(Theme.S(12), snapCard.HeaderH + Theme.S(6), grid, grid);
            unsnapBtn.SetBounds(Theme.S(12) + grid + Theme.S(12), snapCard.HeaderH + Theme.S(6) + Theme.S(60), Theme.S(140), Theme.S(26));
            y += Theme.S(150) + Theme.S(12);

            fxCard.SetBounds(0, y, cw, Theme.S(110));
            blurToggle.SetBounds(Theme.S(12), fxCard.HeaderH + Theme.S(2), cw - Theme.S(24), Theme.S(34));
            cancelAllBtn.SetBounds(Theme.S(12), fxCard.HeaderH + Theme.S(44), Theme.S(180), Theme.S(28));
        }
    }

    /// <summary>轻量自绘文本行。</summary>
    class LabelEx : Control
    {
        public string Text2 = "";
        public Color TextColor = Theme.Text;
        public bool Multiline;

        public LabelEx()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Text2.Length == 0) return;
            var c = TextColor;
            if (c == Color.Empty) c = Enabled ? Theme.Text : Theme.TextFaint;
            if (Multiline)
            {
                TextRenderer.DrawText(e.Graphics, Text2, Font, new Rectangle(0, 0, Width, Height), c,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                    TextFormatFlags.NoPrefix);
            }
            else
            {
                UiUtil.Text(e.Graphics, Text2, Font, new RectangleF(0, 0, Width, Height), c);
            }
        }
    }

    /// <summary>3×3 分屏吸附选择器：每格画出该格将占据的区域。</summary>
    class SnapGrid : Control
    {
        public event Action<SnapKind> SnapPicked;

        int hover = -1;

        public SnapGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = Theme.Sz(84, 84);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = CellAt(e.X, e.Y);
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = CellAt(e.X, e.Y);
            if (i >= 0)
            {
                var kinds = new[]
                {
                    SnapKind.TopLeft, SnapKind.Top, SnapKind.TopRight,
                    SnapKind.Left, SnapKind.Max, SnapKind.Right,
                    SnapKind.BottomLeft, SnapKind.Bottom, SnapKind.BottomRight
                };
                var h = SnapPicked;
                if (h != null) h(kinds[i]);
            }
            base.OnMouseDown(e);
        }

        int CellAt(int x, int y)
        {
            if (Width <= 0 || Height <= 0) return -1;
            int c = Math.Min(2, x * 3 / Width);
            int r = Math.Min(2, y * 3 / Height);
            return r * 3 + c;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = ClientRectangle; r.Width--; r.Height--;
            Theme.FillRound(g, r, Theme.InputBg, Theme.S(8));
            using (var pen = new Pen(Theme.Border)) Theme.RoundRect(g, r, Theme.S(8));

            int cw = Width / 3, ch = Height / 3;
            for (int i = 0; i < 9; i++)
            {
                int cx = (i % 3) * cw, cy = (i / 3) * ch;
                if (i == hover)
                {
                    var cell = new Rectangle(cx + Theme.S(2), cy + Theme.S(2), cw - Theme.S(4), ch - Theme.S(4));
                    Theme.FillRound(g, cell, Theme.AccentSoft, Theme.S(4));
                }
                // 画小方块示意（中格是铺满，四角是角块……）
                var q = Quad(i);
                var inner = new Rectangle(cx + q.X * cw / 3 + Theme.S(4), cy + q.Y * ch / 3 + Theme.S(4),
                    q.W * cw / 3 - Theme.S(8), q.H * ch / 3 - Theme.S(8));
                using (var b = new SolidBrush(i == hover ? Theme.Accent : Theme.TextDim))
                    Theme.FillRound(g, inner, i == hover ? Theme.Accent : Theme.TextDim, Theme.S(2));
            }
        }

        struct Q { public int X, Y, W, H; }

        static Q Quad(int i)
        {
            switch (i)
            {
                case 0: return new Q { X = 0, Y = 0, W = 2, H = 2 };
                case 1: return new Q { X = 0, Y = 0, W = 3, H = 2 };
                case 2: return new Q { X = 1, Y = 0, W = 2, H = 2 };
                case 3: return new Q { X = 0, Y = 0, W = 2, H = 3 };
                case 4: return new Q { X = 0, Y = 0, W = 3, H = 3 };
                case 5: return new Q { X = 1, Y = 0, W = 2, H = 3 };
                case 6: return new Q { X = 0, Y = 1, W = 2, H = 2 };
                case 7: return new Q { X = 0, Y = 1, W = 3, H = 2 };
                default: return new Q { X = 1, Y = 1, W = 2, H = 2 };
            }
        }
    }
}
