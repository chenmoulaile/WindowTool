using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    /// 布局方案：把当前整桌窗口的位置 / 大小 / 置顶 / 透明度存成一个命名方案，一键切回。
    sealed class LayoutPage : Page
    {
        MacList list;
        MacField nameField;
        MacButton btnSave, btnApply, btnDel;
        Label lblHint;

        public LayoutPage(MainForm h) : base(h) { }

        public override string Key { get { return "layout"; } }
        public override string Title { get { return "布局方案"; } }

        public override void Build()
        {
            nameField = new MacField();
            nameField.Placeholder = "方案名，例如 刷题 / 直播";
            nameField.Width = H(180);

            btnSave = Btn("保存当前布局", MacTone.Accent, (s, e) => Save());
            btnApply = Btn("套用", MacTone.Tinted, (s, e) => Apply());
            btnDel = Btn("删除", MacTone.Danger, (s, e) => Delete());
            Fit(btnSave); Fit(btnApply); Fit(btnDel);

            lblHint = Lbl("", Theme.FontSmall, Theme.TextDim);

            list = new MacList();
            list.EmptyText = "还没有方案：摆好窗口后点「保存当前布局」";
            list.ItemActivate += (s, e) => Apply();

            FreeCard("已保存的方案", H(268), Layout, nameField, btnSave, btnApply, btnDel, list, lblHint);

            Card("说明",
                Row("匹配方式", "窗口句柄每次开机都变，所以按 进程名 + 标题 重新找窗口"),
                Row("最小化窗口", "抓取时跳过最小化的窗口；套用时会自动还原"),
                Row("上限", "每个方案最多记录 40 个窗口"));

            OnTick(Sync);
        }

        void Layout(MacCard card, int w)
        {
            int pad = H(14);
            Put(list, card, pad, H(36), w - pad * 2, H(210));
            Put(lblHint, card, pad, H(250), w - pad * 2, H(18));
            Put(btnDel, card, w - pad - btnDel.Width, 0, btnDel.Width, H(26));
            Put(btnApply, card, btnDel.Left - H(6) - btnApply.Width, 0, btnApply.Width, H(26));
            Put(btnSave, card, btnApply.Left - H(8) - btnSave.Width, 0, btnSave.Width, H(26));
            Put(nameField, card, pad, 0, btnSave.Left - pad - H(10), H(26));
        }

        void Save()
        {
            string nm = nameField.Text.Trim();
            if (nm.Length == 0) { Host.ToastWarn("请先给方案起个名字。"); return; }
            var p = Host.CapturePreset(nm);
            if (p.Items.Count == 0) { Host.ToastWarn("没有可记录的窗口。"); return; }
            Host.AddPreset(p);
            Rebuild();
            Host.ToastGood("已保存「" + nm + "」，含 " + p.Items.Count + " 个窗口。");
        }

        void Apply()
        {
            var it = list.SelectedItem;
            if (it == null) { Host.ToastWarn("请先选中一个方案。"); return; }
            var p = it.Tag as Preset;
            int hit = Host.ApplyPreset(p);
            if (hit == 0) Host.ToastWarn("「" + p.Name + "」里的窗口当前都不存在，没能套用。");
            else Host.ToastGood("已套用「" + p.Name + "」：" + hit + " / " + p.Items.Count + " 个窗口。");
        }

        void Delete()
        {
            var it = list.SelectedItem;
            if (it == null) { Host.ToastWarn("请先选中要删除的方案。"); return; }
            var p = it.Tag as Preset;
            Host.DeletePreset(p);
            Rebuild();
            Host.Toast("已删除方案「" + p.Name + "」。");
        }

        void Rebuild()
        {
            var items = new List<MacListItem>();
            foreach (var p in Host.Presets)
                items.Add(new MacListItem
                {
                    Title = p.Name,
                    Subtitle = p.Items.Count + " 个窗口",
                    ShowIcon = true,
                    Icon = Glyph.Layers,
                    Detail = "",
                    Tag = p
                });
            list.SetItems(items);
        }

        bool syncing;
        void Sync()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                if (list.Items.Count != Host.Presets.Count) Rebuild();
                var sel = list.SelectedItem;
                bool has = sel != null;
                btnApply.Enabled = has;
                btnDel.Enabled = has;
                btnSave.Enabled = Host.WindowList.Count > 0;
                lblHint.Text = "当前桌面有 " + Host.WindowList.Count + " 个可记录的窗口；共 " +
                    Host.Presets.Count + " 个方案";
            }
            finally { syncing = false; }
        }
    }
}
