using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    sealed partial class MainForm
    {
        MacButton mbPrev, mbPlay, mbNext, mbEject, mbLyric;
        MacSlider mbSeek;
        Label mbTitle, mbArtist, mbT0, mbT1;
        MiniArt mbArt;
        bool seekByUser;

        void BuildMiniBar()
        {
            mini = new MacPanel();
            mini.Visible = false;
            mini.OnRefresh = RefreshMiniBar;
            mini.OnResized = LayoutMiniBar;
            Controls.Add(mini);

            mbArt = new MiniArt();
            mini.Controls.Add(mbArt);

            mbTitle = MacUi.Label("", Theme.FontUiMedium, Theme.Text);
            mbArtist = MacUi.Label("", Theme.FontSmall, Theme.TextDim);
            mini.Controls.Add(mbTitle);
            mini.Controls.Add(mbArtist);

            mbPrev = MiniBtn(Glyph.Prev, "上一首", (s, e) => { music.Prev(); });
            mbPlay = MiniBtn(Glyph.Play, "播放 / 暂停（Ctrl+Alt+P）", (s, e) => { music.Toggle(); });
            mbPlay.Tone = MacTone.Accent;
            mbNext = MiniBtn(Glyph.Next, "下一首", (s, e) => { music.Next(true); });
            mbEject = MiniBtn(Glyph.Eject, "停止并清除", (s, e) => { music.Eject(); UpdateMiniVisibility(); });
            mbLyric = MiniBtn(Glyph.Text, "桌面歌词", (s, e) => SetDesktopLyric(!DesktopLyricOn));

            mbSeek = new MacSlider();
            mbSeek.Minimum = 0;
            mbSeek.Maximum = 1000;
            mbSeek.ShowValue = false;
            mbSeek.ValueChanged += (s, e) =>
            {
                if (!seekByUser) return;
                mbT0.Text = Fmt(TimeSpan.FromSeconds(mbSeek.Value / 1000.0 * mbSeek.Aux));
            };
            mbSeek.DragFinished += (s, e) =>
            {
                if (!seekByUser) return;
                seekByUser = false;
                music.Seek(mbSeek.Value / 1000.0);
            };
            mini.Controls.Add(mbSeek);

            mbT0 = MacUi.Label("0:00", Theme.FontSmall, Theme.TextDim);
            mbT1 = MacUi.Label("0:00", Theme.FontSmall, Theme.TextDim);
            mbT1.TextAlign = ContentAlignment.MiddleRight;
            mini.Controls.Add(mbT0);
            mini.Controls.Add(mbT1);

            LayoutMiniBar();
            RefreshMiniBar();
        }

        MacButton MiniBtn(Glyph icon, string tip, EventHandler onClick)
        {
            var b = new MacButton();
            b.ShowIcon = true;
            b.Icon = icon;
            b.IconSize = Theme.S(14);
            b.Tone = MacTone.Normal;
            b.AccessibleName = tip;
            b.AccessibleDescription = tip;
            b.Click += onClick;
            mini.Controls.Add(b);
            return b;
        }

        void LayoutMiniBar()
        {
            if (mini == null) return;
            int W = mini.Width, Hh = mini.Height;
            if (W <= 0 || Hh <= 0) return;

            int art = Theme.S(44);
            mbArt.SetBounds(Theme.S(14), (Hh - art) / 2, art, art);

            int metaX = art + Theme.S(26);
            int metaW = Theme.S(150);
            mbTitle.SetBounds(metaX, Theme.S(14), metaW, Theme.S(22));
            mbArtist.SetBounds(metaX, Theme.S(37), metaW, Theme.S(20));

            int bs = Theme.S(30);
            int gap = Theme.S(6);
            int rightW = (bs + gap) * 2;
            int t0w = Theme.S(52);

            int zone0 = metaX + metaW + Theme.S(14);
            int zone1 = W - Theme.S(14) - rightW;

            int trio = bs * 3 + gap * 2;
            int tx = zone0 + Math.Max(0, (zone1 - zone0 - trio) / 2);
            int rowY = Hh - bs - Theme.S(12);
            mbPrev.SetBounds(tx, rowY, bs, bs);
            mbPlay.SetBounds(tx + bs + gap, rowY, bs, bs);
            mbNext.SetBounds(tx + (bs + gap) * 2, rowY, bs, bs);

            int seekY = Theme.S(14);
            mbT0.SetBounds(zone0, seekY, t0w, Theme.S(20));
            mbT1.SetBounds(zone1 - t0w, seekY, t0w, Theme.S(20));
            int sx0 = zone0 + t0w + Theme.S(4);
            int sx1 = zone1 - t0w - Theme.S(4);
            mbSeek.SetBounds(sx0, seekY, Math.Max(Theme.S(60), sx1 - sx0), Theme.S(20));

            mbLyric.SetBounds(W - Theme.S(14) - bs, rowY, bs, bs);
            mbEject.SetBounds(mbLyric.Left - gap - bs, rowY, bs, bs);
            mini.Invalidate(true);
        }

        static string Fmt(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
        }

        void RefreshMiniBar()
        {
            if (music == null || mini == null || !music.HasSong) return;
            var tr = music.Current;
            mbTitle.Text = tr.Title;
            mbArtist.Text = (tr.Artist.Length == 0 ? "" : tr.Artist + " · ") + tr.Source;
            mbPlay.Icon = music.Playing ? Glyph.Pause : Glyph.Play;
            mbLyric.Tone = DesktopLyricOn ? MacTone.Tinted : MacTone.Normal;

            var d = music.Dur;
            double secs = d.TotalSeconds;
            mbSeek.Aux = secs;
            if (!seekByUser)
            {
                mbSeek.Maximum = 1000;
                mbSeek.SetValueSilent(secs > 0 ? (int)(music.Pos.TotalSeconds / secs * 1000) : 0);
            }
            mbT0.Text = Fmt(music.Pos);
            mbT1.Text = Fmt(d);
            mbArt.SetPlaying(music.Playing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (mini != null && mini.Visible)
            {
                var p = mini.PointToClient(PointToScreen(e.Location));
                if (mbSeek.Bounds.Contains(p)) seekByUser = true;
            }
        }
    }


    /// 迷你条左侧封面块：无封面时画渐变底 + 音符，播放时轻微脉动。
    sealed class MiniArt : Control
    {
        public bool Playing;
        int phase;
        readonly Timer t;

        public MiniArt()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            t = new Timer { Interval = 90 };
            t.Tick += (s, e) => { phase = (phase + 1) % 40; Invalidate(); };
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && Playing) t.Start(); else t.Stop();
        }

        public void SetPlaying(bool on)
        {
            if (Playing == on) return;
            Playing = on;
            if (on && Visible) t.Start(); else t.Stop();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = Theme.S(9);
            using (var p = Theme.RoundRect(r, rad))
            {
                using (var br = new LinearGradientBrush(r, Theme.Accent,
                    Theme.Blend(Theme.Accent, Theme.IsDark ? Color.Black : Color.White, 0.42f), 45f))
                    g.FillPath(br, p);
            }
            float k = Width * (Playing ? 0.40f + (phase % 8) * 0.004f : 0.40f);
            Icons.Draw(g, Glyph.Music, new RectangleF((Width - k) / 2f, (Height - k) / 2f, k, k), Color.White);
        }
    }
}
