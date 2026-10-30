using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    enum DisplayFit
    {
        CoverFill,
        FitContain,
        StretchFill
    }

    /// 背景窗体（图片）。图片背景用 GDI+ 直接绘制，
    /// 整个程序不再引用任何 WPF 程序集，常驻内存显著降低。
    class BgWindow : Form
    {
        protected readonly IntPtr target;
        protected readonly DisplayFit fit;
        Image img;

        public BgWindow(IntPtr targetHwnd, DisplayFit displayFit)
        {
            target = targetHwnd;
            fit = displayFit;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Opaque |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        public BgWindow(IntPtr targetHwnd, Image image, DisplayFit displayFit) : this(targetHwnd, displayFit)
        {
            img = image;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | 0x08000000;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (img == null) { base.OnPaint(e); return; }
            var g = e.Graphics;
            g.Clear(Color.Black);
            int cw = ClientSize.Width, ch = ClientSize.Height;
            int iw = img.Width, ih = img.Height;
            if (cw <= 0 || ch <= 0 || iw <= 0 || ih <= 0) return;

            int dx, dy, dw, dh, sx = 0, sy = 0, sw = iw, sh = ih;
            if (fit == DisplayFit.StretchFill)
            {
                dx = 0; dy = 0; dw = cw; dh = ch;
            }
            else if (fit == DisplayFit.FitContain)
            {
                double scale = Math.Min((double)cw / iw, (double)ch / ih);
                dw = Math.Max(1, (int)Math.Round(iw * scale));
                dh = Math.Max(1, (int)Math.Round(ih * scale));
                dx = (cw - dw) / 2;
                dy = (ch - dh) / 2;
            }
            else
            {
                double scale = Math.Max((double)cw / iw, (double)ch / ih);
                sw = Math.Max(1, (int)Math.Round(cw / scale));
                sh = Math.Max(1, (int)Math.Round(ch / scale));
                sx = (iw - sw) / 2;
                sy = (ih - sh) / 2;
                dx = 0; dy = 0; dw = cw; dh = ch;
            }
            g.DrawImage(img, new Rectangle(dx, dy, dw, dh), sx, sy, sw, sh, GraphicsUnit.Pixel);
        }

        public void Follow()
        {
            try
            {
                if (!Native.IsWindow(target)) { SafeHide(); return; }
                Native.RECT r;
                if (!Native.GetWindowRect(target, out r)) { SafeHide(); return; }
                if (Native.IsIconic(target)) { SafeHide(); return; }
                var b = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                if (b.Width <= 0 || b.Height <= 0) { SafeHide(); return; }
                if (!Visible)
                {
                    Bounds = b;
                    Show();
                    Native.SetWindowPos(Handle, target, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                }
                else
                {
                    if (Bounds != b) Bounds = b;
                    Native.SetWindowPos(Handle, target, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void SafeHide() { try { if (Visible) Hide(); } catch { } }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (img != null)
            {
                var old = img;
                img = null;
                old.Dispose();
            }
            base.OnFormClosed(e);
        }
    }
}
