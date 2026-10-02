using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimLuaKit
{
    // On-screen kit version + hashes (compare to a published release).
    internal static class AuthenticityWatermark
    {
        public const string KitVersion = "0.1.0";
        public const string KitBrand = "RimKit";

        private static bool computed;
        private static string hostSha256 = "?";
        private static string nativeSha256 = "?";
        private static string hostShort = "????????";
        private static string nativeShort = "????????";
        private static string proofCode = "RLK-????????";
        private static string hostPath = "";
        private static string nativePath = "";
        private static string statusLine = "computing…";

        public static bool ShowOnScreens
        {
            get
            {
                if (LuaConfigBridge.Settings?.Bools != null &&
                    LuaConfigBridge.Settings.Bools.TryGetValue("rimlua.show_watermark", out bool v))
                {
                    return v;
                }
                return true;
            }
        }

        public static string ProofCode
        {
            get
            {
                EnsureComputed();
                return proofCode;
            }
        }

        public static void EnsureComputed()
        {
            if (computed) return;
            computed = true;
            try
            {
                string assembliesDir = NativeAbi.ResolveDllDirectory();
                hostPath = Path.Combine(assembliesDir ?? "", "RimLuaHost.dll");
                string modRoot = Directory.GetParent(assembliesDir ?? "")?.FullName ?? assembliesDir;
                nativePath = Path.Combine(modRoot ?? "", "Native", "rimlua_core.dll");

                hostSha256 = HashFile(hostPath);
                nativeSha256 = HashFile(nativePath);
                hostShort = Short(hostSha256);
                nativeShort = Short(nativeSha256);
                proofCode = "RLK-" + hostShort + nativeShort;
                statusLine = File.Exists(hostPath) && File.Exists(nativePath) ? "LEGIT CHECKSUM" : "MISSING BINARY";
                Log.Message("[RimKit] Authenticity " + proofCode +
                            " host=" + hostSha256 + " native=" + nativeSha256);
            }
            catch (Exception e)
            {
                statusLine = "CHECKSUM FAIL";
                proofCode = "RLK-ERROR";
                Log.Warning("[RimKit] Authenticity watermark failed: " + e.Message);
            }
        }

        public static Rect LastDrawRect { get; private set; }

        public static void Draw(bool clickable = true)
        {
            if (!ShowOnScreens) return;
            EnsureComputed();

            try
            {
                Text.Font = GameFont.Tiny;
                Color prev = GUI.color;
                GUI.color = new Color(0.75f, 0.85f, 1f, 0.92f);

                float w = 420f;
                float h = 54f;
                float x = 12f;
                float y = UI.screenHeight - h - 10f;
                Rect r = new Rect(x, y, w, h);
                LastDrawRect = r;

                Widgets.DrawBoxSolid(r, new Color(0f, 0f, 0f, 0.45f));

                string line1 = KitBrand + " " + KitVersion + "  ·  " + statusLine;
                string line2 = "host " + hostShort + "   native " + nativeShort;
                string line3 = "code " + proofCode + (clickable ? "   (click for hub)" : "");

                Widgets.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 8f, 16f), line1);
                Widgets.Label(new Rect(r.x + 6f, r.y + 18f, r.width - 8f, 16f), line2);
                Widgets.Label(new Rect(r.x + 6f, r.y + 34f, r.width - 8f, 16f), line3);

                GUI.color = prev;
                Text.Font = GameFont.Small;

                // ButtonInvisible / TipRegion touch Mouse.IsInputBlockedNow, which NREs
                // during LongEventsOnGUI before the UI root finishes Instantiating.
                if (clickable && Find.WindowStack != null)
                {
                    if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
                    {
                        MainMenuRimKitButton.OpenHub();
                    }
                    TooltipHandler.TipRegion(r, "Open RimKit hub (settings, credits, authenticity)");
                }
            }
            catch (Exception e)
            {
                Log.WarningOnce("[RimKit] watermark draw skipped: " + e.Message, 0x52494D4B);
            }
        }

        public static void DrawSettings(Listing_Standard listing)
        {
            EnsureComputed();
            listing.Label("Authenticity");
            listing.Label("Version: " + KitVersion);
            listing.Label("Proof code: " + proofCode);
            listing.Gap(4f);

            Text.Font = GameFont.Tiny;
            listing.Label("Host SHA256");
            listing.Label(hostSha256);
            listing.Gap(2f);
            listing.Label("Native SHA256");
            listing.Label(nativeSha256);
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            bool show = ShowOnScreens;
            listing.CheckboxLabeled("Show load/menu watermark", ref show);
            if (LuaConfigBridge.Settings != null)
            {
                bool prev = ShowOnScreens;
                LuaConfigBridge.Settings.Bools["rimlua.show_watermark"] = show;
                if (prev != show)
                {
                    LuaConfigBridge.Settings.Write();
                }
            }
            if (listing.ButtonText("Copy proof code"))
            {
                GUIUtility.systemCopyBuffer = proofCode + "\nhost=" + hostSha256 + "\nnative=" + nativeSha256;
                Messages.Message("[RimKit] proof copied to clipboard", MessageTypeDefOf.TaskCompletion, false);
            }
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
            if (string.IsNullOrEmpty(sha) || sha == "missing" || sha.Length < 8) return "????????";
            return sha.Substring(0, 8).ToUpperInvariant();
        }
    }

    [HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
    internal static class Patch_LongEvent_Watermark
    {
        public static void Postfix()
        {
            // No click handling during long events.
            AuthenticityWatermark.Draw(clickable: false);
        }
    }

    [HarmonyPatch(typeof(UIRoot_Entry), nameof(UIRoot_Entry.UIRootOnGUI))]
    internal static class Patch_UIRootEntry_Watermark
    {
        public static void Postfix()
        {
            AuthenticityWatermark.Draw(clickable: true);
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
    internal static class Patch_MainMenu_Watermark
    {
        public static void Postfix()
        {
            AuthenticityWatermark.Draw(clickable: true);
        }
    }
}
