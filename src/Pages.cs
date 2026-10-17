using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowTool
{
    sealed class WindowPage : Page
    {
        MacList list;
        MacField search;
        MacButton btnRefresh, btnPick;
        MacSwitch swTop, swOp2;
        MacSlider slAlpha;
        MacSegmented segLayer;
        MacButton btnBottom, btnCancelAll;
        Label lblTarget, lblPinned;
        string filter = "";

        public WindowPage(MainForm h) : base(h) { }

        public override string Key { get { return "window"; } }
        public override string Title { get { return "窗口管理"; } }

        public override void Build()
        {
            search = new MacField();
            search.ShowIcon = true;
            search.Icon = Glyph.Search;
            search.Placeholder = "搜索窗口标题或进程名…";
            search.Width = H(220);
            search.TextChanged += (s, e) => { filter = search.Text.Trim(); RebuildList(); };

            btnRefresh = IBtn(Glyph.Refresh, MacTone.Normal, (s, e) => { Host.RefreshWindows(); RebuildList(); });
            btnRefresh.Width = H(38);
            btnPick = Btn("点选窗口", MacTone.Accent, (s, e) => Host.BeginPick());
            Fit(btnPick);

            list = new MacList();
            list.EmptyText = "没有匹配的窗口";
            list.SelectedIndexChanged += (s, e) =>
            {
                var it = list.SelectedItem;
                if (it != null) Host.SetTarget((IntPtr)it.Tag);
            };
            list.ItemActivate += (s, e) => { Host.SetTop(Host.Target, !Manager.IsTopmost(Host.Target)); };
            Host.TargetChanged += (s, e) => RebuildList();

            FreeCard("目标窗口", 268, LayoutPicker, search, btnRefresh, btnPick, list);

            swTop = new MacSwitch();
            swTop.CheckedChanged += (s, e) =>
            {
                if (!Host.Ok(Host.Target)) { swTop.SetCheckedSilent(false); return; }
                Host.SetTop(Host.Target, swTop.Checked);
            };
            swOp2 = new MacSwitch();
            swOp2.CheckedChanged += (s, e) =>
            {
                if (!Host.Ok(Host.Target)) { swOp2.SetCheckedSilent(false); return; }
                Host.ToggleOp2(Host.Target);
            };
            slAlpha = Slider(40, 255, 255, (s, e) => Host.SetAlpha(Host.Target, slAlpha.Value));
            slAlpha.Width = H(220);
            slAlpha.Suffix = "";
            lblTarget = Lbl("", Theme.FontSmall, Theme.TextDim);
            lblTarget.Width = H(300);

            Card("置顶与穿透",
                Row("始终置顶", "让该窗口浮在其他窗口之上（Ctrl+Alt+1）", swTop),
                Row("点击穿透", "鼠标点击落到下层窗口，同时保持置顶（Ctrl+Alt+2）", swOp2),
                Row("窗口透明度", "40 最透明 · 255 完全不透明，每个窗口独立记忆", slAlpha));

            segLayer = new MacSegmented();
            segLayer.SetItems(new[] { "上移一层", "下移一层" });
            segLayer.SizeToCells();
            segLayer.SelectedIndexChanged += (s, e) =>
            {
                if (segLayer.SelectedIndex == 0) Host.LayerDir(Host.Target, true);
                else if (segLayer.SelectedIndex == 1) Host.LayerDir(Host.Target, false);
            };
            btnBottom = Btn("移到最底", MacTone.Normal, (s, e) => Host.LayerBottom(Host.Target));
            btnBottom.Width = H(92);
            btnCancelAll = Btn("还原全部穿透置顶", MacTone.Danger, (s, e) => Host.CancelAllOp2());
            btnCancelAll.Width = H(150);
            lblPinned = Lbl("", Theme.FontSmall, Theme.TextDim);
            lblPinned.Width = H(300);

            Card("图层顺序",
                Row("层级调整", "仅在同一层级带内移动，不会误跨置顶层（Ctrl+Alt+3 / 4）", segLayer, btnBottom),
                Row("已开启穿透置顶", "先开启的窗口始终压在后开启的之上", btnCancelAll));

            OnTick(Sync);
        }

        void LayoutPicker(MacCard card, int w)
        {
            int pad = H(14);
            int limit = PackRight(card, 0, H(26), H(6), btnPick, btnRefresh);
            Put(search, card, pad, 0, limit - pad * 2, H(26));
            Put(list, card, pad, H(34), w - pad * 2, H(226));
        }

        void RebuildList()
        {
            var items = new List<MacListItem>();
            foreach (var win in Host.WindowList)
            {
                if (filter.Length > 0)
                {
                    string hay = (win.Title + " " + win.Proc + " " + win.Cls).ToLowerInvariant();
                    if (!hay.Contains(filter.ToLowerInvariant())) continue;
                }
                string flags = "";
                if (Manager.HasOp2(win.Hwnd)) flags = "穿透置顶";
                else if (Manager.IsTopmost(win.Hwnd)) flags = "置顶";
                else if (Host.HasBg(win.Hwnd)) flags = "有背景";
                items.Add(new MacListItem
                {
                    Title = win.Title,
                    Subtitle = win.Proc + " · " + win.Cls,
                    Detail = flags,
                    DetailColor = flags.Length == 0 ? Theme.TextFaint : Theme.Accent,
                    Image = win.Icon,
                    Tag = win.Hwnd
                });
            }
            IntPtr keep = Host.Target;
            list.SetItems(items);
            for (int i = 0; i < items.Count; i++)
                if ((IntPtr)items[i].Tag == keep) { list.SelectedIndex = i; return; }
            if (items.Count > 0) list.SelectedIndex = 0;
        }

        bool syncing;

        void Sync()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                var t = Host.Target;
                bool ok = Host.Ok(t);
                var e = ok ? Host.Entry(t) : null;
                lblTarget.Text = ok
                    ? "当前：" + (e == null ? TitleOf(t) : e.Title) + "　·　句柄 " + t.ToInt64()
                    : "当前：未选择窗口";
                swTop.Enabled = ok;
                swOp2.Enabled = ok;
                slAlpha.Enabled = ok;
                segLayer.Enabled = ok;
                btnBottom.Enabled = ok;
                swTop.SetCheckedSilent(ok && Manager.IsTopmost(t));
                swOp2.SetCheckedSilent(ok && Manager.HasOp2(t));
                if (ok) slAlpha.SetValueSilent(Manager.GetLastAlpha(t));
                btnCancelAll.Enabled = Manager.PinnedCount > 0;
                lblPinned.Text = Manager.PinnedCount + " 个";
                int idx = list.SelectedIndex;
                if (idx >= 0 && idx < list.Items.Count && (IntPtr)list.Items[idx].Tag != t) RebuildList();
            }
            finally { syncing = false; }
        }

        static string TitleOf(IntPtr h)
        {
            var sb = new System.Text.StringBuilder(512);
            Native.GetWindowText(h, sb, sb.Capacity);
            return sb.Length == 0 ? "(无标题)" : sb.ToString();
        }
    }

    sealed class BackgroundPage : Page
    {
        MacButton btnImg, btnVid, btnFolder, btnOff, btnRandom, btnPrev, btnNext;
        MacSwitch swMem;
        MacSlider slBgAlpha;
        MacSegmented segFit;
        MacNumber numMin, numSec;
        MacList listVid;
        Label lblState;

        public BackgroundPage(MainForm h) : base(h) { }

        public override string Key { get { return "bg"; } }
        public override string Title { get { return "窗口背景"; } }

        public override void Build()
        {
            btnImg = Btn("选择图片", MacTone.Accent, (s, e) => ApplyTo(Host.PickImageAndApply));
            btnVid = Btn("选择视频", MacTone.Normal, (s, e) => ApplyTo(Host.PickVideoAndApply));
            btnFolder = Btn("加载视频文件夹", MacTone.Normal, (s, e) => PickFolder());
            btnOff = Btn("取消当前背景", MacTone.Danger, (s, e) => ApplyTo(Host.ClearBgOn));
            Fit(btnImg); Fit(btnVid); Fit(btnFolder); Fit(btnOff);

            Card("背景来源",
                Row("图片背景", "目标窗口半透明，背景图紧贴其下方跟随", btnImg, btnVid),
                Row("视频背景", "无限循环播放，可作为轮播列表随机切换", btnFolder),
                Row("还原", "移除背景并恢复原始样式（Ctrl+Alt+0）", btnOff));

            segFit = new MacSegmented();
            segFit.SetItems(new[] { "填充裁剪", "完整显示", "变形铺满" });
            segFit.SelectedIndex = Host.Cfg.Int("fitMode", 0);
            segFit.SizeToCells();
            segFit.SelectedIndexChanged += (s, e) => Host.Fit = (DisplayFit)segFit.SelectedIndex;

            slBgAlpha = Slider(60, 250, Host.Cfg.Int("bgAlpha", 160), (s, e) => Host.SetBgAlpha(slBgAlpha.Value));
            slBgAlpha.Width = H(200);
            slBgAlpha.Suffix = "";

            numMin = Num(0, 120, Host.Cfg.Int("switchMin", 0), (s, e) => Host.Save());
            numMin.Width = H(52);
            numSec = Num(0, 59, Host.Cfg.Int("switchSec", 0), (s, e) => Host.Save());
            numSec.Width = H(52);

            Card("显示与切换",
                Row("显示模式", "填充裁剪无黑边不变形，推荐首选", segFit),
                Row("背景态透明度", "越低越能看清被背景挡住的窗口内容", slBgAlpha),
                Row("定时自动切换", "分与秒均为 0 表示关闭", numMin, numSec));

            btnRandom = Btn("随机切换", MacTone.Tinted, (s, e) => { Host.RandomVideo(); RebuildVideos(); });
            btnPrev = IBtn(Glyph.Prev, MacTone.Normal, (s, e) => Step(-1));
            btnNext = IBtn(Glyph.Next, MacTone.Normal, (s, e) => Step(1));
            Fit(btnRandom); btnPrev.Width = H(38); btnNext.Width = H(38);
            listVid = new MacList();
            listVid.EmptyText = "尚未加载视频文件夹";
            listVid.SelectedIndexChanged += (s, e) =>
            {
                var it = listVid.SelectedItem;
                if (it == null) return;
                int i = (int)it.Tag;
                Host.SetPlaylistTarget(Host.Target);
                Host.SwitchVideoTo(Host.Target, i);
            };
            lblState = Lbl("", Theme.FontSmall, Theme.TextDim);
            lblState.Width = H(380);
            Card("轮播控制",
                Row("切换视频", "上一条 / 下一条 / 随机，也可用 Ctrl+Alt+R", btnNext, btnPrev, btnRandom),
                Row("列表状态", "", lblState));
            FreeCard("轮播列表", H(150), LayoutVid, listVid);

            swMem = new MacSwitch();
            swMem.Checked = Host.BgMemory;
            swMem.CheckedChanged += (s, e) => { Host.Cfg.Set("bgMem", swMem.Checked); Host.Save(); };
            Card("持久化", Row("记住各窗口背景", "下次出现同名进程与标题的窗口时自动恢复（约 2 分钟内）", swMem));

            OnTick(Sync);
        }

        void LayoutVid(MacCard card, int w)
        {
            int pad = H(14);
            Put(listVid, card, pad, 0, w - pad * 2, H(150));
        }

        void Step(int dir)
        {
            var pl = Host.VideoPlaylist;
            if (pl == null || pl.Length == 0) { Host.ToastWarn("请先加载视频文件夹。"); return; }
            int i = Host.VideoIndex + dir;
            if (i < 0) i = pl.Length - 1;
            if (i >= pl.Length) i = 0;
            Host.SetPlaylistTarget(Host.Target);
            Host.SwitchVideoTo(Host.Target, i);
            RebuildVideos();
        }

        void ApplyTo(Action<IntPtr> act)
        {
            var t = Host.Target;
            if (!Host.Ok(t)) { Host.ToastWarn("请先在「窗口管理」页选择一个目标窗口。"); return; }
            act(t);
            RebuildVideos();
        }

        void PickFolder()
        {
            var t = Host.Target;
            if (!Host.Ok(t)) { Host.ToastWarn("请先在「窗口管理」页选择一个目标窗口。"); return; }
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择包含视频的文件夹";
                if (dlg.ShowDialog(Host) != DialogResult.OK) return;
                if (!Host.LoadVideoFolder(dlg.SelectedPath)) { Host.ToastWarn("该文件夹没有可用视频文件。"); return; }
                Host.SetPlaylistTarget(t);
                Host.SwitchVideo(t);
                RebuildVideos();
            }
        }

        void RebuildVideos()
        {
            var pl = Host.VideoPlaylist;
            var items = new List<MacListItem>();
            if (pl != null)
                for (int i = 0; i < pl.Length; i++)
                    items.Add(new MacListItem
                    {
                        Title = System.IO.Path.GetFileName(pl[i]),
                        ShowIcon = true,
                        Icon = Glyph.Film,
                        Detail = i == Host.VideoIndex ? "播放中" : "",
                        DetailColor = Theme.Green,
                        Tag = i
                    });
            listVid.SetItems(items);
        }

        void Sync()
        {
            var t = Host.Target;
            bool ok = Host.Ok(t);
            btnImg.Enabled = ok; btnVid.Enabled = ok; btnFolder.Enabled = ok;
            btnOff.Enabled = ok && Host.HasBg(t);
            segFit.SetIndexSilent(Host.Cfg.Int("fitMode", 0));
            int n = Manager.PinnedCount;
            lblState.Text = "轮播 " + (Host.VideoPlaylist == null ? 0 : Host.VideoPlaylist.Length) +
                " 个视频　·　当前背景窗口 " + Host.BgCount + " 个　·　穿透置顶 " + n + " 个";
        }
    }

    sealed class AlertsPage : Page
    {
        MacSwitch swOn, swLag;
        MacField kwProc, kwTitle;
        MacButton btnTest;
        Label lblStatus;

        public AlertsPage(MainForm h) : base(h) { }

        public override string Key { get { return "alerts"; } }
        public override string Title { get { return "被控提醒"; } }

        public override void Build()
        {
            swOn = new MacSwitch();
            swOn.Checked = Host.Cfg.Bool("uuEnabled", false);
            swOn.CheckedChanged += (s, e) =>
            {
                Configure();
                if (swOn.Checked) UuMonitor.Start(); else UuMonitor.Stop();
                Host.Save();
            };
            swLag = new MacSwitch();
            swLag.Checked = Host.Cfg.Bool("uuLag", true);
            swLag.CheckedChanged += (s, e) => { Configure(); Host.Save(); };

            kwProc = Field(Host.Cfg.Str("uuProc", "uu,gameviewer"), "逗号分隔多个关键词", null);
            kwProc.Width = H(200);
            kwTitle = Field(Host.Cfg.Str("uuTitle", "远程"), "留空则不校验标题", null);
            kwTitle.Width = H(140);
            kwProc.TextChanged += (s, e) => { if (swOn.Checked) Configure(); Host.Save(); };
            kwTitle.TextChanged += (s, e) => { if (swOn.Checked) Configure(); Host.Save(); };

            btnTest = Btn("试弹一次提醒", MacTone.Normal, (s, e) => UuMonitor.TestOnce());
            Fit(btnTest);

            Card("检测开关",
                Row("启用被控提醒", "在远程控制接管时第一时间在鼠标旁提示", swOn),
                Row("界面卡顿时也提醒", "采样消息循环延迟，捕捉远控造成的异常卡顿", swLag));

            Card("匹配规则",
                Row("进程名 / 路径包含", "匹配进程名或 exe 完整路径，逗号分隔", kwProc),
                Row("窗口标题包含", "作为补充判据，命中即提醒", kwTitle),
                Row("手动测试", "验证提醒样式，不影响检测状态", btnTest));

            lblStatus = Lbl("", Theme.FontUi, Theme.TextDim);
            lblStatus.Width = H(420);
            lblStatus.Height = H(18);
            Card("当前状态", Row("监控状态", "", lblStatus));

            var info = Lbl(
                "判定为启发式：内存 >100MB 进入被控态，<50MB 判定解除，中间区间保持原状态以防抖。" +
                "被控期间红色迷你条跟随鼠标循环提醒（显示 2 秒、隐藏 4 秒），解除时单次提示 3 秒。",
                Theme.FontSmall, Theme.TextDim);
            info.Height = H(46);
            FreeCard("判定逻辑", H(46), (card, w) => Put(info, card, H(14), 0, w - H(28), H(46)));

            UuMonitor.StatusChanged += msg =>
            {
                try { BeginInvoke((Action)delegate { lblStatus.Text = msg; }); } catch { }
            };
            OnTick(() => { if (!swOn.Checked) lblStatus.Text = "未启用。"; });
        }

        void Configure()
        {
            UuMonitor.Configure(kwProc.Text, kwTitle.Text, swLag.Checked);
        }
    }
}
