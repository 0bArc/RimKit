using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimKit
{
    internal sealed class Dialog_RimLuaHub : Window
    {
        private enum Tab
        {
            About,
            Authenticity,
            Settings,
            Mods,
            Credits
        }

        private Tab tab = Tab.About;
        private Vector2 scroll;
        private static readonly Tab[] Tabs = { Tab.About, Tab.Authenticity, Tab.Settings, Tab.Mods, Tab.Credits };

        public Dialog_RimLuaHub()
        {
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            draggable = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                AuthenticityWatermark.KitBrand + "  " + AuthenticityWatermark.KitVersion);
            Text.Font = GameFont.Small;

            float tabY = inRect.y + 36f;
            float tabH = 32f;
            float tabW = inRect.width / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                Tab t = Tabs[i];
                Rect tr = new Rect(inRect.x + i * tabW, tabY, tabW - 2f, tabH);
                if (Widgets.ButtonText(tr, t.ToString()))
                {
                    tab = t;
                    scroll = Vector2.zero;
                }
                if (tab == t)
                {
                    Widgets.DrawBoxSolid(new Rect(tr.x, tr.yMax - 3f, tr.width, 3f), new Color(0.85f, 0.7f, 0.35f));
                }
            }

            Rect body = new Rect(inRect.x, tabY + tabH + 8f, inRect.width, inRect.height - (tabY + tabH + 8f) - 40f);
            Widgets.DrawMenuSection(body);
            Rect inner = body.ContractedBy(10f);

            switch (tab)
            {
                case Tab.About:
                    DrawAbout(inner);
                    break;
                case Tab.Authenticity:
                    DrawAuthenticity(inner);
                    break;
                case Tab.Settings:
                    DrawSettingsTab(inner);
                    break;
                case Tab.Mods:
                    DrawMods(inner);
                    break;
                case Tab.Credits:
                    DrawCredits(inner);
                    break;
            }
        }

        // Lua mods and the permissions they declared in meta.capabilities (written to About/RimKit.json by rimkit mod sync).
        private void DrawMods(Rect rect)
        {
            var rows = new List<string[]>();
            foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
            {
                if (pack?.RootDir == null || !System.IO.Directory.Exists(System.IO.Path.Combine(pack.RootDir, "Lua"))) continue;
                string perms = "not declared: everything is allowed";
                string version = "";
                string manifest = System.IO.Path.Combine(pack.RootDir, "About", "RimKit.json");
                if (System.IO.File.Exists(manifest) && Json.TryParse(System.IO.File.ReadAllText(manifest), out object parsed) && parsed is Dictionary<string, object> d)
                {
                    version = Json.GetString(d, "version", "");
                    List<string> caps = Json.GetStringList(d, "capabilities");
                    if (d.ContainsKey("capabilities")) perms = caps.Count == 0 ? "none (no reflection, hooks or file access)" : string.Join(", ", caps);
                }

                rows.Add(new[] { pack.Name + (version.Length > 0 ? "  " + version : ""), perms });
            }

            var view = new Rect(0f, 0f, rect.width - 16f, rows.Count * 44f + 30f);
            Widgets.BeginScrollView(rect, ref scroll, view);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0f, 0f, view.width, 24f), "Permissions each Lua mod declared. Reflection, hooks and file writing need a declared permission.");
            Text.Font = GameFont.Small;
            float y = 28f;
            foreach (string[] r in rows)
            {
                Widgets.Label(new Rect(0f, y, view.width, 22f), r[0]);
                GUI.color = r[1].StartsWith("not declared") ? new Color(0.85f, 0.7f, 0.35f) : Color.gray;
                Widgets.Label(new Rect(14f, y + 20f, view.width - 14f, 22f), r[1]);
                GUI.color = Color.white;
                y += 44f;
            }

            Widgets.EndScrollView();
        }

        private void DrawAbout(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(rect);
            list.Label("Lua-first RimWorld modding kit.");
            list.Label("Thin C# host + sandboxed C++ Lua runtime.");
            list.GapLine();
            list.Label("Package: stratware.rimkit");
            list.Label("Author: Team Stratware.win");
            list.Label("Requires: Harmony");
            list.GapLine();
            list.Label("Enable Lua mods that ship a Lua/ folder. Load RimKit before them.");
            list.Gap();
            if (list.ButtonText("Open Mods config"))
            {
                Find.WindowStack.Add(new Page_ModsConfig());
            }
            if (list.ButtonText("Open RimKit mod folder"))
            {
                TryOpenModFolder();
            }
            list.End();
        }

        private void DrawAuthenticity(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(rect);
            AuthenticityWatermark.DrawSettings(list);
            list.GapLine();
            AuthGate.Ensure(force: false);
            list.Label(AuthGate.IsAuthorized() ? "pAuth gate: AUTHORIZED" : "pAuth gate: BLOCKED");
            if (!AuthGate.IsAuthorized())
            {
                list.Label(AuthGate.FailureReason());
            }
            int q = LuaThreatScanner.QuarantinedPackageIds.Count;
            if (q > 0)
            {
                list.Label("Lua quarantine: " + q + " pack(s) skipped (others still load).");
                foreach (string id in LuaThreatScanner.QuarantinedPackageIds)
                {
                    list.Label("  - " + id);
                }
            }
            else
            {
                list.Label("Lua quarantine: none");
            }
            list.Gap(4f);
            Text.Font = GameFont.Tiny;
            list.Label("Allowlist + quarantine report:");
            list.Label(string.IsNullOrEmpty(AuthGate.Report()) ? "(no report yet)" : AuthGate.Report());
            Text.Font = GameFont.Small;
            list.GapLine();
            list.Label("Binary allowlist must match (Harmony + host + native).");
            list.Label("Hostile Lua packs are quarantined only; clean packs always load.");
            list.Label("Other mods Assemblies/*.dll remain full process trust (RimWorld model).");
            list.End();
        }

        private void DrawSettingsTab(Rect rect)
        {
            LuaConfigBridge.DrawSettings(rect);
        }

        private void DrawCredits(Rect rect)
        {
            string body =
                "RimKit\n" +
                "Team Stratware.win\n\n" +
                "Built on:\n" +
                "  - RimWorld (Ludeon Studios)\n" +
                "  - Harmony (pardeike)\n" +
                "  - Lua 5.4\n" +
                "  - sol2\n\n" +
                "Sandbox: Lua cannot reach os/io/process APIs.\n" +
                "Builtin pAuth: binary allowlist + per-mod Lua quarantine + sandboxed VM.\n" +
                "C# Assemblies/*.dll mods remain full process trust.";

            Widgets.Label(rect, body);
        }

        private static void TryOpenModFolder()
        {
            try
            {
                string assembliesDir = NativeAbi.ResolveDllDirectory();
                string modRoot = Directory.GetParent(assembliesDir)?.FullName ?? assembliesDir;
                Application.OpenURL(new Uri(modRoot).AbsoluteUri);
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] open mod folder failed: " + e.Message);
                Messages.Message("[RimKit] could not open mod folder", MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    internal static class MainMenuRimKitButton
    {
        public static void OpenHub()
        {
            if (Find.WindowStack == null) return;
            foreach (Window w in Find.WindowStack.Windows)
            {
                if (w is Dialog_RimLuaHub) return;
            }
            Find.WindowStack.Add(new Dialog_RimLuaHub());
        }

        public static void DrawButton(Rect menuControlsRect)
        {
            float w = Mathf.Clamp(menuControlsRect.width, 160f, 220f);
            float h = 40f;
            float x = menuControlsRect.x + (menuControlsRect.width - w) * 0.5f;
            float leftSlot = menuControlsRect.x - w - 18f;
            if (leftSlot >= 12f)
            {
                x = leftSlot;
            }
            float y = menuControlsRect.y + (menuControlsRect.height - h) * 0.5f;
            Rect btn = new Rect(x, y, w, h);

            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(btn, "RimKit"))
            {
                OpenHub();
            }
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoMainMenuControls))]
    internal static class Patch_MainMenu_RimKitButton
    {
        public static void Postfix(Rect rect, bool anyMapFiles)
        {
            MainMenuRimKitButton.DrawButton(rect);
        }
    }

    [HarmonyPatch(typeof(Dialog_Options), "DoModOptions")]
    internal static class Patch_DialogOptions_DoModOptions
    {
        public static bool Prefix(Listing_Standard listing)
        {
            listing.Gap(24f);
            Rect row = listing.GetRect(42f);
            float w = 240f;
            Rect btn = new Rect(row.x + (row.width - w) * 0.5f, row.y, w, row.height);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(btn, "RimKit"))
            {
                MainMenuRimKitButton.OpenHub();
            }
            listing.Gap(10f);
            Rect hint = listing.GetRect(48f);
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(hint, "Settings · Authenticity · Credits");
            Text.Anchor = TextAnchor.UpperLeft;
            return false;
        }
    }
}
