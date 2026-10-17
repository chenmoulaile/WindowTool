using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowTool
{
    /// 无边框 macOS 式窗口外壳：自绘标题带 + 红绿灯 + 可拖拽/缩放 + 圆角与投影。
    class MacForm : Form
    {
        const int WM_NCHITTEST = 0x0084;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int HTCLIENT = 1;
        const int HTCAPTION = 2;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                  HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int GWL_STYLE = -16;
        const int WS_THICKFRAME = 0x00040000;
        const int WS_MINIMIZEBOX = 0x00020000;
        const int WS_MAXIMIZEBOX = 0x00010000;
        const int WS_SYSMENU = 0x00080000;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        protected int TitleBand { get { return Theme.S(44); } }
        protected int Grip { get { return Theme.S(6); } }

        readonly TrafficLights lights = new TrafficLights();
        bool rounded = true;
        Padding chrome = Padding.Empty;

        public event Action ZoomRequested;
        public bool CanZoom = true;

        public MacForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Window;
            MinimizeBox = true;
            MaximizeBox = true;
            KeyPreview = true;
            Controls.Add(lights);
            lights.Location = Theme.P(16, 14);
            lights.Close += delegate { Close(); };
            lights.Minimize += delegate { WindowState = FormWindowState.Minimized; };
            lights.Zoom += delegate
            {
                if (ZoomRequested != null) ZoomRequested();
                else ToggleZoom();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // 只保留 WS_THICKFRAME 用于边缘拖拽缩放；非客户区渲染在 ApplyChrome 里关掉，
                // 否则 Win11 会在无边框窗口四周画出一圈灰色粗边框
                cp.Style |= WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyChrome();
        }

        protected virtual void ApplyChrome()
        {
            try
            {
                int ncOff = 1;                      // DWMWA_NCRENDERING_POLICY(3) = DWMNCRP_DISABLED
                DwmSetWindowAttribute(Handle, 3, ref ncOff, 4);
                int ncDisabled = 0;                 // DWMWA_NCRENDERING_ENABLED(1) = FALSE
                DwmSetWindowAttribute(Handle, 1, ref ncDisabled, 4);
                int pref = 2;                       // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
                DwmSetWindowAttribute(Handle, 33, ref pref, 4);
                int dark = Theme.IsDark ? 1 : 0;
                DwmSetWindowAttribute(Handle, 19, ref dark, 4);   // 暗色标题栏
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);   // 暗色边框
            }
            catch { }
            ApplyRegion();
            // 无边框窗口最大化默认会盖住任务栏，显式限制到工作区
            try { MaximizedBounds = Screen.FromHandle(Handle).WorkingArea; } catch { }
        }

        void ApplyRegion()
        {
            if (!rounded || Width < 8 || Height < 8) { Region = null; return; }
            int rad = Theme.S(Math.Max(2, Math.Min(14, Theme.Radius + 4)));
            Region old = Region;
            using (var p = Theme.RoundRect(new Rectangle(0, 0, Width, Height), rad))
                Region = new Region(p);
            if (old != null) old.Dispose();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRegion();
        }

        public void ToggleZoom()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else WindowState = FormWindowState.Maximized;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.Window);
            OnChromePaint(g);
            int rad = Theme.S(Math.Max(2, Math.Min(14, Theme.Radius + 4)));
            using (var p = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), rad))
                Theme.StrokePath(g, p, Theme.Alpha(Theme.Text, Theme.IsDark ? 46 : 26), Math.Max(1f, Theme.S(1)));
        }

        /// 子类绘制标题带内容（页面标题等）。
        protected virtual void OnChromePaint(Graphics g) { }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HitTest(m.LParam);
                return;
            }
            base.WndProc(ref m);
        }

        int HitTest(IntPtr lparam)
        {
            int sx = (short)((long)lparam & 0xFFFF);
            int sy = (short)(((long)lparam >> 16) & 0xFFFF);
            var p = PointToClient(new Point(sx, sy));
            int w = Width, h = Height, gp = Grip;

            bool l = p.X < gp, r = p.X >= w - gp, t = p.Y < gp, b = p.Y >= h - gp;
            if (t && l) return HTTOPLEFT;
            if (t && r) return HTTOPRIGHT;
            if (b && l) return HTBOTTOMLEFT;
            if (b && r) return HTBOTTOMRIGHT;
            if (l) return HTLEFT;
            if (r) return HTRIGHT;
            if (t) return HTTOP;
            if (b) return HTBOTTOM;

            if (p.Y < TitleBand && !OverInteractive(p)) return HTCAPTION;
            return HTCLIENT;
        }

        bool OverInteractive(Point p)
        {
            if (lights.Bounds.Contains(p)) return true;
            foreach (Control c in Controls)
            {
                if (!c.Visible || c == lights) continue;
                if (c.Bounds.Contains(p)) return true;
                foreach (Control gc in c.Controls)
                    if (gc.Visible && gc.Bounds.Contains(new Point(p.X - c.Left, p.Y - c.Top))) return true;
            }
            return false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            lights.Dispose();
            base.OnFormClosed(e);
        }
    }

    /// 红绿灯：整组悬停时淡入 × − + 符号，与 macOS 行为一致。
    sealed class TrafficLights : Control
    {
        const int D = 12;
        const int GAP = 8;
        int hot = -1;
        readonly HoverFade fade;

        public event Action Close;
        public event Action Minimize;
        public event Action Zoom;

        public TrafficLights()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = Theme.Sz(D * 3 + GAP * 2 + 4, D + 4);
            fade = new HoverFade(this);
        }

        Rectangle Dot(int i)
        {
            int d = Theme.S(D), gp = Theme.S(GAP);
            return new Rectangle(Theme.S(2) + i * (d + gp), Theme.S(2), d, d);
        }

        protected override void OnMouseEnter(EventArgs e) { fade.To(1f, 130); Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; fade.To(0f, 160); Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < 3; i++) if (Dot(i).Contains(e.Location)) h = i;
            if (h != hot) { hot = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < 3; i++)
            {
                if (!Dot(i).Contains(e.Location)) continue;
                if (i == 0) { if (Close != null) Close(); }
                else if (i == 1) { if (Minimize != null) Minimize(); }
                else if (i == 2) { if (Zoom != null) Zoom(); }
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            Color[] fills = { Color.FromArgb(255, 95, 86), Color.FromArgb(255, 189, 46), Color.FromArgb(40, 201, 63) };
            Glyph[] glyphs = { Glyph.Close, Glyph.Minus, Glyph.Enlarge };
            float a = fade.Value;

            for (int i = 0; i < 3; i++)
            {
                var r = Dot(i);
                using (var b = new SolidBrush(fills[i])) g.FillEllipse(b, r);
                using (var pen = new Pen(Color.FromArgb((int)(46 * a), 0, 0, 0), Math.Max(1f, Theme.S(1))))
                    g.DrawEllipse(pen, r);
                if (a < 0.02f) continue;
                Color gc = Color.FromArgb((int)(150 * a), 0, 0, 0);
                if (i == 2) gc = Color.FromArgb((int)(255 * a), 255, 255, 255);
                var gr = new RectangleF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.22f, r.Width * 0.56f, r.Height * 0.56f);
                Icons.Draw(g, glyphs[i], gr, gc);
            }
        }
    }

    sealed class SidebarItem
    {
        public string Key = "";
        public string Title = "";
        public string Subtitle = "";
        public Glyph Icon;
        public Color Tile = Theme.Accent;
    }

    /// 侧栏导航：彩色圆角图标块 + 标题/副标题，选中胶囊在行间滑行。
    sealed class MacSidebar : Control, IThemed
    {
        readonly List<SidebarItem> items = new List<SidebarItem>();
        int sel, hover = -1;
        float pillPos;            // 连续行号：胶囊滑动的实际位置
        readonly HoverFade hoverFade;
        readonly object pillTag = new object();
        public event EventHandler SelectedIndexChanged;
        public string Header;

        public MacSidebar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Width = Theme.S(230);
            Font = Theme.FontUi;
            hoverFade = new HoverFade(this);
        }

        public void ApplyTheme()
        {
            Width = Theme.S(230);
            Font = Theme.FontUi;
            Invalidate();
        }

        public List<SidebarItem> Items { get { return items; } }

        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                int v = value < 0 || value >= items.Count ? sel : value;
                if (v == sel) return;
                sel = v;
                GlidePill(v);
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// 外部（热键、恢复上次页面）直接落位，不播放滑动动画。
        public void SetSelectedSilent(int index)
        {
            if (index < 0 || index >= items.Count) return;
            sel = index;
            pillPos = index;
            Invalidate();
        }

        void GlidePill(int target)
        {
            float from = pillPos;
            if (Math.Abs(target - from) < 0.01f) { pillPos = target; return; }
            int ms = 230 + (int)Math.Min(90, Math.Abs(target - from) * 45);
            Motion.To(pillTag, ms, Ease.Spring, delegate(float t)
            {
                pillPos = from + (target - from) * t;
                Invalidate();
            });
        }

        public SidebarItem Selected { get { return sel >= 0 && sel < items.Count ? items[sel] : null; } }

        int RowTop { get { return Theme.S(84); } }
        int RowH { get { return Theme.S(52); } }
        int RowGap { get { return Theme.S(4); } }

        Rectangle RowRect(int i)
        {
            int pad = Theme.S(10);
            return new Rectangle(pad, RowTop + i * (RowH + RowGap), Width - pad * 2, RowH);
        }

        Rectangle PillRect(float pos)
        {
            int pad = Theme.S(10);
            return new Rectangle(pad, RowTop + (int)Math.Round(pos * (RowH + RowGap)), Width - pad * 2, RowH);
        }

        float Weight(int i)
        {
            float w = 1f - Math.Abs(pillPos - i);
            return w < 0f ? 0f : (w > 1f ? 1f : w);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < items.Count; i++) if (RowRect(i).Contains(e.Location)) h = i;
            if (h != hover)
            {
                hover = h;
                hoverFade.To(h >= 0 && h != sel ? 1f : 0f, h >= 0 ? 140 : 110);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            hoverFade.To(0f, 130);
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < items.Count; i++) if (RowRect(i).Contains(e.Location)) { SelectedIndex = i; return; }
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) SelectedIndex = Math.Min(items.Count - 1, sel + 1);
            else if (e.KeyCode == Keys.Up) SelectedIndex = Math.Max(0, sel - 1);
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));

            using (var pen = new Pen(Theme.Separator, Math.Max(1f, Theme.S(1))))
                g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

            if (!string.IsNullOrEmpty(Header))
                MacUi.LeftText(g, Header, Theme.FontTitle,
                    new RectangleF(Theme.S(16), Theme.S(48), Width - Theme.S(32), Theme.S(28)), Theme.Text);

            // 悬停胶囊先画：滑动胶囊要压在它上面
            if (hover >= 0 && hover != sel && hoverFade.Value > 0.01f)
            {
                using (var p = Theme.RoundRect(RowRect(hover), Theme.S(9)))
                    Theme.FillPath(g, p, Theme.Alpha(Theme.Hover, (int)(255 * hoverFade.Value)));
            }
            if (items.Count > 0)
            {
                var pr = PillRect(pillPos);
                using (var p = Theme.RoundRect(pr, Theme.S(9)))
                {
                    Theme.FillPath(g, p, Theme.Accent);
                    Theme.StrokePath(g, p, Theme.Alpha(Color.Black, 20), Math.Max(1f, Theme.S(1)));
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var r = RowRect(i);
                float w = Weight(i);
                bool isSel = w > 0.5f;

                int tile = Theme.S(26);
                var tr = new Rectangle(r.X + Theme.S(9), r.Y + (r.Height - tile) / 2, tile, tile);
                DrawTile(g, tr, it.Tile, w, isSel);
                float isz = tile * 0.6f;
                Icons.Draw(g, it.Icon, new RectangleF(tr.X + (tile - isz) / 2f, tr.Y + (tile - isz) / 2f, isz, isz),
                    Color.White);

                Color tc = Blend(Theme.Text, Theme.OnAccent, w);
                Color sc = Blend(Theme.TextDim, Color.FromArgb(205, 255, 255, 255), w);
                float tx = tr.Right + Theme.S(11);
                bool two = !string.IsNullOrEmpty(it.Subtitle);
                if (two)
                {
                    int tw = (int)(Width - tx) - Theme.S(10);
                    MacUi.LeftText(g, Theme.Ellipsis(it.Title, Font, tw), Font,
                        new RectangleF(tx, r.Y + Theme.S(6), tw, Theme.S(22)), tc);
                    MacUi.LeftText(g, Theme.Ellipsis(it.Subtitle, Theme.FontSmall, tw), Theme.FontSmall,
                        new RectangleF(tx, r.Y + Theme.S(29), tw, Theme.S(20)), sc);
                }
                else
                {
                    MacUi.LeftText(g, Theme.Ellipsis(it.Title, Font, Width - (int)tx - Theme.S(10)), Font,
                        new RectangleF(tx, r.Y, Width - tx - Theme.S(10), r.Height), tc);
                }
            }
        }

        static Color Blend(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// 图标块：上亮下暗的渐变 + 顶部内高光 + 落地投影，选中时整体提亮一档。
        void DrawTile(Graphics g, Rectangle r, Color baseColor, float w, bool isSel)
        {
            Color top = Theme.Lighten(baseColor, 0.22f);
            Color bottom = Theme.Darken(baseColor, 0.14f);
            if (isSel)
            {
                top = Blend(top, Color.FromArgb(74, 255, 255, 255), 0.55f * w);
                bottom = Blend(bottom, Color.FromArgb(40, 255, 255, 255), 0.55f * w);
            }
            var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            float rad = Theme.S(7);
            using (var sh = Theme.RoundRectF(new RectangleF(r.X, r.Y + Theme.S(1.5f), r.Width, r.Height), rad))
                Theme.FillPath(g, sh, Theme.Alpha(Color.Black, Theme.IsDark ? 60 : 34));
            using (var p = Theme.RoundRectF(rf, rad))
            {
                Theme.FillGradient(g, p, rf, top, bottom);
                Theme.StrokePath(g, p, Theme.Alpha(Color.Black, 26), Math.Max(1f, Theme.S(1) * 0.9f));
            }
            using (var hi = Theme.RoundRectSidesF(new RectangleF(rf.X + 1f, rf.Y + 1f, rf.Width - 2f, rf.Height * 0.5f),
                rad - 1f, true, false))
            using (var b = new SolidBrush(Theme.Alpha(Color.White, 44)))
                g.FillPath(b, hi);
        }
    }

    /// 一页设置内容：卡片纵向堆叠、可滚动。子类在 Build 中声明卡片与行，基类负责排版。
    abstract class Page : Panel, IThemed
    {
        protected MainForm Host;
        protected readonly MacScrollPanel scroll = new MacScrollPanel();
        readonly List<CardSpec> specs = new List<CardSpec>();
        readonly List<Action> tickers = new List<Action>();
        readonly object slideTag = new object();
        Rectangle hostRect;

        /// 入场位移：由 Motion 逐帧改写，只移动面板本身，不重排卡片。
        public int SlideDx;

        /// 每帧通知宿主重绘标题带——标题要跟着页面一起滑，但页面本身画不到那条带子上。
        public Action SlideChanged;

        public abstract string Key { get; }
        public abstract string Title { get; }
        public virtual string Subtitle { get { return ""; } }

        sealed class CardSpec
        {
            public MacCard Card;
            public string Title;
            public readonly List<MacRow> Rows = new List<MacRow>();
            public readonly List<Control> Loose = new List<Control>();
            public int ExtraHeight;
            public Action<MacCard, int> Looser;
        }

        public Page(MainForm host)
        {
            Host = host;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Window;
            Font = Theme.FontUi;
            scroll.SetBounds(0, 0, 10, 10);
            Controls.Add(scroll);
        }

        public abstract void Build();
        public virtual void OnShow() { }
        public virtual void OnHide() { }

        public void Place(Rectangle area)
        {
            hostRect = area;
            ApplyBounds();
        }

        public void ApplyBounds()
        {
            SetBounds(hostRect.X + SlideDx, hostRect.Y, hostRect.Width, hostRect.Height);
        }

        /// dir=+1 从右侧滑入，-1 从左侧；配合侧栏胶囊的滑动方向读起来才连贯。
        public void SlideIn(int dir)
        {
            if (!Motion.Enabled) { SlideDx = 0; ApplyBounds(); return; }
            int from = Theme.S(22) * dir;
            SlideDx = from;
            Motion.To(slideTag, 270, Ease.Out, delegate(float t)
            {
                SlideDx = (int)Math.Round(from * (1f - t));
                ApplyBounds();
                if (SlideChanged != null) SlideChanged();
            }, delegate { SlideDx = 0; ApplyBounds(); });
        }

        public virtual void Tick()
        {
            foreach (var t in tickers) { try { t(); } catch (Exception ex) { Program.Log(ex); } }
        }

        protected void OnTick(Action a) { if (a != null) tickers.Add(a); }

        protected static int H(int logical) { return Theme.S(logical); }

        protected MacRow Row(string title, string subtitle, params Control[] trailing)
        {
            var r = new MacRow();
            r.Title = title ?? "";
            r.Subtitle = subtitle ?? "";
            r.Height = r.RowHeight;
            r.Trailing(trailing);
            return r;
        }

        protected MacCard Card(string title, params MacRow[] rows)
        {
            var s = new CardSpec();
            s.Title = title ?? "";
            s.Card = new MacCard();
            s.Card.Title = s.Title;
            scroll.Surface.Controls.Add(s.Card);
            foreach (var r in rows)
            {
                if (r == null) continue;
                s.Rows.Add(r);
                s.Card.Controls.Add(r);
            }
            specs.Add(s);
            return s.Card;
        }

        /// 无行结构的自定义卡片：layout(card, cardWidth) 在每次排版时回调摆放内部控件。
        protected MacCard FreeCard(string title, int height, Action<MacCard, int> layout, params Control[] children)
        {
            var s = new CardSpec();
            s.Title = title ?? "";
            s.Card = new MacCard();
            s.Card.Title = s.Title;
            s.ExtraHeight = height;
            s.Looser = layout;
            if (children != null) s.Loose.AddRange(children);
            scroll.Surface.Controls.Add(s.Card);
            foreach (var c in children) if (c != null) s.Card.Controls.Add(c);
            specs.Add(s);
            return s.Card;
        }

        public virtual void Relayout(Size area)
        {
            scroll.SetBounds(0, 0, area.Width, area.Height);
            int w = Math.Max(H(120), area.Width - H(14));
            int y = H(6);
            foreach (var s in specs)
            {
                int top = s.Title.Length == 0 ? 0 : H(26);
                int body = H(10);
                if (s.Rows.Count > 0)
                {
                    foreach (var r in s.Rows) body += r.RowHeight;
                    body += H(4);
                }
                else body += s.ExtraHeight;

                int h = top + body;
                s.Card.SetBounds(0, y, w, h);

                int ry = top + H(4);
                foreach (var r in s.Rows)
                {
                    r.SetBounds(2, ry, w - 4, r.RowHeight);
                    r.ApplyTheme();
                    ry += r.RowHeight;
                }
                if (s.Rows.Count == 0 && s.Looser != null) s.Looser(s.Card, w);
                y += h + H(14);
            }
            scroll.SetContent(area.Width, y + H(6));
        }

        /// 从卡片右侧向左紧排一组控件，返回最左一个的左边界（用于放弹性控件）。
        protected static int PackRight(MacCard card, int y, int h, int gap, params Control[] rightToLeft)
        {
            int top = card.Title.Length == 0 ? 0 : H(26);
            int x = card.Width - H(14);
            for (int i = 0; i < rightToLeft.Length; i++)
            {
                var c = rightToLeft[i];
                if (c == null) continue;
                x -= c.Width;
                c.SetBounds(x, top + y, c.Width, h <= 0 ? c.Height : h);
                x -= gap;
            }
            return x + gap;
        }

        /// 从卡片内容区左侧向右紧排，返回下一个可用的 x。
        protected static int PackLeft(MacCard card, int y, int h, int gap, params Control[] leftToRight)
        {
            int top = card.Title.Length == 0 ? 0 : H(26);
            int x = H(14);
            for (int i = 0; i < leftToRight.Length; i++)
            {
                var c = leftToRight[i];
                if (c == null) continue;
                c.SetBounds(x, top + y, c.Width, h <= 0 ? c.Height : h);
                x += c.Width + gap;
            }
            return x - gap;
        }

        /// 等宽居中一排（播放键一类的图标按钮组）。
        protected static void PackCenter(MacCard card, int y, int h, int gap, params Control[] row)
        {
            int top = card.Title.Length == 0 ? 0 : H(26);
            int n = 0;
            foreach (var c in row) if (c != null) n++;
            if (n == 0) return;
            int total = 0;
            foreach (var c in row) if (c != null) total += c.Width;
            total += gap * (n - 1);
            int x = Math.Max(H(14), (card.Width - total) / 2);
            foreach (var c in row)
            {
                if (c == null) continue;
                c.SetBounds(x, top + y, c.Width, h <= 0 ? c.Height : h);
                x += c.Width + gap;
            }
        }

        /// 以卡片内容区为原点摆放（像素值；逻辑尺寸用 H() 换算）。
        protected static void Put(Control c, MacCard card, int x, int y, int w, int h)
        {
            if (c == null || card == null) return;
            int top = card.Title.Length == 0 ? 0 : H(26);
            c.SetBounds(x, top + y, w <= 0 ? c.Width : w, h <= 0 ? c.Height : h);
        }

        public virtual void ApplyTheme()
        {
            Font = Theme.FontUi;
            scroll.ApplyTheme();
            Theme.Restyle(scroll.Surface);
            Invalidate();
        }

        protected MacButton Btn(string text, MacTone tone, EventHandler onClick)
        {
            var b = new MacButton();
            b.Text = text;
            b.Tone = tone;
            b.Click += onClick;
            return b;
        }

        protected MacButton IBtn(Glyph icon, MacTone tone, EventHandler onClick)
        {
            var b = new MacButton();
            b.ShowIcon = true;
            b.Icon = icon;
            b.IconSize = H(15);
            b.Tone = tone;
            b.Click += onClick;
            return b;
        }

        protected Label Lbl(string text, Font f, Color c)
        {
            var l = MacUi.Label(text, f, c);
            return l;
        }

        /// 按实测文字宽度定宽，避免中文按钮被省略号截断。
        protected static MacButton Fit(MacButton b, int extra)
        {
            int w = TextRenderer.MeasureText(b.Text ?? "", b.Font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            if (b.ShowIcon) w += (b.IconSize > 0 ? b.IconSize : H(15)) + H(6);
            b.Width = w + H(34) + extra;
            return b;
        }

        protected static MacButton Fit(MacButton b) { return Fit(b, 0); }

        protected MacSwitch Switch(bool on, EventHandler onChange)
        {
            var s = new MacSwitch();
            s.Checked = on;
            s.CheckedChanged += onChange;
            return s;
        }

        protected MacSlider Slider(int min, int max, int val, EventHandler onChange)
        {
            var s = new MacSlider();
            s.Minimum = min;
            s.Maximum = max;
            s.Value = val;
            s.ValueChanged += onChange;
            return s;
        }

        protected MacField Field(string text, string placeholder, EventHandler onChange)
        {
            var f = new MacField();
            f.Text = text;
            f.Placeholder = placeholder;
            if (onChange != null) f.TextChanged += onChange;
            return f;
        }

        protected MacNumber Num(decimal min, decimal max, decimal val, EventHandler onChange)
        {
            var n = new MacNumber();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = val;
            if (onChange != null) n.ValueChanged += onChange;
            return n;
        }
    }
}
