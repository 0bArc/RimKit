using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Verse;

namespace RimKit
{
    // Scan over each mod's Lua/. Hits quarantine that package id.
    // Comments are removed first. Rules that look for a call or a library name run on a view with string
    // literals blanked, so a word inside a message or a table key is not a hit. Member names do not count
    // either: obj.load(...), obj:load(...) and "function load(" are not calls to the global load.
    // Rules that inspect string contents (for example a type name passed to a reflection call) run on the
    // text with strings kept.
    public static class LuaThreatScanner
    {
        // block: the mod is quarantined. These reach the operating system or the CLR in ways the Lua sandbox cannot see.
        // warn: the sandbox already removes the function (load, dofile, debug, io and os are nil, _G is frozen), so using it only
        // fails at run time. They are reported as notes and the mod still loads. A rule is global-only: "x.os.y" and "x.debug.y" are
        // member names, not the global libraries.
        private static readonly (string id, Regex rx, bool inStrings, bool block)[] Rules =
        {
            ("os.execute", new Regex(@"(?<![.:\w])os\s*\.\s*execute\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, true),
            ("os library", new Regex(@"(?<![.:\w])os\s*\.\s*(remove|rename|execute|exit|getenv|setenv|tmpname)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, true),
            ("io library", new Regex(@"(?<![.:\w])io\s*\.\s*(open|popen|input|output|lines|read|write)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, true),
            ("require os/io/debug", new Regex(@"\brequire\s*\(\s*[""'](os|io|debug|ffi|bit|jit)[""']\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled), true, true),
            ("global dangerous member", new Regex(@"(?<![.:\w])(_G|_ENV)\s*\.\s*(os|io|package)\b", RegexOptions.Compiled), false, true),
            ("ffi", new Regex(@"(?<![.:\w])ffi\s*\.", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, true),
            ("package.loadlib", new Regex(@"(?<![.:\w])package\s*\.\s*loadlib\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, true),
            ("Process start", new Regex(@"\bSystem\.Diagnostics\.Process\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), true, true),
            ("System.IO", new Regex(@"\bSystem\.IO\.(File|Directory|Path)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), true, true),
            ("reflect static System", new Regex(@"\breflect\s*\.\s*static_call\s*\([^)]*System\.", RegexOptions.IgnoreCase | RegexOptions.Compiled), true, true),
            ("rim.cs static System", new Regex(@"\brim\s*\.\s*cs\s*\.\s*static_call\s*\([^)]*System\.", RegexOptions.IgnoreCase | RegexOptions.Compiled), true, true),
            ("Assembly.Load", new Regex(@"\bAssembly\s*\.\s*Load(From|File)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), true, true),

            ("dofile", new Regex(@"(?<![.:\w])(?<!function\s+)dofile\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
            ("loadfile", new Regex(@"(?<![.:\w])(?<!function\s+)loadfile\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
            ("load()", new Regex(@"(?<![.:\w])(?<!function\s+)load\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
            ("loadstring", new Regex(@"(?<![.:\w])(?<!function\s+)loadstring\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
            ("package.cpath", new Regex(@"(?<![.:\w])package\s*\.\s*cpath\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
            ("debug library", new Regex(@"(?<![.:\w])debug\s*\.\s*\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
            ("global table indexing", new Regex(@"(?<![.:\w])(_G|_ENV)\s*\[", RegexOptions.Compiled), false, false),
            ("global loader member", new Regex(@"(?<![.:\w])(_G|_ENV)\s*\.\s*(load|dofile|loadfile|loadstring|debug|getfenv|setfenv)\b", RegexOptions.Compiled), false, false),
            ("getfenv/setfenv", new Regex(@"(?<![.:\w])(?<!function\s+)(getfenv|setfenv)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), false, false),
        };

        private static readonly HashSet<string> Quarantined =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string LastReport { get; private set; } = "";

        public static IReadOnlyCollection<string> QuarantinedPackageIds => Quarantined;

        public static bool IsQuarantined(string packageId)
        {
            return !string.IsNullOrEmpty(packageId) && Quarantined.Contains(packageId);
        }

        public static void ScanAndQuarantine(out string report)
        {
            Quarantined.Clear();
            var lines = new List<string>();
            int files = 0;
            int cleanPacks = 0;
            int dirtyPacks = 0;

            foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
            {
                if (pack?.RootDir == null || string.IsNullOrEmpty(pack.PackageId))
                {
                    continue;
                }

                string luaDir = Path.Combine(pack.RootDir, "Lua");
                if (!Directory.Exists(luaDir))
                {
                    continue;
                }

                var packHits = new List<string>();
                var packNotes = new List<string>();
                foreach (string path in Directory.EnumerateFiles(luaDir, "*.lua", SearchOption.AllDirectories))
                {
                    files++;
                    string text;
                    try
                    {
                        text = File.ReadAllText(path);
                    }
                    catch (Exception e)
                    {
                        packHits.Add("cannot read " + path + " (" + e.Message + ")");
                        continue;
                    }

                    string rel = path;
                    if (path.StartsWith(pack.RootDir, StringComparison.OrdinalIgnoreCase))
                    {
                        rel = path.Substring(pack.RootDir.Length).TrimStart('\\', '/');
                    }

                    foreach (string hit in Analyze(text))
                    {
                        packHits.Add(rel + ":" + hit);
                    }

                    foreach (string note in Warnings(text))
                    {
                        packNotes.Add(rel + ":" + note);
                    }
                }

                if (packHits.Count == 0)
                {
                    cleanPacks++;
                    lines.Add("OK: Lua clean " + pack.PackageId);
                    if (packNotes.Count > 0)
                    {
                        lines.Add("NOTE: " + pack.PackageId + " uses " + packNotes.Count + " call(s) the sandbox removes (it still loads, they will fail when run)");
                        foreach (string note in packNotes) lines.Add("  - " + note);
                    }
                }
                else
                {
                    dirtyPacks++;
                    Quarantined.Add(pack.PackageId);
                    lines.Add("QUARANTINE: " + pack.PackageId + " (" + packHits.Count + " hit(s))");
                    foreach (string hit in packHits)
                    {
                        lines.Add("  - " + hit);
                    }
                }
            }

            if (dirtyPacks == 0)
            {
                lines.Insert(0, "OK: Lua threat scan clean (" + files + " files, " + cleanPacks + " packs)");
                LastReport = string.Join("\n", lines);
                Log.Message("[RimKit/pAuth] " + lines[0]);
            }
            else
            {
                lines.Insert(0,
                    "WARN: quarantined " + dirtyPacks + " Lua pack(s); " + cleanPacks +
                    " clean pack(s) still load (" + files + " files scanned)");
                LastReport = string.Join("\n", lines);
                Log.Warning("[RimKit/pAuth] " + LastReport);
            }

            report = LastReport;
        }

        // Kept for older call sites. True when nothing was quarantined.
        public static bool ScanAllEnabledMods(out string failure, out string report)
        {
            ScanAndQuarantine(out report);
            if (Quarantined.Count == 0)
            {
                failure = "";
                return true;
            }

            failure = "quarantined: " + string.Join(", ", Quarantined);
            return false;
        }

        // Returns one entry per blocking rule that matches: "line [rule] near: text". Empty when nothing would quarantine the mod.
        public static List<string> Analyze(string text) => Scan(text, true);

        // Returns one entry per rule the sandbox already neutralizes. A mod with only these still loads.
        public static List<string> Warnings(string text) => Scan(text, false);

        private static List<string> Scan(string text, bool block)
        {
            var hits = new List<string>();
            string stripped = StripLuaComments(text);
            string code = BlankStrings(stripped);
            foreach (var (id, rx, inStrings, isBlock) in Rules)
            {
                if (isBlock != block) continue;
                string view = inStrings ? stripped : code;
                Match m = rx.Match(view);
                if (m.Success)
                {
                    hits.Add(LineOf(view, m.Index) + " [" + id + "] near: " + Snip(view, m.Index));
                }
            }

            return hits;
        }

        private static string StripLuaComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                if (i + 2 < src.Length && src[i] == '-' && src[i + 1] == '-' && src[i + 2] == '[')
                {
                    int j = i + 3;
                    int eq = 0;
                    while (j < src.Length && src[j] == '=')
                    {
                        eq++;
                        j++;
                    }
                    if (j < src.Length && src[j] == '[')
                    {
                        j++;
                        string closer = "]" + new string('=', eq) + "]";
                        int end = src.IndexOf(closer, j, StringComparison.Ordinal);
                        i = end < 0 ? src.Length : end + closer.Length;
                        continue;
                    }
                }
                if (i + 1 < src.Length && src[i] == '-' && src[i + 1] == '-')
                {
                    while (i < src.Length && src[i] != '\n')
                    {
                        i++;
                    }
                    continue;
                }
                sb.Append(src[i++]);
            }
            return sb.ToString();
        }

        // Replaces the inside of every string literal with spaces. Newlines stay so line numbers match.
        internal static string BlankStrings(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];
                if (c == '"' || c == '\'')
                {
                    sb.Append(c);
                    i++;
                    while (i < src.Length && src[i] != c && src[i] != '\n')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length)
                        {
                            sb.Append(' ');
                            i++;
                        }
                        sb.Append(' ');
                        i++;
                    }
                    if (i < src.Length && src[i] == c)
                    {
                        sb.Append(c);
                        i++;
                    }
                    continue;
                }
                if (c == '[' && i + 1 < src.Length && (src[i + 1] == '[' || src[i + 1] == '='))
                {
                    int j = i + 1;
                    int eq = 0;
                    while (j < src.Length && src[j] == '=')
                    {
                        eq++;
                        j++;
                    }
                    if (j < src.Length && src[j] == '[')
                    {
                        string closer = "]" + new string('=', eq) + "]";
                        int end = src.IndexOf(closer, j + 1, StringComparison.Ordinal);
                        int stop = end < 0 ? src.Length : end + closer.Length;
                        for (int k = i; k < stop; k++)
                        {
                            sb.Append(src[k] == '\n' ? '\n' : ' ');
                        }
                        i = stop;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        internal static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }
            return line;
        }

        private static string Snip(string text, int index)
        {
            int start = Math.Max(0, index - 12);
            int len = Math.Min(48, text.Length - start);
            return text.Substring(start, len).Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
