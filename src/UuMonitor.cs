using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    static class UuMonitor
    {
        static Timer timer;
        static AlertForm alert;
        static string[] procKeys = new string[0];
        static string titleKey = "";
        static bool lastMatched;

        public static event Action<string> StatusChanged;

        public static bool Running { get { return timer != null && timer.Enabled; } }

        public static void EnsureCreated()
        {
            if (alert == null || alert.IsDisposed) alert = new AlertForm();
            if (timer == null)
            {
                timer = new Timer();
                timer.Interval = 1000;
                timer.Tick += (s, e) => Poll();
            }
        }

        public static void Configure(string procCsv, string titleKw)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(procCsv))
            {
                var parts = procCsv.Split(new[] { ',', '，', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts) list.Add(p.Trim().ToLowerInvariant());
            }
            procKeys = list.ToArray();
            titleKey = (titleKw ?? "").Trim();
        }

        public static void Start()
        {
            EnsureCreated();
            lastMatched = false;
            timer.Start();
            RaiseStatus("监控中…");
        }

        public static void Stop()
        {
            if (timer != null) timer.Stop();
            if (alert != null && !alert.IsDisposed) alert.SetVisible(false);
            lastMatched = false;
            RaiseStatus("已停用。");
        }

        static void Poll()
        {
            try
            {
                bool m = Check();
                if (m != lastMatched)
                {
                    lastMatched = m;
                    alert.SetVisible(m);
                }
                RaiseStatus(m ? "检测到被控特征！鼠标旁已提示。" : "监控中…未检测到被控。");
            }
            catch (Exception ex)
            {
                RaiseStatus("监控出错：" + ex.Message);
            }
        }

        public static void TestOnce()
        {
            EnsureCreated();
            alert.SetVisible(true);
            var t = new Timer { Interval = 3000 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (!lastMatched) alert.SetVisible(false); };
            t.Start();
        }

        static bool Check()
        {
            if (procKeys.Length == 0) return false;
            bool procHit = false;
            foreach (var p in Process.GetProcesses())
            {
                string name = null;
                try { name = p.ProcessName.ToLowerInvariant(); }
                catch { }
                finally { p.Dispose(); }
                if (name == null) continue;
                foreach (var k in procKeys)
                {
                    if (name.Contains(k)) { procHit = true; break; }
                }
                if (procHit) break;
            }
            if (!procHit) return false;
            if (titleKey.Length == 0) return true;

            bool titleHit = false;
            Native.EnumWindows((h, lp) =>
            {
                if (titleHit) return true;
                if (!Native.IsWindowVisible(h)) return true;
                int len = Native.GetWindowTextLength(h);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 2);
                Native.GetWindowText(h, sb, sb.Capacity);
                if (sb.ToString().Contains(titleKey)) titleHit = true;
                return !titleHit;
            }, IntPtr.Zero);
            return titleHit;
        }

        static void RaiseStatus(string msg)
        {
            var h = StatusChanged;
            if (h != null) h(msg);
        }
    }
}
