using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>
    /// 布局方案页：保存整桌窗口摆位，一键套回。
    /// </summary>
    sealed class LayoutPage : Page
    {
        UiCard saveCard;
        UiInput nameInput;
        UiButton saveBtn;
        LabelEx hint;

        UiCard listCard;
        readonly List<PresetRow> rows = new List<PresetRow>();
        LabelEx empty;

        public LayoutPage(AppForm app)
            : base(app, "layout", "布局方案")
        {
        }

        public override void Build()
        {
            saveCard = new UiCard { Title = "保存当前布局" };
            hint = new LabelEx
            {
                Font = Theme.FontSmall,
                TextColor = Theme.TextDim,
                Text2 = "记录所有可见顶层窗口的位置 / 大小 / 置顶 / 透明度（最多 40 个），按「进程名 + 标题」匹配套回。"
            };
            saveCard.Controls.Add(hint);

            nameInput = new UiInput();
            UiUtil.SetCue(nameInput.Box, "输入方案名，例如：写题布局");
            saveCard.Controls.Add(nameInput);

            saveBtn = new UiButton { Label = "保存当前布局", Icon = Glyph.Plus, Kind = UiButtonKind.Primary };
            saveBtn.Click += (s, e) =>
            {
                string nm = nameInput.Text2.Trim();
                if (nm.Length == 0) { App.ToastWarn("请先填写方案名。"); return; }
                var p = App.CapturePreset(nm);
                if (p.Items.Count == 0) { App.ToastWarn("当前没有可记录的窗口。"); return; }
                App.AddPreset(p);
                nameInput.Text2 = "";
                App.ToastGood("已保存「" + nm + "」（" + p.Items.Count + " 个窗口）。");
                RebuildRows();
            };
            saveCard.Controls.Add(saveBtn);
            Controls.Add(saveCard);

            listCard = new UiCard { Title = "已保存的方案" };
            empty = new LabelEx { Font = Theme.FontSmall, TextColor = Theme.TextFaint, Text2 = "（还没有保存过方案）" };
            listCard.Controls.Add(empty);
            Controls.Add(listCard);

            RebuildRows();
        }

        void RebuildRows()
        {
            foreach (var r in rows)
            {
                listCard.Controls.Remove(r);
                r.Dispose();
            }
            rows.Clear();

            foreach (var p in App.Presets)
            {
                var row = new PresetRow(App, p);
                row.Changed += RebuildRows;
                listCard.Controls.Add(row);
                rows.Add(row);
            }
            empty.Visible = rows.Count == 0;
            Relayout(Width, Height);
        }

        public override void OnShow()
        {
            RebuildRows();
        }

        public override void Relayout(int w, int h)
        {
            if (saveCard == null) return;
            int cw = Math.Min(w - Theme.S(8), Theme.S(560));
            int x = Math.Max(0, (w - cw) / 2);

            saveCard.SetBounds(x, 0, cw, Theme.S(146));
            hint.SetBounds(Theme.S(14), saveCard.HeaderH + Theme.S(2), cw - Theme.S(28), Theme.S(34));
            hint.Multiline = true;
            nameInput.SetBounds(Theme.S(14), saveCard.HeaderH + Theme.S(42), cw - Theme.S(28) - Theme.S(150), Theme.S(30));
            saveBtn.SetBounds(cw - Theme.S(14) - Theme.S(140), saveCard.HeaderH + Theme.S(42), Theme.S(140), Theme.S(30));

            int ly = Theme.S(146) + Theme.S(14);
            int rowsH = 0;
            foreach (var r in rows) rowsH += r.Height + Theme.S(6);
            int listH = Math.Max(Theme.S(140), rowsH + listCard.HeaderH + Theme.S(46));
            listCard.SetBounds(x, ly, cw, listH);
            empty.SetBounds(Theme.S(14), listCard.HeaderH + Theme.S(6), cw - Theme.S(28), Theme.S(18));

            int yy = listCard.HeaderH + Theme.S(6);
            foreach (var r in rows)
            {
                r.SetBounds(Theme.S(10), yy, cw - Theme.S(20), r.Height);
                yy += r.Height + Theme.S(6);
            }
        }

        sealed class PresetRow : Control
        {
            readonly AppForm app;
            readonly Preset preset;
            LabelEx nameL, infoL;
            UiButton applyBtn, delBtn;

            public event Action Changed;

            public PresetRow(AppForm a, Preset p)
            {
                app = a;
                preset = p;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

                nameL = new LabelEx { Font = Theme.FontBodyBold, TextColor = Theme.Text, Text2 = p.Name };
                Controls.Add(nameL);
                infoL = new LabelEx
                {
                    Font = Theme.FontSmall,
                    TextColor = Theme.TextDim,
                    Text2 = p.Items.Count + " 个窗口"
                };
                Controls.Add(infoL);

                applyBtn = new UiButton { Label = "套用", Icon = Glyph.Play, Kind = UiButtonKind.Primary, Small = true };
                applyBtn.Click += delegate
                {
                    int hit = app.ApplyPreset(preset);
                    app.Toast(hit > 0
                        ? "已套用「" + preset.Name + "」：命中 " + hit + "/" + preset.Items.Count + " 个窗口。"
                        : "没有匹配到窗口（程序没开或标题已变）。",
                        hit > 0 ? ToastKind.Good : ToastKind.Warn);
                };
                Controls.Add(applyBtn);

                delBtn = new UiButton { Label = "删除", Icon = Glyph.Trash, Kind = UiButtonKind.Danger, Small = true };
                delBtn.Click += delegate
                {
                    app.DeletePreset(preset);
                    var h = Changed;
                    if (h != null) h();
                };
                Controls.Add(delBtn);

                // v2.0.1：尺寸赋值放在子控件创建之后（设 Height 会同步触发 OnResize）
                Height = Theme.S(44);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (applyBtn == null || nameL == null) return;
                int aw = Theme.S(78), dw = Theme.S(78);
                applyBtn.SetBounds(Width - aw - dw - Theme.S(12), (Height - Theme.S(26)) / 2, aw, Theme.S(26));
                delBtn.SetBounds(Width - dw - Theme.S(4), (Height - Theme.S(26)) / 2, dw, Theme.S(26));
                nameL.SetBounds(Theme.S(10), Theme.S(3), Width - aw - dw - Theme.S(28), Theme.S(20));
                infoL.SetBounds(Theme.S(10), Theme.S(22), Width - aw - dw - Theme.S(28), Theme.S(16));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var r = ClientRectangle; r.Width--; r.Height--;
                Theme.FillRound(g, r, Theme.InputBg, Theme.S(6));
                using (var pen = new Pen(Theme.Border)) Theme.RoundRect(g, r, Theme.S(6));
            }
        }
    }
}
