using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowTool
{
    sealed class Track
    {
        public string Title = "";
        public string Artist = "";
        public string Source = "本地";   // 本地 / 网易
        public string LocalPath = "";
        public string NetId = "";

        public override string ToString()
        {
            string who = string.IsNullOrEmpty(Artist) ? Title : Artist + " - " + Title;
            return (Source == "网易" ? "[网] " : "") + who;
        }
    }

    static class Lrc
    {
        // 解析 LRC 文本 → (时间, 歌词) 升序列表
        public static List<KeyValuePair<TimeSpan, string>> Parse(string lrc)
        {
            var list = new List<KeyValuePair<TimeSpan, string>>();
            if (string.IsNullOrEmpty(lrc)) return list;
            try
            {
                var re = new Regex(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]");
                foreach (var raw in lrc.Replace("\r", "").Split('\n'))
                {
                    var ms = re.Matches(raw);
                    if (ms.Count == 0) continue;
                    string text = re.Replace(raw, "").Trim();
                    if (text.Length == 0) continue;
                    if (text.StartsWith("<") || text.StartsWith("by:") || text.StartsWith("ti:")) continue;
                    foreach (Match m in ms)
                    {
                        int min = int.Parse(m.Groups[1].Value);
                        int sec = int.Parse(m.Groups[2].Value);
                        int frac = 0;
                        if (m.Groups[3].Success)
                        {
                            string f = m.Groups[3].Value;
                            if (f.Length == 1) frac = int.Parse(f) * 100;
                            else if (f.Length == 2) frac = int.Parse(f) * 10;
                            else frac = int.Parse(f.Substring(0, 3));
                        }
                        list.Add(new KeyValuePair<TimeSpan, string>(
                            new TimeSpan(0, min, sec) + TimeSpan.FromMilliseconds(frac), text));
                    }
                }
                list.Sort((a, b) => a.Key.CompareTo(b.Key));
            }
            catch { }
            return list;
        }

        // 当前播放位置对应的歌词行下标（-1 = 还没唱到）
        public static int IndexAt(List<KeyValuePair<TimeSpan, string>> lines, TimeSpan pos)
        {
            int lo = 0, hi = lines.Count - 1, ans = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (lines[mid].Key <= pos) { ans = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ans;
        }
    }

    /// 网易云音乐官方 Web 接口（搜索/试听链接/歌词），VIP 或无版权歌曲会返回空链接
    static class NetEaseApi
    {
        static string Get(string url)
        {
            using (var wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                wc.Headers.Add("Referer", "https://music.163.com");
                return wc.DownloadString(url);
            }
        }

        static string Un(string s)
        {
            try { return Regex.Unescape(s ?? ""); }
            catch { return s ?? ""; }
        }

        // 搜索歌曲；返回 Track 列表（Source=网易）
        public static List<Track> Search(string keyword, int limit)
        {
            var list = new List<Track>();
            if (string.IsNullOrWhiteSpace(keyword)) return list;
            string json = Get("https://music.163.com/api/search/get/web?s=" +
                Uri.EscapeDataString(keyword) + "&type=1&offset=0&limit=" + limit);
            var re = new Regex(@"\{""id"":(\d+),""name"":""((?:[^""\\]|\\.)*)"",""artists"":\[\{""id"":\d+,""name"":""((?:[^""\\]|\\.)*)""");
            foreach (Match m in re.Matches(json))
            {
                var t = new Track
                {
                    Source = "网易",
                    NetId = m.Groups[1].Value,
                    Title = Un(m.Groups[2].Value),
                    Artist = Un(m.Groups[3].Value)
                };
                if (t.Title.Length > 0) list.Add(t);
            }
            return list;
        }

        // 试听直链；br = 128000/192000/320000/999000；VIP/无版权返回 null
        public static string SongUrl(string id, int br)
        {
            string json = Get("https://music.163.com/api/song/enhance/player/url?id=" + id + "&br=" + br);
            var m = Regex.Match(json, @"""url""\s*:\s*""([^""]+)""");
            return m.Success ? Un(m.Groups[1].Value) : null;
        }

        public static string Lyric(string id)
        {
            try
            {
                string json = Get("https://music.163.com/api/song/lyric?id=" + id + "&lv=1&kv=1&tv=-1");
                var m = Regex.Match(json, @"""lyric""\s*:\s*""((?:[^""\\]|\\.)*)""");
                return m.Success ? Un(m.Groups[1].Value) : null;
            }
            catch { return null; }
        }
    }

    /// 本地歌库扫描：网易云/酷狗等客户端默认下载目录 + 系统音乐库
    static class LocalLibrary
    {
        static readonly string[] Exts = { ".mp3", ".flac", ".m4a", ".wav", ".wma", ".ogg", ".ape" };

        public static string LyricPathFor(string audioPath)
        {
            string basep = Path.Combine(
                Path.GetDirectoryName(audioPath),
                Path.GetFileNameWithoutExtension(audioPath));
            string lrc = basep + ".lrc";
            return File.Exists(lrc) ? lrc : null;
        }

        static void AddDir(List<Track> list, string dir, bool recursive, HashSet<string> seen)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                var opt = recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;
                foreach (var f in Directory.GetFiles(dir, "*.*", opt))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    bool ok = false;
                    foreach (var e in Exts) if (e == ext) { ok = true; break; }
                    if (!ok) continue;
                    if (!seen.Add(f.ToLowerInvariant())) continue;

                    string name = Path.GetFileNameWithoutExtension(f);
                    string artist = "", title = name;
                    int dash = name.IndexOf(" - ", StringComparison.Ordinal);
                    if (dash > 0)
                    {
                        artist = name.Substring(0, dash).Trim();
                        title = name.Substring(dash + 3).Trim();
                    }
                    list.Add(new Track
                    {
                        Source = "本地",
                        LocalPath = f,
                        Title = title.Length > 0 ? title : name,
                        Artist = artist
                    });
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public static List<Track> Scan()
        {
            var list = new List<Track>();
            var seen = new HashSet<string>();
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

            AddDir(list, Path.Combine(home, "CloudMusic"), false, seen);          // 网易云默认下载
            AddDir(list, Path.Combine(home, "KuGou", "KugouMusic"), false, seen); // 酷狗默认下载
            AddDir(list, Path.Combine(home, "KuGou"), false, seen);
            AddDir(list, music, true, seen);                                      // 系统音乐库（含 QQ音乐等子目录）
            return list;
        }
    }

    /// 播放器：WPF MediaPlayer 封装；播完检测由外部定时器轮询驱动
    sealed class MusicPlayer
    {
        readonly System.Windows.Media.MediaPlayer mp;
        long openedAt;
        bool waitUrl;

        public MusicPlayer()
        {
            mp = new System.Windows.Media.MediaPlayer();
            mp.Volume = 0.8;
            openedAt = DateTime.Now.Ticks;
        }

        public void OpenFile(string path)
        {
            waitUrl = false;
            openedAt = DateTime.Now.Ticks;
            mp.Open(new Uri(path, UriKind.Absolute));
            mp.Play();
        }

        public void OpenUrl(string url)
        {
            waitUrl = false;
            openedAt = DateTime.Now.Ticks;
            mp.Open(new Uri(url, UriKind.Absolute));
            mp.Play();
        }

        public void BeginWaitUrl() { waitUrl = true; openedAt = DateTime.Now.Ticks; }
        public bool WaitTimedOut { get { return waitUrl && (DateTime.Now.Ticks - openedAt) > TimeSpan.FromSeconds(8).Ticks; } }

        public void Play() { mp.Play(); }
        public void Pause() { mp.Pause(); }
        public void Stop() { try { mp.Stop(); } catch { } }
        public void Close() { try { mp.Close(); } catch { } }

        public bool CanPlay()
        {
            try { return mp.NaturalDuration.HasTimeSpan && mp.NaturalDuration.TimeSpan > TimeSpan.Zero; }
            catch { return false; }
        }

        public TimeSpan Position
        {
            get { try { return mp.Position; } catch { return TimeSpan.Zero; } }
            set { try { mp.Position = value; } catch { } }
        }

        public TimeSpan Duration
        {
            get
            {
                try { return mp.NaturalDuration.HasTimeSpan ? mp.NaturalDuration.TimeSpan : TimeSpan.Zero; }
                catch { return TimeSpan.Zero; }
            }
        }

        public double Volume   // 0..100
        {
            get { try { return mp.Volume * 100; } catch { return 80; } }
            set { try { mp.Volume = Math.Max(0, Math.Min(100, value)) / 100.0; } catch { } }
        }
    }
}
