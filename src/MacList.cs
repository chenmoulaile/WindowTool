using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    sealed class MacListItem
    {
        public string Title = "";
        public string Subtitle = "";
        public string Detail = "";
        public Glyph Icon;
        public bool ShowIcon;
        public Image Image;
        public object Tag;
        public bool Enabled = true;
        public Color DetailColor = Color.Empty;

        public MacListItem() { }
        public MacListItem(string title) { Title = title ?? ""; }
    }

    /// 自绘列表：macOS 表格风格 —— 圆角选中条、可选图标、右侧次要文本、内嵌滚动条。
    sealed class MacList : Control, IThemed
    {
        readonly List<MacListItem> items = new List<MacListItem>();
        int sel = -1, hover = -1, top;
        int rowH;
        bool draggingBar;
        float dragGrab;

        public event EventHandler SelectedIndexChanged;
        public event EventHandler ItemActivate;
        public event MouseEventHandler ItemRightClick;

        public string EmptyText = "（空）";

        public MacList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.FontUi;
            DoubleBuffered = true;
            rowH = Theme.S(46);
        }

        public void ApplyTheme()
        {
            Font = Theme.FontUi;
            rowH = Theme.S(46);
            ClampTop();
            Invalidate();
        }

        public List<MacListItem> Items { get { return items; } }

        public void SetItems(IEnumerable<MacListItem> list)
        {
            items.Clear();
            if (list != null) items.AddRange(list);
            if (sel >= items.Count) sel = items.Count - 1;
            ClampTop();
            Invalidate();
        }

        public void ClearSelection() { sel = -1; Invalidate(); }

        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                int v = value < -1 || value >= items.Count ? sel : value;
                if (v == sel) return;
                sel = v;
                EnsureVisible(v);
                Invalidate();
                RaiseSelected();
            }
        }

        public MacListItem SelectedItem { get { return sel >= 0 && sel < items.Count ? items[sel] : null; } }

        void RaiseSelected() { var h = SelectedIndexChanged; if (h != null) h(this, EventArgs.Empty); }

        void RaiseActivate() { var h = ItemActivate; if (h != null) h(this, EventArgs.Empty); }

        int VisibleRows { get { return Math.Max(1, (Height - Theme.S(2)) / rowH); } }
        bool NeedsScroll { get { return items.Count > VisibleRows; } }
        int BarW { get { return NeedsScroll ? Theme.S(8) : 0; } }

        void ClampTop()
        {
            int maxTop = Math.Max(0, items.Count - VisibleRows);
            if (top > maxTop) top = maxTop;
            if (top < 0) top = 0;
        }

        void EnsureVisible(int i)
        {
            if (i < 0) return;
            ClampTop();
            if (i < top) top = i;
            else if (i >= top + VisibleRows) top = i - VisibleRows + 1;
            ClampTop();
        }

        int Hit(Point p)
        {
            if (p.Y < Theme.S(1)) return -1;
            int i = top + (p.Y - Theme.S(1)) / rowH;
            if (i < 0 || i >= items.Count) return -1;
            if (p.X > Width - BarW - Theme.S(2)) return -1;
            return i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (draggingBar && NeedsScroll)
            {
                int trackH = Height - Theme.S(4);
                float f = trackH <= 0 ? 0f : (e.Y - dragGrab - Theme.S(2)) / Math.Max(1, trackH - BarHeight());
                top = Math.Max(0, Math.Min(items.Count - VisibleRows, (int)Math.Round(f * (items.Count - VisibleRows))));
                ClampTop();
                Invalidate();
                return;
            }
            int h = Hit(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { if (hover != -1) { hover = -1; Invalidate(); } base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left && NeedsScroll && e.X > Width - BarW - Theme.S(4))
            {
                int barY = BarY();
                if (e.Y >= barY && e.Y <= barY + BarHeight())
                {
                    draggingBar = true;
                    dragGrab = e.Y - barY;
                    Capture = true;
                    return;
                }
                int maxTop = Math.Max(0, items.Count - VisibleRows);
                float f = (e.Y - Theme.S(2)) / Math.Max(1, Height - Theme.S(4));
                top = Math.Max(0, Math.Min(maxTop, (int)(f * maxTop) - VisibleRows / 2));
                ClampTop();
                Invalidate();
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                int i = Hit(e.Location);
                if (i >= 0 && items[i].Enabled) SelectedIndex = i;
            }
            else if (e.Button == MouseButtons.Right)
            {
                int i = Hit(e.Location);
                if (i >= 0) SelectedIndex = i;
                var h = ItemRightClick;
                if (h != null) h(this, e);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (draggingBar) { draggingBar = false; Capture = false; }
            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (Hit(e.Location) >= 0) RaiseActivate();
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int step = Math.Max(1, VisibleRows / 6);
            top += e.Delta < 0 ? step : -step;
            ClampTop();
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Up || k == Keys.Down || k == Keys.PageUp || k == Keys.PageDown
                || k == Keys.Home || k == Keys.End || k == Keys.Return || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (items.Count == 0) return;
            if (e.KeyCode == Keys.Down) SelectedIndex = Math.Min(items.Count - 1, sel + 1);
            else if (e.KeyCode == Keys.Up) SelectedIndex = Math.Max(0, sel < 0 ? 0 : sel - 1);
            else if (e.KeyCode == Keys.PageDown) SelectedIndex = Math.Min(items.Count - 1, sel + VisibleRows);
            else if (e.KeyCode == Keys.PageUp) SelectedIndex = Math.Max(0, sel - VisibleRows);
            else if (e.KeyCode == Keys.Home) SelectedIndex = 0;
            else if (e.KeyCode == Keys.End) SelectedIndex = items.Count - 1;
            else if (e.KeyCode == Keys.Return) RaiseActivate();
            base.OnKeyDown(e);
        }

        protected override void OnSizeChanged(EventArgs e) { ClampTop(); base.OnSizeChanged(e); }

        int BarHeight()
        {
            float r = VisibleRows / (float)Math.Max(1, items.Count);
            return Math.Max(Theme.S(28), (int)((Height - Theme.S(4)) * Math.Min(1f, r)));
        }

        int BarY()
        {
            int maxTop = Math.Max(1, items.Count - VisibleRows);
            float f = top / (float)maxTop;
            return Theme.S(2) + (int)(f * Math.Max(0, (Height - Theme.S(4)) - BarHeight()));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));

            if (items.Count == 0)
            {
                int ew = Math.Max(0, Width - BarW - Theme.S(16));
                MacUi.CenterText(g, Theme.Ellipsis(EmptyText, Theme.FontUi, ew), Theme.FontUi,
                    new RectangleF(0, 0, Width - BarW, Height), Theme.TextFaint);
                return;
            }

            int inset = Theme.S(4);
            int contentW = Width - BarW - inset * 2;
            for (int i = top; i < items.Count; i++)
            {
                int y = Theme.S(1) + (i - top) * rowH;
                if (y > Height) break;
                var it = items[i];
                var rowRect = new Rectangle(inset, y + Theme.S(1), contentW, rowH - Theme.S(2));

                bool isSel = i == sel;
                if (isSel)
                {
                    using (var p = Theme.RoundRect(rowRect, Theme.S(6)))
                        Theme.FillPath(g, p, Theme.Accent);
                }
                else if (i == hover && it.Enabled)
                {
                    using (var p = Theme.RoundRect(rowRect, Theme.S(6)))
                        Theme.FillPath(g, p, Theme.Hover);
                }

                Color tc = isSel ? Theme.OnAccent : (it.Enabled ? Theme.Text : Theme.TextFaint);
                Color sc = isSel ? Theme.Alpha(Color.White, 200) : Theme.TextDim;

                float tx = rowRect.X + Theme.S(10);
                int pad = Theme.S(10);
                if (it.Image != null)
                {
                    int isz = Theme.S(20);
                    g.DrawImage(it.Image, new Rectangle((int)tx, rowRect.Y + (rowRect.Height - isz) / 2, isz, isz));
                    tx += isz + Theme.S(8);
                }
                else if (it.ShowIcon)
                {
                    int isz = Theme.S(16);
                    Icons.Draw(g, it.Icon, new RectangleF(tx, rowRect.Y + (rowRect.Height - isz) / 2f, isz, isz), tc);
                    tx += isz + Theme.S(8);
                }

                string detail = it.Detail ?? "";
                int dw = detail.Length > 0 ? TextRenderer.MeasureText(g, detail, Theme.FontSmall, Size.Empty,
                    TextFormatFlags.NoPadding).Width + Theme.S(12) : 0;
                float textW = Math.Max(Theme.S(20), rowRect.Right - tx - pad + Theme.S(10) - dw);

                bool two = !string.IsNullOrEmpty(it.Subtitle);
                if (two)
                {
                    MacUi.LeftText(g, Theme.Ellipsis(it.Title, Font, (int)textW), Font,
                        new RectangleF(tx, rowRect.Y + Theme.S(5), textW, Theme.S(22)), tc);
                    MacUi.LeftText(g, Theme.Ellipsis(it.Subtitle, Theme.FontSmall, (int)textW), Theme.FontSmall,
                        new RectangleF(tx, rowRect.Y + Theme.S(26), textW, Theme.S(19)), sc);
                }
                else
                {
                    MacUi.LeftText(g, Theme.Ellipsis(it.Title, Font, (int)textW), Font,
                        new RectangleF(tx, rowRect.Y, textW, rowRect.Height), tc);
                }

                if (detail.Length > 0)
                {
                    Color dc = isSel ? Theme.Alpha(Color.White, 200)
                        : (it.DetailColor.IsEmpty ? Theme.TextDim : it.DetailColor);
                    var dsz = TextRenderer.MeasureText(g, detail, Theme.FontSmall, Size.Empty, TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, detail, Theme.FontSmall,
                        new Point(rowRect.Right - pad - dsz.Width, rowRect.Y + (rowRect.Height - dsz.Height) / 2),
                        dc, TextFormatFlags.NoPadding);
                }
            }

            if (NeedsScroll)
            {
                int by = BarY(), bh = BarHeight();
                using (var p = Theme.RoundRect(new Rectangle(Width - BarW - Theme.S(1), by, BarW, bh), BarW))
                    Theme.FillPath(g, p, Theme.Alpha(Theme.Text, draggingBar ? 120 : 60));
            }
        }
    }
}
