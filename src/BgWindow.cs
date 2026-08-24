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

    sealed class BgWindow : Form
    {
        readonly IntPtr target;
        readonly DisplayFit fit;
        Image img;
        System.Windows.Forms.Integration.ElementHost host;
        System.Windows.Controls.MediaElement media;

        public BgWindow(IntPtr targetHwnd) : this(targetHwnd, DisplayFit.CoverFill) { }

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
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        static System.Windows.Media.Stretch WpfStretch(DisplayFit f)
        {
            if (f == DisplayFit.FitContain) return System.Windows.Media.Stretch.Uniform;
            if (f == DisplayFit.StretchFill) return System.Windows.Media.Stretch.Fill;
            return System.Windows.Media.Stretch.UniformToFill;
        }

        public BgWindow(IntPtr targetHwnd, Image image, DisplayFit displayFit) : this(targetHwnd, displayFit)
        {
            img = image;
        }

        public BgWindow(IntPtr targetHwnd, string videoPath, DisplayFit displayFit) : this(targetHwnd, displayFit)
        {
            media = new System.Windows.Controls.MediaElement
            {
                LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                UnloadedBehavior = System.Windows.Controls.MediaState.Close,
                Stretch = WpfStretch(displayFit)
            };
            media.MediaFailed += (s, ev) =>
            {
                Program.Log(new Exception("MediaFailed: " + (ev.ErrorException == null ? "" : ev.ErrorException.Message)));
                try { media.Close(); } catch { }
            };

            System.Windows.Media.MediaClock clock = null;
            try
            {
                var timeline = new System.Windows.Media.MediaTimeline(new Uri(videoPath))
                {
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
                clock = timeline.CreateClock();
            }
            catch (Exception ex) { Program.Log(ex); }

            var pendingClock = clock;
            media.Loaded += (s, ev) =>
            {
                try
                {
                    if (pendingClock != null) media.Clock = pendingClock;
                    else { media.Source = new Uri(videoPath); media.Play(); }
                }
                catch (Exception ex) { Program.Log(ex); }
            };

            host = new System.Windows.Forms.Integration.ElementHost { Dock = DockStyle.Fill, Child = media };
            Controls.Add(host);
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
            try
            {
                if (host != null)
                {
                    host.Child = null;
                    host.Dispose();
                    host = null;
                }
                if (media != null) { try { media.Close(); } catch { } media = null; }
            }
            catch (Exception ex) { Program.Log(ex); }
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
