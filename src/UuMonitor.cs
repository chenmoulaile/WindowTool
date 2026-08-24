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

        // 内存判定：UU相关进程最大内存 >100MB=被控；<50MB=解除；中间保持原状态（滞回防抖）
        const double CtrlOnMB = 100;
        const double CtrlOffMB = 50;
        static bool controlled;
        static string memInfo = "";
        static DateTime releaseShownUntil = DateTime.MinValue;

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
            controlled = false;
            releaseShownUntil = DateTime.MinValue;
            lastFastTick = Stopwatch.GetTimestamp();
            fast.Start();
            slow.Start();
            RaiseStatus("监控中…(进程内存/标题" + (lagAlertEnabled ? "/卡顿" : "") + ")");
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
            alert.ShowOnce(3000);
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
                string memReason = EvalMemory();
                string why = memReason;
                if (why == null && titleKey.Length > 0 && TitleHit())
                    why = "标题含\"" + titleKey + "\"";
                if (why == null && lagging && lagAlertEnabled)
                    why = "疑似异常卡顿";

                if (why != null)
                {
                    if (reason == null) phase = 0;
                    reason = "你已被控制";
                    // 循环提醒：显示约2秒、隐藏约4秒，直到状态解除
                    bool vis = (phase % 6) < 2;
                    alert.SetText(reason);
                    alert.SetVisible(vis);
                    RaiseStatus("检测到被控！(" + why + ") 鼠标旁持续提醒。");
                }
                else
                {
                    reason = null;
                    // "已解除控制"提示显示期间不要把它藏掉
                    if (DateTime.Now >= releaseShownUntil) alert.SetVisible(false);
                    RaiseStatus(controlled
                        ? "监控中…已过峰值，等待回落到" + CtrlOffMB + "MB以下。"
                        : "监控中…" + memInfo + "，未检测到被控。");
                }
                wasLagging = lagging;
            }
            catch (Exception ex)
            {
                RaiseStatus("监控出错：" + ex.Message);
            }
        }

        // 内存滞回判定：>100MB 进入被控；<50MB 解除并弹"已解除控制"
        static string EvalMemory()
        {
            if (procKeys.Length == 0) return null;
            long max = 0;
            int count = 0;
            foreach (var p in Process.GetProcesses())
            {
                string name = null;
                string path = null;
                long ws = 0;
                try { name = p.ProcessName.ToLowerInvariant(); } catch { }
                try { path = p.MainModule.FileName.ToLowerInvariant(); } catch { }
                try { ws = p.WorkingSet64; } catch { }
                finally { p.Dispose(); }

                bool isUu = false;
                foreach (var k in procKeys)
                {
                    if ((name != null && name.Contains(k)) ||
                        (path != null && path.Contains(k))) { isUu = true; break; }
                }
                if (!isUu) continue;
                count++;
                if (ws > max) max = ws;
            }
            double mb = max / 1048576.0;
            memInfo = count == 0 ? "未发现UU进程" : string.Format("UU进程{0}个 最大{1:F0}MB", count, mb);

            if (!controlled && mb > CtrlOnMB)
            {
                controlled = true;
            }
            else if (controlled && mb < CtrlOffMB)
            {
                controlled = false;
                OnReleased();
            }
            return controlled ? string.Format("内存{0:F0}MB", mb) : null;
        }

        static void OnReleased()
        {
            alert.SetText("已解除控制");
            alert.ShowOnce(3000);
            releaseShownUntil = DateTime.Now.AddSeconds(3.5);
            RaiseStatus("已解除控制。");
        }

        // 任意可见窗口标题包含关键词
        static bool TitleHit()
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
            return titleHit;
        }

        static void RaiseStatus(string msg)
        {
            var h = StatusChanged;
            if (h != null) h(msg);
        }
    }
}
