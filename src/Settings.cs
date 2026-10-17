using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WindowTool
{
    /// 零依赖键值设置存储。保持与旧版 settings.txt 的键名兼容。
    sealed class Settings
    {
        readonly Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);

        public string Path;

        public static Settings Load(string path)
        {
            var s = new Settings();
            s.Path = path;
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) s.d[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
            catch { }
            return s;
        }

        public string Str(string k, string def)
        {
            string v;
            return d.TryGetValue(k, out v) && v.Length > 0 ? v : def;
        }

        public int Int(string k, int def)
        {
            string v;
            int n;
            if (d.TryGetValue(k, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return def;
        }

        public bool Bool(string k, bool def)
        {
            string v;
            if (d.TryGetValue(k, out v)) return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            return def;
        }

        public void Set(string k, string v) { d[k] = v ?? ""; }
        public void Set(string k, int v) { d[k] = v.ToString(CultureInfo.InvariantCulture); }
        public void Set(string k, bool v) { d[k] = v ? "1" : "0"; }
        public bool Has(string k) { return d.ContainsKey(k); }

        public void Remove(string k) { d.Remove(k); }

        public IEnumerable<KeyValuePair<string, string>> All { get { return d; } }

        public void Save()
        {
            if (string.IsNullOrEmpty(Path)) return;
            var sb = new StringBuilder();
            foreach (var kv in d) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            try { File.WriteAllText(Path, sb.ToString()); }
            catch (Exception ex) { Program.Log(ex); }
        }
    }
}
