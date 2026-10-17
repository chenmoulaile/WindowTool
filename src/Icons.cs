using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowTool
{
    enum Glyph
    {
        Pin, PinSlash, Layers, Droplet, Picture, Film, Music, Text, Shield, Box, Gear,
        Search, Play, Pause, Prev, Next, Shuffle, Repeat, Heart, Volume,
        ChevronL, ChevronR, ChevronD, Close, Minus, Plus, Enlarge, Check,
        Target, Folder, Refresh, Tray, Stop, Eject, Wave, Sliders, Info, Warning, Clock,
        Moon, Sun, Power, Window, Grid, Bolt
    }

    /// 线性矢量图标族：全部画在同一套归一化网格上，描边等宽、圆角端点、留白一致，
    /// 这样 15px 与 30px 下看起来才是同一族图标，而不是混了三种风格。
    static class Icons
    {
        public static void Draw(Graphics g, Glyph kind, RectangleF box, Color color)
        {
            if (box.Width < 2f || box.Height < 2f) return;
            float u = Math.Min(box.Width, box.Height);
            float sw = u * 0.082f;
            if (sw < 1.1f) sw = 1.1f;
            if (sw > 3.4f) sw = 3.4f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, sw))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                using (var fill = new SolidBrush(color))
                    Paint(g, kind, box, pen, fill, u);
            }
        }

        static RectangleF R(RectangleF b, float dx, float dy, float dw, float dh)
        {
            return new RectangleF(b.X + dx * b.Width, b.Y + dy * b.Height, dw * b.Width, dh * b.Height);
        }

        static void RoundFrame(Graphics g, Pen pen, RectangleF r, float u)
        {
            using (var p = Theme.RoundRectF(r, u * 0.16f)) g.DrawPath(pen, p);
        }

        static void Paint(Graphics g, Glyph k, RectangleF b, Pen pen, Brush fill, float u)
        {
            float x = b.X, y = b.Y, w = b.Width, h = b.Height;
            float cx = x + w / 2f, cy = y + h / 2f;

            switch (k)
            {
                case Glyph.Pin:
                {
                    using (var path = new GraphicsPath())
                    {
                        var head = R(b, 0.22f, 0.1f, 0.56f, 0.44f);
                        path.AddArc(head, 180, 180);
                        path.AddLine(head.Left, head.Top + head.Height / 2f, head.Left, y + h * 0.6f);
                        path.AddLine(head.Left, y + h * 0.6f, x + w * 0.44f, y + h * 0.66f);
                        path.AddLine(x + w * 0.44f, y + h * 0.66f, x + w * 0.56f, y + h * 0.66f);
                        path.AddLine(x + w * 0.56f, y + h * 0.66f, head.Right, y + h * 0.6f);
                        path.AddLine(head.Right, y + h * 0.6f, head.Right, head.Top + head.Height / 2f);
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    g.DrawLine(pen, cx, y + h * 0.66f, cx, y + h * 0.9f);
                    break;
                }
                case Glyph.PinSlash:
                    Paint(g, Glyph.Pin, b, pen, fill, u);
                    g.DrawLine(pen, x + w * 0.18f, y + h * 0.82f, x + w * 0.82f, y + h * 0.18f);
                    break;

                case Glyph.Layers:
                {
                    g.DrawPolygon(pen, new[] {
                        new PointF(cx, y + h * 0.08f), new PointF(x + w * 0.86f, y + h * 0.3f),
                        new PointF(cx, y + h * 0.52f), new PointF(x + w * 0.14f, y + h * 0.3f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.14f, y + h * 0.5f), new PointF(cx, y + h * 0.72f),
                        new PointF(x + w * 0.86f, y + h * 0.5f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.14f, y + h * 0.68f), new PointF(cx, y + h * 0.9f),
                        new PointF(x + w * 0.86f, y + h * 0.68f) });
                    break;
                }

                case Glyph.Droplet:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(cx, y + h * 0.08f, x + w * 0.2f, y + h * 0.42f,
                            x + w * 0.16f, y + h * 0.92f, cx, y + h * 0.92f);
                        path.AddBezier(cx, y + h * 0.92f, x + w * 0.84f, y + h * 0.92f,
                            x + w * 0.8f, y + h * 0.42f, cx, y + h * 0.08f);
                        g.DrawPath(pen, path);
                    }
                    break;
                }

                case Glyph.Picture:
                {
                    var frame = R(b, 0.08f, 0.14f, 0.84f, 0.72f);
                    RoundFrame(g, pen, frame, u);
                    g.DrawEllipse(pen, frame.X + frame.Width * 0.14f, frame.Y + frame.Height * 0.16f,
                        u * 0.13f, u * 0.13f);
                    g.DrawLines(pen, new[] {
                        new PointF(frame.Left + u * 0.08f, frame.Bottom - u * 0.14f),
                        new PointF(frame.Left + frame.Width * 0.36f, frame.Top + frame.Height * 0.52f),
                        new PointF(frame.Left + frame.Width * 0.56f, frame.Bottom - u * 0.2f),
                        new PointF(frame.Left + frame.Width * 0.72f, frame.Top + frame.Height * 0.36f),
                        new PointF(frame.Right - u * 0.08f, frame.Bottom - u * 0.14f) });
                    break;
                }

                case Glyph.Window:
                {
                    var frame = R(b, 0.08f, 0.12f, 0.84f, 0.76f);
                    RoundFrame(g, pen, frame, u);
                    float bar = frame.Top + frame.Height * 0.3f;
                    g.DrawLine(pen, frame.Left, bar, frame.Right, bar);
                    float d = u * 0.06f;
                    for (int i = 0; i < 3; i++)
                        g.FillEllipse(fill, frame.Left + u * 0.1f + i * d * 2.2f,
                            frame.Top + frame.Height * 0.15f - d / 2f, d, d);
                    break;
                }

                case Glyph.Film:
                {
                    var frame = R(b, 0.1f, 0.12f, 0.8f, 0.76f);
                    RoundFrame(g, pen, frame, u);
                    float l = frame.Left + frame.Width * 0.26f;
                    float r2 = frame.Left + frame.Width * 0.74f;
                    g.DrawLine(pen, l, frame.Top, l, frame.Bottom);
                    g.DrawLine(pen, r2, frame.Top, r2, frame.Bottom);
                    float hs = u * 0.08f;
                    for (int i = 0; i < 3; i++)
                    {
                        float hy = frame.Top + frame.Height * (0.18f + i * 0.3f);
                        g.FillRectangle(fill, frame.Left + frame.Width * 0.06f, hy, hs, hs);
                        g.FillRectangle(fill, frame.Right - frame.Width * 0.06f - hs, hy, hs, hs);
                    }
                    break;
                }

                case Glyph.Music:
                {
                    g.FillEllipse(fill, x + w * 0.14f, y + h * 0.62f, u * 0.26f, u * 0.21f);
                    g.FillEllipse(fill, x + w * 0.6f, y + h * 0.7f, u * 0.26f, u * 0.21f);
                    g.DrawLine(pen, x + w * 0.37f, y + h * 0.72f, x + w * 0.37f, y + h * 0.2f);
                    g.DrawLine(pen, x + w * 0.83f, y + h * 0.8f, x + w * 0.83f, y + h * 0.28f);
                    using (var beam = new Pen(pen.Color, u * 0.13f))
                    {
                        beam.StartCap = LineCap.Round;
                        beam.EndCap = LineCap.Round;
                        g.DrawLine(beam, x + w * 0.37f, y + h * 0.22f, x + w * 0.83f, y + h * 0.3f);
                    }
                    break;
                }

                case Glyph.Text:
                    g.DrawLine(pen, x + w * 0.16f, y + h * 0.26f, x + w * 0.84f, y + h * 0.26f);
                    g.DrawLine(pen, x + w * 0.16f, y + h * 0.5f, x + w * 0.68f, y + h * 0.5f);
                    g.DrawLine(pen, x + w * 0.16f, y + h * 0.74f, x + w * 0.52f, y + h * 0.74f);
                    break;

                case Glyph.Shield:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[] {
                            new PointF(cx, y + h * 0.08f), new PointF(x + w * 0.86f, y + h * 0.22f),
                            new PointF(x + w * 0.86f, cy), new PointF(cx, y + h * 0.94f),
                            new PointF(x + w * 0.14f, cy), new PointF(x + w * 0.14f, y + h * 0.22f) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    g.DrawLine(pen, cx, y + h * 0.34f, cx, y + h * 0.56f);
                    g.FillEllipse(fill, cx - u * 0.045f, y + h * 0.66f, u * 0.09f, u * 0.09f);
                    break;
                }

                case Glyph.Warning:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[] {
                            new PointF(cx, y + h * 0.1f), new PointF(x + w * 0.93f, y + h * 0.87f),
                            new PointF(x + w * 0.07f, y + h * 0.87f) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    g.DrawLine(pen, cx, y + h * 0.4f, cx, y + h * 0.64f);
                    g.FillEllipse(fill, cx - u * 0.05f, y + h * 0.73f, u * 0.1f, u * 0.1f);
                    break;
                }

                case Glyph.Box:
                {
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.5f, y + h * 0.08f), new PointF(x + w * 0.88f, y + h * 0.29f),
                        new PointF(x + w * 0.88f, y + h * 0.71f), new PointF(x + w * 0.5f, y + h * 0.92f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.5f, y + h * 0.08f), new PointF(x + w * 0.12f, y + h * 0.29f),
                        new PointF(x + w * 0.12f, y + h * 0.71f), new PointF(x + w * 0.5f, y + h * 0.92f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.12f, y + h * 0.29f), new PointF(cx, y + h * 0.5f),
                        new PointF(x + w * 0.88f, y + h * 0.29f) });
                    g.DrawLine(pen, cx, y + h * 0.5f, cx, y + h * 0.92f);
                    break;
                }

                case Glyph.Gear:
                {
                    float ro = u * 0.29f, ri = u * 0.13f;
                    g.DrawEllipse(pen, cx - ro, cy - ro, ro * 2, ro * 2);
                    g.DrawEllipse(pen, cx - ri, cy - ri, ri * 2, ri * 2);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4.0;
                        float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                        g.DrawLine(pen, cx + dx * ro * 0.94f, cy + dy * ro * 0.94f,
                            cx + dx * u * 0.43f, cy + dy * u * 0.43f);
                    }
                    break;
                }

                case Glyph.Search:
                {
                    float d = u * 0.46f;
                    g.DrawEllipse(pen, x + w * 0.14f, y + h * 0.14f, d, d);
                    g.DrawLine(pen, x + w * 0.14f + d * 0.86f, y + h * 0.14f + d * 0.86f,
                        x + w * 0.86f, y + h * 0.86f);
                    break;
                }

                case Glyph.Play:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[] {
                            new PointF(x + w * 0.3f, y + h * 0.16f), new PointF(x + w * 0.84f, cy),
                            new PointF(x + w * 0.3f, y + h * 0.84f) });
                        path.CloseFigure();
                        g.FillPath(fill, path);
                    }
                    break;
                }
                case Glyph.Pause:
                    g.FillRectangle(fill, x + w * 0.3f, y + h * 0.2f, u * 0.13f, u * 0.6f);
                    g.FillRectangle(fill, x + w * 0.58f, y + h * 0.2f, u * 0.13f, u * 0.6f);
                    break;
                case Glyph.Stop:
                {
                    var sq = R(b, 0.24f, 0.24f, 0.52f, 0.52f);
                    using (var p = Theme.RoundRectF(sq, u * 0.1f)) g.FillPath(fill, p);
                    break;
                }
                case Glyph.Prev:
                    g.FillPolygon(fill, new[] {
                        new PointF(x + w * 0.68f, y + h * 0.2f), new PointF(x + w * 0.32f, cy),
                        new PointF(x + w * 0.68f, y + h * 0.8f) });
                    g.FillRectangle(fill, x + w * 0.22f, y + h * 0.22f, u * 0.08f, u * 0.56f);
                    break;
                case Glyph.Next:
                    g.FillPolygon(fill, new[] {
                        new PointF(x + w * 0.32f, y + h * 0.2f), new PointF(x + w * 0.68f, cy),
                        new PointF(x + w * 0.32f, y + h * 0.8f) });
                    g.FillRectangle(fill, x + w * 0.7f, y + h * 0.22f, u * 0.08f, u * 0.56f);
                    break;
                case Glyph.Shuffle:
                {
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.1f, y + h * 0.24f), new PointF(x + w * 0.32f, y + h * 0.24f),
                        new PointF(x + w * 0.68f, y + h * 0.76f), new PointF(x + w * 0.9f, y + h * 0.76f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.1f, y + h * 0.76f), new PointF(x + w * 0.32f, y + h * 0.76f),
                        new PointF(x + w * 0.68f, y + h * 0.24f), new PointF(x + w * 0.9f, y + h * 0.24f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.78f, y + h * 0.64f), new PointF(x + w * 0.9f, y + h * 0.76f),
                        new PointF(x + w * 0.78f, y + h * 0.88f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.78f, y + h * 0.12f), new PointF(x + w * 0.9f, y + h * 0.24f),
                        new PointF(x + w * 0.78f, y + h * 0.36f) });
                    break;
                }
                case Glyph.Repeat:
                {
                    var arc = R(b, 0.16f, 0.26f, 0.68f, 0.46f);
                    g.DrawArc(pen, arc, 190, 160);
                    g.DrawArc(pen, arc, 10, 160);
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.1f, y + h * 0.44f), new PointF(x + w * 0.17f, y + h * 0.3f),
                        new PointF(x + w * 0.26f, y + h * 0.44f) });
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.74f, y + h * 0.56f), new PointF(x + w * 0.83f, y + h * 0.7f),
                        new PointF(x + w * 0.9f, y + h * 0.56f) });
                    break;
                }
                case Glyph.Heart:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(cx, y + h * 0.82f, x + w * 0.06f, y + h * 0.5f,
                            x + w * 0.14f, y + h * 0.12f, cx, y + h * 0.32f);
                        path.AddBezier(cx, y + h * 0.82f, x + w * 0.94f, y + h * 0.5f,
                            x + w * 0.86f, y + h * 0.12f, cx, y + h * 0.32f);
                        g.DrawPath(pen, path);
                    }
                    break;
                }
                case Glyph.Volume:
                {
                    g.FillPolygon(fill, new[] {
                        new PointF(x + w * 0.1f, y + h * 0.38f), new PointF(x + w * 0.3f, y + h * 0.38f),
                        new PointF(x + w * 0.5f, y + h * 0.18f), new PointF(x + w * 0.5f, y + h * 0.82f),
                        new PointF(x + w * 0.3f, y + h * 0.62f), new PointF(x + w * 0.1f, y + h * 0.62f) });
                    g.DrawArc(pen, R(b, 0.46f, 0.3f, 0.24f, 0.4f), -52, 104);
                    g.DrawArc(pen, R(b, 0.56f, 0.16f, 0.36f, 0.68f), -46, 92);
                    break;
                }

                case Glyph.ChevronL: g.DrawLines(pen, new[] { new PointF(x + w * 0.62f, y + h * 0.2f), new PointF(x + w * 0.38f, cy), new PointF(x + w * 0.62f, y + h * 0.8f) }); break;
                case Glyph.ChevronR: g.DrawLines(pen, new[] { new PointF(x + w * 0.38f, y + h * 0.2f), new PointF(x + w * 0.62f, cy), new PointF(x + w * 0.38f, y + h * 0.8f) }); break;
                case Glyph.ChevronD: g.DrawLines(pen, new[] { new PointF(x + w * 0.2f, y + h * 0.38f), new PointF(cx, y + h * 0.62f), new PointF(x + w * 0.8f, y + h * 0.38f) }); break;
                case Glyph.Close: g.DrawLine(pen, x + w * 0.27f, y + h * 0.27f, x + w * 0.73f, y + h * 0.73f); g.DrawLine(pen, x + w * 0.73f, y + h * 0.27f, x + w * 0.27f, y + h * 0.73f); break;
                case Glyph.Minus: g.DrawLine(pen, x + w * 0.24f, cy, x + w * 0.76f, cy); break;
                case Glyph.Plus: g.DrawLine(pen, x + w * 0.24f, cy, x + w * 0.76f, cy); g.DrawLine(pen, cx, y + h * 0.24f, cx, y + h * 0.76f); break;
                case Glyph.Enlarge:
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.54f, y + h * 0.18f), new PointF(x + w * 0.82f, y + h * 0.18f), new PointF(x + w * 0.82f, y + h * 0.46f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.46f, y + h * 0.82f), new PointF(x + w * 0.18f, y + h * 0.82f), new PointF(x + w * 0.18f, y + h * 0.54f) });
                    break;
                case Glyph.Check: g.DrawLines(pen, new[] { new PointF(x + w * 0.2f, cy), new PointF(x + w * 0.42f, y + h * 0.74f), new PointF(x + w * 0.82f, y + h * 0.26f) }); break;

                case Glyph.Target:
                    g.DrawEllipse(pen, cx - u * 0.3f, cy - u * 0.3f, u * 0.6f, u * 0.6f);
                    g.FillEllipse(fill, cx - u * 0.07f, cy - u * 0.07f, u * 0.14f, u * 0.14f);
                    g.DrawLine(pen, cx, y + h * 0.04f, cx, y + h * 0.14f);
                    g.DrawLine(pen, cx, y + h * 0.86f, cx, y + h * 0.96f);
                    g.DrawLine(pen, x + w * 0.04f, cy, x + w * 0.14f, cy);
                    g.DrawLine(pen, x + w * 0.86f, cy, x + w * 0.96f, cy);
                    break;

                case Glyph.Folder:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[] {
                            new PointF(x + w * 0.1f, y + h * 0.78f), new PointF(x + w * 0.1f, y + h * 0.22f),
                            new PointF(x + w * 0.4f, y + h * 0.22f), new PointF(x + w * 0.48f, y + h * 0.34f),
                            new PointF(x + w * 0.9f, y + h * 0.34f), new PointF(x + w * 0.9f, y + h * 0.78f) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    break;
                }

                case Glyph.Refresh:
                {
                    // 环形箭头：缺口朝右上，箭头用实心三角而不是折线，小尺寸下才不会糊成一个钩子
                    float d = u * 0.62f;
                    var ring = new RectangleF(cx - d / 2f, cy - d / 2f, d, d);
                    g.DrawArc(pen, ring, 300, 236);
                    float ex = cx + d / 2f * 0.87f, ey = cy - d / 2f * 0.5f;
                    g.FillPolygon(fill, new[] {
                        new PointF(ex + u * 0.16f, ey - u * 0.02f),
                        new PointF(ex - u * 0.06f, ey - u * 0.16f),
                        new PointF(ex - u * 0.02f, ey + u * 0.16f) });
                    break;
                }

                case Glyph.Tray:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[] {
                            new PointF(x + w * 0.1f, y + h * 0.3f), new PointF(x + w * 0.36f, y + h * 0.3f),
                            new PointF(cx, y + h * 0.52f), new PointF(x + w * 0.64f, y + h * 0.3f),
                            new PointF(x + w * 0.9f, y + h * 0.3f), new PointF(x + w * 0.9f, y + h * 0.72f),
                            new PointF(x + w * 0.1f, y + h * 0.72f) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    break;
                }

                case Glyph.Eject:
                    g.FillPolygon(fill, new[] {
                        new PointF(cx, y + h * 0.16f), new PointF(x + w * 0.74f, y + h * 0.56f),
                        new PointF(x + w * 0.26f, y + h * 0.56f) });
                    g.FillRectangle(fill, x + w * 0.26f, y + h * 0.68f, u * 0.48f, u * 0.12f);
                    break;

                case Glyph.Wave:
                    for (int i = 0; i < 5; i++)
                    {
                        float px = x + w * (0.16f + i * 0.17f);
                        float amp = u * (i % 2 == 0 ? 0.28f : 0.13f);
                        g.DrawLine(pen, px, cy - amp, px, cy + amp);
                    }
                    break;

                case Glyph.Sliders:
                    g.DrawLine(pen, x + w * 0.14f, y + h * 0.32f, x + w * 0.86f, y + h * 0.32f);
                    g.DrawLine(pen, x + w * 0.14f, y + h * 0.68f, x + w * 0.86f, y + h * 0.68f);
                    float kr = u * 0.11f;
                    g.FillEllipse(fill, x + w * 0.62f - kr, y + h * 0.32f - kr, kr * 2, kr * 2);
                    g.FillEllipse(fill, x + w * 0.32f - kr, y + h * 0.68f - kr, kr * 2, kr * 2);
                    break;

                case Glyph.Info:
                    g.DrawEllipse(pen, cx - u * 0.34f, cy - u * 0.34f, u * 0.68f, u * 0.68f);
                    g.DrawLine(pen, cx, y + h * 0.44f, cx, y + h * 0.7f);
                    g.FillEllipse(fill, cx - u * 0.05f, y + h * 0.27f, u * 0.1f, u * 0.1f);
                    break;

                case Glyph.Clock:
                    g.DrawEllipse(pen, cx - u * 0.34f, cy - u * 0.34f, u * 0.68f, u * 0.68f);
                    g.DrawLine(pen, cx, cy, cx, y + h * 0.3f);
                    g.DrawLine(pen, cx, cy, x + w * 0.68f, cy);
                    break;

                case Glyph.Moon:
                {
                    // 月牙：外圆按奇偶填充，挖掉偏右上的内圆
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(cx - u * 0.36f, cy - u * 0.36f, u * 0.72f, u * 0.72f);
                        path.AddEllipse(cx - u * 0.02f, cy - u * 0.46f, u * 0.66f, u * 0.66f);
                        path.FillMode = FillMode.Alternate;
                        g.FillPath(fill, path);
                    }
                    break;
                }

                case Glyph.Sun:
                {
                    g.DrawEllipse(pen, cx - u * 0.19f, cy - u * 0.19f, u * 0.38f, u * 0.38f);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4.0;
                        float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                        g.DrawLine(pen, cx + dx * u * 0.3f, cy + dy * u * 0.3f,
                            cx + dx * u * 0.44f, cy + dy * u * 0.44f);
                    }
                    break;
                }

                case Glyph.Power:
                {
                    float d = u * 0.6f;
                    g.DrawArc(pen, cx - d / 2f, cy - d / 2f + u * 0.08f, d, d, 290, 250);
                    g.DrawLine(pen, cx, y + h * 0.1f, cx, cy + u * 0.02f);
                    break;
                }

                case Glyph.Grid:
                {
                    float cell = u * 0.32f, gap = u * 0.1f;
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < 2; j++)
                        {
                            var r = new RectangleF(cx + (i * 2 - 1) * (cell + gap) / 2f,
                                cy + (j * 2 - 1) * (cell + gap) / 2f, cell, cell);
                            using (var p = Theme.RoundRectF(r, u * 0.07f)) g.DrawPath(pen, p);
                        }
                    break;
                }

                case Glyph.Bolt:
                    g.DrawLines(pen, new[] {
                        new PointF(x + w * 0.6f, y + h * 0.08f), new PointF(x + w * 0.28f, y + h * 0.54f),
                        new PointF(x + w * 0.5f, y + h * 0.54f), new PointF(x + w * 0.4f, y + h * 0.92f),
                        new PointF(x + w * 0.72f, y + h * 0.46f), new PointF(x + w * 0.5f, y + h * 0.46f),
                        new PointF(x + w * 0.6f, y + h * 0.08f) });
                    break;
            }
        }
    }
}
