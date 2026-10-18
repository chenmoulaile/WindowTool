using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowTool
{
    /// macOS 风格托盘菜单：圆角面板、整条强调色悬停胶囊、矢量图标、逐条错峰滑入。
    /// 条目走 OwnerDraw——ToolStrip 只在 item.Image 非空时才回调 RenderItemImage，
    /// 靠渲染器画图标会静默失效，自己接管 Paint 才有确定的绘制路径。
    sealed class MacMenu : ContextMenuStrip
    {

        /// 打开前回调：置顶列表、播放态随时变化，条目必须按当前状态重建。
        public Action OnRefresh;

        public MacMenu() : base()
        {
            // 只赋 Renderer：它的 setter 会顺带把 RenderMode 切成 Custom。
            // 反过来先写 RenderMode=Custom 会抛 NotSupportedException，
            // 而写 RenderMode=Professional 则把自绘渲染器换回内置实例、静默失效。
            Renderer = new MacMenuRenderer();
            Font = Theme.FontUi;
            BackColor = Surface();
            ForeColor = Theme.Text;
        }

        public static Color Surface()
        {
            return Theme.IsDark ? Color.FromArgb(46, 46, 51) : Color.FromArgb(251, 251, 253);
        }

        // macOS 菜单是紧凑的：行高 22、图标槽 22。之前 30 的行长得很"Windows 95"。
        public int Pad { get { return Theme.S(5); } }
        public int RowH { get { return Theme.S(20); } }
        public int Gutter { get { return Theme.S(24); } }
        public int HeadH { get { return Theme.S(17); } }

        /// 分隔线条目的宽度：与最宽条目对齐，靠 Layout 阶段回填。
        public int LineWidth
        {
            get
            {
                int w = Theme.S(120);
                foreach (ToolStripItem it in Items)
                {
                    var m = it as MacMenuItem;
                    if (m != null && !m.Line && m.Width > w) w = m.Width;
                }
                return w + Pad * 2;
            }
        }

        /// 所有条目统一成最宽那条的宽度。
        /// 之前每行按自己的文字定宽，于是胶囊有的长有的短、图标列也对不齐，
        /// 而且最长的条目会顶出面板边界——这就是看到的"错位"。
        void Fit()
        {
            int content = 0;
            foreach (ToolStripItem it in Items)
            {
                var m = it as MacMenuItem;
                if (m == null) continue;
                m.Relayout();                     // 分隔线/小标题的高度在这里才落定
                if (!m.Line) content = Math.Max(content, m.NaturalWidth);
            }
            if (content < Theme.S(196)) content = Theme.S(196);
            foreach (ToolStripItem it in Items)
            {
                var m = it as MacMenuItem;
                if (m != null) m.Width = content;
            }
            // 改条目宽度不会让 ToolStrip 自己重算外框：不显式定尺寸，
            // 面板会停在上一次的宽度上，条目就被裁掉——看起来就是"错位"。
            Padding = new Padding(Pad, Pad, Pad, Pad);
            AutoSize = false;
            Width = content + Pad * 2;
            PerformLayout();
            // ToolStrip 在右侧还留了一条内部空隙（滚动按钮位）。不补偿的话
            // 左内缩 12、右内缩 22，胶囊看起来明显偏左——就是"廉价感"的来源之一。
            int chrome = Width - DisplayRectangle.Width - Pad * 2;
            if (chrome < 0) chrome = 0;
            Padding = new Padding(Pad + chrome, Pad, Pad + chrome, Pad);
            Width = content + (Pad + chrome) * 2;
            PerformLayout();
            int inner = Math.Max(Theme.S(80), DisplayRectangle.Width);
            foreach (ToolStripItem it in Items)
            {
                var m = it as MacMenuItem;
                if (m != null) m.Width = inner;
            }
            PerformLayout();
            int bottom = 0;
            foreach (ToolStripItem it in Items) bottom = Math.Max(bottom, it.Bounds.Bottom);
            Height = bottom + Pad;
            PerformLayout();
        }

        public MacMenuItem AddItem(string text, Glyph icon, EventHandler onClick)
        {
            return AddItem(text, icon, MacTone.Normal, onClick);
        }

        /// 分组小标题：不可点，只负责把长菜单切成几段。
        public MacMenuItem AddHeader(string text)
        {
            var h = new MacMenuItem(this, text, Glyph.Close, MacTone.Normal, null);
            h.Header = true;
            h.Enabled = false;
            Items.Add(h);
            Fit();
            return h;
        }

        public MacMenuItem AddItem(string text, Glyph icon, MacTone tone, EventHandler onClick)
        {
            var it = new MacMenuItem(this, text, icon, tone, onClick);
            Items.Add(it);
            Fit();
            return it;
        }

        /// 分隔线用自绘条目而不是 ToolStripSeparator：后者的默认绘制会在
        /// 左侧图标槽留下两段亮线，怎么覆盖都盖不住。
        public MacMenuItem AddSep()
        {
            var s = new MacMenuItem(this, "", Glyph.Close, MacTone.Normal, null);
            s.Line = true;
            s.Enabled = false;
            Items.Add(s);
            Fit();
            return s;
        }

        protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
        {
            if (OnRefresh != null) { try { OnRefresh(); } catch (Exception ex) { Program.Log(ex); } }
            Font = Theme.FontUi;
            BackColor = Surface();
            ForeColor = Theme.Text;
            Padding = new Padding(Pad, Pad, Pad + Theme.S(6), Pad);
            Fit();
            base.OnOpening(e);
            ApplyShape();
        }

        /// 圆角靠窗口 Region 裁出来：菜单是独立 HWND，不能像普通控件那样画透明角。
        void ApplyShape()
        {
            try
            {
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                int rad = Theme.S(11);
                Region old = Region;
                using (var p = Theme.RoundRect(new Rectangle(0, 0, w, h), rad))
                    Region = new Region(p);
                if (old != null) old.Dispose();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Visible) ApplyShape();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW：跟随 Region 的圆角投影
                return cp;
            }
        }
    }

    /// 单条菜单项：自己画悬停胶囊、图标、文字与勾选。
    sealed class MacMenuItem : ToolStripMenuItem
    {
        readonly MacMenu menu;
        readonly HoverFade fade;
        public Glyph Icon = Glyph.ChevronR;
        public MacTone Tone = MacTone.Normal;
        public bool Flag;
        public bool Line;
        public bool Header;
        public string Hint = "";

        public MacMenuItem(MacMenu menu, string text, Glyph icon, MacTone tone, EventHandler onClick)
            : base(text)
        {
            this.menu = menu;
            Icon = icon;
            Tone = tone;
            AutoToolTip = false;
            AutoSize = false;
            Height = menu.RowH;
            Margin = new Padding(0);
            DisplayStyle = ToolStripItemDisplayStyle.None;   // 文字与图标全部自己画
            fade = new HoverFade(delegate { if (!IsDisposed) Invalidate(); });
            if (onClick != null) Click += onClick;
            // ToolStripItem 没有虚 OnSelected / SelectedChanged，只能挂鼠标事件
            MouseEnter += delegate { fade.To(1f, 110); };
            MouseLeave += delegate { fade.To(0f, 150); };
            Relayout();
        }

        /// 按文字实测的自然宽度（不含右对齐勾选的额外位置）。
        public int NaturalWidth
        {
            get
            {
                int tw = TextRenderer.MeasureText(Text ?? "", menu.Font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                int hw = string.IsNullOrEmpty(Hint) ? 0 :
                    TextRenderer.MeasureText(Hint, Theme.FontSmall, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + Theme.S(22);
                return menu.Gutter + tw + Theme.S(30) + hw;
            }
        }

        public void Relayout()
        {
            Height = Line ? Theme.S(9) : (Header ? menu.HeadH : menu.RowH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            // 不 Clear：面板底色与描边由宿主画，条目一擦就把它断成一块块矩形
            if (Line)
            {
                using (var pen = new Pen(Theme.Separator, Math.Max(1f, Theme.S(1))))
                    g.DrawLine(pen, Theme.S(10), Height / 2, Math.Max(Theme.S(10), Width - Theme.S(8)), Height / 2);
                return;
            }
            if (Header)
            {
                MacUi.LeftText(g, Text ?? "", Theme.FontGroup,
                    new RectangleF(Theme.S(11), 0, Width - Theme.S(18), Height), Theme.TextFaint);
                return;
            }

            float a = fade.Value;
            Color hot = Tone == MacTone.Danger ? Theme.Red : Theme.Accent;
            if (a > 0.01f && Enabled)
            {
                // 软底 + 左侧强调条。整条刷成实心强调色的那种高亮块
                // 在小菜单里非常突兀，是"廉价感"的主要来源。
                var pill = new RectangleF(Theme.S(3), 1, Width - Theme.S(6), Height - 2);
                using (var p = Theme.RoundRectF(pill, Theme.S(5)))
                    Theme.FillPath(g, p, Theme.Alpha(hot, (int)((Theme.IsDark ? 54 : 32) * a)));
                var bar = new RectangleF(Theme.S(2), pill.Y + pill.Height * 0.24f,
                    Theme.S(2), pill.Height * 0.52f);
                using (var p = Theme.RoundRectF(bar, Theme.S(1)))
                    Theme.FillPath(g, p, Theme.Alpha(hot, (int)(230 * a)));
            }

            Color text = !Enabled ? Theme.TextFaint : Theme.Text;
            Color glyph = !Enabled ? Theme.TextFaint : Theme.TextDim;
            if (a > 0.01f && Enabled)
            {
                text = Theme.Blend(text, hot, a);
                glyph = Theme.Blend(glyph, hot, a);
            }

            int d = Theme.S(12);
            Icons.Draw(g, Icon, new RectangleF(Theme.S(8), (Height - d) / 2f, d, d), glyph);

            int right = Width - Theme.S(8);
            if (Flag)
            {
                int cd = Theme.S(12);
                Icons.Draw(g, Glyph.Check,
                    new RectangleF(right - cd, (Height - cd) / 2f, cd, cd),
                    Enabled ? Theme.Accent : Theme.TextFaint);
                right -= cd + Theme.S(8);
            }
            if (!string.IsNullOrEmpty(Hint))
            {
                int hw = TextRenderer.MeasureText(Hint, Theme.FontSmall, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                TextRenderer.DrawText(g, Hint, Theme.FontSmall,
                    new Rectangle(right - hw, 0, hw + 2, Height),
                    Enabled ? Theme.TextFaint : Theme.Alpha(Theme.TextFaint, 120),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            TextRenderer.DrawText(g, Text ?? "", menu.Font,
                new Rectangle(menu.Gutter, 0, Math.Max(Theme.S(8), right - menu.Gutter - Theme.S(6)), Height), text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// 面板底、分隔线与子菜单箭头。条目本身由 MacMenuItem 自绘。
    sealed class MacMenuRenderer : ToolStripProfessionalRenderer
    {
        public MacMenuRenderer() { RoundedEdges = false; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            var menu = e.ToolStrip as MacMenu;
            var g = e.Graphics;
            if (menu == null) { base.OnRenderToolStripBackground(e); return; }
            Theme.Hint(g);
            g.Clear(MacMenu.Surface());
            var r = new Rectangle(0, 0, menu.Width - 1, menu.Height - 1);
            int rad = Theme.S(11);
            // 顶部柔光必须一路渐隐到底：用半高的实心块会在条目没铺满的右侧留一条硬边
            using (var p = Theme.RoundRect(r, rad))
            {
                var rf = new RectangleF(0, 0, menu.Width, menu.Height);
                using (var glow = new LinearGradientBrush(rf,
                    Theme.Alpha(Color.White, Theme.IsDark ? 8 : 26),
                    Theme.Alpha(Color.White, 0), 90f))
                {
                    g.SetClip(p);
                    g.FillRectangle(glow, 0, 0, menu.Width, menu.Height);
                    g.ResetClip();
                }
                // 描边固定 1 物理像素：按 DPI 放大到 2px 会让左边缘看起来像一排小方块
                Theme.StrokePath(g, p, Theme.Alpha(Theme.Text, Theme.IsDark ? 46 : 30), 1f);
            }
        }

        /// 条目自绘，这里必须让路：否则文字会被画两遍，看起来像加粗。
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is MacMenuItem) return;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is MacMenuItem) return;
            base.OnRenderItemBackground(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            int d = Theme.S(9);
            var box = new RectangleF(e.ArrowRectangle.X + e.ArrowRectangle.Width - d - Theme.S(4),
                e.ArrowRectangle.Y + (e.ArrowRectangle.Height - d) / 2f, d, d);
            Icons.Draw(g, Glyph.ChevronR, box,
                e.Item.Selected && e.Item.Enabled ? Color.White : Theme.TextDim);
        }
    }
}