using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    /// <summary>
    /// AppForm 的操作层：全局热键、窗口操作、图片背景、布局方案、
    /// 自动置顶规则、多显示器移动、托盘。
    /// </summary>
    sealed partial class AppForm
    {
        // ==================================================================
        // 全局热键
        // ==================================================================

        public List<string> HotkeyFails = new List<string>();

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
            Try(m, HK_BGOFF, '0', "0", fails);
            Try(m, HK_MONNEXT, 'N', "N", fails);
            Try(m, HK_ALPHA_DEC, Native.VK_OEM_MINUS, "-", fails);
            Try(m, HK_ALPHA_INC, Native.VK_OEM_PLUS, "=", fails);

            // 吸附走 Ctrl+Alt+Shift，避开上面的 Ctrl+Alt 组，也躲开显卡驱动的箭头绑定
            uint ms = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT;
            Try(ms, HK_SNAP_L, Native.VK_LEFT, "Shift+左", fails);
            Try(ms, HK_SNAP_R, Native.VK_RIGHT, "Shift+右", fails);
            Try(ms, HK_SNAP_T, Native.VK_UP, "Shift+上", fails);
            Try(ms, HK_SNAP_B, Native.VK_DOWN, "Shift+下", fails);
            Try(ms, HK_SNAP_Q1, 'Q', "Shift+Q", fails);
            Try(ms, HK_SNAP_Q2, 'W', "Shift+W", fails);
            Try(ms, HK_SNAP_Q3, 'E', "Shift+E", fails);
            Try(ms, HK_SNAP_Q4, 'R', "Shift+R", fails);
            Try(ms, HK_SNAP_MAX, 'M', "Shift+M", fails);
            Try(ms, HK_SNAP_BACK, 'B', "Shift+B", fails);
            HotkeyFails = fails;
        }

        void Try(uint mods, int id, uint vk, string label, List<string> fails)
        {
            if (!Native.RegisterHotKey(Handle, id, mods, vk)) fails.Add(label);
        }

        void UnregisterHotkeys()
        {
            if (!IsHandleCreated) return;
            for (int id = HK_TOP; id <= HK_ALPHA_INC; id++) Native.UnregisterHotKey(Handle, id);
            Native.UnregisterHotKey(Handle, HK_MONNEXT);
            for (int id = HK_SNAP_L; id <= HK_SNAP_BACK; id++) Native.UnregisterHotKey(Handle, id);
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
                case HK_CANCELALL: CancelAllOp2(); return;
                case HK_SNAP_L: SnapTo(HotTarget(), SnapKind.Left); return;
                case HK_SNAP_R: SnapTo(HotTarget(), SnapKind.Right); return;
                case HK_SNAP_T: SnapTo(HotTarget(), SnapKind.Top); return;
                case HK_SNAP_B: SnapTo(HotTarget(), SnapKind.Bottom); return;
                case HK_SNAP_Q1: SnapTo(HotTarget(), SnapKind.TopLeft); return;
                case HK_SNAP_Q2: SnapTo(HotTarget(), SnapKind.TopRight); return;
                case HK_SNAP_Q3: SnapTo(HotTarget(), SnapKind.BottomLeft); return;
                case HK_SNAP_Q4: SnapTo(HotTarget(), SnapKind.BottomRight); return;
                case HK_SNAP_MAX: SnapTo(HotTarget(), SnapKind.Max); return;
                case HK_SNAP_BACK: UnsnapTo(HotTarget()); return;
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
                case HK_BGOFF: ClearBgOn(t); break;
                case HK_MONNEXT: MoveToNextMonitor(t); break;
                case HK_ALPHA_DEC: NudgeAlpha(t, -15); break;
                case HK_ALPHA_INC: NudgeAlpha(t, 15); break;
            }
            if (current != null) current.Tick();
        }

        // ==================================================================
        // 窗口操作（带结果反馈）
        // ==================================================================

        public void SetTop(IntPtr h, bool top)
        {
            if (!Ok(h)) { ToastWarn("没有可用的目标窗口。"); return; }
            bool ok = Manager.SetTopmost(h, top);
            if (ok)
                Toast(top ? "已置顶：" + TitleOf(h) : "已取消置顶。", top ? ToastKind.Good : ToastKind.Info);
            else
                ToastBad("无法置顶该窗口（可能是高权限程序），请右键本程序以管理员身份运行。");
            RebuildTrayMenu();
        }

        public void ToggleOp2(IntPtr h)
        {
            if (!Ok(h)) { ToastWarn("没有可用的目标窗口。"); return; }
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
            Toast(Manager.CancelOp2(h) ? "已取消该窗口的穿透置顶并还原样式。" : "该窗口当前没有穿透置顶。",
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
            if (!up && Manager.HasOp2(h)) { ToastWarn("该窗口处于穿透置顶，需保持最上层。"); return; }
            bool moved = up ? Manager.MoveLayerUp(h) : Manager.MoveLayerDown(h);
            Toast(moved ? (up ? "已上移一层。" : "已下移一层。") : (up ? "已在所在层级最上面。" : "已在所在层级最下面。"),
                ToastKind.Info);
        }

        public void LayerBottom(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.HasOp2(h)) { ToastWarn("该窗口处于穿透置顶，不能移到最底。"); return; }
            Manager.MoveLayerBottom(h);
            Toast("已移到最底层。", ToastKind.Info);
        }

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

        public void SnapTo(IntPtr h, SnapKind kind)
        {
            if (!Ok(h)) return;
            if (Manager.Snap(h, kind)) Toast("已吸附：" + SnapName(kind), ToastKind.Good);
            else Toast("吸附失败（窗口可能处于全屏或特殊状态）。", ToastKind.Warn);
        }

        public void UnsnapTo(IntPtr h)
        {
            if (!Ok(h)) return;
            if (Manager.Unsnap(h)) Toast("已还原到吸附前的位置与大小。", ToastKind.Good);
            else Toast("该窗口没有被本工具吸附过。", ToastKind.Info);
        }

        public static string SnapName(SnapKind k)
        {
            switch (k)
            {
                case SnapKind.Left: return "左半";
                case SnapKind.Right: return "右半";
                case SnapKind.Top: return "上半";
                case SnapKind.Bottom: return "下半";
                case SnapKind.TopLeft: return "左上四分之一";
                case SnapKind.TopRight: return "右上四分之一";
                case SnapKind.BottomLeft: return "左下四分之一";
                case SnapKind.BottomRight: return "右下四分之一";
                case SnapKind.Max: return "铺满";
            }
            return "居中";
        }

        public void ToggleBlur(IntPtr h)
        {
            if (!Ok(h)) return;
            bool on = !Manager.IsBlurred(h);
            if (Manager.ApplyBlur(h, on))
                Toast(on ? "已开启毛玻璃；若无变化说明该系统或该窗口不支持。" : "已关闭毛玻璃。",
                    on ? ToastKind.Good : ToastKind.Info);
            else ToastBad("毛玻璃设置失败。");
        }

        /// 移到下一块显示器（保持相对位置）。单屏时提示。
        public void MoveToNextMonitor(IntPtr h)
        {
            if (!Ok(h)) return;
            var screens = Screen.AllScreens;
            if (screens.Length < 2) { ToastWarn("当前只有一块显示器。"); return; }

            Native.RECT r;
            if (!Native.GetWindowRect(h, out r)) return;
            Screen cur = Screen.FromHandle(h);
            int idx = Array.IndexOf(screens, cur);
            Screen dst = screens[(idx + 1 + screens.Length) % screens.Length];

            bool zoomed = Native.IsZoomed(h);
            if (zoomed) Native.ShowWindow(h, Native.SW_RESTORE);

            double rx = (r.Left - (double)cur.WorkingArea.Left) / Math.Max(1, cur.WorkingArea.Width);
            double ry = (r.Top - (double)cur.WorkingArea.Top) / Math.Max(1, cur.WorkingArea.Height);
            int w = r.Right - r.Left, hh = r.Bottom - r.Top;

            int nx = dst.WorkingArea.Left + (int)(rx * dst.WorkingArea.Width);
            int ny = dst.WorkingArea.Top + (int)(ry * dst.WorkingArea.Height);
            nx = Math.Min(nx, dst.WorkingArea.Right - w);
            ny = Math.Min(ny, dst.WorkingArea.Bottom - hh);
            if (nx < dst.WorkingArea.Left) nx = dst.WorkingArea.Left;
            if (ny < dst.WorkingArea.Top) ny = dst.WorkingArea.Top;

            Native.SetWindowPos(h, IntPtr.Zero, nx, ny, w, hh, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            if (zoomed)
            {
                Native.SetWindowPos(h, IntPtr.Zero, dst.WorkingArea.Left, dst.WorkingArea.Top,
                    dst.WorkingArea.Width, dst.WorkingArea.Height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Native.ShowWindow(h, 3);   // SW_MAXIMIZE
            }
            Toast("已移到显示器：" + dst.DeviceName, ToastKind.Good);
        }

        // ==================================================================
        // 自动置顶规则
        // ==================================================================

        public List<string> AutoRules { get { return autoRules; } }

        void LoadAutoRules()
        {
            autoRules.Clear();
            int n = cfg.Int("autoruleCount", 0);
            for (int i = 0; i < n; i++)
            {
                string s = cfg.Str("ar" + i, "");
                if (s.Length > 0 && !autoRules.Contains(s)) autoRules.Add(s);
            }
        }

        public void SaveAutoRules()
        {
            cfg.Set("autoruleCount", autoRules.Count);
            for (int i = 0; i < autoRules.Count; i++) cfg.Set("ar" + i, autoRules[i]);
            for (int i = autoRules.Count; ; i++)
            {
                if (!cfg.Has("ar" + i)) break;
                cfg.Remove("ar" + i);
            }
            cfg.Save();
        }

        public void AddAutoRule(string proc)
        {
            if (string.IsNullOrEmpty(proc)) return;
            if (!autoRules.Contains(proc)) autoRules.Add(proc);
            SaveAutoRules();
        }

        public void RemoveAutoRule(string proc)
        {
            autoRules.Remove(proc);
            SaveAutoRules();
        }

        /// 周期扫描：匹配规则的新窗口出现时自动置顶（一次）。
        /// notify=true 时弹提示；前台事件触发的静默执行。
        void ApplyAutoRules(bool notify)
        {
            if (autoRules.Count == 0) return;
            // 清理已销毁的
            autoPinned.RemoveWhere(IsDead);

            List<WinEntry> snap;
            lock (enumLock) snap = new List<WinEntry>(winList);
            foreach (var w in snap)
            {
                if (autoPinned.Contains(w.Hwnd)) continue;
                if (!autoRules.Contains(w.Proc)) continue;
                if (!Native.IsWindow(w.Hwnd) || !Native.IsWindowVisible(w.Hwnd)) continue;
                if (Manager.IsTopmost(w.Hwnd)) { autoPinned.Add(w.Hwnd); continue; }
                if (Manager.SetTopmost(w.Hwnd, true))
                {
                    autoPinned.Add(w.Hwnd);
                    if (notify) ToastGood("已按规则自动置顶：" + w.Proc);
                }
            }
        }

        static bool IsDead(IntPtr h) { return !Native.IsWindow(h); }

        // ==================================================================
        // 图片背景
        // ==================================================================

        DisplayFit CurrentFit()
        {
            int i = cfg.Int("fitMode", 0);
            if (i == 1) return DisplayFit.FitContain;
            if (i == 2) return DisplayFit.StretchFill;
            return DisplayFit.CoverFill;
        }

        public DisplayFit Fit
        {
            get { return CurrentFit(); }
            set { cfg.Set("fitMode", (int)value); Save(); ReapplyFit(); }
        }

        public byte BgAlpha() { return (byte)Math.Max(60, Math.Min(250, cfg.Int("bgAlpha", 160))); }

        public bool HasBg(IntPtr h) { lock (bgForms) return bgForms.ContainsKey(h); }
        public int BgCount { get { lock (bgForms) return bgForms.Count; } }

        public void PickImageAndApply(IntPtr h)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择背景图片";
                dlg.Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Image img;
                try { img = Image.FromFile(dlg.FileName); }
                catch (Exception ex) { ToastBad("无法加载图片：" + ex.Message); return; }
                var fit = CurrentFit();
                var meta = new BgMeta { Kind = "img", Path = dlg.FileName, Alpha = BgAlpha(), Fit = (int)fit };
                ApplyBgCore(h, delegate { return new BgWindow(h, img, fit); }, meta);
                Toast("图片背景已应用。", ToastKind.Good);
            }
        }

        public void ApplyBgCore(IntPtr h, Func<BgWindow> createBg, BgMeta meta)
        {
            CloseBgForm(h);
            byte a = meta.Alpha;
            Manager.ApplyBg(h, a);
            var bg = createBg();
            lock (bgForms) { bgForms[h] = bg; bgMeta[h] = meta; }
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
                Toast("已取消背景并还原窗口样式。", ToastKind.Good);
            }
            else Toast("该窗口当前没有背景。", ToastKind.Info);
            if (current != null) current.Tick();
        }

        void CloseBgForm(IntPtr h)
        {
            lock (bgForms)
            {
                bgMeta.Remove(h);
                BgWindow bg;
                if (bgForms.TryGetValue(h, out bg))
                {
                    bgForms.Remove(h);
                    try { bg.Hide(); } catch { }
                    try { bg.Close(); } catch { }
                }
            }
            SaveBgMemory();
        }

        void OnBgTick(object sender, EventArgs e)
        {
            List<IntPtr> dead = null;
            List<BgWindow> follow = null;
            lock (bgForms)
            {
                foreach (var kv in bgForms)
                {
                    if (!Native.IsWindow(kv.Key)) { if (dead == null) dead = new List<IntPtr>(); dead.Add(kv.Key); }
                    else { if (follow == null) follow = new List<BgWindow>(); follow.Add(kv.Value); }
                }
            }
            if (follow != null) foreach (var b in follow) b.Follow();
            if (dead != null) foreach (var k in dead) { CloseBgForm(k); Manager.ClearBg(k); }
        }

        public void SetBgAlpha(int v)
        {
            cfg.Set("bgAlpha", Math.Max(60, Math.Min(250, v)));
            Save();
            lock (bgForms)
            {
                foreach (var kv in new Dictionary<IntPtr, BgMeta>(bgMeta))
                {
                    kv.Value.Alpha = (byte)Math.Max(60, Math.Min(250, v));
                    if (Ok(kv.Key)) Manager.ApplyBg(kv.Key, kv.Value.Alpha);
                }
            }
        }

        void ReapplyFit()
        {
            // 图片背景：重建对应 BgWindow 以应用新显示模式
            Dictionary<IntPtr, BgMeta> snap;
            lock (bgForms) snap = new Dictionary<IntPtr, BgMeta>(bgMeta);
            foreach (var kv in snap)
            {
                if (kv.Value.Kind != "img" || !Ok(kv.Key)) continue;
                try
                {
                    string path = kv.Value.Path;
                    var fit = CurrentFit();
                    var meta = new BgMeta { Kind = "img", Path = path, Alpha = kv.Value.Alpha, Fit = (int)fit };
                    var img = Image.FromFile(path);
                    ApplyBgCore(kv.Key, delegate { return new BgWindow(kv.Key, img, fit); }, meta);
                }
                catch (Exception ex) { Program.Log(ex); }
            }
        }

        public Dictionary<IntPtr, BgMeta> BgSnapshot()
        {
            lock (bgForms) return new Dictionary<IntPtr, BgMeta>(bgMeta);
        }

        // ---------------- 背景记忆 ----------------

        public bool BgMemory { get { return cfg.Bool("bgMem", true); } set { cfg.Set("bgMem", value); Save(); } }

        static string Esc(string s) { return (s ?? "").Replace("|", "｜").Replace("\r", "").Replace("\n", ""); }

        void SaveBgMemory()
        {
            if (!BgMemory) return;
            var lines = new List<string>();
            lock (bgForms)
            {
                foreach (var kv in bgForms)
                {
                    BgMeta meta;
                    if (!bgMeta.TryGetValue(kv.Key, out meta)) continue;
                    uint pid; Native.GetWindowThreadProcessId(kv.Key, out pid);
                    lines.Add(Esc(ProcName(pid)) + "|" + Esc(TitleOf(kv.Key)) + "|" + meta.Kind + "|" +
                              Esc(meta.Path) + "|" + meta.Alpha + "|" + meta.Fit);
                }
            }
            if (lines.Count == 0 && pendingRestore.Count > 0)
                foreach (var ent in pendingRestore) lines.Add(string.Join("|", ent));
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
                if (ent[2] != "img") { done.Add(ent); continue; }   // 旧版本的视频条目直接跳过
                IntPtr wh = FindWindowMatch(ent[0], ent[1]);
                if (wh == IntPtr.Zero) continue;
                try
                {
                    byte al; int fitN;
                    if (!byte.TryParse(ent[4], out al)) al = 160;
                    if (!int.TryParse(ent[5], out fitN)) fitN = 0;
                    var meta = new BgMeta { Kind = "img", Path = ent[3], Alpha = al, Fit = fitN };
                    var fit = (DisplayFit)fitN;
                    var img = Image.FromFile(ent[3]);
                    IntPtr w = wh;
                    ApplyBgCore(wh, delegate { return new BgWindow(w, img, fit); }, meta);
                    Toast("已恢复窗口背景：" + ent[1], ToastKind.Good);
                }
                catch (Exception ex) { Program.Log(ex); }
                done.Add(ent);
            }
            foreach (var d in done) pendingRestore.Remove(d);
            if (restoreTries > 240) pendingRestore.Clear();
        }

        // ==================================================================
        // 布局方案
        // ==================================================================

        readonly List<Preset> presets = new List<Preset>();

        public List<Preset> Presets { get { return presets; } }

        static int Parse0(string s)
        {
            int v;
            return int.TryParse(s, out v) ? v : 0;
        }

        static string Unesc(string s)
        {
            return (s ?? "").Replace('｜', '|');
        }

        public void LoadPresets()
        {
            presets.Clear();
            int n = cfg.Int("presetCount", 0);
            for (int i = 0; i < n; i++)
            {
                string nm = cfg.Str("p" + i + "_name", "");
                if (nm.Length == 0) continue;
                var p = new Preset { Name = Unesc(nm) };
                int m = cfg.Int("p" + i + "_count", 0);
                for (int j = 0; j < m; j++)
                {
                    string raw = cfg.Str("p" + i + "_e" + j, "");
                    var f = raw.Split('|');
                    if (f.Length < 9) continue;
                    int x;
                    if (!int.TryParse(f[2], out x)) continue;
                    p.Items.Add(new PresetEntry
                    {
                        Proc = f[0],
                        Title = Unesc(f[1]),
                        X = x, Y = Parse0(f[3]), W = Parse0(f[4]), H = Parse0(f[5]),
                        HasRect = f[6] == "1", Top = f[7] == "1", Alpha = Parse0(f[8])
                    });
                }
                if (p.Items.Count > 0) presets.Add(p);
            }
        }

        public void SavePresets()
        {
            cfg.Set("presetCount", presets.Count);
            for (int i = 0; i < presets.Count; i++)
            {
                var p = presets[i];
                cfg.Set("p" + i + "_name", Esc(p.Name));
                cfg.Set("p" + i + "_count", p.Items.Count);
                for (int j = 0; j < p.Items.Count; j++)
                {
                    var e = p.Items[j];
                    cfg.Set("p" + i + "_e" + j,
                        Esc(e.Proc) + "|" + Esc(e.Title) + "|" + e.X + "|" + e.Y + "|" +
                        e.W + "|" + e.H + "|" + (e.HasRect ? "1" : "0") + "|" +
                        (e.Top ? "1" : "0") + "|" + e.Alpha);
                }
                for (int j = p.Items.Count; ; j++)
                {
                    if (!cfg.Has("p" + i + "_e" + j)) break;
                    cfg.Remove("p" + i + "_e" + j);
                }
            }
            for (int i = presets.Count; ; i++)
            {
                if (!cfg.Has("p" + i + "_name")) break;
                cfg.Remove("p" + i + "_name");
                cfg.Remove("p" + i + "_count");
                for (int j = 0; ; j++)
                {
                    if (!cfg.Has("p" + i + "_e" + j)) break;
                    cfg.Remove("p" + i + "_e" + j);
                }
            }
            cfg.Save();
        }

        public Preset CapturePreset(string name)
        {
            var p = new Preset { Name = name };
            List<WinEntry> snap;
            lock (enumLock) snap = new List<WinEntry>(winList);
            foreach (var w in snap)
            {
                if (!Native.IsWindow(w.Hwnd) || Native.IsIconic(w.Hwnd)) continue;
                Native.RECT r;
                if (!Native.GetWindowRect(w.Hwnd, out r)) continue;
                if (r.Right - r.Left <= 0 || r.Bottom - r.Top <= 0) continue;
                p.Items.Add(new PresetEntry
                {
                    Proc = w.Proc,
                    Title = w.Title,
                    X = r.Left, Y = r.Top,
                    W = r.Right - r.Left, H = r.Bottom - r.Top,
                    HasRect = true,
                    Top = Manager.IsTopmost(w.Hwnd),
                    Alpha = Manager.GetLastAlpha(w.Hwnd)
                });
                if (p.Items.Count >= 40) break;
            }
            return p;
        }

        public int ApplyPreset(Preset p)
        {
            if (p == null) return 0;
            int hit = 0;
            foreach (var e in p.Items)
            {
                IntPtr h = FindWindowMatch(e.Proc, e.Title);
                if (h == IntPtr.Zero) continue;
                if (Native.IsIconic(h)) Native.ShowWindow(h, Native.SW_RESTORE);
                if (e.HasRect && e.W > 0 && e.H > 0)
                    Native.SetWindowPos(h, IntPtr.Zero, e.X, e.Y, e.W, e.H,
                        Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Manager.SetTopmost(h, e.Top);
                if (e.Alpha > 0 && e.Alpha < 255) Manager.ApplyOpacity(h, (byte)e.Alpha);
                else if (e.Alpha == 255) Manager.ApplyOpacity(h, 255);
                hit++;
            }
            return hit;
        }

        public void AddPreset(Preset p)
        {
            for (int i = presets.Count - 1; i >= 0; i--)
                if (presets[i].Name == p.Name) presets.RemoveAt(i);
            presets.Add(p);
            SavePresets();
        }

        public void DeletePreset(Preset p)
        {
            presets.Remove(p);
            SavePresets();
        }

        // ==================================================================
        // 窗口匹配 / 标题
        // ==================================================================

        public static string TitleOf(IntPtr h)
        {
            var sb = new StringBuilder(512);
            Native.GetWindowText(h, sb, sb.Capacity);
            return sb.Length == 0 ? "(无标题)" : sb.ToString();
        }

        IntPtr FindWindowMatch(string proc, string title)
        {
            if (string.IsNullOrEmpty(title)) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows(delegate(IntPtr h, IntPtr lp)
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

        // ==================================================================
        // 托盘
        // ==================================================================

        void BuildTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Renderer = new DarkMenuRenderer();
            trayMenu.ShowImageMargin = false;

            tray = new NotifyIcon();
            tray.Icon = AppIcon();
            tray.Text = "OI 窗口工具";
            tray.ContextMenuStrip = trayMenu;
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
            tray.DoubleClick += (s, e) => ShowMain();

            RebuildTrayMenu();
        }

        static Icon AppIcon()
        {
            int px = Math.Max(16, Theme.S(16));
            if (px > 128) px = 128;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var st = asm.GetManifestResourceStream("WindowTool.tray.ico"))
                    if (st != null) return new Icon(st, px, px);
            }
            catch (Exception ex) { Program.Log(ex); }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            return SystemIcons.Application;
        }

        public void RebuildTrayMenu()
        {
            if (trayMenu == null) return;
            trayMenu.Items.Clear();

            AddMenuItem("显示主界面", null, delegate { ShowMain(); });

            var pinned = Manager.PinnedByUs();
            if (pinned.Length > 0)
            {
                AddMenuHeader("已置顶 " + pinned.Length + " 个");
                int shown = Math.Min(pinned.Length, 6);
                for (int i = 0; i < shown; i++)
                {
                    IntPtr hh = pinned[i];
                    string t = Theme.Ellipsis(TitleOf(hh), Theme.FontBody, Theme.S(190));
                    AddMenuItem("取消置顶「" + t + "」", null, delegate
                    {
                        if (Manager.HasOp2(hh)) Manager.CancelOp2(hh);
                        else Manager.SetTopmost(hh, false);
                        Toast("已还原一个置顶窗口。", ToastKind.Good);
                    });
                }
                if (pinned.Length > shown)
                {
                    AddMenuItem("全部取消置顶", null, delegate
                    {
                        int n = Manager.CancelAll();
                        Toast("已还原 " + n + " 个置顶窗口。", ToastKind.Good);
                    });
                }
            }

            AddMenuHeader("桌面背景");
            AddMenuItem("选择图片…", null, delegate { TrayApplyBg(); });
            var off = AddMenuItem("关闭背景", null, delegate { TrayBgOff(); });
            off.Enabled = BgCount > 0;

            AddMenuHeader("首选项");
            var dark = AddMenuItem("深色模式", null, delegate { SetTrayDark(!Theme.IsDark); });
            dark.Checked = Theme.IsDark;
            var guard = AddMenuItem("置顶守护", null, delegate { ToggleGuard(); });
            guard.Checked = GuardEnabled;
            var run = AddMenuItem("开机自动运行", null, delegate { ToggleAutoRun(); });
            run.Checked = AutoRunOn();

            AddMenuHeader("");
            AddMenuItem("退出", null, delegate { reallyExit = true; Close(); });
        }

        ToolStripMenuItem AddMenuItem(string text, Image img, EventHandler onClick)
        {
            var it = new ToolStripMenuItem(text, img);
            it.ForeColor = Theme.Text;
            it.Font = Theme.FontBody;
            if (onClick != null) it.Click += onClick;
            trayMenu.Items.Add(it);
            return it;
        }

        ToolStripMenuItem AddMenuHeader(string text)
        {
            if (text.Length == 0)
            {
                trayMenu.Items.Add(new ToolStripSeparator());
                return null;
            }
            var it = new ToolStripMenuItem(text);
            it.Enabled = false;
            it.ForeColor = Theme.TextFaint;
            it.Font = Theme.FontSmall;
            trayMenu.Items.Add(it);
            return it;
        }

        void ToggleGuard()
        {
            GuardEnabled = !GuardEnabled;
            Toast(GuardEnabled ? "置顶守护已开启。" : "置顶守护已关闭。", ToastKind.Info);
            RebuildTrayMenu();
        }

        void SetTrayDark(bool on)
        {
            cfg.Set("darkMode", on ? 1 : 0);
            ApplyTheme();
            Save();
            RebuildTrayMenu();
        }

        void TrayApplyBg()
        {
            IntPtr t = MenuTarget();
            if (!Ok(t)) return;
            PickImageAndApply(t);
        }

        void TrayBgOff()
        {
            IntPtr t = MenuTarget();
            if (!Ok(t)) return;
            bool had = Manager.ClearBg(t);
            if (had) CloseBgForm(t);
            Balloon(had ? "已取消背景并还原窗口样式。" : "该窗口当前没有背景。");
        }

        IntPtr MenuTarget()
        {
            IntPtr t = HotTarget();
            if (!Ok(t)) { Balloon("请先在「窗口」页选择一个目标窗口。"); return IntPtr.Zero; }
            return t;
        }

        void Balloon(string text)
        {
            if (tray == null) return;
            try
            {
                tray.BalloonTipTitle = "OI 窗口工具";
                tray.BalloonTipText = text;
                tray.ShowBalloonTip(1600);
            }
            catch { }
        }

        void ShowMain()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        public bool AutoRunOn()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    return k != null && k.GetValue("WindowTool") != null;
            }
            catch { }
            return false;
        }

        public void ToggleAutoRun() { SetAutoRun(!AutoRunOn(), true); }

        public void SetAutoRun(bool on, bool notify)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) { ToastBad("无法打开注册表启动项。"); return; }
                    if (on) k.SetValue("WindowTool", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("WindowTool", false);
                }
                if (notify) ToastGood(on ? "已开启开机自动运行。" : "已关闭开机自动运行。");
                RebuildTrayMenu();
            }
            catch (Exception ex) { Program.Log(ex); ToastBad("设置失败：" + ex.Message); }
        }
    }

    /// <summary>托盘菜单的暗色渲染器。</summary>
    sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(Point.Empty, e.Item.Size);
            bool sel = e.Item.Selected && e.Item.Enabled;
            using (var b = new SolidBrush(sel ? Theme.CardHi : Theme.Rail))
                g.FillRectangle(b, r.X, r.Y, r.Width - 1, r.Height);
        }
    }

    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Theme.Rail; } }
        public override Color MenuStripGradientEnd { get { return Theme.Rail; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Rail; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Rail; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Rail; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Rail; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Rail; } }
        public override Color MenuItemSelected { get { return Theme.CardHi; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.CardHi; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.CardHi; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.CardHi; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.CardHi; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
        public override Color ToolStripBorder { get { return Theme.Border; } }
        public override Color ToolStripGradientBegin { get { return Theme.Rail; } }
        public override Color ToolStripGradientMiddle { get { return Theme.Rail; } }
        public override Color ToolStripGradientEnd { get { return Theme.Rail; } }
    }
}
