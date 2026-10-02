using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Verse;

namespace RimLuaKit
{
    // Pattern scan over each mod's Lua/. Hits quarantine that package id.
    public static class LuaThreatScanner
    {
        private static readonly (string id, Regex rx)[] Rules =
        {
            ("os.execute", new Regex(@"\bos\s*\.\s*execute\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("os library", new Regex(@"\bos\s*\.\s*(remove|rename|execute|exit|getenv|setenv|tmpname)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("io library", new Regex(@"\bio\s*\.\s*(open|popen|input|output|lines|read|write)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("dofile", new Regex(@"\bdofile\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("loadfile", new Regex(@"\bloadfile\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("load()", new Regex(@"\bload\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("loadstring", new Regex(@"\bloadstring\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("package.loadlib", new Regex(@"\bpackage\s*\.\s*loadlib\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("package.cpath", new Regex(@"\bpackage\s*\.\s*cpath\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("require os/io/debug", new Regex(@"\brequire\s*\(\s*[""'](os|io|debug|ffi|bit|jit)[""']\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("debug library", new Regex(@"\bdebug\s*\.\s*\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("ffi", new Regex(@"\bffi\s*\.", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("getfenv/setfenv", new Regex(@"\b(getfenv|setfenv)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Process start", new Regex(@"\bSystem\.Diagnostics\.Process\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("System.IO", new Regex(@"\bSystem\.IO\.(File|Directory|Path)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("reflect static System", new Regex(@"\breflect\s*\.\s*static_call\s*\([^)]*System\.", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("rim.cs static System", new Regex(@"\brim\s*\.\s*cs\s*\.\s*static_call\s*\([^)]*System\.", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Assembly.Load", new Regex(@"\bAssembly\s*\.\s*Load(From|File)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
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

                    string stripped = StripLuaComments(text);
                    foreach (var (id, rx) in Rules)
                    {
                        Match m = rx.Match(stripped);
                        if (!m.Success)
                        {
                            continue;
                        }

                        string rel = path;
                        if (path.StartsWith(pack.RootDir, StringComparison.OrdinalIgnoreCase))
                        {
                            rel = path.Substring(pack.RootDir.Length).TrimStart('\\', '/');
                        }

                        packHits.Add(rel + " [" + id + "] near: " + Snip(stripped, m.Index));
                    }
                }

                if (packHits.Count == 0)
                {
                    cleanPacks++;
                    lines.Add("OK: Lua clean " + pack.PackageId);
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

        private static string Snip(string text, int index)
        {
            int start = Math.Max(0, index - 12);
            int len = Math.Min(48, text.Length - start);
            return text.Substring(start, len).Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
