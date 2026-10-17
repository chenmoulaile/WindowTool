using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    sealed partial class MainForm
    {
        // ---------------- 全局热键 ----------------

        void RegisterHotkeys()
        {
            uint m = Native.MOD_CONTROL | Native.MOD_ALT;
            var fails = new List<string>();
            Try(m, HK_TOP, '1', "1", fails);
            Try(m, HK_OP2, '2', "2", fails);
            Try(m, HK_UP, '3', "3", fails);
            Try(m, HK_DOWN, '4', "4", fails);
            Try(m, HK_CANCELONE, '5', "5", fails);
            Try(m, HK_CANCELALL, '6', "6", fails);
            Try(m, HK_IMG, '7', "7", fails);
            Try(m, HK_VID, '8', "8", fails);
            Try(m, HK_BGOFF, '0', "0", fails);
            Try(m, HK_VRAND, 'R', "R", fails);
            Try(m, HK_PLAY, 'P', "P", fails);
            Try(m, HK_ALPHA_DEC, Native.VK_OEM_MINUS, "-", fails);
            Try(m, HK_ALPHA_INC, Native.VK_OEM_PLUS, "=", fails);
            HotkeyFails = fails;
        }

        void Try(uint mods, int id, uint vk, string label, List<string> fails)
        {
            if (!Native.RegisterHotKey(Handle, id, mods, vk)) fails.Add(label);
        }

        public List<string> HotkeyFails = new List<string>();

        void UnregisterHotkeys()
        {
            if (!IsHandleCreated) return;
            for (int id = HK_TOP; id <= HK_PLAY; id++) Native.UnregisterHotKey(Handle, id);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) { DispatchHotkey(m.WParam.ToInt32()); return; }
            if (Program.WM_ALREADY_RUNNING != 0 && m.Msg == Program.WM_ALREADY_RUNNING)
            {
                try { BeginInvoke(new Action(ActivateSelf)); } catch { }
                return;
            }
            base.WndProc(ref m);
        }

        void ActivateSelf()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Native.SetForegroundWindow(Handle);
            Activate();
        }

        IntPtr HotTarget()
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != Handle)
            {
                uint pid;
                Native.GetWindowThreadProcessId(fg, out pid);
                if (pid != selfPid && Native.IsWindowVisible(fg)) return fg;
            }
            return selected;
        }

        void DispatchHotkey(int id)
        {
            switch (id)
            {
                case HK_PLAY: music.Toggle(); RebuildTrayMenu(); return;
                case HK_CANCELALL: CancelAllOp2(); return;
                case HK_VRAND: RandomVideo(); return;
            }
            IntPtr t = HotTarget();
            switch (id)
            {
                case HK_TOP: SetTop(t, !Manager.IsTopmost(t)); break;
                case HK_OP2: ToggleOp2(t); break;
                case HK_UP: LayerDir(t, true); break;
                case HK_DOWN: LayerDir(t, false); break;
                case HK_CANCELONE: CancelOp2(t); break;
                case HK_IMG: if (Ok(t)) PickImageAndApply(t); else Toast("热键：没有可用的目标窗口。", ToastKind.Warn); break;
                case HK_VID: if (Ok(t)) PickVideoAndApply(t); else Toast("热键：没有可用的目标窗口。", ToastKind.Warn); break;
                case HK_BGOFF: ClearBgOn(t); break;
                case HK_ALPHA_DEC: NudgeAlpha(t, -15); break;
                case HK_ALPHA_INC: NudgeAlpha(t, 15); break;
            }
            if (current != null) current.Tick();
        }

        // ---------------- 窗口操作 ----------------

        public void SetTop(IntPtr h, bool top)
        {
            if (!Ok(h)) return;
            Manager.SetTopmost(h, top);
            Toast(top ? "已置顶：" + TitleOf(h) : "已取消置顶。", top ? ToastKind.Good : ToastKind.Info);
        }

        public void ToggleOp2(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.HasOp2(h))
            {
                Manager.CancelOp2(h);
                Toast("已取消该窗口的穿透置顶并还原样式。", ToastKind.Info);
            }
            else
            {
                Manager.ApplyOp2(h);
                Toast("已开启穿透置顶（透明 245）。先开启的始终在最上层。", ToastKind.Good);
            }
            RebuildTrayMenu();
        }

        public void CancelOp2(IntPtr h)
        {
            if (!Ok(h)) return;
            Toast(Manager.CancelOp2(h) ? "已取消该窗口的②操作并还原样式。" : "该窗口当前没有②操作。",
                ToastKind.Info);
            RebuildTrayMenu();
        }

        public void CancelAllOp2()
        {
            int n = Manager.CancelAll();
            Toast(n > 0 ? "已取消全部穿透置顶（" + n + " 个窗口）。" : "当前没有需要还原的窗口。", ToastKind.Info);
            RebuildTrayMenu();
        }

        public void LayerDir(IntPtr h, bool up)
        {
            if (!Ok(h)) return;
            if (!up && Manager.HasOp2(h)) { Toast("该窗口处于穿透置顶，需保持最上层。", ToastKind.Warn); return; }
            bool moved = up ? Manager.MoveLayerUp(h) : Manager.MoveLayerDown(h);
            Toast(moved ? (up ? "已上移一层。" : "已下移一层。") : (up ? "已在所在层级最上面。" : "已在所在层级最下面。"),
                ToastKind.Info);
        }

        public void LayerBottom(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.HasOp2(h)) { Toast("该窗口处于穿透置顶，不能移到最底。", ToastKind.Warn); return; }
            Manager.MoveLayerBottom(h);
            Toast("已移到最底层。", ToastKind.Info);
        }

        // 透明度增量必须以目标窗口自身的当前值为基准，否则热键会套用列表里另一个窗口的值
        public void NudgeAlpha(IntPtr h, int delta)
        {
            if (!Ok(h)) return;
            int v = Manager.GetLastAlpha(h) + delta;
            v = Math.Max(40, Math.Min(255, v));
            Manager.ApplyOpacity(h, (byte)v);
            Toast("透明度 → " + v, ToastKind.Info);
            if (current != null) current.Tick();
        }

        public void SetAlpha(IntPtr h, int v)
        {
            if (!Ok(h)) return;
            Manager.ApplyOpacity(h, (byte)Math.Max(40, Math.Min(255, v)));
        }

        // ---------------- 背景 ----------------

        DisplayFit CurrentFit()
        {
            int i = cfg.Int("fitMode", 0);
            if (i == 1) return DisplayFit.FitContain;
            if (i == 2) return DisplayFit.StretchFill;
            return DisplayFit.CoverFill;
        }

        public void PickImageAndApply(IntPtr h)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择背景图片";
                dlg.Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Image img;
                try { img = Image.FromFile(dlg.FileName); }
                catch (Exception ex) { Toast("无法加载图片：" + ex.Message, ToastKind.Bad); return; }
                var fit = CurrentFit();
                var meta = new BgMeta { Kind = "img", Path = dlg.FileName, Alpha = BgAlpha(), Fit = (int)fit };
                ApplyBgCore(h, () => new BgWindow(h, img, fit), meta);
                Toast("图片背景已应用。", ToastKind.Good);
            }
        }

        public void PickVideoAndApply(IntPtr h)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择背景视频";
                dlg.Filter = "视频|*.mp4;*.wmv;*.avi;*.mkv;*.mov;*.m4v|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string path = dlg.FileName;
                videoFiles = new[] { path };
                videoIdx = 0;
                playlistTarget = h;
                lastSwitch = DateTime.Now;
                ApplyBgCore(h, () => new BgWindow(h, path, CurrentFit()),
                    new BgMeta { Kind = "vid", Path = path, Alpha = BgAlpha(), Fit = (int)CurrentFit() });
                Toast("视频背景循环播放中：" + Path.GetFileName(path), ToastKind.Good);
            }
        }

        public byte BgAlpha() { return (byte)Math.Max(60, Math.Min(250, cfg.Int("bgAlpha", 160))); }

        public bool HasBg(IntPtr h) { return bgForms.ContainsKey(h); }
        public int BgCount { get { return bgForms.Count; } }

        public void ApplyBgCore(IntPtr h, Func<BgWindow> createBg, BgMeta meta)
        {
            CloseBgForm(h);
            byte a = meta.Alpha;
            Manager.ApplyBg(h, a);
            var bg = createBg();
            bgForms[h] = bg;
            bgMeta[h] = meta;
            Manager.SetBgHwnd(h, bg.Handle);
            bg.Follow();
            SaveBgMemory();
            if (current != null) current.Tick();
        }

        public void ClearBgOn(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.ClearBg(h))
            {
                CloseBgForm(h);
                videoFiles = null;
                playlistTarget = IntPtr.Zero;
                Toast("已取消背景并还原窗口样式。", ToastKind.Good);
            }
            else Toast("该窗口当前没有背景。", ToastKind.Info);
            if (current != null) current.Tick();
        }

        void CloseBgForm(IntPtr h)
        {
            bgMeta.Remove(h);
            BgWindow bg;
            if (bgForms.TryGetValue(h, out bg))
            {
                bgForms.Remove(h);
                try { bg.Hide(); } catch { }
                try { bg.Close(); } catch { }
            }
            SaveBgMemory();
        }

        void OnBgTick(object sender, EventArgs e)
        {
            var dead = new List<IntPtr>();
            foreach (var kv in bgForms)
            {
                if (!Native.IsWindow(kv.Key)) dead.Add(kv.Key);
                else kv.Value.Follow();
            }
            foreach (var k in dead) { CloseBgForm(k); Manager.ClearBg(k); }
        }

        // ---------------- 视频轮播 ----------------

        readonly Random rng = new Random();
        string[] videoFiles;
        int videoIdx = -1;
        IntPtr playlistTarget;
        DateTime lastSwitch = DateTime.MinValue;

        public string[] VideoPlaylist { get { return videoFiles; } }
        public int VideoIndex { get { return videoIdx; } }
        public IntPtr PlaylistTarget { get { return playlistTarget; } }

        public bool LoadVideoFolder(string dir)
        {
            var exts = new[] { "*.mp4", "*.wmv", "*.avi", "*.mkv", "*.mov", "*.m4v" };
            var found = new List<string>();
            try
            {
                foreach (var pat in exts)
                    foreach (var f in Directory.GetFiles(dir, pat)) found.Add(f);
            }
            catch (Exception ex) { Program.Log(ex); return false; }
            if (found.Count == 0) return false;
            found.Sort(StringComparer.OrdinalIgnoreCase);
            videoFiles = found.ToArray();
            videoIdx = -1;
            return true;
        }

        public void SetPlaylistTarget(IntPtr h) { playlistTarget = h; }

        public void RandomVideo()
        {
            if (videoFiles == null || videoFiles.Length == 0)
            {
                Toast("请先在「背景」页加载视频文件夹。", ToastKind.Warn);
                return;
            }
            IntPtr t = Ok(playlistTarget) && bgForms.ContainsKey(playlistTarget) ? playlistTarget : HotTarget();
            SwitchVideo(t);
        }

        public bool SwitchVideo(IntPtr h)
        {
            if (videoFiles == null || videoFiles.Length == 0 || !Ok(h)) return false;
            int ni = videoIdx;
            if (videoFiles.Length > 1)
            {
                int guard = 0;
                do { ni = rng.Next(videoFiles.Length); guard++; } while (ni == videoIdx && guard < 20);
            }
            videoIdx = ni;
            try
            {
                string path = videoFiles[ni];
                lastSwitch = DateTime.Now;
                var fit = CurrentFit();
                ApplyBgCore(h, () => new BgWindow(h, path, fit),
                    new BgMeta { Kind = "vid", Path = path, Alpha = BgAlpha(), Fit = (int)fit });
                Toast("视频 [" + (ni + 1) + "/" + videoFiles.Length + "] " + Path.GetFileName(path), ToastKind.Good);
                return true;
            }
            catch (Exception ex) { Program.Log(ex); Toast("切换视频失败：" + ex.Message, ToastKind.Bad); return false; }
        }

        public bool SwitchVideoTo(IntPtr h, int index)
        {
            if (videoFiles == null || index < 0 || index >= videoFiles.Length || !Ok(h)) return false;
            videoIdx = index;
            string path = videoFiles[index];
            lastSwitch = DateTime.Now;
            var fit = CurrentFit();
            ApplyBgCore(h, () => new BgWindow(h, path, fit),
                new BgMeta { Kind = "vid", Path = path, Alpha = BgAlpha(), Fit = (int)fit });
            return true;
        }

        void AutoSwitchVideo()
        {
            if (videoFiles == null || videoFiles.Length <= 1) return;
            int total = cfg.Int("switchMin", 0) * 60 + cfg.Int("switchSec", 0);
            if (total <= 0) return;
            if (!Ok(playlistTarget)) return;
            if ((DateTime.Now - lastSwitch).TotalSeconds >= total) SwitchVideo(playlistTarget);
        }

        // ---------------- 背景记忆 ----------------

        static string Esc(string s) { return (s ?? "").Replace("|", "｜").Replace("\r", "").Replace("\n", ""); }

        public bool BgMemory { get { return cfg.Bool("bgMem", true); } }

        void SaveBgMemory()
        {
            if (!BgMemory) return;
            var lines = new List<string>();
            foreach (var kv in bgForms)
            {
                BgMeta meta;
                if (!bgMeta.TryGetValue(kv.Key, out meta)) continue;
                uint pid; Native.GetWindowThreadProcessId(kv.Key, out pid);
                lines.Add(Esc(ProcName(pid)) + "|" + Esc(TitleOf(kv.Key)) + "|" + meta.Kind + "|" +
                          Esc(meta.Path) + "|" + meta.Alpha + "|" + meta.Fit);
            }
            if (lines.Count == 0)
            {
                foreach (var ent in pendingRestore) lines.Add(string.Join("|", ent));
            }
            cfg.Set("bgCount", lines.Count);
            for (int i = 0; i < lines.Count; i++) cfg.Set("bg" + i, lines[i]);
            for (int i = lines.Count; ; i++)
            {
                if (!cfg.Has("bg" + i)) break;
                cfg.Remove("bg" + i);
            }
        }

        void TryRestoreBackgrounds()
        {
            if (!BgMemory) return;
            if (pendingRestore.Count == 0)
            {
                int n = cfg.Int("bgCount", 0);
                for (int i = 0; i < n; i++)
                {
                    string v = cfg.Str("bg" + i, "");
                    if (v.Length == 0) continue;
                    var parts = v.Split('|');
                    if (parts.Length >= 6) pendingRestore.Add(parts);
                }
                if (pendingRestore.Count == 0) return;
            }

            restoreTries++;
            var done = new List<string[]>();
            foreach (var ent in pendingRestore)
            {
                IntPtr wh = FindWindowMatch(ent[0], ent[1]);
                if (wh == IntPtr.Zero) continue;
                try
                {
                    var meta = new BgMeta
                    {
                        Kind = ent[2],
                        Path = ent[3],
                        Alpha = (byte)int.Parse(ent[4]),
                        Fit = int.Parse(ent[5])
                    };
                    var fit = (DisplayFit)meta.Fit;
                    if (meta.Kind == "img")
                    {
                        var img = Image.FromFile(meta.Path);
                        ApplyBgCore(wh, () => new BgWindow(wh, img, fit), meta);
                    }
                    else
                    {
                        videoFiles = new[] { meta.Path };
                        videoIdx = 0;
                        playlistTarget = wh;
                        lastSwitch = DateTime.Now;
                        ApplyBgCore(wh, () => new BgWindow(wh, meta.Path, fit), meta);
                    }
                    Toast("已恢复窗口背景：" + ent[1], ToastKind.Good);
                }
                catch (Exception ex) { Program.Log(ex); }
                done.Add(ent);
            }
            foreach (var d in done) pendingRestore.Remove(d);
            if (restoreTries > 240) pendingRestore.Clear();
        }

        IntPtr FindWindowMatch(string proc, string title)
        {
            if (string.IsNullOrEmpty(title)) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows((h, lp) =>
            {
                if (found != IntPtr.Zero || !Native.IsWindowVisible(h)) return true;
                var sb = new StringBuilder(512);
                Native.GetWindowText(h, sb, sb.Capacity);
                if (sb.ToString() != title) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (ProcName(pid) != proc) return true;
                found = h;
                return false;
            }, IntPtr.Zero);
            return found;
        }
    }
}
