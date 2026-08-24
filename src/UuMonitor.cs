using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace WindowTool
{
    static class UuMonitor
    {
        // 快速计时器：采样 UI 线程消息延迟（卡顿检测）
        const int FastMs = 250;
        // 慢速计时器：进程/标题扫描与决策
        const int SlowMs = 1000;

        static Timer fast;
        static Timer slow;
        static AlertForm alert;

        static string[] procKeys = new string[0];
        static string titleKey = "";
        static bool lagAlertEnabled;

        static readonly List<float> samples = new List<float>();
        static long lastFastTick;
        static bool lagging;
        static bool wasLagging;

        static string reason;      // 当前提醒原因；null=无
        static int phase;          // 提醒循环相位（慢速tick计数）

        public static event Action<string> StatusChanged;

        public static bool Running { get { return slow != null && slow.Enabled; } }

        public static void EnsureCreated()
        {
            if (alert == null || alert.IsDisposed) alert = new AlertForm();
            if (fast == null)
            {
                fast = new Timer { Interval = FastMs };
                fast.Tick += (s, e) => SampleLag();
            }
            if (slow == null)
            {
                slow = new Timer { Interval = SlowMs };
                slow.Tick += (s, e) => Decide();
            }
        }

        public static void Configure(string procCsv, string titleKw, bool alertOnLag)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(procCsv))
            {
                var parts = procCsv.Split(new[] { ',', '，', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts) list.Add(p.Trim().ToLowerInvariant());
            }
            procKeys = list.ToArray();
            titleKey = (titleKw ?? "").Trim();
            lagAlertEnabled = alertOnLag;
        }

        public static void Start()
        {
            EnsureCreated();
            samples.Clear();
            lagging = false;
            wasLagging = false;
            reason = null;
            phase = 0;
            lastFastTick = Stopwatch.GetTimestamp();
            fast.Start();
            slow.Start();
            RaiseStatus("监控中…(进程窗口/标题" + (lagAlertEnabled ? "/卡顿" : "") + ")");
        }

        public static void Stop()
        {
            if (fast != null) fast.Stop();
            if (slow != null) slow.Stop();
            if (alert != null && !alert.IsDisposed) alert.SetVisible(false);
            reason = null;
            lagging = false;
            RaiseStatus("已停用。");
        }

        public static void TestOnce()
        {
            EnsureCreated();
            alert.SetText("你已被控制");
            alert.SetVisible(true);
            var t = new Timer { Interval = 3000 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (reason == null) alert.SetVisible(false); };
            t.Start();
        }

        // ---------- 卡顿检测：UI线程 250ms 定时器的实际间隔漂移 ----------
        static void SampleLag()
        {
            try
            {
                long now = Stopwatch.GetTimestamp();
                double ms = (now - lastFastTick) * 1000.0 / Stopwatch.Frequency;
                lastFastTick = now;
                if (ms <= 0 || ms > 5000) return; // 忽略挂起恢复等异常样本
                lock (samples)
                {
                    samples.Add((float)(ms / FastMs));
                    if (samples.Count > 40) samples.RemoveAt(0);
                    if (samples.Count >= 24)
                    {
                        float sum = 0f;
                        for (int i = 0; i < samples.Count; i++) sum += samples[i];
                        float avg = sum / samples.Count;
                        // 滞回：>2.2 判定卡顿，<1.5 解除（10秒窗口均值）
                        if (!lagging && avg > 2.2f) lagging = true;
                        else if (lagging && avg < 1.5f) lagging = false;
                    }
                }
            }
            catch { }
        }

        // ---------- 每秒决策 ----------
        static void Decide()
        {
            try
            {
                phase++;
                string hit = Scan();
                string why = hit;
                if (why == null && lagging && lagAlertEnabled) why = "疑似异常卡顿";

                if (why != reason)
                {
                    reason = why;
                    phase = 0;
                }

                if (reason != null)
                {
                    // 循环提醒：显示约2秒、隐藏约4秒，直到状态解除
                    bool vis = (phase % 6) < 2;
                    alert.SetText(reason);
                    alert.SetVisible(vis);
                    string src = reason == "你已被控制"
                        ? "检测到被控特征！鼠标旁持续提醒。(" + hit + ")"
                        : "系统持续卡顿中，鼠标旁提醒。";
                    RaiseStatus(src);
                }
                else
                {
                    alert.SetVisible(false);
                    RaiseStatus(lagging ? "监控中…系统略卡(未达提醒阈值)" : "监控中…未检测到被控。");
                }
                wasLagging = lagging;
            }
            catch (Exception ex)
            {
                RaiseStatus("监控出错：" + ex.Message);
            }
        }

        // 返回命中描述；未命中返回 null
        static string Scan()
        {
            // 1) 进程名或 exe 完整路径包含关键词 => 直接判定被控
            //    （如 GameViewerServer.exe 一运行即触发，无需等窗口出现）
            string nameFallback = null;
            foreach (var p in Process.GetProcesses())
            {
                string name = null;
                string path = null;
                try { name = p.ProcessName.ToLowerInvariant(); } catch { }
                try { path = p.MainModule.FileName.ToLowerInvariant(); } catch { }
                finally { p.Dispose(); }

                foreach (var k in procKeys)
                {
                    if (path != null && path.Contains(k))
                    {
                        string shown = "exe:" + path;
                        return shown.Length > 60 ? shown.Substring(0, 57) + "…" : shown;
                    }
                    if (nameFallback == null && name != null && name.Contains(k))
                        nameFallback = "进程:" + name;
                }
            }
            if (nameFallback != null) return nameFallback;

            // 2) 任意可见窗口标题包含关键词
            if (titleKey.Length > 0)
            {
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
                if (titleHit) return "标题含\"" + titleKey + "\"";
            }
            return null;
        }

        static void RaiseStatus(string msg)
        {
            var h = StatusChanged;
            if (h != null) h(msg);
        }
    }
}
