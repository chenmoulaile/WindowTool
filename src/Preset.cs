using System;
using System.Collections.Generic;

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
}
