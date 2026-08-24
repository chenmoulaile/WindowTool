using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WindowTool
{
    sealed class MouseCapture : IDisposable
    {
        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        const int WH_MOUSE_LL = 14;
        const int WM_LBUTTONDOWN = 0x0201;

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc proc, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT { public Native.POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

        IntPtr hook;
        public event Action<Point> Clicked;

        public bool Active { get { return hook != IntPtr.Zero; } }

        public void Start()
        {
            if (hook != IntPtr.Zero) return;
            hook = SetWindowsHookEx(WH_MOUSE_LL, Proc, GetModuleHandle(null), 0);
        }

        public void Stop()
        {
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
        }

        IntPtr Proc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && (int)wParam == WM_LBUTTONDOWN)
            {
                var ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                var pt = new Point(ms.pt.X, ms.pt.Y);
                var ev = Clicked;
                Stop();
                if (ev != null) ev(pt);
                return new IntPtr(1);
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        public void Dispose() { Stop(); }
    }
}
