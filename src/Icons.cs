using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowTool
{
    /// <summary>
    /// 统一网格的线性图标族：所有符号画在 24x24 归一化网格上，
    /// 等宽描边、圆头端点，缩放到任意目标矩形。
    /// </summary>
    static class Icons
    {
        public const float Stroke = 1.8f;

        public static void Draw(Graphics g, Glyph glyph, Rectangle r, Color c)
        {
            if (r.Width < 2 || r.Height < 2) return;
            float k = Math.Min(r.Width, r.Height) / 24f;
            float ox = r.X + (r.Width - 24f * k) / 2f;
            float oy = r.Y + (r.Height - 24f * k) / 2f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(c, Stroke * Math.Max(0.6f, k)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                DrawGlyph(g, pen, glyph, k, ox, oy);
            }
        }

        static PointF Pt(float x, float y, float k, float ox, float oy)
        {
            return new PointF(ox + x * k, oy + y * k);
        }

        static void Line(Graphics g, Pen p, float x1, float y1, float x2, float y2, float k, float ox, float oy)
        {
            g.DrawLine(p, Pt(x1, y1, k, ox, oy), Pt(x2, y2, k, ox, oy));
        }

        static void Poly(Graphics g, Pen p, int[] pts, float k, float ox, float oy)
        {
            for (int i = 0; i + 3 < pts.Length; i += 2)
                g.DrawLine(p, Pt(pts[i], pts[i + 1], k, ox, oy), Pt(pts[i + 2], pts[i + 3], k, ox, oy));
        }

        static void Rect(Graphics g, Pen p, float x, float y, float w, float h, float k, float ox, float oy)
        {
            g.DrawRectangle(p, ox + x * k, oy + y * k, w * k, h * k);
        }

        static void Circle(Graphics g, Pen p, float cx, float cy, float rad, float k, float ox, float oy)
        {
            g.DrawEllipse(p, ox + (cx - rad) * k, oy + (cy - rad) * k, rad * 2 * k, rad * 2 * k);
        }

        static void Arc(Graphics g, Pen p, float cx, float cy, float rad, float a1, float a2, float k, float ox, float oy)
        {
            g.DrawArc(p, ox + (cx - rad) * k, oy + (cy - rad) * k, rad * 2 * k, rad * 2 * k, a1, a2);
        }

        static void DrawGlyph(Graphics g, Pen p, Glyph gl, float k, float ox, float oy)
        {
            switch (gl)
            {
                case Glyph.Pin:
                    // 图钉：竖轴 + 圆头
                    Line(g, p, 12, 4, 12, 13, k, ox, oy);
                    Circle(g, p, 12, 16.5f, 4.5f, k, ox, oy);
                    Line(g, p, 12, 21, 12, 23, k, ox, oy);
                    break;

                case Glyph.PinOff:
                    Line(g, p, 12, 4, 12, 10, k, ox, oy);
                    Arc(g, p, 12, 16.5f, 4.5f, 60, 280, k, ox, oy);
                    Line(g, p, 12, 21, 12, 23, k, ox, oy);
                    Line(g, p, 4, 4, 20, 20, k, ox, oy);
                    break;

                case Glyph.Ghost:
                    // 点击穿透：虚影窗口
                    Rect(g, p, 5, 5, 14, 14, k, ox, oy);
                    Line(g, p, 5, 9, 19, 9, k, ox, oy);
                    Line(g, p, 8, 3, 8, 7, k, ox, oy);
                    Line(g, p, 16, 3, 16, 7, k, ox, oy);
                    Line(g, p, 9, 14, 15, 14, k, ox, oy);
                    break;

                case Glyph.GhostOff:
                    Rect(g, p, 5, 5, 14, 14, k, ox, oy);
                    Line(g, p, 5, 9, 19, 9, k, ox, oy);
                    Line(g, p, 4, 4, 20, 20, k, ox, oy);
                    break;

                case Glyph.LayerUp:
                    Poly(g, p, new[] { 12, 4, 6, 10, 18, 10 }, k, ox, oy);
                    Poly(g, p, new[] { 6, 10, 6, 16, 18, 16, 18, 10 }, k, ox, oy);
                    Line(g, p, 9, 20, 15, 20, k, ox, oy);
                    break;

                case Glyph.LayerDown:
                    Poly(g, p, new[] { 12, 20, 6, 14, 18, 14 }, k, ox, oy);
                    Poly(g, p, new[] { 6, 14, 6, 8, 18, 8, 18, 14 }, k, ox, oy);
                    Line(g, p, 9, 4, 15, 4, k, ox, oy);
                    break;

                case Glyph.LayerBottom:
                    Poly(g, p, new[] { 12, 8, 6, 13, 18, 13 }, k, ox, oy);
                    Line(g, p, 6, 17, 18, 17, k, ox, oy);
                    Line(g, p, 6, 20.5f, 18, 20.5f, k, ox, oy);
                    break;

                case Glyph.Grid:
                    // 3x3 分屏吸附
                    Rect(g, p, 4, 4, 16, 16, k, ox, oy);
                    Line(g, p, 4, 9.33f, 20, 9.33f, k, ox, oy);
                    Line(g, p, 4, 14.66f, 20, 14.66f, k, ox, oy);
                    Line(g, p, 9.33f, 4, 9.33f, 20, k, ox, oy);
                    Line(g, p, 14.66f, 4, 14.66f, 20, k, ox, oy);
                    break;

                case Glyph.Monitor:
                    Rect(g, p, 3, 5, 18, 12, k, ox, oy);
                    Line(g, p, 12, 17, 12, 20, k, ox, oy);
                    Line(g, p, 8, 20, 16, 20, k, ox, oy);
                    break;

                case Glyph.MonitorNext:
                    Rect(g, p, 2, 6, 13, 10, k, ox, oy);
                    Rect(g, p, 16, 9, 6, 6, k, ox, oy);
                    Line(g, p, 18, 12, 20, 12, k, ox, oy);
                    break;

                case Glyph.Picture:
                    Rect(g, p, 4, 5, 16, 14, k, ox, oy);
                    Poly(g, p, new[] { 5, 17, 10, 11, 13, 14, 16, 10, 19, 17 }, k, ox, oy);
                    Circle(g, p, 9, 9, 1.6f, k, ox, oy);
                    break;

                case Glyph.Layers:
                    Poly(g, p, new[] { 12, 3, 21, 8, 12, 13, 3, 8, 12, 3 }, k, ox, oy);
                    Poly(g, p, new[] { 4, 12, 12, 17, 20, 12 }, k, ox, oy);
                    Poly(g, p, new[] { 4, 16, 12, 20, 20, 16 }, k, ox, oy);
                    break;

                case Glyph.Gear:
                    Circle(g, p, 12, 12, 3.2f, k, ox, oy);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (float)(i * Math.PI / 4);
                        float x1 = (float)(Math.Cos(a) * 6.2), y1 = (float)(Math.Sin(a) * 6.2);
                        float x2 = (float)(Math.Cos(a) * 8.6), y2 = (float)(Math.Sin(a) * 8.6);
                        Line(g, p, 12 + x1, 12 + y1, 12 + x2, 12 + y2, k, ox, oy);
                    }
                    break;

                case Glyph.Power:
                    Line(g, p, 12, 3, 12, 11, k, ox, oy);
                    Arc(g, p, 12, 12, 8, 300, 240, k, ox, oy);
                    break;

                case Glyph.Moon:
                    // 月牙
                    g.DrawArc(p, ox + 4 * k, oy + 4 * k, 16 * k, 16 * k, 40, 200);
                    g.DrawArc(p, ox + 8 * k, oy + 4 * k, 12 * k, 16 * k, 320, 180);
                    break;

                case Glyph.Sun:
                    Circle(g, p, 12, 12, 4, k, ox, oy);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (float)(i * Math.PI / 4);
                        float x1 = (float)(Math.Cos(a) * 6.6), y1 = (float)(Math.Sin(a) * 6.6);
                        float x2 = (float)(Math.Cos(a) * 8.8), y2 = (float)(Math.Sin(a) * 8.8);
                        Line(g, p, 12 + x1, 12 + y1, 12 + x2, 12 + y2, k, ox, oy);
                    }
                    break;

                case Glyph.Search:
                    Circle(g, p, 10.5f, 10.5f, 6, k, ox, oy);
                    Line(g, p, 15, 15, 20, 20, k, ox, oy);
                    break;

                case Glyph.Close:
                    Line(g, p, 6, 6, 18, 18, k, ox, oy);
                    Line(g, p, 18, 6, 6, 18, k, ox, oy);
                    break;

                case Glyph.Plus:
                    Line(g, p, 12, 5, 12, 19, k, ox, oy);
                    Line(g, p, 5, 12, 19, 12, k, ox, oy);
                    break;

                case Glyph.Minus:
                    Line(g, p, 5, 12, 19, 12, k, ox, oy);
                    break;

                case Glyph.Trash:
                    Line(g, p, 4, 7, 20, 7, k, ox, oy);
                    Poly(g, p, new[] { 9, 7, 9, 4, 15, 4, 15, 7 }, k, ox, oy);
                    Poly(g, p, new[] { 6, 7, 7, 20, 17, 20, 18, 7 }, k, ox, oy);
                    break;

                case Glyph.Crosshair:
                    Circle(g, p, 12, 12, 6.5f, k, ox, oy);
                    Line(g, p, 12, 2, 12, 6, k, ox, oy);
                    Line(g, p, 12, 18, 12, 22, k, ox, oy);
                    Line(g, p, 2, 12, 6, 12, k, ox, oy);
                    Line(g, p, 18, 12, 22, 12, k, ox, oy);
                    break;

                case Glyph.Shield:
                    Poly(g, p, new[] { 12, 3, 20, 6, 20, 12, 12, 21, 4, 12, 4, 6, 12, 3 }, k, ox, oy);
                    Poly(g, p, new[] { 9, 12, 12, 15, 16, 10 }, k, ox, oy);
                    break;

                case Glyph.Play:
                    Poly(g, p, new[] { 9, 6, 18, 12, 9, 18, 9, 6 }, k, ox, oy);
                    break;

                case Glyph.Dot:
                    using (var b = new SolidBrush(p.Color))
                        g.FillEllipse(b, ox + 9 * k, oy + 9 * k, 6 * k, 6 * k);
                    break;

                case Glyph.Command:
                    // 命令徽标：圆角方块 + 中心点（用于 logo 位）
                    Rect(g, p, 5, 5, 14, 14, k, ox, oy);
                    Line(g, p, 5, 9, 19, 9, k, ox, oy);
                    Line(g, p, 5, 15, 19, 15, k, ox, oy);
                    break;

                case Glyph.Keyboard:
                    Rect(g, p, 3, 7, 18, 10, k, ox, oy);
                    Line(g, p, 6, 10, 6, 10.01f, k, ox, oy);
                    Line(g, p, 9, 10, 9, 10.01f, k, ox, oy);
                    Line(g, p, 12, 10, 12, 10.01f, k, ox, oy);
                    Line(g, p, 15, 10, 15, 10.01f, k, ox, oy);
                    Line(g, p, 7.5f, 13.5f, 16.5f, 13.5f, k, ox, oy);
                    break;

                case Glyph.Bolt:
                    Poly(g, p, new[] { 13, 2, 6, 13, 11, 13, 9, 22, 18, 10, 12, 10, 13, 2 }, k, ox, oy);
                    break;
            }
        }
    }

    enum Glyph
    {
        Pin, PinOff, Ghost, GhostOff, LayerUp, LayerDown, LayerBottom,
        Grid, Monitor, MonitorNext, Picture, Layers, Gear, Power, Moon, Sun,
        Search, Close, Plus, Minus, Trash, Crosshair, Shield, Play, Dot, Command, Keyboard, Bolt
    }
}
