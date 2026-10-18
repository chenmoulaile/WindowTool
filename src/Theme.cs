using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WindowTool
{
    public enum AccentHue { Blue, Purple, Pink, Red, Orange, Yellow, Green }

    /// 设计令牌：颜色、字体、圆角、DPI 缩放。所有像素尺寸必须经 S/P/Sz 换算。
    static class Theme
    {
        public static int Radius = 8;
        public static bool IsDark;
        public static float Scale = 1f;
        public static AccentHue Hue = AccentHue.Blue;

        public static Color Window;      // 窗口底
        public static Color Sidebar;     // 侧栏（比窗口略亮/暗，模拟毛玻璃）
        public static Color Card;        // 卡片/输入面
        public static Color CardSunken;  // 轨道、分段控件底槽
        public static Color Hover;
        public static Color Pressed;
        public static Color Separator;
        public static Color Border;
        public static Color Text;
        public static Color TextDim;
        public static Color TextFaint;
        public static Color Accent;
        public static Color AccentSoft;  // 强调色淡底（选中行、tinted 按钮）
        public static Color OnAccent;
        public static Color Green;
        public static Color Red;
        public static Color Knob;        // 滑块/开关的圆钮

        public static Font FontUi;
        public static Font FontUiMedium;
        public static Font FontUiBold;
        public static Font FontSmall;
        public static Font FontSection;
        public static Font FontGroup;
        public static Font FontTitle;
        public static Font FontMono;

        static Theme()
        {
            BuildFonts(1f);
            ApplyLight();
        }

        // DPI 变化时重建字体：GDI 字号按物理 DPI 自动放大，但 WinForms 传入的是逻辑点，
        // 这里显式按 Scale 还原，保证与像素布局同比例。
        public static void BuildFonts(float scale)
        {
            float k = scale <= 0f ? 1f : scale;
            FontUi = F(9f, FontStyle.Regular, k);
            FontUiMedium = F(9f, FontStyle.Bold, k);
            FontUiBold = F(9.5f, FontStyle.Bold, k);
            FontSmall = F(8f, FontStyle.Regular, k);
            FontSection = F(9.5f, FontStyle.Bold, k);
            FontGroup = F(8.5f, FontStyle.Bold, k);
            FontTitle = F(13f, FontStyle.Bold, k);
            FontMono = new Font("Consolas", 8.5f * k);
        }

        static Font F(float pt, FontStyle style, float k)
        {
            foreach (var fam in new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" })
            {
                try
                {
                    var f = new Font(fam, pt * k, style);
                    if (string.Equals(f.FontFamily.Name, fam, StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch { }
            }
            return new Font(FontFamily.GenericSansSerif, pt * k, style);
        }

        /// 侧栏宽度只有一个来源：MainForm 排版、标题带起点都要跟它一致。
        public static int SidebarW { get { return S(208); } }

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static float S(float v) { return v * Scale; }
        public static Point P(int x, int y) { return new Point(S(x), S(y)); }
        public static Size Sz(int w, int h) { return new Size(S(w), S(h)); }

        public static readonly Dictionary<AccentHue, Color[]> Hues = new Dictionary<AccentHue, Color[]>
        {
            { AccentHue.Blue,   new[] { Color.FromArgb(0, 122, 255),  Color.FromArgb(10, 132, 255) } },
            { AccentHue.Purple, new[] { Color.FromArgb(88, 86, 214),  Color.FromArgb(191, 90, 242) } },
            { AccentHue.Pink,   new[] { Color.FromArgb(255, 45, 85),  Color.FromArgb(255, 55, 95) } },
            { AccentHue.Red,    new[] { Color.FromArgb(255, 59, 48),  Color.FromArgb(255, 69, 58) } },
            { AccentHue.Orange, new[] { Color.FromArgb(255, 149, 0),  Color.FromArgb(255, 159, 10) } },
            { AccentHue.Yellow, new[] { Color.FromArgb(255, 204, 0),  Color.FromArgb(255, 214, 10) } },
            { AccentHue.Green,  new[] { Color.FromArgb(52, 199, 89),  Color.FromArgb(48, 209, 88) } },
        };

        public static void SetDark(bool dark)
        {
            IsDark = dark;
            if (dark) ApplyDark(); else ApplyLight();
        }

        public static void SetHue(AccentHue hue)
        {
            Hue = hue;
            if (IsDark) ApplyDark(); else ApplyLight();
        }

        static Color AccentFor()
        {
            Color[] pair;
            if (!Hues.TryGetValue(Hue, out pair)) pair = Hues[AccentHue.Blue];
            return IsDark ? pair[1] : pair[0];
        }

        static void ApplyLight()
        {
            Window = Color.FromArgb(242, 242, 247);
            Sidebar = Color.FromArgb(236, 236, 242);
            Card = Color.White;
            CardSunken = Color.FromArgb(228, 228, 233);
            Hover = Color.FromArgb(244, 244, 248);
            Pressed = Color.FromArgb(233, 233, 239);
            Separator = Color.FromArgb(225, 225, 232);
            Border = Color.FromArgb(210, 210, 218);
            Text = Color.FromArgb(29, 29, 31);
            TextDim = Color.FromArgb(110, 110, 118);
            TextFaint = Color.FromArgb(152, 152, 160);
            Accent = AccentFor();
            AccentSoft = Blend(Accent, Color.White, 0.14f);
            OnAccent = Color.White;
            Green = Color.FromArgb(52, 199, 89);
            Red = Color.FromArgb(255, 59, 48);
            Knob = Color.White;
        }

        static void ApplyDark()
        {
            Window = Color.FromArgb(30, 30, 32);
            Sidebar = Color.FromArgb(38, 38, 41);
            Card = Color.FromArgb(46, 46, 50);
            CardSunken = Color.FromArgb(60, 60, 66);
            Hover = Color.FromArgb(58, 58, 63);
            Pressed = Color.FromArgb(70, 70, 76);
            Separator = Color.FromArgb(62, 62, 68);
            Border = Color.FromArgb(78, 78, 84);
            Text = Color.FromArgb(245, 245, 247);
            TextDim = Color.FromArgb(152, 152, 158);
            TextFaint = Color.FromArgb(110, 110, 118);
            Accent = AccentFor();
            AccentSoft = Blend(Accent, Color.FromArgb(46, 46, 50), 0.24f);
            OnAccent = Color.White;
            Green = Color.FromArgb(48, 209, 88);
            Red = Color.FromArgb(255, 69, 58);
            Knob = Color.FromArgb(235, 235, 238);
        }

        public static Color Blend(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int a) { return Color.FromArgb(a, c.R, c.G, c.B); }

        public static Color HoverOf(Color c) { return Blend(c, IsDark ? Color.White : Color.Black, 0.06f); }
        public static Color PressedOf(Color c) { return Blend(c, IsDark ? Color.White : Color.Black, 0.13f); }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            return RoundRectF(r.X, r.Y, r.Width, r.Height, radius);
        }

        /// float 版圆角路径：矢量图标与渐变块需要亚像素定位，取整会让小尺寸图标变形。
        public static GraphicsPath RoundRectF(RectangleF r, float radius)
        {
            return RoundRectF(r.X, r.Y, r.Width, r.Height, radius);
        }

        static GraphicsPath RoundRectF(float x, float y, float w, float h, float radius)
        {
            var p = new GraphicsPath();
            float rad = radius;
            float max = Math.Max(0f, Math.Min(w, h) / 2f);
            if (rad > max) rad = max;
            if (rad < 1f || w < 2f || h < 2f)
            {
                p.AddRectangle(new RectangleF(x, y, w, h));
                return p;
            }
            float d = rad * 2f;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// 上亮下暗的渐变填充：图标块靠它获得 iOS 式的一点厚度。
        public static void FillGradient(Graphics g, GraphicsPath path, RectangleF r, Color top, Color bottom)
        {
            using (var b = new LinearGradientBrush(
                new RectangleF(r.X, r.Y, Math.Max(1f, r.Width), Math.Max(1f, r.Height)), top, bottom, 90f))
                g.FillPath(b, path);
        }

        public static Color Lighten(Color c, float t) { return Blend(c, Color.White, t); }
        public static Color Darken(Color c, float t) { return Blend(c, Color.Black, t); }

        // 只有左上/右上（或左下/右下）圆角的路径，用于卡片分组的首/尾行
        public static GraphicsPath RoundRectSides(Rectangle r, int radius, bool top, bool bottom)
        {
            return RoundRectSidesF(r.X, r.Y, r.Width, r.Height, radius, top, bottom);
        }

        public static GraphicsPath RoundRectSidesF(RectangleF r, float radius, bool top, bool bottom)
        {
            return RoundRectSidesF(r.X, r.Y, r.Width, r.Height, radius, top, bottom);
        }

        static GraphicsPath RoundRectSidesF(float x, float y, float w, float h, float radius, bool top, bool bottom)
        {
            var p = new GraphicsPath();
            float rad = Math.Min(radius, Math.Min(w / 2f, h / 2f));
            if (rad < 1f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
            float d = rad * 2f;
            if (top && bottom) return RoundRectF(x, y, w, h, radius);
            if (top)
            {
                p.AddArc(x, y, d, d, 180, 90);
                p.AddArc(x + w - d, y, d, d, 270, 90);
                p.AddLine(x + w, y + h, x, y + h);
            }
            else
            {
                p.AddLine(x, y, x + w, y);
                p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                p.AddArc(x, y + h - d, d, d, 90, 90);
            }
            p.CloseFigure();
            return p;
        }

        public static int CornerRadius(int w, int h)
        {
            int max = Math.Max(0, Math.Min(w, h) / 2 - 1);
            int r = S(Radius);
            return r > max ? max : r;
        }

        // 自绘圆角控件需要不透明底色擦四角，否则透出父控件残影
        public static Color SolidBack(Control c)
        {
            Control p = c.Parent;
            while (p != null && p.BackColor.A == 0) p = p.Parent;
            return p == null ? Window : p.BackColor;
        }

        public static void Hint(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static void FillPath(Graphics g, GraphicsPath path, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
        }

        public static void StrokePath(Graphics g, GraphicsPath path, Color c, float width)
        {
            using (var p = new Pen(c, width)) g.DrawPath(p, path);
        }

        /// 投影层数开关：多层叠加是整页重绘里最贵的一项，低画质/低端机可以砍掉。
        public static int ShadowQuality = 100;   // 0~100，按百分比缩减层数

        // macOS 式柔和投影：多层半透明圆角叠加
        public static void Shadow(Graphics g, Rectangle r, int radius, int spread, Color tint)
        {
            if (ShadowQuality < 100)
                spread = spread * Math.Max(0, ShadowQuality) / 100;
            if (spread <= 0) return;
            for (int i = spread; i >= 1; i--)
            {
                var rr = new Rectangle(r.X - i, r.Y - i + 1, r.Width + i * 2, r.Height + i * 2);
                int a = (int)Math.Max(6, 26 - i * 4);
                using (var p = RoundRect(rr, radius + i))
                using (var b = new SolidBrush(Color.FromArgb(a, tint)))
                    g.FillPath(b, p);
            }
        }

        const TextFormatFlags Measure = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        /// 省略号预算必须与绘制时的 NoPadding 度量一致，否则中文会被无谓截断。
        public static string Ellipsis(string s, Font f, int maxWidth)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (TextRenderer.MeasureText(s, f, Size.Empty, Measure).Width <= maxWidth) return s;
            for (int n = s.Length - 1; n > 0; n--)
            {
                string cand = s.Substring(0, n) + "…";
                if (TextRenderer.MeasureText(cand, f, Size.Empty, Measure).Width <= maxWidth) return cand;
            }
            return "…";
        }

        // 主题切换时递归刷新所有自绘控件
        public static void Restyle(Control root)
        {
            root.Invalidate(true);
            foreach (Control c in root.Controls)
            {
                var themed = c as IThemed;
                if (themed != null) themed.ApplyTheme();
                Restyle(c);
            }
        }
    }

    interface IThemed { void ApplyTheme(); }
}
