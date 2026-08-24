using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    static class Theme
    {
        public static int Radius = 8;
        public static bool IsDark;

        public static Color Bg;
        public static Color Card;
        public static Color Surface;
        public static Color SurfaceHover;
        public static Color Border;
        public static Color Text;
        public static Color TextDim;
        public static Color Accent;
        public static Color AccentHover;
        public static Color Green;

        public static readonly Font FontUi = new Font("微软雅黑", 9f);
        public static readonly Font FontTitle = new Font("微软雅黑", 9f, FontStyle.Bold);

        static Theme()
        {
            ApplyLight();
        }

        public static void SetDark(bool dark)
        {
            IsDark = dark;
            if (dark) ApplyGitHubDark();
            else ApplyLight();
        }

        static void ApplyLight()
        {
            Bg = Color.FromArgb(243, 244, 248);
            Card = Color.White;
            Surface = Color.FromArgb(240, 241, 245);
            SurfaceHover = Color.FromArgb(226, 229, 238);
            Border = Color.FromArgb(214, 218, 230);
            Text = Color.FromArgb(31, 36, 48);
            TextDim = Color.FromArgb(122, 128, 144);
            Accent = Color.FromArgb(59, 130, 246);
            AccentHover = Color.FromArgb(111, 160, 255);
            Green = Color.FromArgb(34, 164, 93);
        }

        static void ApplyGitHubDark()
        {
            Bg = Color.FromArgb(13, 17, 23);
            Card = Color.FromArgb(22, 27, 34);
            Surface = Color.FromArgb(33, 38, 45);
            SurfaceHover = Color.FromArgb(48, 54, 61);
            Border = Color.FromArgb(48, 54, 61);
            Text = Color.FromArgb(230, 237, 243);
            TextDim = Color.FromArgb(139, 148, 158);
            Accent = Color.FromArgb(47, 129, 247);
            AccentHover = Color.FromArgb(88, 166, 255);
            Green = Color.FromArgb(63, 185, 80);
        }

        public static int CornerRadius(int width, int height)
        {
            int max = Math.Max(0, Math.Min(width, height) / 2 - 1);
            int r = Radius;
            return r > max ? max : r;
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int rad = radius;
            int max = Math.Max(0, Math.Min(r.Width, r.Height) / 2);
            if (rad > max) rad = max;
            if (rad < 1 || r.Width < 2 || r.Height < 2)
            {
                p.AddRectangle(r);
                return p;
            }
            float d = rad * 2f;
            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 找到一个不透明祖先的背景色：自绘圆角控件的四角用它擦底，避免透明父控件导致四角发白/发黑
        public static Color SolidBack(Control c)
        {
            Control p = c.Parent;
            while (p != null && p.BackColor.A == 0) p = p.Parent;
            return p == null ? Bg : p.BackColor;
        }

        public static void StyleButton(Button b, bool primary)
        {
            var db = b as DarkButton;
            if (db != null) db.Primary = primary;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.Transparent;
            b.ForeColor = primary ? Color.White : Text;
            b.Cursor = Cursors.Hand;
            b.Font = FontUi;
            b.UseVisualStyleBackColor = false;
            b.Region = null;
            b.Invalidate();
        }

        public static void StyleCombo(ComboBox c)
        {
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = IsDark ? Surface : Color.White;
            c.ForeColor = Text;
            c.Font = FontUi;
            c.Region = null;
        }

        public static void StyleNum(NumericUpDown n)
        {
            n.BorderStyle = BorderStyle.FixedSingle;
            n.BackColor = IsDark ? Surface : Color.White;
            n.ForeColor = Text;
            n.Font = FontUi;
            n.Region = null;
        }

        public static void StyleText(TextBox t)
        {
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = IsDark ? Surface : Color.White;
            t.ForeColor = Text;
            t.Font = FontUi;
            t.Region = null;
        }

        public static void StyleCheck(CheckBox c)
        {
            c.FlatStyle = FlatStyle.Flat;
            c.ForeColor = Text;
            c.Font = FontUi;
            c.Cursor = Cursors.Hand;
            c.BackColor = Color.Transparent;
        }

        public static void StyleLabel(Label l, Color fore)
        {
            l.ForeColor = fore;
            l.Font = FontUi;
            l.BackColor = Color.Transparent;
            l.Tag = RoleOf(fore);
        }

        static string RoleOf(Color c)
        {
            if (c == Accent) return "accent";
            if (c == Green) return "green";
            if (c == Text) return "text";
            return "dim";
        }

        public static void Restyle(Control root)
        {
            ApplyThemeTo(root);
            foreach (Control child in root.Controls) Restyle(child);
        }

        static void ApplyThemeTo(Control c)
        {
            Button btn = c as Button;
            if (btn != null)
            {
                var db = btn as DarkButton;
                bool prim = db != null && db.Primary;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.Transparent;
                btn.ForeColor = prim ? Color.White : Text;
                btn.Invalidate();
                return;
            }
            if (c is DarkGroupBox) { c.ForeColor = Text; c.Invalidate(); return; }
            if (c is DarkSlider) { ((DarkSlider)c).Invalidate(); return; }
            ComboBox cmb = c as ComboBox;
            if (cmb != null) { StyleCombo(cmb); return; }
            NumericUpDown num = c as NumericUpDown;
            if (num != null) { StyleNum(num); return; }
            TextBox tb = c as TextBox;
            if (tb != null) { StyleText(tb); return; }
            CheckBox chk = c as CheckBox;
            if (chk != null) { StyleCheck(chk); return; }
            Label lbl = c as Label;
            if (lbl != null)
            {
                string role = lbl.Tag as string;
                if (role == "accent") lbl.ForeColor = Accent;
                else if (role == "green") lbl.ForeColor = Green;
                else if (role == "text") lbl.ForeColor = Text;
                else lbl.ForeColor = TextDim;
            }
        }
    }

    sealed class DarkButton : Button
    {
        bool hover, pressed;
        public bool Primary;

        public DarkButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Theme.SolidBack(this));

            int rad = Theme.CornerRadius(Width, Height);
            var rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));

            Color fill, border, fore;
            if (!Enabled)
            {
                fill = Theme.Surface;
                border = Theme.Border;
                fore = Theme.TextDim;
            }
            else if (Primary)
            {
                fill = pressed ? Theme.Accent : (hover ? Theme.AccentHover : Theme.Accent);
                border = fill;
                fore = Color.White;
            }
            else
            {
                fill = pressed ? Theme.Border : (hover ? Theme.SurfaceHover : (Theme.IsDark ? Theme.Surface : Theme.Card));
                border = Theme.Border;
                fore = Theme.Text;
            }

            using (var path = Theme.RoundRect(rect, rad))
            using (var br = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    sealed class DarkGroupBox : GroupBox
    {
        public DarkGroupBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.FontUi;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Theme.SolidBack(this));
            var r = new Rectangle(1, 10, Width - 3, Height - 12);
            int rad = Theme.CornerRadius(r.Width, r.Height);
            using (var path = Theme.RoundRect(r, rad))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Theme.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Theme.FontTitle, new Point(14, 2), Theme.Accent);
        }
    }

    sealed class DarkSlider : Control
    {
        int min = 0, max = 100, val = 0;
        bool dragging, hover;
        const float Pad = 12f;

        public event EventHandler ValueChanged;

        public DarkSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 32;
        }

        public int Minimum { get { return min; } set { min = value; if (val < min) { val = min; RaiseChange(); } Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; if (val > max) { val = max; RaiseChange(); } Invalidate(); } }
        public TickStyle TickStyle { set { } get { return TickStyle.None; } }

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                RaiseChange();
            }
        }

        void RaiseChange()
        {
            var h = ValueChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        float Frac()
        {
            if (max <= min) return 0f;
            return (val - min) / (float)(max - min);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Width - Pad * 2;
            float cy = Height / 2f;

            using (var track = Theme.RoundRect(new Rectangle((int)Pad, (int)(cy - 2), (int)w, 6), 3))
            using (var b = new SolidBrush(Theme.Border))
                g.FillPath(b, track);

            float fx = Pad + Frac() * w;
            if (fx > Pad + 2)
            {
                using (var filled = Theme.RoundRect(new Rectangle((int)Pad, (int)(cy - 2), (int)(fx - Pad), 6), 3))
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, filled);
            }

            var thumbRect = new RectangleF(fx - 7, cy - 7, 14, 14);
            if (hover || dragging)
                using (var b = new SolidBrush(Color.FromArgb(70, Theme.Accent)))
                    g.FillEllipse(b, fx - 11, cy - 11, 22, 22);
            using (var b = new SolidBrush(hover || dragging ? Theme.AccentHover : Theme.Accent))
                g.FillEllipse(b, thumbRect);
            using (var b = new SolidBrush(Theme.IsDark ? Theme.Card : Color.White))
                g.FillEllipse(b, fx - 4.5f, cy - 4.5f, 9, 9);
        }

        void UpdateFromX(int x)
        {
            float w = Width - Pad * 2;
            float f = (x - Pad) / w;
            if (f < 0) f = 0; if (f > 1) f = 1;
            Value = min + (int)Math.Round(f * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; Capture = true; UpdateFromX(e.X); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) UpdateFromX(e.X);
            else if (!hover) { hover = true; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false; Capture = false; Invalidate();
            base.OnMouseUp(e);
        }
    }
}
