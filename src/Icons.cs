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
        Target, Folder, Refresh, Tray, Stop, Eject, Wave, Sliders, Info, Warning, Clock
    }

    /// 线性矢量图标，风格对齐 SF Symbols：等宽描边、圆角端点、几何居中。
    static class Icons
    {
        public static void Draw(Graphics g, Glyph kind, RectangleF box, Color color)
        {
            float sw = Math.Max(1.2f, Math.Min(box.Width, box.Height) * 0.085f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, sw))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                using (var fill = new SolidBrush(color))
                    Paint(g, kind, box, pen, fill);
            }
        }

        static RectangleF R(RectangleF b, float dx, float dy, float dw, float dh)
        {
            return new RectangleF(b.X + dx * b.Width, b.Y + dy * b.Height, dw * b.Width, dh * b.Height);
        }

        static void Paint(Graphics g, Glyph k, RectangleF b, Pen pen, Brush fill)
        {
            float x = b.X, y = b.Y, w = b.Width, h = b.Height;
            float cx = x + w / 2f, cy = y + h / 2f;
            float u = Math.Min(w, h);
            switch (k)
            {
                case Glyph.Pin:
                {
                    // 图钉：圆头 + 针
                    using (var path = new GraphicsPath())
                    {
                        var head = R(b, 0.24f, 0.12f, 0.52f, 0.42f);
                        path.AddArc(head, 180, 180);
                        path.AddLine(head.Left, head.Top + head.Height / 2, head.Left, y + h * 0.62f);
                        path.AddLine(head.Right, y + h * 0.62f, head.Right, head.Top + head.Height / 2);
                        g.DrawPath(pen, path);
                    }
                    g.DrawLine(pen, cx, y + h * 0.62f, cx, y + h * 0.9f);
                    break;
                }
                case Glyph.PinSlash:
                    DrawSimple(g, Glyph.Pin, b, pen, fill);
                    g.DrawLine(pen, x + w * 0.16f, y + h * 0.84f, x + w * 0.84f, y + h * 0.16f);
                    break;

                case Glyph.Layers:
                    g.DrawPolygon(pen, new[] {
                        new PointF(cx, y + h * 0.14f), new PointF(x + w * 0.88f, cy),
                        new PointF(cx, y + h * 0.86f), new PointF(x + w * 0.12f, cy) });
                    g.DrawLine(pen, x + w * 0.2f, y + h * 0.32f, x + w * 0.5f, y + h * 0.14f);
                    break;

                case Glyph.Droplet:
                {
                    using (var path = new GraphicsPath())
                    {
                        var bulge = R(b, 0.2f, 0.36f, 0.6f, 0.5f);
                        path.AddArc(bulge, 0, 180);
                        path.AddLine(bulge.Left, bulge.Top, cx, y + h * 0.1f);
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    break;
                }

                case Glyph.Picture:
                {
                    var frame = R(b, 0.1f, 0.16f, 0.8f, 0.68f);
                    g.DrawRectangle(pen, frame.X, frame.Y, frame.Width, frame.Height);
                    g.DrawEllipse(pen, frame.X + frame.Width * 0.16f, frame.Y + frame.Height * 0.18f, u * 0.11f, u * 0.11f);
                    g.DrawLines(pen, new[] {
                        new PointF(frame.Left, frame.Bottom - u * 0.1f),
                        new PointF(frame.Left + frame.Width * 0.35f, frame.Top + frame.Height * 0.5f),
                        new PointF(frame.Left + frame.Width * 0.6f, frame.Bottom - u * 0.16f),
                        new PointF(frame.Right - u * 0.1f, frame.Top + frame.Height * 0.34f),
                        new PointF(frame.Right, frame.Bottom - u * 0.12f) });
                    break;
                }

                case Glyph.Film:
                {
                    var frame = R(b, 0.1f, 0.16f, 0.8f, 0.68f);
                    g.DrawRectangle(pen, frame.X, frame.Y, frame.Width, frame.Height);
                    g.DrawLine(pen, frame.Left + frame.Width / 3, frame.Top, frame.Left + frame.Width / 3, frame.Bottom);
                    g.DrawLine(pen, frame.Left + frame.Width * 2 / 3, frame.Top, frame.Left + frame.Width * 2 / 3, frame.Bottom);
                    break;
                }

                case Glyph.Music:
                {
                    g.FillEllipse(fill, x + w * 0.2f, y + h * 0.62f, u * 0.24f, u * 0.2f);
                    g.FillEllipse(fill, x + w * 0.58f, y + h * 0.72f, u * 0.24f, u * 0.2f);
                    g.DrawLine(pen, x + w * 0.44f, y + h * 0.72f, x + w * 0.44f, y + h * 0.2f);
                    g.DrawLine(pen, x + w * 0.82f, y + h * 0.82f, x + w * 0.82f, y + h * 0.3f);
                    g.DrawLine(pen, x + w * 0.44f, y + h * 0.2f, x + w * 0.82f, y + h * 0.3f);
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
                            new PointF(cx, y + h * 0.1f), new PointF(x + w * 0.86f, y + h * 0.24f),
                            new PointF(x + w * 0.86f, cy), new PointF(cx, y + h * 0.9f),
                            new PointF(x + w * 0.14f, cy), new PointF(x + w * 0.14f, y + h * 0.24f) });
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
                        path.AddLines(new[] { new PointF(cx, y + h * 0.12f), new PointF(x + w * 0.92f, y + h * 0.86f), new PointF(x + w * 0.08f, y + h * 0.86f) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    g.DrawLine(pen, cx, y + h * 0.4f, cx, y + h * 0.64f);
                    g.FillEllipse(fill, cx - u * 0.05f, y + h * 0.73f, u * 0.1f, u * 0.1f);
                    break;
                }

                case Glyph.Box:
                {
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.5f, y + h * 0.1f), new PointF(x + w * 0.88f, y + h * 0.3f), new PointF(x + w * 0.88f, y + h * 0.7f), new PointF(x + w * 0.5f, y + h * 0.9f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.5f, y + h * 0.1f), new PointF(x + w * 0.12f, y + h * 0.3f), new PointF(x + w * 0.12f, y + h * 0.7f), new PointF(x + w * 0.5f, y + h * 0.9f) });
                    g.DrawLine(pen, x + w * 0.12f, y + h * 0.3f, x + w * 0.5f, y + h * 0.5f);
                    g.DrawLine(pen, x + w * 0.88f, y + h * 0.3f, x + w * 0.5f, y + h * 0.5f);
                    g.DrawLine(pen, x + w * 0.5f, y + h * 0.5f, x + w * 0.5f, y + h * 0.9f);
                    break;
                }

                case Glyph.Gear:
                {
                    float ro = u * 0.38f, ri = u * 0.27f;
                    var pts = new PointF[16];
                    for (int i = 0; i < 8; i++)
                    {
                        double a0 = i * Math.PI / 4;
                        pts[i * 2] = new PointF(cx + (float)Math.Cos(a0) * ro, cy + (float)Math.Sin(a0) * ro);
                        pts[i * 2 + 1] = new PointF(cx + (float)Math.Cos(a0 + Math.PI / 8) * ri, cy + (float)Math.Sin(a0 + Math.PI / 8) * ri);
                    }
                    g.DrawPolygon(pen, pts);
                    g.DrawEllipse(pen, cx - u * 0.12f, cy - u * 0.12f, u * 0.24f, u * 0.24f);
                    break;
                }

                case Glyph.Search:
                    g.DrawEllipse(pen, x + w * 0.16f, y + h * 0.16f, u * 0.44f, u * 0.44f);
                    g.DrawLine(pen, x + w * 0.56f, y + h * 0.56f, x + w * 0.84f, y + h * 0.84f);
                    break;

                case Glyph.Play:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[] { new PointF(x + w * 0.3f, y + h * 0.18f), new PointF(x + w * 0.82f, cy), new PointF(x + w * 0.3f, y + h * 0.82f) });
                        path.CloseFigure();
                        g.FillPath(fill, path);
                    }
                    break;
                }
                case Glyph.Pause:
                    g.FillRectangle(fill, x + w * 0.28f, y + h * 0.2f, u * 0.14f, u * 0.6f);
                    g.FillRectangle(fill, x + w * 0.58f, y + h * 0.2f, u * 0.14f, u * 0.6f);
                    break;
                case Glyph.Stop:
                    g.FillRectangle(fill, x + w * 0.3f, y + h * 0.3f, u * 0.4f, u * 0.4f);
                    break;
                case Glyph.Prev:
                    g.FillPolygon(fill, new[] { new PointF(x + w * 0.66f, y + h * 0.22f), new PointF(x + w * 0.34f, cy), new PointF(x + w * 0.66f, y + h * 0.78f) });
                    g.FillRectangle(fill, x + w * 0.24f, y + h * 0.22f, u * 0.07f, u * 0.56f);
                    break;
                case Glyph.Next:
                    g.FillPolygon(fill, new[] { new PointF(x + w * 0.34f, y + h * 0.22f), new PointF(x + w * 0.66f, cy), new PointF(x + w * 0.34f, y + h * 0.78f) });
                    g.FillRectangle(fill, x + w * 0.69f, y + h * 0.22f, u * 0.07f, u * 0.56f);
                    break;
                case Glyph.Shuffle:
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.16f, y + h * 0.26f), new PointF(x + w * 0.4f, y + h * 0.26f), new PointF(x + w * 0.8f, y + h * 0.74f), new PointF(x + w * 0.86f, y + h * 0.74f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.16f, y + h * 0.74f), new PointF(x + w * 0.4f, y + h * 0.74f), new PointF(x + w * 0.8f, y + h * 0.26f), new PointF(x + w * 0.86f, y + h * 0.26f) });
                    break;
                case Glyph.Repeat:
                {
                    var arc = R(b, 0.2f, 0.22f, 0.6f, 0.56f);
                    g.DrawArc(pen, arc, 300, 240);
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.74f, y + h * 0.2f), new PointF(x + w * 0.84f, y + h * 0.34f), new PointF(x + w * 0.7f, y + h * 0.42f) });
                    break;
                }
                case Glyph.Heart:
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(x + w * 0.5f, y + h * 0.8f, x + w * 0.08f, y + h * 0.5f, x + w * 0.14f, y + h * 0.14f, x + w * 0.5f, y + h * 0.34f);
                        path.AddBezier(x + w * 0.5f, y + h * 0.8f, x + w * 0.92f, y + h * 0.5f, x + w * 0.86f, y + h * 0.14f, x + w * 0.5f, y + h * 0.34f);
                        g.DrawPath(pen, path);
                    }
                    break;
                }
                case Glyph.Volume:
                    g.FillPolygon(fill, new[] { new PointF(x + w * 0.14f, y + h * 0.4f), new PointF(x + w * 0.34f, y + h * 0.4f), new PointF(x + w * 0.52f, y + h * 0.22f), new PointF(x + w * 0.52f, y + h * 0.78f), new PointF(x + w * 0.34f, y + h * 0.6f), new PointF(x + w * 0.14f, y + h * 0.6f) });
                    g.DrawArc(pen, R(b, 0.44f, 0.28f, 0.3f, 0.44f), -50, 100);
                    g.DrawArc(pen, R(b, 0.56f, 0.16f, 0.44f, 0.68f), -45, 90);
                    break;

                case Glyph.ChevronL: g.DrawLines(pen, new[] { new PointF(x + w * 0.6f, y + h * 0.22f), new PointF(x + w * 0.38f, cy), new PointF(x + w * 0.6f, y + h * 0.78f) }); break;
                case Glyph.ChevronR: g.DrawLines(pen, new[] { new PointF(x + w * 0.4f, y + h * 0.22f), new PointF(x + w * 0.62f, cy), new PointF(x + w * 0.4f, y + h * 0.78f) }); break;
                case Glyph.ChevronD: g.DrawLines(pen, new[] { new PointF(x + w * 0.22f, y + h * 0.4f), new PointF(cx, y + h * 0.62f), new PointF(x + w * 0.78f, y + h * 0.4f) }); break;
                case Glyph.Close: g.DrawLine(pen, x + w * 0.28f, y + h * 0.28f, x + w * 0.72f, y + h * 0.72f); g.DrawLine(pen, x + w * 0.72f, y + h * 0.28f, x + w * 0.28f, y + h * 0.72f); break;
                case Glyph.Minus: g.DrawLine(pen, x + w * 0.26f, cy, x + w * 0.74f, cy); break;
                case Glyph.Plus: g.DrawLine(pen, x + w * 0.26f, cy, x + w * 0.74f, cy); g.DrawLine(pen, cx, y + h * 0.26f, cx, y + h * 0.74f); break;
                case Glyph.Enlarge:
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.56f, y + h * 0.2f), new PointF(x + w * 0.8f, y + h * 0.2f), new PointF(x + w * 0.8f, y + h * 0.44f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.44f, y + h * 0.8f), new PointF(x + w * 0.2f, y + h * 0.8f), new PointF(x + w * 0.2f, y + h * 0.56f) });
                    break;
                case Glyph.Check: g.DrawLines(pen, new[] { new PointF(x + w * 0.2f, cy), new PointF(x + w * 0.42f, y + h * 0.72f), new PointF(x + w * 0.82f, y + h * 0.28f) }); break;

                case Glyph.Target:
                    g.DrawEllipse(pen, x + w * 0.2f, y + h * 0.2f, u * 0.6f, u * 0.6f);
                    g.FillEllipse(fill, cx - u * 0.07f, cy - u * 0.07f, u * 0.14f, u * 0.14f);
                    g.DrawLine(pen, cx, y + h * 0.06f, cx, y + h * 0.2f);
                    g.DrawLine(pen, cx, y + h * 0.8f, cx, y + h * 0.94f);
                    g.DrawLine(pen, x + w * 0.06f, cy, x + w * 0.2f, cy);
                    g.DrawLine(pen, x + w * 0.8f, cy, x + w * 0.94f, cy);
                    break;

                case Glyph.Folder:
                {
                    using (var path = new GraphicsPath())
                    {
                        var body = R(b, 0.1f, 0.24f, 0.8f, 0.56f);
                        path.AddLines(new[] { new PointF(body.Left, body.Bottom), new PointF(body.Left, body.Top),
                            new PointF(body.Left + body.Width * 0.34f, body.Top), new PointF(body.Left + body.Width * 0.44f, body.Top - h * 0.1f),
                            new PointF(body.Right, body.Top - h * 0.1f), new PointF(body.Right, body.Bottom) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    break;
                }

                case Glyph.Refresh:
                {
                    var arc = R(b, 0.2f, 0.2f, 0.6f, 0.6f);
                    g.DrawArc(pen, arc, 40, 280);
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.76f, y + h * 0.14f), new PointF(x + w * 0.82f, y + h * 0.32f), new PointF(x + w * 0.62f, y + h * 0.34f) });
                    break;
                }

                case Glyph.Tray:
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.14f, y + h * 0.3f), new PointF(x + w * 0.4f, y + h * 0.3f), new PointF(cx, y + h * 0.56f), new PointF(x + w * 0.6f, y + h * 0.3f), new PointF(x + w * 0.86f, y + h * 0.3f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.14f, y + h * 0.66f), new PointF(x + w * 0.14f, y + h * 0.3f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * 0.86f, y + h * 0.3f), new PointF(x + w * 0.86f, y + h * 0.66f) });
                    break;

                case Glyph.Eject:
                    g.FillPolygon(fill, new[] { new PointF(cx, y + h * 0.2f), new PointF(x + w * 0.72f, y + h * 0.56f), new PointF(x + w * 0.28f, y + h * 0.56f) });
                    g.FillRectangle(fill, x + w * 0.28f, y + h * 0.66f, u * 0.44f, u * 0.1f);
                    break;

                case Glyph.Wave:
                    for (int i = 0; i < 5; i++)
                    {
                        float px = x + w * (0.18f + i * 0.16f);
                        float amp = u * (i % 2 == 0 ? 0.26f : 0.14f);
                        g.DrawLine(pen, px, cy - amp, px, cy + amp);
                    }
                    break;

                case Glyph.Sliders:
                    g.DrawLine(pen, x + w * 0.2f, y + h * 0.3f, x + w * 0.8f, y + h * 0.3f);
                    g.DrawLine(pen, x + w * 0.2f, y + h * 0.7f, x + w * 0.8f, y + h * 0.7f);
                    g.FillEllipse(fill, x + w * 0.56f, y + h * 0.3f - u * 0.09f, u * 0.18f, u * 0.18f);
                    g.FillEllipse(fill, x + w * 0.3f, y + h * 0.7f - u * 0.09f, u * 0.18f, u * 0.18f);
                    break;

                case Glyph.Info:
                    g.DrawEllipse(pen, x + w * 0.16f, y + h * 0.16f, u * 0.68f, u * 0.68f);
                    g.DrawLine(pen, cx, y + h * 0.42f, cx, y + h * 0.68f);
                    g.FillEllipse(fill, cx - u * 0.045f, y + h * 0.28f, u * 0.09f, u * 0.09f);
                    break;

                case Glyph.Clock:
                    g.DrawEllipse(pen, x + w * 0.16f, y + h * 0.16f, u * 0.68f, u * 0.68f);
                    g.DrawLine(pen, cx, cy, cx, y + h * 0.32f);
                    g.DrawLine(pen, cx, cy, x + w * 0.66f, cy);
                    break;
            }
        }

        static void DrawSimple(Graphics g, Glyph k, RectangleF b, Pen pen, Brush fill)
        {
            var save = k;
            if (save == Glyph.PinSlash) Paint(g, Glyph.Pin, b, pen, fill);
        }
    }
}
