using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WindowTool
{
    sealed class WinInfo
    {
        public IntPtr Hwnd;
        public bool Op2Active;
        public int OrigExStyle;
        public bool WasLayered;
        public byte RestoreAlpha;
        public IntPtr BgHwnd;
        public byte BgAlpha;
        public byte LastAlpha = 255;
        public bool TopByUs;   // 置顶是否由本工具设置（退出时用于还原）
        public bool BlurOn;
        public System.Drawing.Rectangle PrevRect;
        public bool HasPrev;   // 吸附前的大小位置，用于「还原」
    }

    public enum SnapKind { Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight, Max, Center }

    static class Manager
    {
        static readonly Dictionary<IntPtr, WinInfo> states = new Dictionary<IntPtr, WinInfo>();
        static readonly List<IntPtr> pinOrder = new List<IntPtr>();

        public static int PinnedCount { get { lock (pinOrder) return pinOrder.Count; } }

        public static IntPtr[] PinnedSnapshot()
        {
            lock (pinOrder) return pinOrder.ToArray();
        }

        public static WinInfo GetInfo(IntPtr h)
        {
            WinInfo s;
            if (!states.TryGetValue(h, out s))
            {
                s = new WinInfo();
                s.Hwnd = h;
                s.OrigExStyle = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                s.WasLayered = (s.OrigExStyle & Native.WS_EX_LAYERED) != 0;
                s.RestoreAlpha = 255;
                if (s.WasLayered)
                {
                    // 记录窗口被本工具修改前的透明度，退出时可还原
                    try
                    {
                        uint key; byte a; uint flags;
                        if (Native.GetLayeredWindowAttributes(h, out key, out a, out flags) && (flags & Native.LWA_ALPHA) != 0)
                            s.RestoreAlpha = a == 0 ? (byte)255 : a;
                    }
                    catch { }
                }
                states[h] = s;
            }
            return s;
        }

        public static WinInfo Find(IntPtr h)
        {
            WinInfo s;
            return states.TryGetValue(h, out s) ? s : null;
        }

        public static bool HasOp2(IntPtr h)
        {
            var s = Find(h);
            return s != null && s.Op2Active;
        }

        public static bool IsTopmost(IntPtr h)
        {
            return (Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0;
        }

        public static void SetTopmost(IntPtr h, bool topmost)
        {
            SetTopmost(h, topmost, true);
        }

        public static void SetTopmost(IntPtr h, bool topmost, bool track)
        {
            if (track) GetInfo(h).TopByUs = topmost;
            Native.SetWindowPos(h, topmost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public static byte GetLastAlpha(IntPtr h)
        {
            var s = Find(h);
            return s == null ? (byte)255 : s.LastAlpha;
        }

        public static void ApplyOpacity(IntPtr h, byte alpha)
        {
            var s = GetInfo(h);
            s.LastAlpha = alpha;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_LAYERED) == 0)
                Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_LAYERED);
            Native.SetLayeredWindowAttributes(h, 0, alpha, Native.LWA_ALPHA);
        }

        public static void ApplyOp2(IntPtr h)
        {
            var s = GetInfo(h);
            if (s.Op2Active) return;
            s.Op2Active = true;

            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED);

            SetTopmost(h, true);

            IntPtr[] snap;
            lock (pinOrder) snap = pinOrder.ToArray();
            if (snap.Length > 0)
            {
                IntPtr last = snap[snap.Length - 1];
                Native.SetWindowPos(h, last, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
            lock (pinOrder) pinOrder.Add(h);

            ApplyOpacity(h, 245);
        }

        public static bool CancelOp2(IntPtr h)
        {
            var s = Find(h);
            if (s == null || !s.Op2Active) return false;
            s.Op2Active = false;
            s.TopByUs = false;
            lock (pinOrder) pinOrder.Remove(h);

            RestoreLayerAndAlpha(s);
            SetTopmost(h, false, false);
            return true;
        }

        static void RestoreLayerAndAlpha(WinInfo s)
        {
            int ex = Native.GetWindowLong(s.Hwnd, Native.GWL_EXSTYLE);
            ex &= ~Native.WS_EX_TRANSPARENT;
            bool needLayered = s.WasLayered || s.BgHwnd != IntPtr.Zero;
            if (!needLayered) ex &= ~Native.WS_EX_LAYERED;
            Native.SetWindowLong(s.Hwnd, Native.GWL_EXSTYLE, ex);

            if (needLayered)
            {
                byte a = s.BgHwnd != IntPtr.Zero ? s.BgAlpha : s.RestoreAlpha;
                Native.SetLayeredWindowAttributes(s.Hwnd, 0, a, Native.LWA_ALPHA);
            }
        }

        public static bool ApplyBg(IntPtr h, byte alpha)
        {
            var s = GetInfo(h);
            s.BgAlpha = alpha;
            ApplyOpacity(h, alpha);
            return true;
        }

        public static void SetBgHwnd(IntPtr h, IntPtr bgHwnd)
        {
            GetInfo(h).BgHwnd = bgHwnd;
        }

        public static IntPtr GetBgHwnd(IntPtr h)
        {
            var s = Find(h);
            return (s == null || !Native.IsWindow(s.BgHwnd)) ? IntPtr.Zero : s.BgHwnd;
        }

        public static bool ClearBg(IntPtr h)
        {
            var s = Find(h);
            if (s == null || s.BgHwnd == IntPtr.Zero) return false;
            s.BgHwnd = IntPtr.Zero;

            if (s.Op2Active)
                ApplyOpacity(h, 245);
            else
                RestoreLayerAndAlpha(s);
            return true;
        }


        public static int CancelAll()
        {
            int n = 0;
            foreach (var h in PinnedSnapshot())
                if (CancelOp2(h)) n++;
            return n;
        }

        // 退出时调用：还原本工具对窗口做过的一切（置顶/穿透/透明度/背景样式）
        public static void RestoreAll()
        {
            IntPtr[] snap;
            lock (states) { snap = new IntPtr[states.Count]; states.Keys.CopyTo(snap, 0); }
            foreach (var h in snap)
            {
                try
                {
                    var s = Find(h);
                    if (s == null || !Native.IsWindow(h)) continue;
                    if (s.Op2Active) { CancelOp2(h); continue; }

                    // 普通窗口：还原分层样式与透明度
                    if (s.WasLayered)
                        Native.SetLayeredWindowAttributes(h, 0, s.RestoreAlpha, Native.LWA_ALPHA);
                    else
                    {
                        int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                        if ((ex & Native.WS_EX_LAYERED) != 0)
                            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex & ~Native.WS_EX_LAYERED);
                    }
                    if (s.TopByUs)
                    {
                        SetTopmost(h, false, false);
                        s.TopByUs = false;
                    }
                }
                catch (Exception ex) { Program.Log(ex); }
            }
        }

        static bool IsMovableNeighbor(IntPtr h)
        {
            if (!Native.IsWindow(h) || !Native.IsWindowVisible(h)) return false;
            return (Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) == 0;
        }

        static IntPtr PrevSameBand(IntPtr h, bool hTopmost)
        {
            var p = Native.GetWindow(h, Native.GW_HWNDPREV);
            while (p != IntPtr.Zero)
            {
                if (IsMovableNeighbor(p) && IsTopmost(p) == hTopmost) return p;
                p = Native.GetWindow(p, Native.GW_HWNDPREV);
            }
            return IntPtr.Zero;
        }

        static IntPtr NextSameBand(IntPtr h, bool hTopmost)
        {
            var n = Native.GetWindow(h, Native.GW_HWNDNEXT);
            while (n != IntPtr.Zero)
            {
                if (IsMovableNeighbor(n) && IsTopmost(n) == hTopmost) return n;
                n = Native.GetWindow(n, Native.GW_HWNDNEXT);
            }
            return IntPtr.Zero;
        }

        public static bool MoveLayerUp(IntPtr h)
        {
            bool top = IsTopmost(h);
            var prev = PrevSameBand(h, top);
            if (prev == IntPtr.Zero) return false;
            Native.SetWindowPos(h, prev, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            return true;
        }

        public static bool MoveLayerDown(IntPtr h)
        {
            bool top = IsTopmost(h);
            var next = NextSameBand(h, top);
            if (next == IntPtr.Zero)
            {
                if (top) return false;
                MoveLayerBottom(h);
                return true;
            }
            Native.SetWindowPos(h, next, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            return true;
        }

        public static void MoveLayerBottom(IntPtr h)
        {
            Native.SetWindowPos(h, Native.HWND_BOTTOM, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        // ---------------- 亚克力 / 模糊 ----------------

        public static bool IsBlurred(IntPtr h)
        {
            var s = Find(h);
            return s != null && s.BlurOn;
        }

        /// 先试 Win10 的亚克力，失败再退回旧的 BLURBEHIND；都不支持时返回 false。
        public static bool ApplyBlur(IntPtr h, bool on)
        {
            if (!Native.IsWindow(h)) return false;
            try
            {
                var pol = new Native.ACCENT_POLICY
                {
                    AccentState = on ? Native.ACCENT_ACRYLIC : Native.ACCENT_DISABLED,
                    AccentFlags = on ? 2 : 0,
                    GradientColor = on ? 0x99FFFFFFu : 0u,
                    AnimationId = 0
                };
                int size = Marshal.SizeOf(pol);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(pol, ptr, false);
                    var data = new Native.WCAATTRIB { Attribute = Native.WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
                    int hr = Native.SetWindowCompositionAttribute(h, ref data);
                    if (on && hr == 0)
                    {
                        pol.AccentState = Native.ACCENT_BLURBEHIND;
                        Marshal.StructureToPtr(pol, ptr, false);
                        data = new Native.WCAATTRIB { Attribute = Native.WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
                        Native.SetWindowCompositionAttribute(h, ref data);
                    }
                }
                finally { Marshal.FreeHGlobal(ptr); }

                var m = new Native.MARGINS();
                if (on) { m.Left = -1; m.Right = -1; m.Top = -1; m.Bottom = -1; }
                Native.DwmExtendFrameIntoClientArea(h, ref m);

                GetInfo(h).BlurOn = on;
                return true;
            }
            catch (Exception ex) { Program.Log(ex); return false; }
        }

        public static void DisableAllBlurs()
        {
            List<IntPtr> snap = null;
            foreach (var kv in states)
                if (kv.Value.BlurOn) { if (snap == null) snap = new List<IntPtr>(); snap.Add(kv.Key); }
            if (snap != null)
                foreach (var h in snap) ApplyBlur(h, false);
        }

        // ---------------- 分屏吸附 ----------------

        static bool WorkArea(IntPtr h, out System.Drawing.Rectangle wa)
        {
            wa = System.Drawing.Rectangle.Empty;
            try
            {
                IntPtr mon = Native.MonitorFromWindow(h, Native.MONITOR_DEFAULTTONEAREST);
                if (mon == IntPtr.Zero) return false;
                var mi = new Native.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
                if (!Native.GetMonitorInfo(mon, ref mi)) return false;
                wa = System.Drawing.Rectangle.FromLTRB(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);
                return wa.Width > 0 && wa.Height > 0;
            }
            catch (Exception ex) { Program.Log(ex); return false; }
        }

        /// 把窗口吸附到工作区的某个区域；第一次吸附时记下原矩形，便于还原。
        public static bool Snap(IntPtr h, SnapKind kind)
        {
            if (!Native.IsWindow(h)) return false;
            System.Drawing.Rectangle wa;
            if (!WorkArea(h, out wa)) return false;

            var s = GetInfo(h);
            if (!s.HasPrev)
            {
                Native.RECT cur;
                if (Native.GetWindowRect(h, out cur))
                {
                    s.PrevRect = System.Drawing.Rectangle.FromLTRB(cur.Left, cur.Top, cur.Right, cur.Bottom);
                    s.HasPrev = true;
                }
            }
            if (Native.IsIconic(h)) Native.ShowWindow(h, Native.SW_RESTORE);

            var target = SnapRect(kind, wa, s.HasPrev ? s.PrevRect : System.Drawing.Rectangle.Empty, s.HasPrev);
            return Native.SetWindowPos(h, IntPtr.Zero, target.X, target.Y, target.Width, target.Height,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }

        /// 纯几何：把某个工作区按吸附类型切出一块。不碰任何窗口，方便离线核对。
        public static System.Drawing.Rectangle SnapRect(SnapKind kind, System.Drawing.Rectangle wa,
            System.Drawing.Rectangle prev, bool hasPrev)
        {
            int halfW = wa.Width / 2, halfH = wa.Height / 2;
            int x = wa.X, y = wa.Y, w = wa.Width, hh = wa.Height;
            switch (kind)
            {
                case SnapKind.Left: w = halfW; break;
                case SnapKind.Right: x = wa.X + halfW; w = wa.Width - halfW; break;
                case SnapKind.Top: hh = halfH; break;
                case SnapKind.Bottom: y = wa.Y + halfH; hh = wa.Height - halfH; break;
                case SnapKind.TopLeft: w = halfW; hh = halfH; break;
                case SnapKind.TopRight: x = wa.X + halfW; w = wa.Width - halfW; hh = halfH; break;
                case SnapKind.BottomLeft: y = wa.Y + halfH; w = halfW; hh = wa.Height - halfH; break;
                case SnapKind.BottomRight:
                    x = wa.X + halfW; y = wa.Y + halfH;
                    w = wa.Width - halfW; hh = wa.Height - halfH; break;
                case SnapKind.Max: break;
                case SnapKind.Center:
                {
                    int cw = Math.Min(hasPrev && prev.Width > 0 ? prev.Width : wa.Width / 2, wa.Width);
                    int ch = Math.Min(hasPrev && prev.Height > 0 ? prev.Height : wa.Height / 2, wa.Height);
                    x = wa.X + (wa.Width - cw) / 2;
                    y = wa.Y + (wa.Height - ch) / 2;
                    w = cw; hh = ch;
                    break;
                }
            }
            return System.Drawing.Rectangle.FromLTRB(x, y, x + w, y + hh);
        }

        /// 回到第一次吸附前的矩形。没有记录时返回 false。
        public static bool Unsnap(IntPtr h)
        {
            var s = Find(h);
            if (s == null || !s.HasPrev || !Native.IsWindow(h)) return false;
            var r = s.PrevRect;
            s.HasPrev = false;
            return Native.SetWindowPos(h, IntPtr.Zero, r.X, r.Y, r.Width, r.Height,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }

        public static bool HasSnapBackup(IntPtr h)
        {
            var s = Find(h);
            return s != null && s.HasPrev;
        }

        public static void Cleanup()
        {
            lock (pinOrder)
                for (int i = pinOrder.Count - 1; i >= 0; i--)
                    if (!Native.IsWindow(pinOrder[i])) pinOrder.RemoveAt(i);

            List<IntPtr> dead = null;
            foreach (var kv in states)
            {
                if (!Native.IsWindow(kv.Key))
                {
                    if (dead == null) dead = new List<IntPtr>();
                    dead.Add(kv.Key);
                }
            }
            if (dead != null)
                foreach (var d in dead) states.Remove(d);
        }
    }
}
