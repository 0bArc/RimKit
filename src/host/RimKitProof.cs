using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace RimLuaKit
{
    // Host/native SHA + short proof code.
    public static class RimKitProof
    {
        private static bool ready;
        private static string hostSha = "missing";
        private static string nativeSha = "missing";
        private static string proof = "RLK-ERROR";

        public static string KitVersion => AuthenticityWatermark.KitVersion;
        public static string HostSha256
        {
            get { Ensure(); return hostSha; }
        }
        public static string NativeSha256
        {
            get { Ensure(); return nativeSha; }
        }
        public static string ProofCode
        {
            get { Ensure(); return proof; }
        }

        public static void Ensure()
        {
            if (ready) return;
            ready = true;
            try
            {
                string assembliesDir = NativeAbi.ResolveDllDirectory();
                string hostPath = Path.Combine(assembliesDir ?? "", "RimLuaHost.dll");
                string modRoot = Directory.GetParent(assembliesDir ?? "")?.FullName ?? assembliesDir;
                string nativePath = Path.Combine(modRoot ?? "", "Native", "rimlua_core.dll");
                hostSha = HashFile(hostPath);
                nativeSha = HashFile(nativePath);
                proof = "RLK-" + Short(hostSha) + Short(nativeSha);
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] RimKitProof failed: " + e.Message);
                hostSha = "missing";
                nativeSha = "missing";
                proof = "RLK-ERROR";
            }
        }

        internal static string HashFile(string path)
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

        internal static string Short(string sha)
        {
            if (string.IsNullOrEmpty(sha) || sha == "missing" || sha.Length < 8) return "????????";
            return sha.Substring(0, 8).ToUpperInvariant();
        }
    }

    internal static class PAuthProbe
    {
        public static bool AllowLuaLoad(out string reason)
        {
            reason = null;
            try
            {
                AuthGate.Ensure(force: true);
                if (AuthGate.IsAuthorized()) return true;
                reason = AuthGate.FailureReason();
                if (string.IsNullOrEmpty(reason)) reason = "RimKit/pAuth rejected this session";
                return false;
            }
            catch (Exception e)
            {
                reason = "pAuth probe error: " + e.Message;
                return false;
            }
        }
    }
}
