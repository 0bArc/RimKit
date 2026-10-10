using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Verse;

namespace RimKit
{
    // Binary allowlist for RimKit + Harmony. Dirty Lua packs get quarantined separately.
    public static class AuthGate
    {
        private static bool ran;
        private static bool ok;
        private static string failure = "not verified yet";
        private static string report = "";
        private static string lastProof = "";

        public static bool IsAuthorized()
        {
            Ensure(force: false);
            return ok;
        }

        public static string FailureReason()
        {
            Ensure(force: false);
            return failure;
        }

        public static string Report()
        {
            Ensure(force: false);
            return report;
        }

        public static string LastProof => lastProof;

        public static void Ensure(bool force = false)
        {
            if (ran && !force) return;
            ran = true;
            var lines = new List<string>();
            try
            {
                bool harmonyOk = VerifyHarmony(lines, out string harmonySha);
                bool rimOk = VerifyRimKit(lines, out string hostSha, out string nativeSha, out string proof);
                lastProof = proof;
                bool listOk = VerifyAllowlist(lines, harmonySha, hostSha, nativeSha);

                LuaThreatScanner.ScanAndQuarantine(out string luaReport);
                foreach (string line in luaReport.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    lines.Add(line);
                }

                ok = harmonyOk && rimOk && listOk;
                failure = ok ? "" : string.Join("; ", lines.Where(l => l.StartsWith("FAIL:")));
                report = string.Join("\n", lines);
                if (ok)
                {
                    int q = LuaThreatScanner.QuarantinedPackageIds.Count;
                    Log.Message("[RimKit/pAuth] AUTHORIZED proof=" + proof + " harmony=" + Short(harmonySha) +
                                (q > 0 ? (" quarantined=" + q) : ""));
                }
                else
                {
                    Log.Error("[RimKit/pAuth] UNAUTHORIZED: " + failure + "\n" + report);
                }
            }
            catch (Exception e)
            {
                ok = false;
                failure = "pAuth exception: " + e.Message;
                report = failure;
                Log.Error("[RimKit/pAuth] " + failure);
            }
        }

