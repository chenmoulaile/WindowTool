using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowTool
{
    enum Ease { Linear, Out, InOut, OutBack, Spring }

    /// 全局补间时钟：所有动画共用一个 ~60fps 定时器。
    /// 每个控件各起一个 Timer 会在页面切换时同时触发十几个 Tick，既费 CPU 又不同步。
    static class Motion
    {
        const int FrameMs = 15;

        static bool enabled = true;
        public static bool Enabled
        {
            get { return enabled; }
            set { enabled = value; if (!value) StopAll(); }
        }

        sealed class Tween
        {
            public object Tag;
            public int Elapsed;
            public int Total;
            public Ease Curve;
            public Action<float> Apply;
            public Action Done;
        }

        static readonly List<Tween> live = new List<Tween>();
        static Timer clock;

        public static void To(object tag, int ms, Ease curve, Action<float> apply)
        {
            To(tag, ms, curve, apply, null);
        }

        /// tag 相同的补间会被替换：快速连点侧栏时旧动画必须让位，否则两条补间抢同一个属性。
        public static void To(object tag, int ms, Ease curve, Action<float> apply, Action done)
        {
            if (apply == null) return;
            if (!enabled || ms <= 0)
            {
                apply(1f);
                if (done != null) done();
                return;
            }
            for (int i = live.Count - 1; i >= 0; i--)
                if (ReferenceEquals(live[i].Tag, tag)) live.RemoveAt(i);
            var t = new Tween();
            t.Tag = tag;
            t.Total = ms;
            t.Curve = curve;
            t.Apply = apply;
            t.Done = done;
            live.Add(t);
            EnsureClock();
            try { t.Apply(0f); } catch (Exception ex) { Program.Log(ex); live.Remove(t); }
        }

        public static void Cancel(object tag)
        {
            for (int i = live.Count - 1; i >= 0; i--) if (ReferenceEquals(live[i].Tag, tag)) live.RemoveAt(i);
            if (live.Count == 0 && clock != null) clock.Stop();
        }

        public static void StopAll()
        {
            live.Clear();
            if (clock != null) clock.Stop();
        }

        static void EnsureClock()
        {
            if (clock == null)
            {
                clock = new Timer();
                clock.Interval = FrameMs;
                clock.Tick += delegate { Step(); };
            }
            if (!clock.Enabled) clock.Start();
        }

        static void Step()
        {
            if (live.Count == 0) { if (clock != null) clock.Stop(); return; }
            // 从后往前遍历：Apply 回调可能启动或结束其它补间
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (i >= live.Count) continue;
                var t = live[i];
                t.Elapsed += FrameMs;
                float p = t.Elapsed >= t.Total ? 1f : (float)t.Elapsed / (float)t.Total;
                bool last = p >= 1f;
                try { t.Apply(Curve(t.Curve, p)); }
                catch (Exception ex) { Program.Log(ex); live.Remove(t); continue; }
                if (last)
                {
                    live.Remove(t);
                    if (t.Done != null) { try { t.Done(); } catch (Exception ex) { Program.Log(ex); } }
                }
            }
            if (live.Count == 0 && clock != null) clock.Stop();
        }

        public static float Curve(Ease kind, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            switch (kind)
            {
                case Ease.InOut:
                    return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.Spring:
                {
                    // 阻尼正弦：到位后轻微回弹一次，用于开关与胶囊位移
                    if (t >= 1f) return 1f;
                    return 1f - (float)Math.Exp(-5.2 * t) * (float)Math.Cos(9.4 * t);
                }
                case Ease.Linear:
                    return t;
                default:
                    return 1f - (1f - t) * (1f - t) * (1f - t);
            }
        }

        // ---------------- 系统偏好 ----------------

        const int SPI_GETCLIENTAREAANIMATION = 0x1048;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(int action, uint param, ref bool v, uint flags);

        /// Windows「在 Windows 中显示动画」关闭时，默认也跟着关掉动画。
        public static bool SystemAllowsAnimation()
        {
            try
            {
                bool on = true;
                if (SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref on, 0)) return on;
            }
            catch { }
            return true;
        }
    }

    /// 悬停淡入值：可交互控件共用同一条曲线，避免" hover 瞬间换色"的生硬感。
    sealed class HoverFade
    {
        readonly object tag = new object();
        readonly Control owner;
        readonly Action repaint;
        public float Value;

        public HoverFade(Control owner) { this.owner = owner; }

        /// ToolStripItem 一类不是 Control 的可绘制对象用这个重载。
        public HoverFade(Action repaint) { this.repaint = repaint; }

        void NeedRepaint()
        {
            if (owner != null) { if (!owner.IsDisposed) owner.Invalidate(); return; }
            if (repaint != null) repaint();
        }

        public void To(float target, int ms)
        {
            float from = Value;
            if (Math.Abs(target - from) < 0.01f) { Value = target; return; }
            Motion.To(tag, ms, Ease.Out, delegate(float t)
            {
                Value = from + (target - from) * t;
                NeedRepaint();
            });
        }

        public void Snap(float v) { Value = v; }
    }
}
