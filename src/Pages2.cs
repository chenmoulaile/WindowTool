using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WindowTool
{
    sealed class MusicPage : Page
    {
        MacSegmented segSrc;
        MacField search;
        MacButton btnSearch, btnScan, btnImport;
        MacList list;
        MacButton btnShuffle, btnPrev, btnPlay, btnNext, btnLoop, btnStop;
        MacSlider seek, vol;
        Label tCur, tDur, lblNow, lblLine;
        MacSwitch swDesk, swBar;
        MacNumber numOffset;
        List<Track> shown = new List<Track>();
        List<Track> localCache;
        bool seeking;
        bool busy;

        public MusicPage(MainForm h) : base(h) { }

        public override string Key { get { return "music"; } }
        public override string Title { get { return "音乐与歌词"; } }

        public override void Build()
        {
            segSrc = new MacSegmented();
            segSrc.SetItems(new[] { "本地歌库", "网易云搜索" });
            segSrc.SizeToCells();
            segSrc.SelectedIndexChanged += (s, e) => { ApplySourceMode(); RebuildRows(); };

            search = new MacField();
            search.ShowIcon = true;
            search.Icon = Glyph.Search;
            search.Placeholder = "输入歌名 / 歌手，回车搜索";
            search.Width = H(220);
            btnSearch = Btn("搜索", MacTone.Accent, (s, e) => DoSearch());
            Fit(btnSearch);
            btnScan = Btn("扫描本地曲库", MacTone.Normal, (s, e) => ScanLocal());
            Fit(btnScan);
            btnImport = Btn("导入文件夹", MacTone.Normal, (s, e) => Import());
            search.Width = H(150);
            Fit(btnImport);

            list = new MacList();
            list.EmptyText = "点击「扫描本地曲库」载入本机音乐，或切到网易云搜索";
            list.ItemActivate += (s, e) => PlaySelected();

            Card("曲库来源",
                Row("曲目列表", "本地歌库扫描网易云客户端下载目录与系统音乐库", segSrc),
                Row("在线搜索", "接口返回的试听链接受版权限制，VIP 歌曲无法播放", search, btnSearch),
                Row("本地曲库", "扫描或临时导入一个文件夹", btnScan, btnImport));
            FreeCard("播放列表", H(240), LayoutLib, list);

            btnShuffle = IBtn(Glyph.Shuffle, MacTone.Normal, (s, e) => ToggleShuffle());
            btnPrev = IBtn(Glyph.Prev, MacTone.Normal, (s, e) => { Host.Music.Prev(); });
            btnPlay = IBtn(Glyph.Play, MacTone.Accent, (s, e) => { Host.Music.Toggle(); });
            btnNext = IBtn(Glyph.Next, MacTone.Normal, (s, e) => { Host.Music.Next(true); });
            btnLoop = IBtn(Glyph.Repeat, MacTone.Normal, (s, e) => CycleLoop());
            btnStop = IBtn(Glyph.Stop, MacTone.Normal, (s, e) => Host.Music.Eject());
            foreach (var b in new[] { btnShuffle, btnPrev, btnPlay, btnNext, btnLoop, btnStop }) b.Width = H(38);

            seek = Slider(0, 1000, 0, (s, e) =>
            {
                if (!seeking) return;
                tCur.Text = Fmt(TimeSpan.FromSeconds(seek.Value / 1000.0 * seek.Aux));
            });
            seek.Width = H(300);
            seek.ShowValue = false;
            seek.DragFinished += (s, e) =>
            {
                if (!seeking) return;
                seeking = false;
                Host.Music.Seek(seek.Value / 1000.0);
            };
            seek.MouseDown += (s, e) => seeking = true;
            tCur = Lbl("0:00", Theme.FontSmall, Theme.TextDim);
            tCur.Width = H(40);
            tDur = Lbl("0:00", Theme.FontSmall, Theme.TextDim);
            tDur.Width = H(40);
            tDur.TextAlign = ContentAlignment.MiddleRight;

            vol = Slider(0, 100, (int)Host.Music.Volume, (s, e) => { Host.Music.Volume = vol.Value; Host.Save(); });
            vol.Width = H(160);
            vol.Suffix = "%";
            lblNow = Lbl("未在播放", Theme.FontUiMedium, Theme.Text);
            lblNow.Width = H(420);
            lblLine = Lbl("", Theme.FontUi, Theme.Accent);
            lblLine.Width = H(420);

            FreeCard("播放", H(130), LayoutPlay, lblNow, btnShuffle, btnPrev, btnPlay, btnNext, btnLoop, btnStop,
                seek, tCur, tDur, vol, lblLine);

            swDesk = new MacSwitch();
            swDesk.Checked = Host.DesktopLyricOn;
            swDesk.CheckedChanged += (s, e) => Host.SetDesktopLyric(swDesk.Checked);
            swBar = new MacSwitch();
            swBar.Checked = Host.TaskbarLyricOn;
            swBar.CheckedChanged += (s, e) => Host.SetTaskbarLyric(swBar.Checked);
            numOffset = Num(-10000, 10000, Host.Cfg.Int("lyricOffset", 0), (s, e) =>
            {
                Host.Music.OffsetMs = (int)numOffset.Value;
                Host.Save();
            });
            numOffset.Width = H(76);

            Card("歌词",
                Row("桌面歌词", "悬浮圆角胶囊，可拖动，不抢焦点", swDesk),
                Row("任务栏歌词", "贴附任务栏上沿，鼠标穿透不挡操作", swBar),
                Row("歌词时间偏移", "单位毫秒，正数让歌词提前，负数延后", numOffset));

            ApplySourceMode();
            OnTick(Sync);
            if (localCache == null && !MainForm.DesignRender) ScanLocal(false);
        }

        void LayoutLib(MacCard card, int w)
        {
            int pad = H(14);
            Put(list, card, pad, 0, w - pad * 2, H(240));
        }

        void LayoutPlay(MacCard card, int w)
        {
            int pad = H(14);
            Put(lblNow, card, pad, 0, w - pad * 2, H(22));
            PackCenter(card, H(32), H(30), H(6), btnShuffle, btnPrev, btnPlay, btnNext, btnLoop, btnStop);
            Put(tCur, card, pad, H(74), H(48), H(18));
            Put(seek, card, pad + H(52), H(72), Math.Max(H(80), w - pad * 2 - H(104) - H(200)), H(22));
            Put(vol, card, w - pad - H(160), H(72), H(160), H(22));
            Put(tDur, card, w - pad - H(160) - H(52), H(74), H(48), H(18));
            Put(lblLine, card, pad, H(100), w - pad * 2, H(22));
        }

        void ApplySourceMode()
        {
            bool net = segSrc.SelectedIndex == 1;
            search.Visible = net;
            btnSearch.Visible = net;
        }

        void ToggleShuffle()
        {
            Host.Music.Shuffle = !Host.Music.Shuffle;
            Host.Save();
            btnShuffle.Tone = Host.Music.Shuffle ? MacTone.Tinted : MacTone.Normal;
            btnShuffle.Invalidate();
        }

        void CycleLoop()
        {
            var m = Host.Music;
            m.Loop = (LoopMode)(((int)m.Loop + 1) % 3);
            Host.Save();
            btnLoop.Tone = m.Loop == LoopMode.Off ? MacTone.Normal : MacTone.Tinted;
            btnLoop.Icon = m.Loop == LoopMode.One ? Glyph.Check : Glyph.Repeat;
            btnLoop.Invalidate();
            Host.Toast(m.Loop == LoopMode.Off ? "循环：关闭" : m.Loop == LoopMode.One ? "循环：单曲" : "循环：列表");
        }

        void ScanLocal(bool notify = true)
        {
            if (busy) return;
            busy = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<Track> found = null;
                try { found = LocalLibrary.Scan(); }
                catch (Exception ex) { Program.Log(ex); }
                try
                {
                    Host.Ui(delegate
                    {
                        busy = false;
                        localCache = found ?? new List<Track>();
                        RebuildRows();
                        if (notify) Host.ToastGood("本地歌库：" + localCache.Count + " 首");
                    });
                }
                catch (Exception ex) { Program.Log(ex); }
            });
        }

        void Import()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择音乐文件夹";
                if (dlg.ShowDialog(Host) != DialogResult.OK) return;
                var add = new List<Track>();
                try
                {
                    foreach (var f in Directory.GetFiles(dlg.SelectedPath, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".mp3" && ext != ".flac" && ext != ".m4a" && ext != ".wav" &&
                            ext != ".wma" && ext != ".ogg" && ext != ".ape") continue;
                        string name = Path.GetFileNameWithoutExtension(f);
                        string artist = "", title = name;
                        int dash = name.IndexOf(" - ", StringComparison.Ordinal);
                        if (dash > 0) { artist = name.Substring(0, dash).Trim(); title = name.Substring(dash + 3).Trim(); }
                        add.Add(new Track { Source = "本地", LocalPath = f, Title = title, Artist = artist });
                    }
                }
                catch (Exception ex) { Program.Log(ex); Host.ToastBad("读取失败：" + ex.Message); return; }
                if (add.Count == 0) { Host.ToastWarn("该文件夹没有音频文件。"); return; }
                Host.Music.Append(add);
                RebuildRows();
                Host.ToastGood("已导入 " + add.Count + " 首");
            }
        }

        void DoSearch()
        {
            string kw = search.Text.Trim();
            if (kw.Length == 0) { Host.ToastWarn("请输入搜索关键词。"); return; }
            if (busy) { Host.ToastWarn("请稍候，上一个请求还没回来。"); return; }
            busy = true;
            Host.Toast("搜索中…");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<Track> res = null;
                string err = null;
                try { res = NetEaseApi.Search(kw, 30); }
                catch (Exception ex) { err = ex.Message; Program.Log(ex); }
                try
                {
                    Host.Ui(delegate
                    {
                        busy = false;
                        if (err != null) { Host.ToastBad("搜索失败：" + err); return; }
                        if (res == null || res.Count == 0) { Host.ToastWarn("没有搜索结果（接口可能受限）。"); return; }
                        shown = res;
                        RebuildRows();
                        Host.ToastGood("找到 " + res.Count + " 首");
                    });
                }
                catch (Exception ex) { Program.Log(ex); }
            });
        }

        void RebuildRows()
        {
            var src = segSrc.SelectedIndex == 1 ? shown : (localCache ?? new List<Track>());
            var items = new List<MacListItem>();
            foreach (var t in src)
            {
                items.Add(new MacListItem
                {
                    Title = t.Title,
                    Subtitle = (t.Artist.Length == 0 ? "未知歌手" : t.Artist) + " · " + t.Source,
                    ShowIcon = true,
                    Icon = t.Source == "网易" ? Glyph.Wave : Glyph.Music,
                    Detail = t.Source,
                    Tag = t
                });
            }
            list.SetItems(items);
        }

        void PlaySelected()
        {
            var it = list.SelectedItem;
            if (it == null) return;
            var t = it.Tag as Track;
            if (t == null) return;
            var m = Host.Music;
            m.SetSongs(SongSourceFor(t));
            int idx = 0;
            for (int i = 0; i < m.Songs.Count; i++) if (m.Songs[i] == t) idx = i;
            m.OpenAt(idx);
            Host.ToastGood("正在播放：" + t.Title);
        }

        List<Track> SongSourceFor(Track t)
        {
            var src = segSrc.SelectedIndex == 1 ? shown : (localCache ?? new List<Track>());
            if (src == null || src.Count == 0) return new List<Track> { t };
            var copy = new List<Track>();
            foreach (var x in src) copy.Add(x);
            return copy;
        }

        static string Fmt(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
        }

        bool syncing;
        void Sync()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                var m = Host.Music;
                if (m == null) return;
                btnPlay.Icon = m.Playing ? Glyph.Pause : Glyph.Play;
                btnShuffle.Tone = m.Shuffle ? MacTone.Tinted : MacTone.Normal;
                btnLoop.Tone = m.Loop == LoopMode.Off ? MacTone.Normal : MacTone.Tinted;
                btnLoop.Icon = m.Loop == LoopMode.One ? Glyph.Check : Glyph.Repeat;

                if (m.HasSong)
                {
                    var tr = m.Current;
                    lblNow.Text = tr.Title + "　" + (tr.Artist.Length == 0 ? "" : "· " + tr.Artist) +
                        (m.Status.Length == 0 ? "" : "　" + m.Status);
                    var d = m.Dur;
                    if (!seeking)
                    {
                        seek.Aux = d.TotalSeconds;
                        seek.SetValueSilent(d.TotalSeconds > 0 ? (int)(m.Pos.TotalSeconds / d.TotalSeconds * 1000) : 0);
                    }
                    tCur.Text = Fmt(m.Pos);
                    tDur.Text = Fmt(d);
                    vol.SetValueSilent((int)Math.Round(m.Volume));
                    int li = m.LyricIndex;
                    lblLine.Text = li >= 0 ? m.LyricLine(li) : "（这首歌没有歌词）";
                }
                else
                {
                    lblNow.Text = "未在播放　·　本地 " + (localCache == null ? 0 : localCache.Count) + " 首";
                    lblLine.Text = "";
                    tCur.Text = "0:00";
                    tDur.Text = "0:00";
                }
                swDesk.Checked = Host.DesktopLyricOn;
                swBar.Checked = Host.TaskbarLyricOn;
                numOffset.Value = m.OffsetMs;
            }
            finally { syncing = false; }
        }
    }

    sealed class EnvPage : Page
    {
        MacButton bSublime, bVscode, b302, bCcChrome, bCcEdge, bMsys2, bTool;
        MacProgress pb;
        Label lbl;

        public EnvPage(MainForm h) : base(h) { }

        public override string Key { get { return "env"; } }
        public override string Title { get { return "竞赛环境"; } }

        public override void Build()
        {
            bSublime = Btn("安装", MacTone.Accent, (s, e) => Host.DlSublime());
            bVscode = Btn("安装", MacTone.Accent, (s, e) => Host.DlVscode());
            b302 = Btn("打开官方页", MacTone.Normal, (s, e) => Host.Open302());
            bCcChrome = Btn("下载并解压", MacTone.Accent, (s, e) => Host.Cc("chrome"));
            bCcEdge = Btn("下载并解压", MacTone.Accent, (s, e) => Host.Cc("edge"));
            bMsys2 = Btn("安装", MacTone.Accent, (s, e) => Host.Msys2());
            bTool = Btn("一键安装工具链", MacTone.Accent, (s, e) => Host.Toolchain());
            foreach (var b in new[] { bSublime, bVscode, b302, bCcChrome, bCcEdge, bMsys2, bTool }) Fit(b);

            Card("编辑器与刷题插件",
                Row("Sublime Text", "获取官方最新版本号 → 下载 x64 安装包 → 静默安装", bSublime),
                Row("VS Code", "官方 latest 通道下载 User 安装包 → 静默安装", bVscode),
                Row("Competitive Companion (Chrome)", "从 GitHub Releases 取官方扩展包，国内直连免 VPN", bCcChrome),
                Row("Competitive Companion (Edge)", "同一扩展包通用，加载已解压的扩展程序", bCcEdge),
                Row("SteamCommunity 302", "打开作者 Dogfight360 的官方页面下载", b302));

            Card("编译工具链",
                Row("MSYS2", "下载 latest 安装包并静默安装到 C:\\msys64", bMsys2),
                Row("clang / clangd / gdb / make / gcc", "UCRT64 工具链，自动把 ucrt64\\bin 加入用户 PATH", bTool));

            pb = new MacProgress();
            pb.Width = H(420);
            lbl = Lbl("安装日志见 %TEMP%\\WindowTool_oi.log", Theme.FontSmall, Theme.TextDim);
            lbl.Width = H(440);
            lbl.Height = H(32);
            FreeCard("进度", H(52), (card, w) =>
            {
                Put(pb, card, H(14), 0, w - H(28), H(6));
                Put(lbl, card, H(14), H(14), w - H(28), H(32));
            }, pb, lbl);

            var note = Lbl(
                "装完工具链后请重启 Sublime Text，LSP + LSP-clangd 即可正常工作（clangd 需在 PATH 中）。" +
                "Chrome 154 起已禁用 --load-extension，必须走「加载已解压的扩展程序」。",
                Theme.FontSmall, Theme.TextDim);
            note.Height = H(46);
            FreeCard("说明", H(46), (card, w) => Put(note, card, H(14), 0, w - H(28), H(46)), note);

            Host.OiUi = new OiUi(
                msg => { try { Host.Ui(delegate { lbl.Text = msg; }); } catch { } },
                p => { try { Host.Ui(delegate { pb.Value = p; }); } catch { } });
        }
    }

    sealed class GeneralPage : Page
    {
        MacSegmented segTheme;
        MacSwatches swatch;
        MacSlider slRadius;
        MacSwitch swAutoRun, swSilent;
        Label lblKeys, lblVer;

        public GeneralPage(MainForm h) : base(h) { }

        public override string Key { get { return "general"; } }
        public override string Title { get { return "通用"; } }

        public override void Build()
        {
            segTheme = new MacSegmented();
            segTheme.SetItems(new[] { "浅色", "深色", "跟随系统" });
            segTheme.SelectedIndex = Host.Cfg.Int("darkMode", Host.Cfg.Bool("dark", false) ? 1 : 0);
            segTheme.SizeToCells();
            segTheme.SelectedIndexChanged += (s, e) =>
            {
                Host.Cfg.Set("darkMode", segTheme.SelectedIndex);
                Host.Cfg.Set("dark", segTheme.SelectedIndex == 1);
                Host.ApplyTheme();
                Host.Save();
            };

            swatch = new MacSwatches();
            swatch.SelectedIndex = Host.Cfg.Int("hue", 0);
            swatch.Width = H(206);
            swatch.SelectedIndexChanged += (s, e) =>
            {
                Host.Cfg.Set("hue", (int)swatch.SelectedHue);
                Host.ApplyTheme();
                Host.Save();
            };

            slRadius = Slider(0, 20, Host.Cfg.Int("radius", 10), (s, e) => Host.SetRadius(slRadius.Value));
            slRadius.Width = H(220);

            Card("外观",
                Row("界面主题", "深色会同步系统标题栏配色", segTheme),
                Row("强调色", "选中态、开关与进度条的主色", swatch),
                Row("控件圆角", "0 为直角，建议 8~12", slRadius));

            swAutoRun = new MacSwitch();
            swAutoRun.CheckedChanged += (s, e) => SetAutoRun(swAutoRun.Checked);
            swSilent = new MacSwitch();
            swSilent.Checked = Host.Cfg.Bool("silentStart", false);
            swSilent.CheckedChanged += (s, e) => { Host.Cfg.Set("silentStart", swSilent.Checked); Host.Save(); };

            Card("启动",
                Row("开机自动运行", "写入当前用户注册表 Run 项", swAutoRun),
                Row("静默启动", "下次启动只驻留托盘，不弹主界面", swSilent));

            lblKeys = Lbl("", Theme.FontSmall, Theme.TextDim);
            lblKeys.Height = H(150);
            FreeCard("全局热键（Ctrl+Alt+…）", H(150), (card, w) => Put(lblKeys, card, H(14), 0, w - H(28), H(150)), lblKeys);

            var btnExit = Btn("退出并还原所有窗口", MacTone.Danger, (s, e) => Host.ExitApp());
            Fit(btnExit);
            var btnCfg = Btn("打开设置文件", MacTone.Normal, (s, e) => Host.OpenSettings());
            Fit(btnCfg);
            lblVer = Lbl("", Theme.FontSmall, Theme.TextDim);
            lblVer.Width = H(420);
            Card("其他",
                Row("彻底退出", "取消全部置顶/穿透、恢复透明度与背景样式", btnExit),
                Row("配置", "所有设置实时写入 exe 同目录的 settings.txt", btnCfg),
                Row("版本", "", lblVer));

            OnTick(Sync);
        }

        static string Pad(string v, int n)
        {
            int w = 0;
            foreach (char c in v) w += c < 256 ? 1 : 2;
            return w >= n ? v : v + new string('　', (n - w + 1) / 2);
        }

        void SetAutoRun(bool on)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) { Host.ToastBad("无法打开注册表启动项。"); return; }
                    if (on) k.SetValue("WindowTool", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("WindowTool", false);
                }
                Host.ToastGood(on ? "已开启开机自动运行。" : "已关闭开机自动运行。");
            }
            catch (Exception ex) { Program.Log(ex); Host.ToastBad("设置失败：" + ex.Message); }
        }

        void Sync()
        {
            bool ar = false;
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    ar = k != null && k.GetValue("WindowTool") != null;
            }
            catch { }
            if (swAutoRun.Checked != ar) swAutoRun.SetCheckedSilent(ar);

            string[] left = {
                "1　置顶切换", "2　穿透置顶 / 取消", "3　上移一层",
                "4　下移一层", "5　取消该窗口②", "6　取消全部②"
            };
            string[] right = {
                "7　图片背景", "8　视频背景", "0　取消背景",
                "R　随机切视频", "P　播放 / 暂停", "- / =　透明度 ∓15"
            };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < left.Length; i++)
            {
                sb.Append(Pad(left[i], 20)).Append("　").Append(right[i]).Append('\n');
            }
            if (Host.HotkeyFails != null && Host.HotkeyFails.Count > 0)
                sb.Append("注册失败：" + string.Join(" ", Host.HotkeyFails.ToArray())
                    + "（可能被显卡驱动或其他软件占用）");
            lblKeys.Text = sb.ToString().TrimEnd();

            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            lblVer.Text = "OI 工具箱 v" + ver.Major + "." + ver.Minor + "　·　" +
                (Application.ExecutablePath.Length > 60 ? "…" + Path.GetFileName(Application.ExecutablePath) : Application.ExecutablePath);
        }
    }
}
