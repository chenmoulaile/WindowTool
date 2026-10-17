using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    /// 桌面歌词：悬浮圆角深色胶囊，可拖动，不抢焦点
    sealed class DesktopLyricsForm : Form
    {
        readonly Font font = new Font("微软雅黑", 15f, FontStyle.Bold);
        string text = "";
        double progress;      // 当前歌词行 0..1，用填色表现进度（公开接口没有逐字时间轴）
        bool dragging;
        Point dragPt;

        public DesktopLyricsForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(22, 24, 28);
            Opacity = 0.9;
            Size = Theme.Sz(420, 52);
            DoubleBuffered = true;
            TopMost = true;
            Cursor = Cursors.SizeAll;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundRect(r, Height))
            {
                using (var br = new SolidBrush(BackColor)) g.FillPath(br, path);
                using (var pen = new Pen(Theme.Alpha(Theme.Accent, 150), Math.Max(1f, Theme.S(1.4f))))
                    g.DrawPath(pen, path);
            }
            var box = new RectangleF(Theme.S(22), 0, Width - Theme.S(44), Height);
            string shown = text.Length == 0 ? "…" : text;
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, shown, font, Rectangle.Round(box), Color.FromArgb(215, 255, 255, 255), flags);

            double pr = progress;
            if (pr > 0.001 && pr < 0.999)
            {
                var old = g.Clip;
                g.SetClip(new RectangleF(box.X, box.Y, box.Width * (float)pr, box.Height));
                TextRenderer.DrawText(g, shown, font, Rectangle.Round(box), Theme.Accent, flags);
                g.Clip = old;
            }
        }

        public void SetProgress(double p01)
        {
            double v = p01 < 0 ? 0 : (p01 > 1 ? 1 : p01);
            if (Math.Abs(v - progress) < 0.004) return;
            progress = v;
            Invalidate();
        }

        public void SetLine(string s)
        {
            if (s == text) return;
            text = s ?? "";
            int w = TextRenderer.MeasureText(text, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + Theme.S(56);
            w = Math.Max(Theme.S(280), Math.Min(Theme.S(900), w));
            int h = Theme.S(52);
            Location = new Point(Location.X + (Width - w) / 2, Location.Y + (Height - h));
            Size = new Size(w, h);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; dragPt = e.Location; Capture = true; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) Location = new Point(Location.X + e.X - dragPt.X, Location.Y + e.Y - dragPt.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false; Capture = false;
            base.OnMouseUp(e);
        }
    }

    /// 任务栏歌词：贴在任务栏上沿的细条，鼠标穿透不挡操作，自动跟随任务栏位置
    sealed class TaskbarLyricsForm : Form
    {
        readonly Font font = new Font("微软雅黑", 10.5f, FontStyle.Bold);
        string text = "";

        public TaskbarLyricsForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(22, 24, 28);
            Opacity = 0.92;
            Size = Theme.Sz(600, 34);
            DoubleBuffered = true;
            TopMost = true;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW |
                              Native.WS_EX_TRANSPARENT; // 鼠标穿透
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundRect(r, Height))
            {
                using (var br = new SolidBrush(BackColor)) g.FillPath(br, path);
                using (var pen = new Pen(Theme.Alpha(Theme.Accent, 130), Math.Max(1f, Theme.S(1))))
                    g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, text.Length == 0 ? "…" : text, font,
                new Rectangle(Theme.S(16), 0, Width - Theme.S(32), Height), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        public void SetLine(string s)
        {
            if (s == text) return;
            text = s ?? "";
            int w = TextRenderer.MeasureText(text, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + Theme.S(40);
            w = Math.Max(Theme.S(260), Math.Min(Theme.S(900), w));
            int h = Theme.S(34);
            Location = new Point(Location.X + (Width - w) / 2, Location.Y + (Height - h));
            Size = new Size(w, h);
            Invalidate();
        }

        // 依据任务栏实时位置停靠（任务栏在底部：贴其上沿；在顶部/左右：贴其内侧边缘附近）
        public void EnsurePosition()
        {
            IntPtr tb = Native.FindWindow("Shell_TrayWnd", null);
            Native.RECT r;
            if (tb == IntPtr.Zero || !Native.GetWindowRect(tb, out r)) return;
            int tbW = r.Right - r.Left, tbH = r.Bottom - r.Top;
            if (tbW <= 0 || tbH <= 0) return;
            int w = Math.Min(tbW - Theme.S(360), Theme.S(700));
            if (w < Theme.S(260)) w = Theme.S(260);
            int h = Theme.S(34);
            int x = r.Left + (tbW - w) / 2;
            int y = tbH > tbW ? r.Top + (tbH - h) / 2 : r.Top - h - Theme.S(2); // 竖屏任务栏贴内侧
            Location = new Point(x, y);
            Size = new Size(w, h);
        }
    }
}
