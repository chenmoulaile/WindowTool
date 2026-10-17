using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    static class MacUi
    {
        public static Label Label(string text, Font font, Color color)
        {
            var l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = false;
            l.AutoEllipsis = false;
            return l;
        }

        public static Label SectionTitle(string text)
        {
            var l = Label(text, Theme.FontSection, Theme.Text);
            l.Tag = "section";
            return l;
        }

        public static Label Hint(string text)
        {
            var l = Label(text, Theme.FontSmall, Theme.TextDim);
            l.Tag = "dim";
            return l;
        }

        const TextFormatFlags Base = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

        public static void CenterText(Graphics g, string s, Font f, RectangleF r, Color c)
        {
            TextRenderer.DrawText(g, s, f, Rectangle.Round(r), c, Base | TextFormatFlags.HorizontalCenter);
        }

        public static void LeftText(Graphics g, string s, Font f, RectangleF r, Color c)
        {
            TextRenderer.DrawText(g, s, f, Rectangle.Round(r), c, Base | TextFormatFlags.Left);
        }
    }

    sealed class MacButton : Button, IThemed
    {
        bool hover, down;
        public MacTone Tone = MacTone.Normal;
        public Glyph Icon = Glyph.Pin;
        public bool ShowIcon;
        public int IconSize;

        public MacButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = Theme.FontUi;
            Height = Theme.S(28);
        }

        public void ApplyTheme() { Font = Theme.FontUi; Height = Theme.S(28); Invalidate(); }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        void Palette(out Color fill, out Color text, out Color border)
        {
            fill = Theme.Card; text = Theme.Text; border = Theme.Border;
            switch (Tone)
            {
                case MacTone.Accent: fill = Theme.Accent; text = Theme.OnAccent; border = fill; break;
                case MacTone.Tinted: fill = Theme.AccentSoft; text = Theme.Accent; border = fill; break;
                case MacTone.Danger: fill = Theme.Red; text = Color.White; border = fill; break;
                case MacTone.Ghost: fill = Color.Transparent; text = Theme.Accent; border = Color.Transparent; break;
                case MacTone.Sunken: fill = Theme.CardSunken; text = Theme.Text; border = Color.Transparent; break;
            }
            if (hover && Enabled)
            {
                if (Tone == MacTone.Ghost) fill = Theme.Alpha(Theme.Accent, Theme.IsDark ? 34 : 20);
                else fill = Theme.PressedOf(fill);
                if (Tone == MacTone.Normal) border = Theme.Blend(Theme.Border, Theme.Text, 0.25f);
            }
            if (down && Enabled)
            {
                if (Tone == MacTone.Ghost) fill = Theme.Alpha(Theme.Accent, Theme.IsDark ? 56 : 38);
                else fill = Theme.PressedOf(fill);
            }
            if (!Enabled)
            {
                if (Tone == MacTone.Ghost || Tone == MacTone.Tinted) { fill = Color.Transparent; text = Theme.Alpha(Theme.TextFaint, 140); }
                else { fill = Theme.Alpha(fill, 90); text = Theme.TextFaint; border = Theme.Alpha(border, 90); }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));

            Color fill, text, border;
            Palette(out fill, out text, out border);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Tone == MacTone.Normal || Tone == MacTone.Sunken) r.Inflate(-1, -1);
            int rad = Theme.CornerRadius(r.Width, r.Height);
            using (var path = Theme.RoundRect(r, rad))
            {
                if (fill.A > 0) Theme.FillPath(g, path, fill);
                if (border.A > 0 && Tone != MacTone.Ghost) Theme.StrokePath(g, path, border, Math.Max(1f, Theme.S(1)));
            }

            string s = Text ?? "";
            int pad = Theme.S(12);
            float tx = r.X + pad;
            float avail = r.Width - pad * 2;
            if (ShowIcon)
            {
                int isz = IconSize > 0 ? IconSize : Theme.S(14);
                var ib = new RectangleF(tx, r.Y + (r.Height - isz) / 2f, isz, isz);
                Icons.Draw(g, Icon, ib, text);
                tx += isz + Theme.S(6);
                avail -= isz + Theme.S(6);
            }
            var tr = new RectangleF(tx, r.Y, Math.Max(0, avail), r.Height);
            MacUi.CenterText(g, Theme.Ellipsis(s, Font, (int)avail), Font, tr, text);
        }
    }

    enum MacTone { Normal, Accent, Tinted, Danger, Ghost, Sunken }

    sealed class MacSwitch : Control, IThemed
    {
        bool hover;
        float anim;
        Timer animT;
        public event EventHandler CheckedChanged;

        public MacSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Width = Theme.S(38);
            Height = Theme.S(22);
            animT = new Timer { Interval = 15 };
            animT.Tick += (s, e) =>
            {
                float target = on ? 1f : 0f;
                anim += (target - anim) * 0.35f;
                if (Math.Abs(target - anim) < 0.02f) { anim = target; animT.Stop(); }
                Invalidate();
            };
        }

        bool on;
        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                animT.Start();
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// 同步显示状态但不触发 CheckedChanged（供 Tick 回读真实窗口状态）
        public void SetCheckedSilent(bool v)
        {
            if (on == v) return;
            on = v;
            anim = v ? 1f : 0f;
            Invalidate();
        }

        public void ApplyTheme()
        {
            Width = Theme.S(38);
            Height = Theme.S(22);
            anim = on ? 1f : 0f;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); base.OnMouseDown(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Space || k == Keys.Enter || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Toggle(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Toggle() { Checked = !on; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            float t = anim;
            Color offTrack = Theme.IsDark
                ? (hover ? Color.FromArgb(88, 88, 94) : Color.FromArgb(72, 72, 78))
                : (hover ? Color.FromArgb(210, 210, 216) : Color.FromArgb(226, 226, 232));
            using (var p = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), Height))
                Theme.FillPath(g, p, Theme.Blend(offTrack, Theme.Green, t));

            float k = Height - Theme.S(4);
            float travel = Width - k - Theme.S(4);
            float kx = Theme.S(2) + travel * t;
            var rect = new RectangleF(kx, Theme.S(2), k, k);
            using (var sh = new SolidBrush(Color.FromArgb(46, 0, 0, 0)))
                g.FillEllipse(sh, rect.X, rect.Y + Theme.S(1.2f), rect.Width, rect.Height);
            using (var b = new SolidBrush(Theme.Knob))
                g.FillEllipse(b, rect);
        }
    }

    sealed class MacSegmented : Control, IThemed
    {
        readonly List<string> items = new List<string>();
        int sel = -1;
        int hoverIdx = -1;

        public event EventHandler SelectedIndexChanged;

        public MacSegmented()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = Theme.S(26);
            Font = Theme.FontUi;
        }

        public string[] Items { get { return items.ToArray(); } }

        public void SetItems(string[] labels)
        {
            items.Clear();
            if (labels != null) items.AddRange(labels);
            if (sel >= items.Count) sel = items.Count - 1;
            Invalidate();
        }

        public string SelectedText { get { return sel >= 0 && sel < items.Count ? items[sel] : ""; } }

        /// 按最长标签给每格定宽，避免中文被省略号截断。
        public void SizeToCells()
        {
            SizeToCells(0);
        }

        public void SizeToCells(int minCellLogical)
        {
            int max = 0;
            foreach (var t in items)
            {
                int w = TextRenderer.MeasureText(t, Font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                if (w > max) max = w;
            }
            int cell = Math.Max(max + Theme.S(20), Theme.S(minCellLogical));
            Width = cell * Math.Max(1, items.Count) + Theme.S(6);
            Invalidate();
        }

        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                int v = value < -1 || value >= items.Count ? sel : value;
                if (v == sel) return;
                sel = v;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public void SetIndexSilent(int v)
        {
            if (v < -1 || v >= items.Count || v == sel) return;
            sel = v;
            Invalidate();
        }

        public void ApplyTheme() { Font = Theme.FontUi; Height = Theme.S(26); Invalidate(); }

        RectangleF Cell(int i)
        {
            int n = Math.Max(1, items.Count);
            float pad = Theme.S(2);
            float w = (Width - pad * 2) / (float)n;
            return new RectangleF(pad + i * w, pad, w, Height - pad * 2);
        }

        int Hit(Point p)
        {
            for (int i = 0; i < items.Count; i++) if (Cell(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h != hoverIdx) { hoverIdx = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int h = Hit(e.Location);
                if (h >= 0) SelectedIndex = h;
            }
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (items.Count == 0) return;
            if (e.KeyCode == Keys.Left && sel > 0) SelectedIndex = sel - 1;
            else if (e.KeyCode == Keys.Right && sel < items.Count - 1) SelectedIndex = sel + 1;
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Theme.RoundRect(outer, Theme.CornerRadius(outer.Width, outer.Height)))
            {
                Theme.FillPath(g, p, Theme.CardSunken);
                Theme.StrokePath(g, p, Theme.Border, Math.Max(1f, Theme.S(1)));
            }
            if (items.Count == 0) return;

            for (int i = 0; i < items.Count; i++)
            {
                var c = Cell(i);
                bool isSel = i == sel;
                if (isSel)
                {
                    var sr = new RectangleF(c.X - Theme.S(1), c.Y - Theme.S(1), c.Width + Theme.S(2), c.Height + Theme.S(2));
                    using (var p = Theme.RoundRect(Rectangle.Round(sr), Theme.S(6)))
                    {
                        using (var sh = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                            g.FillPath(sh, Theme.RoundRect(new Rectangle((int)sr.X, (int)sr.Y + Theme.S(1), (int)sr.Width, (int)sr.Height), Theme.S(6)));
                        Theme.FillPath(g, p, Theme.Card);
                        Theme.StrokePath(g, p, Theme.Border, Math.Max(1f, Theme.S(1)));
                    }
                }
                else if (i == hoverIdx)
                {
                    using (var p = Theme.RoundRect(Rectangle.Round(c), Theme.S(5)))
                        Theme.FillPath(g, p, Theme.Hover);
                }

                Color tc = isSel ? Theme.Text : Theme.TextDim;
                string label = Theme.Ellipsis(items[i], Font, (int)c.Width - Theme.S(10));
                MacUi.CenterText(g, label, Font, new RectangleF(c.X, c.Y - Theme.S(1), c.Width, c.Height), tc);
            }
        }
    }

    sealed class MacSlider : Control, IThemed
    {
        int min, max = 100, val;
        bool drag, hover;
        public event EventHandler ValueChanged;
        public event EventHandler DragFinished;
        public string Suffix = "";
        public bool ShowValue = true;

        public MacSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = Theme.S(24);
            Font = Theme.FontSmall;
            min = 0; val = 0;
        }

        public int Minimum { get { return min; } set { min = value; if (val < min) val = min; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; if (val > max) val = max; Invalidate(); } }
        /// 供宿主暂存的浮点上下文（例如进度条对应的总秒数）
        public double Aux;
        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public void SetValueSilent(int v)
        {
            int nv = Math.Max(min, Math.Min(max, v));
            if (nv == val) return;
            val = nv;
            Invalidate();
        }

        public void ApplyTheme() { Font = Theme.FontSmall; Height = Theme.S(24); Invalidate(); }

        float TrackW { get { return Width - Theme.S(10) * 2 - (ShowValue ? Theme.S(38) : 0); } }
        float Frac { get { return max <= min ? 0f : (val - min) / (float)(max - min); } }
        float ThumbX { get { return Theme.S(10) + Frac * TrackW; } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { drag = true; Capture = true; From(e.X); }
            Focus();
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag) From(e.X);
            else if (!hover) { hover = true; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (drag) { drag = false; Capture = false; Invalidate(); var h = DragFinished; if (h != null) h(this, EventArgs.Empty); }
            base.OnMouseUp(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = Math.Max(1, (max - min) / 40);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value = val - step;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value = val + step;
            base.OnKeyDown(e);
        }

        void From(int px)
        {
            float w = TrackW;
            float f = w <= 0 ? 0f : (px - Theme.S(10)) / w;
            f = Math.Max(0f, Math.Min(1f, f));
            Value = min + (int)Math.Round(f * (max - min));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            float cy = Height / 2f;
            float th = Math.Max(Theme.S(4), Theme.S(4));
            var track = new Rectangle(Theme.S(10), (int)(cy - th / 2), (int)TrackW, (int)th);
            using (var p = Theme.RoundRect(track, (int)th)) Theme.FillPath(g, p, Theme.CardSunken);
            float fx = ThumbX;
            if (fx > track.Left + 1)
            {
                var filled = new Rectangle(track.Left, track.Y, (int)(fx - track.Left), track.Height);
                using (var p = Theme.RoundRect(filled, (int)th)) Theme.FillPath(g, p, Theme.Accent);
            }
            float k = Theme.S(drag || hover ? 18 : 16);
            using (var sh = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                g.FillEllipse(sh, fx - k / 2f, cy - k / 2f + Theme.S(1), k, k);
            using (var b = new SolidBrush(Theme.Knob))
                g.FillEllipse(b, fx - k / 2f, cy - k / 2f, k, k);
            using (var pen = new Pen(Theme.Alpha(Theme.Text, 26), Math.Max(1f, Theme.S(1))))
                g.DrawEllipse(pen, fx - k / 2f, cy - k / 2f, k, k);

            if (ShowValue)
            {
                string s = val + Suffix;
                var sz = TextRenderer.MeasureText(g, s, Font, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, s, Font,
                    new Point(Width - sz.Width - Theme.S(1), (int)(cy - sz.Height / 2f)), Theme.TextDim,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }
    }

    /// macOS「分组卡片」：可选标题绘制在圆角面板上方，内部承载行控件。
    sealed class MacCard : Panel, IThemed
    {
        string title = "";
        public string Title { get { return title; } set { title = value ?? ""; Invalidate(); } }

        public MacCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            // BackColor 供子控件解析圆角底色；自身绘制时四角用父背景擦除
            BackColor = Theme.Card;
            Padding = new Padding(Theme.S(14), Theme.S(12), Theme.S(14), Theme.S(12));
            Font = Theme.FontUi;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Card;
            Padding = new Padding(Theme.S(14), Theme.S(12), Theme.S(14), Theme.S(12));
            Invalidate();
            foreach (Control c in Controls)
            {
                var t = c as IThemed;
                if (t != null) t.ApplyTheme();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            int top = string.IsNullOrEmpty(title) ? 0 : Theme.S(24);
            var r = new Rectangle(0, top, Width - 1, Height - top - 1);
            int rad = Theme.CornerRadius(r.Width, r.Height);
            using (var p = Theme.RoundRect(r, rad))
            {
                Theme.FillPath(g, p, Theme.Card);
                Theme.StrokePath(g, p, Theme.Border, Math.Max(1f, Theme.S(1)));
            }
            if (!string.IsNullOrEmpty(title))
                MacUi.LeftText(g, title, Theme.FontSection, new RectangleF(Theme.S(2), 0, Width - Theme.S(4), top), Theme.Text);
        }
    }

    /// 卡片内的一行：左侧标题/副标题，右侧按加入顺序从右往左排列控件，底部可选分隔线。
    sealed class MacRow : Panel, IThemed
    {
        public string Title = "";
        public string Subtitle = "";
        public bool Separator = true;
        readonly List<Control> trail = new List<Control>();

        public MacRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.S(44);
        }

        public int RowHeight { get { return string.IsNullOrEmpty(Subtitle) ? Theme.S(44) : Theme.S(58); } }

        public MacRow Trailing(params Control[] cs)
        {
            foreach (var c in cs)
            {
                if (c == null) continue;
                trail.Add(c);
                Controls.Add(c);
            }
            PlaceTrail();
            return this;
        }

        public void ApplyTheme()
        {
            Height = RowHeight;
            PlaceTrail();
            Invalidate(true);
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); PlaceTrail(); }

        void PlaceTrail()
        {
            int x = Width - Theme.S(14);
            int gap = Theme.S(8);
            for (int i = 0; i < trail.Count; i++)
            {
                var c = trail[i];
                int w = c.Width;
                x -= w;
                c.SetBounds(x, (Height - c.Height) / 2, w, c.Height);
                x -= gap;
            }
        }

        int TextRight
        {
            get
            {
                int min = Width - Theme.S(14);
                foreach (var c in trail) min = Math.Min(min, c.Left - Theme.S(14));
                return Math.Max(Theme.S(60), min);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            bool two = !string.IsNullOrEmpty(Subtitle);
            float tw = Math.Max(Theme.S(40), TextRight - Theme.S(14));
            if (two)
            {
                MacUi.LeftText(g, Theme.Ellipsis(Title, Theme.FontUi, (int)tw), Theme.FontUi,
                    new RectangleF(Theme.S(14), Theme.S(6), tw, Theme.S(22)), Theme.Text);
                MacUi.LeftText(g, Theme.Ellipsis(Subtitle, Theme.FontSmall, (int)tw), Theme.FontSmall,
                    new RectangleF(Theme.S(14), Theme.S(31), tw, Theme.S(20)), Theme.TextDim);
            }
            else
            {
                MacUi.LeftText(g, Theme.Ellipsis(Title, Theme.FontUi, (int)tw), Theme.FontUi,
                    new RectangleF(Theme.S(14), 0, tw, Height), Theme.Text);
            }
            if (Separator)
                using (var pen = new Pen(Theme.Separator, Math.Max(1f, Theme.S(1))))
                    g.DrawLine(pen, Theme.S(14), Height - 1, Width - 1, Height - 1);
        }
    }

    /// 圆角输入框（可带占位符与左侧图标）。
    class MacField : Panel, IThemed
    {
        protected TextBox box;
        string placeholder = "";
        bool focus;
        public Glyph Icon;
        public bool ShowIcon;

        public MacField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.S(26);
            box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.Card;
            box.ForeColor = Theme.Text;
            box.Font = Theme.FontUi;
            Controls.Add(box);
            box.HandleCreated += (s, e) => SetCue();
            box.GotFocus += (s, e) => { focus = true; Invalidate(); };
            box.LostFocus += (s, e) => { focus = false; Invalidate(); };
            box.TextChanged += (s, e) => Invalidate();
        }

        public new string Text
        {
            get { return box.Text; }
            set { box.Text = value ?? ""; }
        }

        public string Placeholder
        {
            get { return placeholder; }
            set
            {
                placeholder = value ?? "";
                SetCue();
            }
        }

        void SetCue()
        {
            if (!box.IsHandleCreated) return;
            // wParam=TRUE 让提示文字在获得焦点后依然保留
            try { Native.SendMessage(box.Handle, Native.EM_SETCUEBANNER, (IntPtr)1, placeholder); }
            catch (Exception ex) { Program.Log(ex); }
        }

        public new event EventHandler TextChanged
        {
            add { box.TextChanged += value; }
            remove { box.TextChanged -= value; }
        }

        public void SelectAll() { box.SelectAll(); }

        public void ApplyTheme()
        {
            Height = Theme.S(26);
            box.BackColor = Theme.Card;
            box.ForeColor = Theme.Text;
            box.Font = Theme.FontUi;
            Layout();
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Layout(); }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            // 句柄尚未创建时不能 BeginInvoke，直接同步排版
            if (IsHandleCreated) BeginInvoke(new Action(Layout)); else Layout();
        }

        protected virtual int LeftPad { get { return ShowIcon ? Theme.S(30) : Theme.S(10); } }
        protected virtual int RightPad { get { return Theme.S(10); } }

        new void Layout()
        {
            if (box == null) return;
            int w = Math.Max(4, Width - LeftPad - RightPad);
            box.SetBounds(LeftPad, Math.Max(0, (Height - box.PreferredHeight) / 2), w, box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = Theme.CornerRadius(r.Width, r.Height);
            using (var p = Theme.RoundRect(r, rad))
            {
                Theme.FillPath(g, p, Theme.Card);
                Theme.StrokePath(g, p, focus ? Theme.Accent : Theme.Border,
                    Math.Max(1f, Theme.S(focus ? 2 : 1)));
            }
            if (ShowIcon)
                Icons.Draw(g, Icon, new RectangleF(Theme.S(9), Height / 2f - Theme.S(7), Theme.S(14), Theme.S(14)), Theme.TextFaint);

        }
    }

    sealed class MacNumber : MacField, IThemed
    {
        decimal min, max, step = 1m, val;
        bool hoverUp, hoverDn;

        public MacNumber()
        {
            Width = Theme.S(64);
            box.TextAlign = HorizontalAlignment.Left;
            val = 0m;
            box.TextChanged += (s, e) =>
            {
                decimal d;
                if (decimal.TryParse(box.Text, out d)) { val = Clamp(d); Raise(); }
            };
            MouseWheel += (s, e) => { Value = val + (e.Delta > 0 ? step : -step); };
        }

        public decimal Minimum { get { return min; } set { min = value; Value = val; } }
        public decimal Maximum { get { return max; } set { max = value; Value = val; } }
        public decimal Step { get { return step; } set { step = value <= 0 ? 1m : value; } }

        public decimal Value
        {
            get { return val; }
            set
            {
                decimal v = Clamp(value);
                v = Math.Round(v / step) * step;
                v = Clamp(v);
                if (v == val) return;
                val = v;
                Sync();
                Raise();
            }
        }

        public event EventHandler ValueChanged;

        decimal Clamp(decimal v) { return v < min ? min : (v > max ? max : v); }

        void Raise() { var h = ValueChanged; if (h != null) h(this, EventArgs.Empty); }

        void Sync()
        {
            string s = step >= 1m ? ((long)val).ToString() : val.ToString("0.##");
            if (box.Text != s) { box.Text = s; }
        }

        protected override int RightPad { get { return Theme.S(20); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.X > Width - Theme.S(20))
                Value = e.Y < Height / 2 ? val + step : val - step;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool up = e.X > Width - Theme.S(20) && e.Y < Height / 2;
            bool dn = e.X > Width - Theme.S(20) && e.Y >= Height / 2;
            if (up != hoverUp || dn != hoverDn) { hoverUp = up; hoverDn = dn; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverUp = hoverDn = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            float bx = Width - Theme.S(16);
            float cs = Theme.S(9);
            float half = Height / 2f;
            var upRect = new RectangleF(bx, half - cs + Theme.S(1), cs, cs);
            var dnRect = new RectangleF(bx, half + Theme.S(1) - Theme.S(1), cs, cs);
            g.TranslateTransform(upRect.X + cs / 2f, upRect.Y + cs / 2f);
            g.RotateTransform(180);
            Icons.Draw(g, Glyph.ChevronD, new RectangleF(-cs / 2f, -cs / 2f, cs, cs), hoverUp ? Theme.Accent : Theme.TextDim);
            g.ResetTransform();
            Icons.Draw(g, Glyph.ChevronD, dnRect, hoverDn ? Theme.Accent : Theme.TextDim);
        }
    }

    sealed class MacProgress : Control, IThemed
    {
        int max = 100, val;
        public MacProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.S(6);
        }
        public int Maximum { get { return max; } set { max = Math.Max(1, value); Invalidate(); } }
        public int Value { get { return val; } set { val = Math.Max(0, Math.Min(max, value)); Invalidate(); } }
        public void ApplyTheme() { Height = Theme.S(6); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Theme.RoundRect(r, Height)) Theme.FillPath(g, p, Theme.CardSunken);
            int fw = (int)((val / (float)max) * (Width - 1));
            if (fw > 2)
            {
                using (var p = Theme.RoundRect(new Rectangle(0, 0, fw, Height - 1), Height))
                    Theme.FillPath(g, p, Theme.Accent);
            }
        }
    }

    /// 强调色圆形色板，等价于 macOS「通用 → 强调色」。
    sealed class MacSwatches : Control, IThemed
    {
        readonly List<AccentHue> hues = new List<AccentHue>();
        int sel;
        int hover = -1;
        public event EventHandler SelectedIndexChanged;

        public MacSwatches()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.S(26);
            foreach (AccentHue h in Enum.GetValues(typeof(AccentHue))) hues.Add(h);
        }

        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                int v = value < 0 || value >= hues.Count ? sel : value;
                if (v == sel) return;
                sel = v;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public AccentHue SelectedHue { get { return hues[sel]; } }

        public void ApplyTheme() { Height = Theme.S(26); Invalidate(); }

        Rectangle Dot(int i)
        {
            int d = Theme.S(18);
            return new Rectangle(Theme.S(2) + i * (d + Theme.S(10)), (Height - d) / 2, d, d);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < hues.Count; i++) if (Dot(i).Contains(e.Location)) h = i;
            if (h != hover) { hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < hues.Count; i++) if (Dot(i).Contains(e.Location)) { SelectedIndex = i; return; }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            for (int i = 0; i < hues.Count; i++)
            {
                var r = Dot(i);
                Color[] pair;
                if (!Theme.Hues.TryGetValue(hues[i], out pair)) continue;
                Color c = Theme.IsDark ? pair[1] : pair[0];
                using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
                if (i == hover)
                    using (var pen = new Pen(Theme.Alpha(Theme.Text, 90), Math.Max(1f, Theme.S(1))))
                        g.DrawEllipse(pen, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
                if (i == sel)
                {
                    using (var pen = new Pen(Theme.IsDark ? Color.FromArgb(235, 235, 238) : Color.White,
                        Math.Max(1.6f, Theme.S(2))))
                        g.DrawEllipse(pen, r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6);
                }
            }
        }
    }
}
