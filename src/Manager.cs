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
    }

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
