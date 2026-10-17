using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WindowTool
{
    enum LoopMode { Off, One, All }

    /// 播放引擎封装：曲库、循环/随机、音量、歌词与偏移。UI 只读状态并调用控制方法。
    sealed class Player
    {
        readonly MusicPlayer engine = new MusicPlayer();
        public readonly List<Track> Songs = new List<Track>();
        double seekWhenReady = -1;      // 恢复进度时引擎还没就绪，等它报出时长再跳
        public int Index = -1;
        public bool Playing;
        public LoopMode Loop = LoopMode.All;
        public bool Shuffle;
        public int OffsetMs;
        public List<KeyValuePair<TimeSpan, string>> Lyric = new List<KeyValuePair<TimeSpan, string>>();
        public string Status = "";

        readonly MainForm host;
        public event EventHandler Changed;

        public Player(MainForm host) { this.host = host; engine.SetVolumeOnly(80); }

        public double Volume
        {
            get { return engine.Volume; }
            set { engine.Volume = Math.Max(0, Math.Min(100, value)); }
        }

        /// 只记音量，不拉起 WPF 引擎
        public void SetVolumeOnly(double v) { engine.SetVolumeOnly(Math.Max(0, Math.Min(100, v))); }

        public bool HasSong { get { return Index >= 0 && Index < Songs.Count; } }

        /// 引擎是否已被拉起；未播放过时调用方要绕开一切会 JIT 到 WPF 成员的方法
        public bool Started { get { return engine.Active; } }
        public Track Current { get { return HasSong ? Songs[Index] : null; } }
        public TimeSpan Pos { get { return engine.Active ? engine.Position : TimeSpan.Zero; } }
        public TimeSpan Dur { get { return engine.Active ? engine.Duration : TimeSpan.Zero; } }
        public bool CanSeek { get { return engine.Active && engine.CanPlay(); } }

        void Raise() { var h = Changed; if (h != null) h(this, EventArgs.Empty); }

        public void SetSongs(IEnumerable<Track> list)
        {
            Songs.Clear();
            if (list != null) Songs.AddRange(list);
            Index = -1;
            Raise();
        }

        public void Append(IEnumerable<Track> list)
        {
            if (list == null) return;
            foreach (var t in list) Songs.Add(t);
            Raise();
        }

        public void OpenAt(int i)
        {
            if (i < 0 || i >= Songs.Count) return;
            if (i == Index && engine.Active) { Toggle(); return; }
            Index = i;
            Lyric = new List<KeyValuePair<TimeSpan, string>>();
            Playing = true;
            Status = "载入中…";

            var t = Songs[i];
            if (t.Source == "本地")
            {
                try { engine.OpenFile(t.LocalPath); }
                catch (Exception ex) { Program.Log(ex); Status = "无法播放：" + ex.Message; Playing = false; Raise(); return; }
                LoadLocalLyric(t.LocalPath);
            }
            else
            {
                engine.BeginWaitUrl();
                Status = "获取播放地址…";
                string id = t.NetId;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    string url = null;
                    try { url = NetEaseApi.SongUrl(id, 192000); } catch (Exception ex) { Program.Log(ex); }
                    string lyric = null;
                    try { lyric = NetEaseApi.Lyric(id); } catch (Exception ex) { Program.Log(ex); }
                    try
                    {
                        host.Ui(delegate
                        {
                            if (url == null)
                            {
                                Status = "该歌曲需要 VIP 或暂无版权";
                                Playing = false;
                                engine.Stop();
                                Raise();
                                host.ToastBad("无法播放：" + t.Title + "（需要 VIP 或暂无版权）");
                                return;
                            }
                            engine.OpenUrl(url);
                            Lyric = Lrc.Parse(lyric ?? "");
                            Status = "";
                            Raise();
                        });
                    }
                    catch (Exception ex) { Program.Log(ex); }
                });
            }
            Raise();
        }

        void LoadLocalLyric(string audioPath)
        {
            try
            {
                string lp = LocalLibrary.LyricPathFor(audioPath);
                if (lp == null) { Lyric = new List<KeyValuePair<TimeSpan, string>>(); return; }
                var parsed = Lrc.Parse(File.ReadAllText(lp, System.Text.Encoding.UTF8));
                Lyric = parsed;
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void Toggle()
        {
            if (!HasSong) { OpenAt(0); return; }
            // 上次退出前只恢复了列表与选中项，引擎还没开过任何文件
            if (!engine.Active) { OpenAt(Index); return; }
            if (Playing) { engine.Pause(); Playing = false; }
            else { engine.Play(); Playing = true; }
            Raise();
        }

        public void Stop()
        {
            engine.Stop();
            Playing = false;
            Raise();
        }

        public void Eject()
        {
            engine.Close();
            Index = -1;
            Playing = false;
            Lyric = new List<KeyValuePair<TimeSpan, string>>();
            Raise();
        }

        public void Seek(double frac)
        {
            if (!CanSeek) return;
            var d = Dur;
            if (d <= TimeSpan.Zero) return;
            engine.Position = TimeSpan.FromTicks((long)(d.Ticks * Math.Max(0, Math.Min(1, frac))));
        }

        public void Next(bool user)
        {
            if (Songs.Count == 0) return;
            int n;
            if (Shuffle && Songs.Count > 1) n = PickDifferent();
            else n = Index + 1;
            if (n >= Songs.Count)
            {
                if (Loop == LoopMode.All) n = 0;
                else { Stop(); if (user) host.Toast("播放结束。"); return; }
            }
            OpenAt(n < 0 ? 0 : n);
        }

        public void Prev()
        {
            if (Songs.Count == 0) return;
            if (Pos.TotalSeconds > 3) { Seek(0); return; }
            int p = Index - 1;
            if (p < 0) p = Loop == LoopMode.All ? Songs.Count - 1 : 0;
            OpenAt(p);
        }

        int PickDifferent()
        {
            int n = Index;
            for (int i = 0; i < 20 && n == Index; i++) n = rng.Next(Songs.Count);
            return n;
        }

        static readonly Random rng = new Random();

        // 由外部 250ms 定时器驱动：检测播完、推进进度
        public void Poll()
        {
            if (!engine.Active) return;
            if (seekWhenReady >= 0 && CanSeek)
            {
                double s = seekWhenReady;
                seekWhenReady = -1;
                try { engine.Position = TimeSpan.FromSeconds(s); } catch { }
            }
            if (engine.WaitTimedOut)
            {
                Status = "获取播放地址超时";
                Playing = false;
                Raise();
                return;
            }
            if (!Playing || !CanSeek) return;
            if (Dur > TimeSpan.Zero && Pos >= Dur - TimeSpan.FromMilliseconds(350))
            {
                if (Loop == LoopMode.One) { Seek(0); engine.Play(); }
                else Next(false);
            }
        }

        public int LyricIndex
        {
            get
            {
                if (Lyric == null || Lyric.Count == 0) return -1;
                return Lrc.IndexAt(Lyric, Pos + TimeSpan.FromMilliseconds(OffsetMs));
            }
        }

        /// 当前歌词行内的进度 0..1（按本行到下一行的时间差推算）
        public double LineProgress
        {
            get
            {
                int i = LyricIndex;
                if (i < 0 || Lyric == null || i >= Lyric.Count) return 0;
                var start = Lyric[i].Key;
                var end = i + 1 < Lyric.Count ? Lyric[i + 1].Key : start + TimeSpan.FromSeconds(4);
                double span = (end - start).TotalSeconds;
                if (span <= 0.2) return 1;
                double at = (Pos + TimeSpan.FromMilliseconds(OffsetMs) - start).TotalSeconds;
                return Math.Max(0, Math.Min(1, at / span));
            }
        }

        public string LyricLine(int i)
        {
            if (Lyric == null || i < 0 || i >= Lyric.Count) return "";
            return Lyric[i].Value;
        }


        // 恢复只重建列表与选中项，绝不在这里碰 WPF 引擎
        /// 把当前列表（仅本地曲目）、选中项与进度写进设置
        public void RememberState(Settings cfg)
        {
            if (!HasSong) return;
            int keep = Math.Min(Songs.Count, 80);
            int start = Math.Max(0, Math.Min(Index, Math.Max(0, keep - 1)));
            int n = 0;
            for (int i = start; i < keep; i++)
            {
                var t = Songs[i];
                if (t.Source != "本地" || !System.IO.File.Exists(t.LocalPath)) continue;
                cfg.Set("pl" + n, t.LocalPath);
                n++;
                if (n >= 40) break;
            }
            for (int i = n; ; i++)
            {
                if (!cfg.Has("pl" + i)) break;
                cfg.Remove("pl" + i);
            }
            cfg.Set("plCount", n);
            cfg.Set("plPos", (int)Pos.TotalSeconds);
        }

        /// 只恢复列表与选中项，不拉起 WPF 引擎、也不自动播放
        public void RestoreState(Settings cfg)
        {
            int n = cfg.Int("plCount", 0);
            if (n <= 0) return;
            var list = new List<Track>();
            for (int i = 0; i < n; i++)
            {
                string path = cfg.Str("pl" + i, "");
                if (path.Length == 0 || !System.IO.File.Exists(path)) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                string artist = "", title = name;
                int dash = name.IndexOf(" - ", StringComparison.Ordinal);
                if (dash > 0) { artist = name.Substring(0, dash).Trim(); title = name.Substring(dash + 3).Trim(); }
                list.Add(new Track { Source = "本地", LocalPath = path, Title = title, Artist = artist });
            }
            if (list.Count == 0) return;
            Songs.Clear();
            Songs.AddRange(list);
            Index = 0;
            seekWhenReady = cfg.Int("plPos", 0);
            Raise();
        }

        public void Dispose() { try { engine.Close(); } catch { } }
    }

    sealed partial class MainForm
    {
        Player music;
        Timer tmrMusic;
        DesktopLyricsForm deskLyric;
        TaskbarLyricsForm barLyric;
        MacPanel mini;
        bool miniVisible;

        static int MiniHeight { get { return Theme.S(76); } }

        void StartMusic()
        {
            music = new Player(this);
            music.Changed += (s, e) => OnMusicChanged();
            if (!DesignRender)
            {
                tmrMusic = new Timer { Interval = 250 };
                tmrMusic.Tick += (s, e) => MusicTick();
                tmrMusic.Start();
            }
            music.SetVolumeOnly(Math.Max(0, Math.Min(100, cfg.Int("vol", 80))));
            music.RestoreState(cfg);
            UpdateMiniVisibility();
        }

        void StopMusic()
        {
            if (tmrMusic != null) { tmrMusic.Stop(); tmrMusic.Dispose(); tmrMusic = null; }
            if (music != null) music.Dispose();
            CloseLyricForms();
        }

        void MusicTick()
        {
            if (music == null || !music.Started) return;
            music.Poll();
            if (miniVisible && mini != null) mini.Repaint();
            if (deskLyric != null && deskLyric.Visible) UpdateDeskLyric();
            if (barLyric != null && barLyric.Visible) UpdateBarLyric();
        }

        void OnMusicChanged()
        {
            UpdateMiniVisibility();
            if (mini != null) mini.Repaint();
            if (current != null) current.Tick();
            if (tray != null && trayMenu != null) RebuildTrayMenu();
            Save();
        }

        void UpdateMiniVisibility()
        {
            bool want = music != null && music.HasSong;
            if (want == miniVisible) { if (mini != null) mini.Repaint(); return; }
            miniVisible = want;
            LayoutShell();
            if (mini != null) { mini.Visible = want; mini.Repaint(); }
            Invalidate();
        }

        // ---------------- 歌词悬浮层 ----------------

        public bool DesktopLyricOn { get { return cfg.Bool("deskLyric", false); } }
        public bool TaskbarLyricOn { get { return cfg.Bool("barLyric", false); } }

        public void SetDesktopLyric(bool on)
        {
            cfg.Set("deskLyric", on);
            if (on)
            {
                if (deskLyric == null || deskLyric.IsDisposed)
                {
                    deskLyric = new DesktopLyricsForm();
                    var wa = Screen.PrimaryScreen.WorkingArea;
                    deskLyric.Location = new Point(wa.Left + (wa.Width - deskLyric.Width) / 2,
                        wa.Bottom - Theme.S(120));
                }
                deskLyric.Show();
                UpdateDeskLyric();
                ToastGood("桌面歌词已开启。");
            }
            else
            {
                if (deskLyric != null && !deskLyric.IsDisposed) deskLyric.Hide();
                Toast("桌面歌词已关闭。");
            }
            Save();
        }

        public void SetTaskbarLyric(bool on)
        {
            cfg.Bool("barLyric", on);
            cfg.Set("barLyric", on);
            if (on)
            {
                if (barLyric == null || barLyric.IsDisposed) barLyric = new TaskbarLyricsForm();
                barLyric.Show();
                barLyric.EnsurePosition();
                UpdateBarLyric();
                ToastGood("任务栏歌词已开启。");
            }
            else
            {
                if (barLyric != null && !barLyric.IsDisposed) barLyric.Hide();
                Toast("任务栏歌词已关闭。");
            }
            Save();
        }

        void CloseLyricForms()
        {
            try { if (deskLyric != null && !deskLyric.IsDisposed) deskLyric.Close(); } catch { }
            try { if (barLyric != null && !barLyric.IsDisposed) barLyric.Close(); } catch { }
            deskLyric = null;
            barLyric = null;
        }

        void UpdateDeskLyric()
        {
            if (deskLyric == null || deskLyric.IsDisposed) return;
            int i = music.LyricIndex;
            string line = i >= 0 ? music.LyricLine(i) : (music.Current == null ? "" : music.Current.Title);
            deskLyric.SetLine(line);
            deskLyric.SetProgress(i >= 0 && music.Playing ? music.LineProgress : 0);
        }

        void UpdateBarLyric()
        {
            if (barLyric == null || barLyric.IsDisposed) return;
            barLyric.EnsurePosition();
            int i = music.LyricIndex;
            string line = i >= 0 ? music.LyricLine(i) : (music.Current == null ? "" : music.Current.Artist + " - " + music.Current.Title);
            barLyric.SetLine(line.Length == 0 ? "OI 工具箱 · 音乐" : line);
        }

        public void SaveMusic()
        {
            if (music == null) return;
            music.RememberState(cfg);
            cfg.Set("vol", (int)Math.Round(music.Volume));
            cfg.Set("lyricOffset", music.OffsetMs);
            cfg.Set("loop", (int)music.Loop);
            cfg.Set("shuffle", music.Shuffle);
        }

        public Player Music { get { return music; } }
    }

    /// 迷你播放条的容器：负责背景与分隔线，子控件由 BuildMiniBar 摆放。
    sealed class MacPanel : Panel
    {
        public Action OnRefresh;
        public Action OnResized;
        public MacPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void Repaint()
        {
            if (OnRefresh != null) OnRefresh();
            Invalidate(true);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            var r = OnResized;
            if (r != null) r();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hint(g);
            g.Clear(Theme.SolidBack(this));
            using (var pen = new Pen(Theme.Separator, Math.Max(1f, Theme.S(1))))
                g.DrawLine(pen, 0, 0, Width, 0);
        }
    }
}
