using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>
    /// 主题中枢：暗色优先的现代工具风（类 VS Code），
    /// 同时保留浅色档与跟随系统档。全部颜色集中在这里，控件只取不配。
    /// </summary>
    static class Theme
    {
        // ---------------- DPI ----------------
        public static float Scale = 1f;
        public static int S(int v) { return Math.Max(1, (int)Math.Round(v * Scale)); }
        public static Size Sz(int w, int h) { return new Size(S(w), S(h)); }

        // ---------------- 强调色 ----------------
        // 每个提供「填充」（按钮/开关/选中态底色，上有白字）与
        // 「文字」（链接/高亮文字，对深底可读）两个变体。
        public static AccentHue Hue = AccentHue.Azure;

        // ---------------- 调色板（暗色档） ----------------
        // 基底走 VS Code Dark+ 的层次：最深的窗体外壳、略浅的导航栏、再浅的内容卡片。
        static readonly Color DShell      = Color.FromArgb(0x12, 0x13, 0x17);
        static readonly Color DRail       = Color.FromArgb(0x17, 0x18, 0x1D);
        static readonly Color DCard       = Color.FromArgb(0x1E, 0x1F, 0x25);
        static readonly Color DCardHi     = Color.FromArgb(0x26, 0x27, 0x2E);
        static readonly Color DBorder     = Color.FromArgb(0x33, 0x35, 0x3D);
        static readonly Color DText       = Color.FromArgb(0xD4, 0xD7, 0xDC);
        static readonly Color DTextDim    = Color.FromArgb(0x8B, 0x8F, 0x98);
        static readonly Color DTextFaint  = Color.FromArgb(0x5C, 0x60, 0x69);
        static readonly Color DInputBg    = Color.FromArgb(0x14, 0x15, 0x19);

        // ---------------- 调色板（浅色档） ----------------
        static readonly Color LShell      = Color.FromArgb(0xF2, 0xF3, 0xF5);
        static readonly Color LRail       = Color.FromArgb(0xEA, 0xEB, 0xEE);
        static readonly Color LCard       = Color.FromArgb(0xFF, 0xFF, 0xFF);
        static readonly Color LCardHi     = Color.FromArgb(0xF4, 0xF5, 0xF7);
        static readonly Color LBorder     = Color.FromArgb(0xD5, 0xD8, 0xDD);
        static readonly Color LText       = Color.FromArgb(0x24, 0x26, 0x2B);
        static readonly Color LTextDim    = Color.FromArgb(0x63, 0x67, 0x6E);
        static readonly Color LTextFaint  = Color.FromArgb(0x9A, 0x9E, 0xA6);
        static readonly Color LInputBg    = Color.FromArgb(0xFA, 0xFA, 0xFB);

        public static bool IsDark = true;

        // ---------------- 语义色 ----------------
        public static Color Good    { get { return IsDark ? Color.FromArgb(0x3F, 0xC1, 0x8B) : Color.FromArgb(0x14, 0x7D, 0x54); } }
        public static Color Warn    { get { return IsDark ? Color.FromArgb(0xE0, 0xA9, 0x3E) : Color.FromArgb(0x9A, 0x5B, 0x00); } }
        public static Color Bad     { get { return IsDark ? Color.FromArgb(0xE7, 0x6A, 0x5E) : Color.FromArgb(0xBE, 0x2F, 0x28); } }

        // ---------------- 派生色 ----------------
        public static Color Shell    { get { return IsDark ? DShell : LShell; } }
        public static Color Rail     { get { return IsDark ? DRail : LRail; } }
        public static Color Card     { get { return IsDark ? DCard : LCard; } }
        public static Color CardHi   { get { return IsDark ? DCardHi : LCardHi; } }
        public static Color Border   { get { return IsDark ? DBorder : LBorder; } }
        public static Color Text     { get { return IsDark ? DText : LText; } }
        public static Color TextDim  { get { return IsDark ? DTextDim : LTextDim; } }
        public static Color TextFaint{ get { return IsDark ? DTextFaint : LTextFaint; } }
        public static Color InputBg  { get { return IsDark ? DInputBg : LInputBg; } }

        // 强调色：填充变体（实心块）/ 文字变体（深底上的彩色文字）
        public static Color Accent      { get { return AccentFill(Hue); } }
        public static Color AccentSoft  { get { return IsDark ? Alpha(AccentFill(Hue), 42) : Alpha(AccentFill(Hue), 34); } }
        public static Color AccentText  { get { return IsDark ? AccentTextDark(Hue) : AccentFill(Hue); } }

        public static Color Alpha(Color c, int a) { return Color.FromArgb(a, c); }

        static Color AccentFill(AccentHue h)
        {
            switch (h)
            {
                case AccentHue.Jade:    return Color.FromArgb(0x2F, 0x9E, 0x77);
                case AccentHue.Violet:  return Color.FromArgb(0x7C, 0x6C, 0xE0);
                case AccentHue.Terracotta: return Color.FromArgb(0xD1, 0x5C, 0x4B);
                case AccentHue.Moss:    return Color.FromArgb(0x71, 0x8D, 0x3F);
                default:                return Color.FromArgb(0x37, 0x74, 0xD9);
            }
        }

        static Color AccentTextDark(AccentHue h)
        {
            // 暗底上的强调文字需要提亮一档，避免 4.x:1 的边线对比
            switch (h)
            {
                case AccentHue.Jade:    return Color.FromArgb(0x53, 0xC9, 0x9C);
                case AccentHue.Violet:  return Color.FromArgb(0x9A, 0x8D, 0xF0);
                case AccentHue.Terracotta: return Color.FromArgb(0xE8, 0x84, 0x72);
                case AccentHue.Moss:    return Color.FromArgb(0x9C, 0xB5, 0x66);
                default:                return Color.FromArgb(0x62, 0x9B, 0xF2);
            }
        }

        // ---------------- 字体 ----------------
        static Font body, bodyBold, small, title, mono;
        public static Font FontBody     { get { return body ?? (body = New("Microsoft YaHei UI", 9f)); } }
        public static Font FontBodyBold { get { return bodyBold ?? (bodyBold = New("Microsoft YaHei UI", 9f, FontStyle.Bold)); } }
        public static Font FontSmall    { get { return small ?? (small = New("Microsoft YaHei UI", 8.25f)); } }
        public static Font FontTitle    { get { return title ?? (title = New("Microsoft YaHei UI", 12f, FontStyle.Bold)); } }
        public static Font FontMono     { get { return mono ?? (mono = New("Consolas", 9f)); } }

        static Font New(string family, float pt, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(family, pt, style, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, pt, style, GraphicsUnit.Point); }
        }

        // ---------------- 切换 ----------------
        public static void SetDark(bool dark) { IsDark = dark; }

        public static void SetHue(AccentHue h) { Hue = h; }

        /// 通知所有控件按当前主题重画（颜色全部即时取自本类，重画即生效）。
        public static void RestyleAll(Control c)
        {
            if (c == null) return;
            var tb = c as TextBoxBase;
            if (tb != null)
            {
                tb.BackColor = InputBg;
                tb.ForeColor = Text;
            }
            else c.BackColor = Shell;
            foreach (Control child in c.Controls) RestyleAll(child);
            c.Invalidate(true);
        }

        // ---------------- 绘制辅助 ----------------

        public static void FillRound(Graphics g, Rectangle r, Color fill, int radius)
        {
            if (radius < 1 || r.Width < 2 || r.Height < 2) { using (var b = new SolidBrush(fill)) g.FillRectangle(b, r); return; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            using (var path = RoundedPath(r, d))
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        }

        public static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r, int d)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) { p.AddRectangle(new Rectangle(0, 0, 1, 1)); return p; }
            int w = Math.Max(0, r.Width), h = Math.Max(0, r.Height);
            int dd = Math.Min(d, Math.Min(w, h));
            p.AddArc(r.X, r.Y, dd, dd, 180, 90);
            p.AddArc(r.X + w - dd, r.Y, dd, dd, 270, 90);
            p.AddArc(r.X + w - dd, r.Y + h - dd, dd, dd, 0, 90);
            p.AddArc(r.X, r.Y + h - dd, dd, dd, 90, 90);
            p.CloseFigure();
            return p;
        }

        static Pen borderPen;
        static Pen BorderPen
        {
            get
            {
                // 主题切换时 Border 变色，按颜色判断是否需要重建
                if (borderPen == null || borderPen.Color != Border)
                {
                    if (borderPen != null) borderPen.Dispose();
                    borderPen = new Pen(Border);
                }
                return borderPen;
            }
        }

        public static void RoundRect(Graphics g, Rectangle r, int radius)
        {
            if (radius < 1 || r.Width < 2 || r.Height < 2) { g.DrawRectangle(BorderPen, r); return; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            using (var path = RoundedPath(r, d))
                g.DrawPath(BorderPen, path);
        }

        public static string Ellipsis(string s, Font f, int maxPx)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var size = TextRenderer.MeasureText(s, f, Size.Empty, flags);
            if (size.Width <= maxPx) return s;
            // 二分截断，避免逐字符删减的 O(n²)
            int lo = 0, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (TextRenderer.MeasureText(s.Substring(0, mid) + "…", f, Size.Empty, flags).Width <= maxPx) lo = mid;
                else hi = mid - 1;
            }
            return s.Substring(0, lo) + "…";
        }
    }

    enum AccentHue
    {
        Azure = 0,
        Jade = 1,
        Violet = 2,
        Terracotta = 3,
        Moss = 4
    }
}