        private static bool VerifyHarmony(List<string> lines, out string sha)
        {
            sha = "missing";
            Assembly harm = typeof(Harmony).Assembly;
            if (harm == null)
            {
                lines.Add("FAIL: Harmony assembly missing");
                return false;
            }
            string name = harm.GetName().Name ?? "";
            if (!name.Equals("0Harmony", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("HarmonyLib", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("FAIL: unexpected Harmony assembly name: " + name);
                return false;
            }
            sha = HashFile(harm.Location);
            lines.Add("OK: Harmony assembly=" + name + " sha=" + Short(sha));
            if (AccessTools.TypeByName("HarmonyLib.Harmony") == null)
            {
                lines.Add("FAIL: HarmonyLib.Harmony type missing");
                return false;
            }
            lines.Add("OK: HarmonyLib.Harmony type present");
            return sha != "missing";
        }

        private static bool VerifyRimKit(List<string> lines, out string hostSha, out string nativeSha, out string proof)
        {
            hostSha = "missing";
            nativeSha = "missing";
            proof = "RLK-ERROR";
            ModContentPack pack = FindRimKitPack();
            if (pack == null)
            {
                lines.Add("FAIL: RimKit mod (stratware.rimkit) not enabled");
                return false;
            }
            RimKitProof.Ensure();
            hostSha = RimKitProof.HostSha256;
            nativeSha = RimKitProof.NativeSha256;
            proof = RimKitProof.ProofCode;
            if (hostSha == "missing" || nativeSha == "missing")
            {
                lines.Add("FAIL: RimKit binaries missing or unreadable");
                return false;
            }
            lines.Add("OK: RimKit pack=" + (pack.PackageId ?? "?") + " root=" + (pack.RootDir ?? "?"));
            lines.Add("OK: RimKit proof=" + proof);
            lines.Add("OK: host sha=" + Short(hostSha));
            lines.Add("OK: native sha=" + Short(nativeSha));
            return true;
        }

        private static bool VerifyAllowlist(List<string> lines, string harmonySha, string hostSha, string nativeSha)
        {
            string path = FindAllowlistPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                lines.Add("FAIL: Auth/allowlist.json missing under RimKit mod root");
                return false;
            }
            string json = File.ReadAllText(path);
            bool harmMatch = JsonContainsSha(json, "harmony", harmonySha);
            bool hostMatch = JsonContainsSha(json, "rimkit_host", hostSha);
            bool nativeMatch = JsonContainsSha(json, "rimkit_native", nativeSha);
            // Harmony is not ours and updates on its own schedule. An unknown build is noted and allowed, so a Harmony update does not switch
            // every Lua mod off. RimKit's own host and native hashes below stay strict.
            if (!harmMatch) lines.Add("WARN: Harmony sha not in allowlist (allowed, it is a different Harmony build)");
            else lines.Add("OK: Harmony sha allowlisted");
            if (!hostMatch) lines.Add("FAIL: RimKit host sha not in allowlist");
            else lines.Add("OK: RimKit host sha allowlisted");
            if (!nativeMatch) lines.Add("FAIL: RimKit native sha not in allowlist");
            else lines.Add("OK: RimKit native sha allowlisted");
            return hostMatch && nativeMatch;
        }

        /// <summary>
        /// Helm opens a control channel into the game, so its library is held to the same rule as RimKit's own binaries: its hash must be
        /// in Auth/allowlist.json under "helm". A swapped or modified library is refused before it is loaded.
        /// </summary>
        public static bool VerifyHelmLibrary(string path, out string why)
        {
            why = "";
            string sha = HashFile(path);
            if (sha == "missing") { why = "the Helm library is missing: " + path; return false; }
            string list = FindAllowlistPath();
            if (string.IsNullOrEmpty(list) || !File.Exists(list)) { why = "Auth/allowlist.json is missing"; return false; }
            if (!JsonContainsSha(File.ReadAllText(list), "helm", sha))
            {
                why = "the Helm library (sha " + Short(sha) + ") is not in the allowlist. Build it into the RimKit package and run rimkit update --release.";
                return false;
            }

            return true;
        }

        // Workshop builds often run as stratware.rimkit_steam. Also match PackageIdPlayerFacing
        // and fall back to the mod folder that contains this RimLuaHost.dll.
        private static ModContentPack FindRimKitPack()
        {
            const string id = "stratware.rimkit";
            foreach (ModContentPack m in LoadedModManager.RunningModsListForReading)
            {
                if (m == null) continue;
                if (string.Equals(m.PackageId, id, StringComparison.OrdinalIgnoreCase)) return m;
                if (string.Equals(m.PackageIdPlayerFacing, id, StringComparison.OrdinalIgnoreCase)) return m;
                string pid = m.PackageId ?? "";
                if (pid.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase)) return m;
            }

            string assembliesDir = NativeAbi.ResolveDllDirectory();
            string modRoot = Directory.GetParent(assembliesDir ?? "")?.FullName;
            if (string.IsNullOrEmpty(modRoot)) return null;
            foreach (ModContentPack m in LoadedModManager.RunningModsListForReading)
            {
                if (m?.RootDir == null) continue;
                if (string.Equals(Path.GetFullPath(m.RootDir), Path.GetFullPath(modRoot), StringComparison.OrdinalIgnoreCase))
                {
                    return m;
                }
            }
            return null;
        }

        private static string FindAllowlistPath()
        {
            ModContentPack pack = FindRimKitPack();
            if (pack?.RootDir == null) return null;
            return Path.Combine(pack.RootDir, "Auth", "allowlist.json");
        }

        private static bool JsonContainsSha(string json, string key, string sha)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(sha) || sha == "missing") return false;
            string needle = sha.ToLowerInvariant();
            int keyPos = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (keyPos < 0) return false;
            int end = Math.Min(json.Length, keyPos + 4000);
            return json.Substring(keyPos, end - keyPos).ToLowerInvariant().Contains(needle);
        }

        private static string HashFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "missing";
            using (var sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string Short(string sha)
        {
            if (string.IsNullOrEmpty(sha) || sha.Length < 8) return "????????";
            return sha.Substring(0, 8).ToUpperInvariant();
        }
    }
}
