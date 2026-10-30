using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>通用双缓冲与绘制辅助。</summary>
    static class UiUtil
    {
        public static void DoubleBuffer(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        public static void SetCue(TextBox tb, string cue)
        {
            Native.SendMessage(tb.Handle, Native.EM_SETCUEBANNER, (IntPtr)1, cue);
        }

        const TextFormatFlags Left =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c)
        {
            TextRenderer.DrawText(g, s, f, r, c, Left);
        }

        public static void Text(Graphics g, string s, Font f, RectangleF r, Color c)
        {
            TextRenderer.DrawText(g, s, f, ToRect(r), c, Left);
        }

        static Rectangle ToRect(RectangleF r)
        {
            return new Rectangle((int)r.X, (int)r.Y, (int)Math.Ceiling(r.Width), (int)Math.Ceiling(r.Height));
        }

        public static int TextW(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, Size.Empty, Left).Width;
        }
    }

    // ==================================================================
    // 按钮
    // ==================================================================

    enum UiButtonKind
    {
        Primary,   // 强调色实心
        Normal,    // 卡片底 + 描边
        Quiet,     // 无边框，悬停淡底
        Danger     // 危险红描边
    }

    class UiButton : Control
    {
        public UiButtonKind Kind = UiButtonKind.Normal;
        public Glyph? Icon;
        public string Label = "";
        public bool Small;

        bool hover, pressed;

        public UiButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Font = Theme.FontBody;
            Height = Theme.S(30);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false; Invalidate();
            base.OnMouseUp(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool inRect = ClientRectangle.Contains(e.Location);
            if (inRect != hover) { hover = inRect; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = ClientRectangle;
            r.Width--; r.Height--;
            int radius = Theme.S(6);
            var f = Small ? Theme.FontSmall : Theme.FontBody;

            Color bg, fg, border;
            if (Kind == UiButtonKind.Primary)
            {
                bg = Enabled ? Theme.Accent : Theme.Border;
                fg = Color.White;
                border = bg;
            }
            else if (Kind == UiButtonKind.Quiet)
            {
                bg = hover && Enabled ? Theme.CardHi : Theme.Card;
                fg = Enabled ? Theme.Text : Theme.TextFaint;
                border = bg;
            }
            else if (Kind == UiButtonKind.Danger)
            {
                bg = hover && Enabled ? Theme.Bad : Theme.Card;
                fg = hover && Enabled ? Color.White : Theme.Bad;
                border = Theme.Bad;
            }
            else
            {
                bg = !Enabled ? Theme.Card : (pressed ? Theme.CardHi : (hover ? Theme.CardHi : Theme.Card));
                fg = Enabled ? Theme.Text : Theme.TextFaint;
                border = Theme.Border;
            }

            Theme.FillRound(g, r, bg, radius);
            if (Kind != UiButtonKind.Quiet)
                using (var pen = new Pen(Enabled ? border : Theme.Border))
                    Theme.RoundRect(g, r, radius);

            // 内容：图标 + 文本水平居中
            int iconSize = Small ? Theme.S(14) : Theme.S(16);
            int textW = Label.Length == 0 ? 0 : UiUtil.TextW(Label, f);
            int gap = Icon.HasValue && textW > 0 ? Theme.S(6) : 0;
            int total = (Icon.HasValue ? iconSize : 0) + gap + textW;
            int x = (r.Width + 1 - total) / 2;

            if (Icon.HasValue)
            {
                Icons.Draw(g, Icon.Value, new Rectangle(x, (r.Height + 1 - iconSize) / 2, iconSize, iconSize), fg);
                x += iconSize + gap;
            }
            if (textW > 0)
            {
                var tr = new Rectangle(x, 0, r.Width + 1 - x, r.Height + 1);
                TextRenderer.DrawText(g, Label, f, tr, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled) base.OnClick(e);
        }
    }

    // ==================================================================
    // 开关
    // ==================================================================

    class UiToggle : Control
    {
        bool check;
        public bool Checked
        {
            get { return check; }
            // 程序赋值只改状态不触发事件：Toggled 仅在用户点击时触发，
            // 否则「回填真实状态 → 事件 → 再执行操作」会形成回环
            set { if (check != value) { check = value; Invalidate(); } }
        }
        public string Label = "";
        /// 说明文字（第二行，小号灰字）
        public string Hint = "";

        public event EventHandler Toggled;

        public UiToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.FontBody;
            Height = Theme.S(30);
        }

        void OnToggled()
        {
            var h = Toggled;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                Checked = !Checked;
                OnToggled();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int trackW = Theme.S(34), trackH = Theme.S(18);
            int tx = Width - trackW;
            int ty = Hint.Length > 0 ? Theme.S(6) : (Height - trackH) / 2;

            Color track = !Enabled ? Theme.Border : (check ? Theme.Accent : Theme.Border);
            using (var b = new SolidBrush(track))
            {
                // 圆头轨道：矩形 + 左右半圆
                g.FillRectangle(b, tx + trackH / 2, ty, trackW - trackH, trackH);
                g.FillEllipse(b, tx, ty, trackH, trackH);
                g.FillEllipse(b, tx + trackW - trackH, ty, trackH, trackH);
            }
            int knob = trackH - Theme.S(4);
            int kx = check ? tx + trackW - knob - Theme.S(2) : tx + Theme.S(2);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, ty + Theme.S(2), knob, knob);

            // 文本（左侧）
            if (Label.Length > 0)
            {
                var tr = new Rectangle(0, 0, tx - Theme.S(10), Hint.Length > 0 ? Height / 2 : Height);
                var f = Hint.Length > 0 ? Theme.FontBody : Theme.FontBody;
                UiUtil.Text(g, Label, f,
                    new RectangleF(tr.X, tr.Y, tr.Width, Hint.Length > 0 ? tr.Height - Theme.S(2) : tr.Height),
                    Enabled ? Theme.Text : Theme.TextFaint);
                if (Hint.Length > 0)
                    UiUtil.Text(g, Hint, Theme.FontSmall,
                        new RectangleF(0, Height / 2, tx - Theme.S(10), Height / 2), Theme.TextDim);
            }
        }
    }

    // ==================================================================
    // 滑杆
    // ==================================================================

    class UiSlider : Control
    {
        int value, min = 40, max = 255;
        public string Label = "";

        public int Min { get { return min; } set { min = value; } }
        public int Max { get { return max; } set { max = value; } }

        public int Value
        {
            get { return this.value; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v != this.value) { this.value = v; Invalidate(); OnChanged(); }
            }
        }

        public event EventHandler Changed;
        /// 拖动结束（鼠标抬起）才触发，用于持久化
        public event EventHandler Committed;

        bool dragging;
        int trackY;

        public UiSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.FontBody;
            Height = Theme.S(34);
            this.value = min;
        }

        void OnChanged()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        int TrackX { get { return Theme.S(8); } }
        int TrackW { get { return Math.Max(1, Width - Theme.S(16)); } }

        float Fraction { get { return max == min ? 0 : (float)(value - min) / (max - min); } }

        void SetFromX(int x)
        {
            int t = x - TrackX;
            int v = min + (int)Math.Round((double)t / TrackW * (max - min));
            Value = Math.Max(min, Math.Min(max, v));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; SetFromX(e.X); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                var h = Committed;
                if (h != null) h(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            trackY = Height - Theme.S(12);
            int tx = TrackX, tw = TrackW;

            using (var b = new SolidBrush(Theme.Border))
                g.FillRectangle(b, tx, trackY, tw, Theme.S(4));
            int fillW = (int)(tw * Fraction);
            if (fillW > 0)
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, tx, trackY, fillW, Theme.S(4));

            int knob = Theme.S(12);
            int kx = tx + Math.Max(0, Math.Min(tw - knob, fillW - knob / 2));
            using (var b = new SolidBrush(Theme.Accent))
                g.FillEllipse(b, kx, trackY + Theme.S(2) - knob / 2, knob, knob);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx + Theme.S(2), trackY + Theme.S(4) - knob / 2, knob - Theme.S(4), knob - Theme.S(4));

            if (Label.Length > 0)
                UiUtil.Text(g, Label, Theme.FontBody, new RectangleF(Theme.S(8), 0, Width - Theme.S(16) - UiUtil.TextW("0000", Theme.FontBody), trackY - Theme.S(4)), Theme.Text);
            string vtxt = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UiUtil.Text(g, vtxt, Theme.FontMono, new RectangleF(Width - Theme.S(8) - UiUtil.TextW("0000", Theme.FontMono) - Theme.S(4), 0, UiUtil.TextW("0000", Theme.FontMono) + Theme.S(4), trackY - Theme.S(4)), Theme.TextDim);
        }
    }
}

