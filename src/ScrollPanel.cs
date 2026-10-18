using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    /// 视口 + 内容画布：整块画布随滚轮位移，自带细滚动条。
    sealed class MacScrollPanel : Panel, IThemed
    {
        readonly Panel canvas;
        int offset, target, contentH;
        readonly object glideTag = new object();
        readonly MacScrollBar bar = new MacScrollBar();

        public MacScrollPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            canvas = new SurfacePanel();
            Controls.Add(canvas);
            Controls.Add(bar);
            bar.Owner = this;

        }

        void Apply()
        {
            canvas.Top = -offset;
            bar.Invalidate();
            Invalidate();
        }

        /// 只追目标、不重启补间：滚轮连按时重启会让速度反复归零，看起来一顿一顿。
        void Glide()
        {
            Motion.Chase(glideTag, delegate { return offset; }, delegate { return target; },
                delegate(int v)
                {
                    offset = v;
                    if (!IsDisposed) Apply();
                }, 70);
        }

        public void ScrollBy(int dy)
        {
            if (!CanScroll) return;
            target = Math.Max(0, Math.Min(MaxOffset, target + dy));
            Glide();
        }

        /// 内容画布：Panel.SetStyle 是受保护成员，必须由派生类自己开启自绘样式。
        sealed class SurfacePanel : Panel
        {
            public SurfacePanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }
        }

        public Control Surface { get { return canvas; } }
        public int Offset { get { return offset; } }
        public bool CanScroll { get { return contentH > Height; } }
        public int MaxOffset { get { return Math.Max(0, contentH - Height); } }

        public void SetContent(int width, int height)
        {
            contentH = height;
            canvas.SetBounds(0, -offset, width, Math.Max(height, Height));
            bar.Bounds = new Rectangle(width - Theme.S(9), Theme.S(4), Theme.S(5), Height - Theme.S(8));
            bar.Invalidate();
            Clamp();
        }

        /// 立即定位（滚动条拖拽用），不走缓动
        public void SetOffset(int v)
        {
            int nv = Math.Max(0, Math.Min(MaxOffset, v));
            offset = nv; target = nv;
            Motion.Cancel(glideTag);
            canvas.Top = -offset;
            bar.Invalidate();
            Invalidate();
        }

        void Clamp() { if (offset > MaxOffset) SetOffset(MaxOffset); else if (target > MaxOffset) target = MaxOffset; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int per = Math.Max(Theme.S(40), Height / 12);
            ScrollBy(e.Delta < 0 ? per : -per);
            base.OnMouseWheel(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            bar.Bounds = new Rectangle(Width - Theme.S(9), Theme.S(4), Theme.S(5), Height - Theme.S(8));
            Clamp();
        }

        public void ApplyTheme()
        {
            bar.Invalidate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
        }

        internal RectangleF ThumbRect()
        {
            if (!CanScroll) return Rectangle.Empty;
            float track = bar.Height;
            float th = Math.Max(Theme.S(28), track * Height / (float)contentH);
            float f = MaxOffset <= 0 ? 0f : offset / (float)MaxOffset;
            return new RectangleF(0, f * (track - th), bar.Width, th);
        }
    }

    sealed class MacScrollBar : Control
    {
        public MacScrollPanel Owner;
        bool hover, drag;
        float grab;

        public MacScrollBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Owner == null || !Owner.CanScroll) return;
            var t = Owner.ThumbRect();
            if (t.Contains(e.X, e.Y)) { drag = true; grab = e.Y - t.Y; }
            else
            {
                float f = (e.Y - Height / 2f) / Math.Max(1, Height - t.Height);
                Owner.SetOffset((int)(f * Owner.MaxOffset));
                drag = true; grab = t.Height / 2;
            }
            Capture = true;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!drag || Owner == null) return;
            var t = Owner.ThumbRect();
            float f = (e.Y - grab) / Math.Max(1, Height - t.Height);
            Owner.SetOffset((int)(f * Owner.MaxOffset));
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Capture = false; base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            if (Owner == null || !Owner.CanScroll) return;
            var t = Owner.ThumbRect();
            int a = drag ? 150 : (hover ? 110 : 60);
            using (var p = Theme.RoundRect(Rectangle.Round(t), Width))
                Theme.FillPath(g, p, Theme.Alpha(Theme.Text, a));
        }
    }
}
