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

        public AlertForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(72, 20);
            BackColor = Color.White;
            DoubleBuffered = true;

            var lbl = new Label
            {
                Text = "你已被控制",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("微软雅黑", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lbl);

            Region = new Region(Theme.RoundRect(new Rectangle(0, 0, Width, Height), 8));

            follow = new Timer { Interval = 30 };
            follow.Tick += (s, e) =>
            {
                var p = Cursor.Position;
                Location = new Point(p.X + 12, p.Y + 12);
            };

            hideT = new Timer { Interval = 1500 };
            hideT.Tick += (s, e) => SetVisible(false);
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
            using (var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (var pen = new Pen(Theme.Border))
                g.DrawPath(pen, path);
        }

        public void SetVisible(bool v)
        {
            if (v && !Visible)
            {
                var p = Cursor.Position;
                Location = new Point(p.X + 12, p.Y + 12);
                Show();
                follow.Start();
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            follow.Stop();
            hideT.Stop();
            base.OnFormClosed(e);
        }
    }
}