namespace WindowTool
{
    // ==================================================================
    // 分段选择
    // ==================================================================

    class UiSegmented : Control
    {
        string[] items = new string[0];
        int sel;

        public string[] Items
        {
            get { return items; }
            set { items = value ?? new string[0]; if (sel >= items.Length) sel = items.Length - 1; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return sel; }
            set { if (value >= 0 && value < items.Length && value != sel) { sel = value; Invalidate(); OnChanged(); } }
        }

        public event EventHandler Changed;

        public UiSegmented()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = Theme.S(26);
        }

        void OnChanged()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        int ItemW { get { return Math.Max(1, Width / Math.Max(1, items.Length)); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, e.X / ItemW));
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Math.Max(0, Math.Min(items.Length - 1, e.X / ItemW));
            if (i != hoverIdx) { hoverIdx = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        int hoverIdx = -1;

        protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = ClientRectangle; r.Width--; r.Height--;
            Theme.FillRound(g, r, Theme.InputBg, Theme.S(6));
            using (var pen = new Pen(Theme.Border)) Theme.RoundRect(g, r, Theme.S(6));

            for (int i = 0; i < items.Length; i++)
            {
                var seg = new Rectangle(i * ItemW, 0, ItemW, Height);
                if (i == sel)
                    Theme.FillRound(g, new Rectangle(seg.X + Theme.S(2), Theme.S(2), seg.Width - Theme.S(4), Height - Theme.S(4)), Theme.Accent, Theme.S(4));
                else if (i == hoverIdx)
                    Theme.FillRound(g, new Rectangle(seg.X + Theme.S(2), Theme.S(2), seg.Width - Theme.S(4), Height - Theme.S(4)), Theme.CardHi, Theme.S(4));
                var f = Theme.FontSmall;
                TextRenderer.DrawText(g, items[i], f, seg,
                    i == sel ? Color.White : Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }
    }

    // ==================================================================
    // 卡片容器（标题 + 客户区自动布局）
    // ==================================================================

    class UiCard : Control
    {
        public string Title = "";

        public UiCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        /// 标题带高度
        public int HeaderH { get { return Title.Length == 0 ? Theme.S(8) : Theme.S(30); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = ClientRectangle; r.Width--; r.Height--;
            Theme.FillRound(g, r, Theme.Card, Theme.S(10));
            using (var pen = new Pen(Theme.Border)) Theme.RoundRect(g, r, Theme.S(10));
            if (Title.Length > 0)
                UiUtil.Text(g, Title, Theme.FontBodyBold, new RectangleF(Theme.S(14), 0, Width - Theme.S(28), HeaderH), Theme.Text);
        }
    }

    // ==================================================================
    // 文本输入（包一层画边框，内嵌原生 TextBox 保证中文输入法体验）
    // ==================================================================

    class UiInput : Control
    {
        protected TextBox box;
        public event EventHandler TextChangedByUser;

        public string Text2
        {
            get { return box.Text; }
            set { box.Text = value; }
        }

        public TextBox Box { get { return box; } }

        public UiInput()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(30);

            box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.Font = Theme.FontBody;
            box.ForeColor = Theme.Text;
            box.BackColor = Theme.InputBg;
            box.Multiline = false;
            Controls.Add(box);
            box.TextChanged += (s, e) => { var h = TextChangedByUser; if (h != null) h(this, EventArgs.Empty); };
        }

        protected override void OnFontChanged(EventArgs e) { box.Font = Font; base.OnFontChanged(e); }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            box.SetBounds(Theme.S(9), (Height - box.PreferredHeight) / 2, Math.Max(1, Width - Theme.S(18)), box.PreferredHeight);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            box.BackColor = Theme.InputBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = ClientRectangle; r.Width--; r.Height--;
            bool focus = Focused || box.Focused;
            Theme.FillRound(g, r, Theme.InputBg, Theme.S(6));
            using (var pen = new Pen(focus ? Theme.Accent : Theme.Border))
                Theme.RoundRect(g, r, Theme.S(6));
        }
    }

