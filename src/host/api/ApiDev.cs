using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // Developer and ecosystem ops (P6): Development mode, def name export for the editor, the diagnostics bundle, load order and
    // switching a mod off. Ops: dev.mode, dev.export_defs, dev.bundle, mods.order, mods.deactivate.
    internal static class ApiDev
    {
        public static void Register()
        {
            R("dev.mode", DevMode);
            R("dev.export_defs", ExportDefs);
            R("dev.bundle", Bundle);
            R("dev.unregister_mod", UnregisterMod);
            ApiRegistry.Register("dev.helm_publish", a => HelmPublish(a), "advanced", "0.11.0");
            ApiRegistry.Register("dev.helm_active", a => OkBool(HelmBridge.Active), "advanced", "0.11.0");
            ApiRegistry.Register("dev.test_env", a => TestEnv(a), "advanced", "0.11.0");
            ApiRegistry.Register("dev.write_report", a => WriteReport(a), "advanced", "0.11.0");
            ApiRegistry.Register("dev.quit", a => Quit(a), "advanced", "0.11.0");
            R("mods.order", Order);
            R("mods.deactivate", Deactivate);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "advanced", "0.10.0");

        // Called by the native layer before it reloads a mod: takes away the gizmos, alerts, tabs, columns, tools, status lines and settings pages the old version added.
        private static string UnregisterMod(Dictionary<string, string> a) => OkInt(ModOwnership.Release(Str(a, "package_id")));

        private static string DevMode(Dictionary<string, string> a) => OkBool(Prefs.DevMode);

        // Sends an event to the Helm clients that asked for it. Helm session only.
        private static string HelmPublish(Dictionary<string, string> a)
        {
            if (!HelmBridge.Active) return Fail("RK3003", "no Helm session is running");
            return OkBool(HelmBridge.Publish(Str(a, "event"), Str(a, "json")));
        }

        // The launcher (rimkit mod test --in-game) tells the game what to run through environment variables, so nothing in the
        // player's settings or mod list changes: RIMKIT_TEST_MODS (comma separated package ids), RIMKIT_TEST_REPORT, RIMKIT_TEST_FILTER, RIMKIT_TEST_QUIT=1.
        private static string TestEnv(Dictionary<string, string> a)
        {
            string mods = Environment.GetEnvironmentVariable("RIMKIT_TEST_MODS");
            if (string.IsNullOrWhiteSpace(mods)) return OkJson("null");
            var list = Jb.Arr();
            foreach (string id in mods.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0)) list.AddS(id);
            string report = Environment.GetEnvironmentVariable("RIMKIT_TEST_REPORT");
            string filter = Environment.GetEnvironmentVariable("RIMKIT_TEST_FILTER");
            var obj = Jb.Obj().Raw("mods", list.ToString()).B("quit", Environment.GetEnvironmentVariable("RIMKIT_TEST_QUIT") == "1");
            if (!string.IsNullOrEmpty(report)) obj.S("report", report);
            if (!string.IsNullOrEmpty(filter)) obj.S("filter", filter);
            string source = Environment.GetEnvironmentVariable("RIMKIT_TEST_SOURCE");
            if (!string.IsNullOrEmpty(source)) obj.S("source", source);
            return obj.Ok();
        }

        private static string TestFolder() => Path.GetFullPath(Path.Combine(GenFilePaths.ConfigFolderPath, "..", "RimKitTests"));

        // Test reports are the only thing a script may write through this op, and only into the RimKitTests folder or to the one file the launcher named.
        private static string WriteReport(Dictionary<string, string> a)
        {
            string path = Str(a, "path");
            if (string.IsNullOrEmpty(path)) return Fail("RK1001", "path is required");
            string allowed = Environment.GetEnvironmentVariable("RIMKIT_TEST_REPORT");
            string full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(TestFolder(), path));
            bool launcherFile = !string.IsNullOrEmpty(allowed) && string.Equals(full, Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase);
            bool inFolder = full.StartsWith(TestFolder() + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!launcherFile && !inFolder) return Fail("RK4001", "reports can only be written inside " + TestFolder());
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, Str(a, "text") ?? "", new UTF8Encoding(false));
            }
            catch (Exception e) { return Fail("RK5001", "could not write " + full + ": " + e.Message); }
            return OkStr(full);
        }

        private static string Quit(Dictionary<string, string> a)
        {
            if (Environment.GetEnvironmentVariable("RIMKIT_TEST_QUIT") != "1") return Fail("RK4001", "quit only works when rimkit mod test --in-game started the game");
            int code = a.ContainsKey("code") ? Int(a, "code") : 0;
            Log.Message("[RimKit] closing the game after the test run, exit code " + code);
            Application.Quit(code);
            return OkBool(true);
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append(' ');
                else sb.Append(c);
            }

            return sb.Append('"').ToString();
        }

        // Every def name by def type, for the editor's name completion. Written next to the game's config so the extension finds it.
        private static string ExportDefs(Dictionary<string, string> a)
        {
            string path = Str(a, "path");
            if (string.IsNullOrEmpty(path)) path = Path.Combine(GenFilePaths.ConfigFolderPath, "rimkit_defs.json");
            var sb = new StringBuilder("{\"game\":" + Quote(VersionControl.CurrentVersionString) + ",\"kinds\":{");
            bool firstKind = true;
            int total = 0;
            foreach (Type t in GenDefDatabase.AllDefTypesWithDatabases().OrderBy(x => x.Name))
            {
                var db = typeof(DefDatabase<>).MakeGenericType(t);
                var list = AccessTools.Property(db, "AllDefsListForReading")?.GetValue(null, null) as IEnumerable;
                if (list == null) continue;
                var names = list.Cast<Def>().Select(d => d.defName).Where(n => !string.IsNullOrEmpty(n)).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (names.Count == 0) continue;
                if (!firstKind) sb.Append(',');
                firstKind = false;
                sb.Append(Quote(t.Name)).Append(":[").Append(string.Join(",", names.Select(Quote))).Append(']');
                total += names.Count;
            }

            sb.Append("}}");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { return Fail("RK5001", "could not write " + path + ": " + e.Message); }
            return OkStr(path + " (" + total + " defs)");
        }

        // A folder with what a bug report needs and nothing private: the log, the mod list, versions and the profiler numbers.
        // Nothing is sent anywhere. rimkit diag zips the newest folder.
        private static string Bundle(Dictionary<string, string> a)
        {
            string root = Str(a, "path");
            if (string.IsNullOrEmpty(root)) root = Path.Combine(GenFilePaths.ConfigFolderPath, "..", "RimKitDiagnostics");
            string dir = Path.GetFullPath(Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
            try
            {
                Directory.CreateDirectory(dir);
                var rep = new StringBuilder();
                rep.AppendLine("RimKit diagnostics, " + DateTime.Now.ToString("u"));
                rep.AppendLine("RimWorld: " + VersionControl.CurrentVersionStringWithRev);
                rep.AppendLine("Platform: " + Application.platform + ", " + SystemInfo.operatingSystem);
                rep.AppendLine("DLC: Royalty=" + ModsConfig.RoyaltyActive + " Ideology=" + ModsConfig.IdeologyActive + " Biotech=" + ModsConfig.BiotechActive + " Anomaly=" + ModsConfig.AnomalyActive + " Odyssey=" + ModsConfig.OdysseyActive);
                rep.AppendLine("Safe mode: " + SafeMode.IsActive(out string safeReason) + (safeReason != null ? " (" + safeReason + ")" : ""));
                rep.AppendLine();
                rep.AppendLine("Active mods, in load order:");
                int i = 1;
                foreach (ModContentPack m in LoadedModManager.RunningModsListForReading)
                {
                    string manifest = Path.Combine(m.RootDir ?? "", "About", "RimKit.json");
                    rep.AppendLine("  " + (i++) + ". " + m.PackageId + "  " + m.Name + (File.Exists(manifest) ? "  [RimKit manifest]" : "") + (Directory.Exists(Path.Combine(m.RootDir ?? "", "Lua")) ? "  [Lua]" : ""));
                }

                File.WriteAllText(Path.Combine(dir, "report.txt"), rep.ToString(), new UTF8Encoding(false));
                if (a.TryGetValue("profile", out string profile) && !string.IsNullOrEmpty(profile)) File.WriteAllText(Path.Combine(dir, "profiler.json"), profile, new UTF8Encoding(false));
                CopyShared(Application.consoleLogPath, Path.Combine(dir, "Player.log"));
                CopyShared(Path.Combine(GenFilePaths.ConfigFolderPath, "ModsConfig.xml"), Path.Combine(dir, "ModsConfig.xml"));
            }
            catch (Exception e) { return Fail("RK5001", "could not write the bundle: " + e.Message); }
            return OkStr(dir);
        }

        // The log is open in the game, so read it with sharing.
        private static void CopyShared(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || !File.Exists(from)) return;
            using (var src = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var dst = new FileStream(to, FileMode.Create, FileAccess.Write))
                src.CopyTo(dst);
        }

        private static string Order(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ModContentPack m in LoadedModManager.RunningModsListForReading) arr.AddS(m.PackageId);
            return arr.Ok();
        }

        // Turns a mod off in the mod list. It stays off after a restart. The player is told to restart.
        private static string Deactivate(Dictionary<string, string> a)
        {
            string id = Str(a, "package_id");
            if (string.Equals(id, "stratware.rimkit", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "brrainz.harmony", StringComparison.OrdinalIgnoreCase) || (id ?? "").StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase))
                return Fail("RK1001", id + " cannot be switched off from here");
            ModMetaData meta = ModLister.GetActiveModWithIdentifier(id, true);
            if (meta == null) return Fail("RK3001", "no active mod " + id);
            ModsConfig.SetActive(meta.PackageId, false);
            ModsConfig.Save();
            return OkBool(true);
        }
    }
}
