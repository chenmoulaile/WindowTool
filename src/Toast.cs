using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    enum ToastKind { Info, Good, Warn, Bad }

    /// 顶部滑入的浮动提示条，取代旧版内嵌在分组里的一行绿字。
    sealed class MacToast : Panel
    {
        readonly Timer life = new Timer();
        readonly Timer anim = new Timer();
        string msg = "";
        ToastKind kind = ToastKind.Info;
        int targetY, curY;
        bool outgo;

        public MacToast()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.S(38);
            Cursor = Cursors.Default;
            Visible = false;

            life.Interval = 2600;
            life.Tick += (s, e) => { life.Stop(); outgo = true; anim.Start(); };
            anim.Interval = 16;
            anim.Tick += (s, e) => Step();
            Click += (s, e) => Dismiss();
        }

        public void Show(string text, ToastKind k, int widthPx)
        {
            msg = text ?? "";
            kind = k;
            Width = Math.Min(widthPx, Theme.S(460));
            targetY = Theme.S(10);
            outgo = false;
            curY = -Height;
            Visible = true;
            BringToFront();
            life.Stop();
            life.Start();
            anim.Start();
        }

        public void Dismiss() { outgo = true; life.Stop(); anim.Start(); }

        void Step()
        {
            int dest = outgo ? -Height : targetY;
            curY += (dest - curY) / 3;
            if (Math.Abs(dest - curY) <= 2)
            {
                curY = dest;
                anim.Stop();
                if (outgo) Visible = false;
            }
            Invalidate();
        }

        Color Tint()
        {
            switch (kind)
            {
                case ToastKind.Good: return Theme.Green;
                case ToastKind.Warn: return Theme.Warning;
                case ToastKind.Bad: return Theme.Red;
            }
            return Theme.Accent;
        }

        Glyph IconFor()
        {
            switch (kind)
            {
                case ToastKind.Good: return Glyph.Check;
                case ToastKind.Warn: return Glyph.Warning;
                case ToastKind.Bad: return Glyph.Warning;
            }
            return Glyph.Info;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            var r = new Rectangle(0, curY, Width - 1, Height - 1);
            int rad = Theme.S(9);
            Theme.Shadow(g, r, rad, Theme.S(4), Color.Black);
            using (var p = Theme.RoundRect(r, rad))
            {
                Theme.FillPath(g, p, Theme.IsDark ? Color.FromArgb(56, 58, 64) : Color.White);
                Theme.StrokePath(g, p, Theme.Alpha(Tint(), 150), Math.Max(1f, Theme.S(1)));
            }
            int isz = Theme.S(16);
            var ib = new Rectangle(Theme.S(11), r.Y + (r.Height - isz) / 2, isz, isz);
            using (var p = Theme.RoundRect(ib, Theme.S(5))) Theme.FillPath(g, p, Theme.Alpha(Tint(), 34));
            Icons.Draw(g, IconFor(), new RectangleF(ib.X + Theme.S(2), ib.Y + Theme.S(2), isz - Theme.S(4), isz - Theme.S(4)), Tint());
            MacUi.LeftText(g, Theme.Ellipsis(msg, Theme.FontUi, Width - Theme.S(52)), Theme.FontUi,
                new RectangleF(ib.Right + Theme.S(9), r.Y, Width - ib.Right - Theme.S(18), r.Height), Theme.Text);
        }
    }
}