    // ==================================================================
    // 搜索框
    // ==================================================================

    class UiSearch : UiInput
    {
        public UiSearch()
        {
            UiUtil.SetCue(box, "搜索：标题 / 进程 / 类名");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 图标在左，输入区整体右移
            Icons.Draw(e.Graphics, Glyph.Search,
                new Rectangle(Theme.S(8), (Height - Theme.S(14)) / 2, Theme.S(14), Theme.S(14)), Theme.TextFaint);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            box.SetBounds(Theme.S(30), (Height - box.PreferredHeight) / 2, Math.Max(1, Width - Theme.S(38)), box.PreferredHeight);
        }
    }
}

namespace WindowTool
{
    // ==================================================================
    // 左侧图标导航栏
    // ==================================================================

    sealed class NavItem
    {
        public string Key, Title;
        public Glyph Icon;
    }

    class NavRail : Control
    {
        public readonly List<NavItem> Items = new List<NavItem>();
        int sel;

        public NavRail()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                if (value >= 0 && value < Items.Count && value != sel)
                {
                    sel = value;
                    Invalidate();
                    var h = SelectionChanged;
                    if (h != null) h(this, EventArgs.Empty);
                }
            }
        }

        public NavItem Selected
        {
            get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; }
        }

        public event EventHandler SelectionChanged;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) SelectedIndex = HitItem(e.Y);
            base.OnMouseDown(e);
        }

        int HitItem(int y)
        {
            int cell = ItemH;
            if (cell <= 0) return -1;
            int i = (y - TopPad) / cell;
            if (i < 0 || i >= Items.Count) return -1;
            return i;
        }

        int ItemH { get { return Theme.S(50); } }
        int TopPad { get { return Theme.S(16); } }
        int hoverIdx = -1;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = HitItem(e.Y);
            if (i != hoverIdx) { hoverIdx = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 底部徽标：Logo 小标记
            Icons.Draw(g, Glyph.Command,
                new Rectangle((Width - Theme.S(20)) / 2, Height - Theme.S(34), Theme.S(20), Theme.S(20)), Theme.TextFaint);

            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                int y = TopPad + i * ItemH;
                int cell = ItemH;
                var r = new Rectangle(Theme.S(6), y + Theme.S(4), Width - Theme.S(12), cell - Theme.S(8));

                if (i == sel)
                    Theme.FillRound(g, r, Theme.Card, Theme.S(8));
                else if (i == hoverIdx)
                    Theme.FillRound(g, r, Theme.CardHi, Theme.S(8));

                if (i == sel)
                {
                    // 左侧强调指示条
                    using (var b = new SolidBrush(Theme.Accent))
                    {
                        var bar = new Rectangle(Theme.S(1), y + Theme.S(14), Theme.S(3), cell - Theme.S(28));
                        Theme.FillRound(g, bar, Theme.Accent, Theme.S(1));
                    }
                }

                // 图标 + 图标下方的小标题（不需要悬停提示）
                int icon = Theme.S(18);
                int iy = y + Theme.S(7);
                int ix = (Width - icon) / 2;
                Icons.Draw(g, it.Icon, new Rectangle(ix, iy, icon, icon), i == sel ? Theme.AccentText : Theme.TextDim);
                var label = it.Title;
                TextRenderer.DrawText(g, label, Theme.FontSmall,
                    new Rectangle(0, iy + icon + Theme.S(2), Width, Theme.S(13)),
                    i == sel ? Theme.Text : Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
    }

    // ==================================================================
    // 标题栏（常规 − □ × 按钮，可拖动）
    // ==================================================================

    class TitleBar : Control
    {
        public string Title = "";
        public event Action MinClick, MaxClick, CloseClick;

        int hoverBtn = -1;   // 0=min 1=max 2=close
        bool pressedBtn;
        bool maximized;

        public bool Maximized
        {
            get { return maximized; }
            set { maximized = value; Invalidate(); }
        }

        const int BtnW = 12;  // 逻辑单元，实际乘缩放

        public TitleBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(40);
            Cursor = Cursors.Default;
        }

        Rectangle BtnRect(int i)
        {
            int w = Theme.S(44), h = Height;
            return new Rectangle(Width - w * (3 - i), 0, w, h);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int i = HitBtn(e.X, e.Y);
                if (i >= 0) { pressedBtn = true; Invalidate(); }
                else FindForm().SendMessageSafe();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && HitBtn(e.X, e.Y) < 0 && MaxClick != null)
                MaxClick();
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (pressedBtn)
            {
                pressedBtn = false;
                int i = HitBtn(e.X, e.Y);
                if (i == 0 && MinClick != null) MinClick();
                else if (i == 1 && MaxClick != null) MaxClick();
                else if (i == 2 && CloseClick != null) CloseClick();
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = HitBtn(e.X, e.Y);
            if (i != hoverBtn) { hoverBtn = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverBtn = -1; pressedBtn = false; Invalidate(); base.OnMouseLeave(e); }

        int HitBtn(int x, int y)
        {
            for (int i = 0; i < 3; i++)
                if (BtnRect(i).Contains(x, y)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            UiUtil.Text(g, Title, Theme.FontBodyBold, new RectangleF(Theme.S(14), 0, Width - Theme.S(140), Height), Theme.Text);

            for (int i = 0; i < 3; i++)
            {
                var r = BtnRect(i);
                bool hot = hoverBtn == i && !pressedBtn;
                if (i == 2 && (hot || pressedBtn))
                    using (var b = new SolidBrush(Theme.Bad)) g.FillRectangle(b, r);
                else if (hot)
                    using (var b = new SolidBrush(Theme.CardHi)) g.FillRectangle(b, r);

                Color c = Theme.TextDim;
                int cx = r.X + r.Width / 2, cy = Height / 2, w = Theme.S(10);

                if (i == 0)   // −
                {
                    using (var p = new Pen(c, 1.6f)) g.DrawLine(p, cx - w / 2, cy, cx + w / 2, cy);
                }
                else if (i == 1)  // □ / 还原态双框
                {
                    if (!maximized)
                    {
                        using (var p = new Pen(c, 1.6f)) g.DrawRectangle(p, cx - w / 2, cy - w / 2, w, w);
                    }
                    else
                    {
                        using (var p = new Pen(c, 1.6f))
                        {
                            g.DrawRectangle(p, cx - w / 2, cy - w / 4, w, w);
                            g.DrawRectangle(p, cx - w / 4, cy - w / 2 - 1, w - Theme.S(2), w - Theme.S(2));
                        }
                    }
                }
                else          // ×
                {
                    using (var p = new Pen(i == 2 && (hot || pressedBtn) ? Color.White : c, 1.6f))
                    {
                        g.DrawLine(p, cx - w / 2, cy - w / 2, cx + w / 2, cy + w / 2);
                        g.DrawLine(p, cx + w / 2, cy - w / 2, cx - w / 2, cy + w / 2);
                    }
                }
            }
        }
    }

    static class FormExt
    {
        /// 让宿主窗体进入拖动（WM_NCLBUTTONDOWN + HTCAPTION）。
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        const int WM_NCLBUTTONDOWN = 0x00A1;
        static readonly IntPtr HTCAPTION = new IntPtr(2);

        public static void SendMessageSafe(this Form f)
        {
            try
            {
                if (f == null || !f.IsHandleCreated) return;
                ReleaseCapture();
                SendMessage(f.Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
            }
            catch { }
        }
    }

    // ==================================================================
    // 浮动提示条
    // ==================================================================

    class ToastHost : Control
    {
        string msg = "";
        Color tone;
        float alpha;
        Timer fade;
        int lifeUntil;   // Environment.TickCount

        public ToastHost()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            fade = new Timer();
            fade.Interval = 30;
            fade.Tick += OnFade;
        }

        public void Show(string text, Color color, int width)
        {
            msg = text;
            tone = color;
            Width = width;
            Height = Theme.S(34);
            Visible = true;
            alpha = 1;
            lifeUntil = Environment.TickCount + 2600;
            BringToFront();
            if (!fade.Enabled) fade.Start();
            Invalidate();
        }

        void OnFade(object sender, EventArgs e)
        {
            if (Environment.TickCount > lifeUntil)
            {
                alpha -= 0.12f;
                if (alpha <= 0)
                {
                    fade.Stop();
                    Visible = false;
                    return;
                }
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // 点一下立即收起
            fade.Stop();
            Visible = false;
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Visible || msg.Length == 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int a = Math.Max(0, Math.Min(255, (int)(alpha * 255)));
            var r = ClientRectangle; r.Width--; r.Height--;
            using (var bmp = new Bitmap(Width, Height))
            {
                using (var bg = Graphics.FromImage(bmp))
                {
                    bg.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.FillRound(bg, r, Theme.Card, Theme.S(8));
                    using (var pen = new Pen(Theme.Border)) Theme.RoundRect(bg, r, Theme.S(8));
                    // 左侧语义色指示条
                    using (var b = new SolidBrush(tone))
                        bg.FillRectangle(b, r.X + 1, r.Y + Theme.S(8), Theme.S(3), r.Height - Theme.S(16));
                    UiUtil.Text(bg, msg, Theme.FontBody,
                        new RectangleF(r.X + Theme.S(14), r.Y, r.Width - Theme.S(20), r.Height), Theme.Text);
                }
                // 整体透明度（GDI+ 不支持绘制到控件时带 alpha，用 ColorMatrix 二次合成）
                if (a >= 255) { e.Graphics.DrawImage(bmp, 0, 0); return; }
                var cm = new System.Drawing.Imaging.ColorMatrix();
                cm.Matrix33 = a / 255f;
                using (var ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    var dest = new Rectangle(0, 0, Width, Height);
                    e.Graphics.DrawImage(bmp, dest, 0, 0, Width, Height, GraphicsUnit.Pixel, ia);
                }
            }
        }
    }
}

namespace WindowTool
{
    // ==================================================================
    // 页面基类
    // ==================================================================

    abstract class Page : Panel
    {
        public readonly AppForm App;
        public readonly string Key;
        public readonly string Title;

        protected Page(AppForm app, string key, string title)
        {
            App = app;
            Key = key;
            Title = title;
            BackColor = Theme.Shell;
            UiUtil.DoubleBuffer(this);
            AutoScroll = false;
        }

        public abstract void Build();
        public virtual void Relayout(int w, int h) { }
        public virtual void Tick() { }
        public virtual void OnShow() { }
        public virtual void OnHide() { }

        /// 放进内容区并重排。
        public void Place(Rectangle r)
        {
            SetBounds(r.X, r.Y, r.Width, r.Height);
            Relayout(r.Width, r.Height);
        }
    }

    // ==================================================================
    // 窗口列表：虚拟行 + 滚轮（不创建每行控件，几百个窗口也不卡）
    // ==================================================================

    class WinList : Control
    {
        public AppForm App;
        public readonly List<WinEntry> Items = new List<WinEntry>();
        int scroll;
        int hover = -1;
        public IntPtr Selected = IntPtr.Zero;

        public event EventHandler SelectedChanged;

        const int RowH = 34;    // 逻辑像素

        public WinList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Height = Theme.S(200);
        }

        public void Select(IntPtr h)
        {
            if (Selected != h)
            {
                Selected = h;
                Invalidate();
                var ev = SelectedChanged;
                if (ev != null) ev(this, EventArgs.Empty);
            }
        }

        void FireSel()
        {
            var ev = SelectedChanged;
            if (ev != null) ev(this, EventArgs.Empty);
        }

        int RowsVisible { get { return Math.Max(1, Height / Theme.S(RowH)); } }
        int MaxScroll
        {
            get { return Math.Max(0, Items.Count * Theme.S(RowH) - Height); }
        }

        public void EnsureVisible(int index)
        {
            int y = index * Theme.S(RowH);
            if (y < scroll) scroll = y;
            else if (y + Theme.S(RowH) > scroll + Height) scroll = y + Theme.S(RowH) - Height;
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0) lines = 3;
            int delta = Theme.S(RowH) * lines * (e.Delta > 0 ? 1 : -1);
            int ns = Math.Max(0, Math.Min(MaxScroll, scroll - delta));
            if (ns != scroll) { scroll = ns; Invalidate(); }
            // 不调 base：滚轮已消化，不再冒泡给外层滚动容器
        }

        int RowAt(int y) { return (y + scroll) / Theme.S(RowH); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = RowAt(e.Y);
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int i = RowAt(e.Y);
                if (i >= 0 && i < Items.Count)
                {
                    Selected = Items[i].Hwnd;
                    Invalidate();
                    FireSel();
                }
            }
            base.OnMouseDown(e);
            Focus();
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = ClientRectangle; r.Width--; r.Height--;
            Theme.FillRound(g, r, Theme.Card, Theme.S(10));
            using (var pen = new Pen(Theme.Border)) Theme.RoundRect(g, r, Theme.S(10));

            g.SetClip(new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2));

            int rowH = Theme.S(RowH);
            int first = scroll / rowH;
            int y = first * rowH - scroll;

            for (int i = first; i < Items.Count && y < Height; i++, y += rowH)
            {
                var it = Items[i];
                bool sel = it.Hwnd == Selected;
                var row = new Rectangle(Theme.S(3), y + 1, Width - Theme.S(6), rowH - 2);

                if (sel) Theme.FillRound(g, row, Theme.AccentSoft, Theme.S(6));
                else if (i == hover) Theme.FillRound(g, row, Theme.CardHi, Theme.S(6));

                // 进程图标
                int icon = Theme.S(20);
                if (it.Icon != null)
                    g.DrawImage(it.Icon, new Rectangle(Theme.S(10), y + (rowH - icon) / 2, icon, icon));

                // 标题（主）+ 进程名（副）
                int tx = Theme.S(36);
                int tw = Width - tx - Theme.S(86);
                UiUtil.Text(g, Theme.Ellipsis(it.Title, Theme.FontBody, tw - Theme.S(70)), Theme.FontBody,
                    new RectangleF(tx, y + Theme.S(3), tw, rowH / 2 - Theme.S(3)), sel ? Theme.Text : Theme.Text);
                UiUtil.Text(g, it.Proc, Theme.FontSmall,
                    new RectangleF(tx, y + rowH / 2, tw, rowH / 2 - Theme.S(4)), Theme.TextDim);

                // 右侧状态徽标：置顶 / 穿透 / 背景 / 拒绝
                int bx = Width - Theme.S(8);
                DrawChip(g, ref bx, y, "顶", Manager.IsTopmost(it.Hwnd), Theme.Accent);
                DrawChip(g, ref bx, y, "穿", Manager.HasOp2(it.Hwnd), Theme.Warn);
                DrawChip(g, ref bx, y, "景", App != null && App.HasBg(it.Hwnd), Theme.Good);
                if (Manager.IsDenied(it.Hwnd))
                    DrawChip(g, ref bx, y, "拒", true, Theme.Bad);
            }

            // 滚动条
            if (MaxScroll > 0)
            {
                int trackX = Width - Theme.S(4);
                float frac = (float)Height / (Items.Count * rowH);
                int thumbH = Math.Max(Theme.S(24), (int)(Height * frac));
                int thumbY = (int)((float)scroll / MaxScroll * (Height - thumbH));
                using (var b = new SolidBrush(Theme.Border))
                    g.FillRectangle(b, trackX, thumbY, Theme.S(3), thumbH);
            }
        }

        void DrawChip(Graphics g, ref int right, int rowY, string label, bool on, Color c)
        {
            right -= Theme.S(20);
            if (!on) return;
            var rect = new Rectangle(right, rowY + Theme.S(6), Theme.S(17), Theme.S(17));
            Theme.FillRound(g, rect, Theme.Alpha(c, 56), Theme.S(4));
            TextRenderer.DrawText(g, label, Theme.FontSmall, rect, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
