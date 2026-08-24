using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    sealed class AlertForm : Form
    {
        readonly Timer follow;
        readonly Timer hideT;
        readonly Label lbl;

        public AlertForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = Theme.Sz(52, 14); // 迷你尺寸
            BackColor = Color.FromArgb(207, 34, 46); // GitHub danger 红
            DoubleBuffered = true;

            lbl = new Label
            {
                Text = "你已被控制",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("微软雅黑", 6.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lbl);

            ApplyShape();

            follow = new Timer { Interval = 30 };
            follow.Tick += (s, e) =>
            {
                var p = Cursor.Position;
                Location = new Point(p.X + Theme.S(10), p.Y + Theme.S(10));
            };

            hideT = new Timer { Interval = 1500 };
            hideT.Tick += (s, e) => SetVisible(false);
        }

        void ApplyShape()
        {
            Region old = Region;
            Region = new Region(Theme.RoundRect(new Rectangle(0, 0, Width, Height), Theme.CornerRadius(Width, Height)));
            if (old != null) old.Dispose();
        }

        public void SetText(string text)
        {
            if (lbl.Text == text) return;
            lbl.Text = text;
            int w = TextRenderer.MeasureText(text, lbl.Font).Width + Theme.S(8);
            Size = new Size(Math.Max(Theme.S(52), w), Theme.S(14));
            ApplyShape();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOPMOST | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | 0x08000000;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), Theme.CornerRadius(Width, Height)))
            using (var pen = new Pen(Color.FromArgb(139, 20, 23)))
                g.DrawPath(pen, path);
        }

        public void SetVisible(bool v)
        {
            SetVisible(v, 1500);
        }

        public void SetVisible(bool v, int autoHideMs)
        {
            if (v && !Visible)
            {
                var p = Cursor.Position;
                Location = new Point(p.X + Theme.S(10), p.Y + Theme.S(10));
                Show();
                follow.Start();
                hideT.Interval = autoHideMs;
                hideT.Stop();
                hideT.Start();
            }
            else if (!v && Visible)
            {
                follow.Stop();
                hideT.Stop();
                Hide();
            }
        }

        // 单次显示 ms 毫秒后自动消失（用于"已解除控制"提示）
        public void ShowOnce(int ms)
        {
            SetVisible(true, ms);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            follow.Stop();
            hideT.Stop();
            base.OnFormClosed(e);
        }
    }
}
