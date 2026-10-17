using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace WindowTool
{
    /// 一个窗口在方案里的快照。HWND 重启就变了，所以按 进程名+标题 重新匹配。
    sealed class PresetEntry
    {
        public string Proc = "";
        public string Title = "";
        public int X, Y, W, H;
        public bool Top;
        public int Alpha = 255;
        public bool HasRect;
    }

    sealed class Preset
    {
        public string Name = "";
        public readonly List<PresetEntry> Items = new List<PresetEntry>();
        public override string ToString() { return Name; }
    }

    sealed partial class MainForm
    {
        readonly List<Preset> presets = new List<Preset>();

        public List<Preset> Presets { get { return presets; } }

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
                    int x, y, w, hh, al;
                    bool hasRect, top;
                    if (!int.TryParse(f[2], out x)) continue;
                    y = Parse0(f[3]); w = Parse0(f[4]); hh = Parse0(f[5]); al = Parse0(f[8]);
                    hasRect = f[6] == "1"; top = f[7] == "1";
                    p.Items.Add(new PresetEntry
                    {
                        Proc = f[0], Title = Unesc(f[1]),
                        X = x, Y = y, W = w, H = hh,
                        HasRect = hasRect, Top = top, Alpha = al
                    });
                }
                if (p.Items.Count > 0) presets.Add(p);
            }
        }

        static int Parse0(string s)
        {
            int v;
            return int.TryParse(s, out v) ? v : 0;
        }

        static string Unesc(string s)
        {
            return (s ?? "").Replace('｜', '|');
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

        /// 抓当前桌面上所有可见顶层窗口的矩形 / 置顶 / 透明度。
        public Preset CapturePreset(string name)
        {
            var p = new Preset { Name = name };
            foreach (var w in winList)
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
                if (p.Items.Count >= 40) break;      // 上限，避免 settings.txt 无限膨胀
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
    }
}
