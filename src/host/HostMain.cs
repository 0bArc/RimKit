using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimLuaKit;
using Verse;

namespace RimKit
{
    [StaticConstructorOnStartup]
    public static class HostMain
    {
        private const string HarmonyId = "stratware.rimkit";

        static HostMain()
        {
            try
            {
                EnsureNativePath();
                var harmony = new Harmony(HarmonyId);
                HarmonyBridge.Init(harmony);
                EventCatalog.Init(harmony);
                harmony.PatchAll();

                BindCallbacksAndInit();
                // Wait until play + auth.
                RunStartupAuthCheck();
                Log.Message("[RimKit] Host assembled. Waiting for play + builtin pAuth gate.");
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] Failed to start: " + e);
            }
        }

        private static void RunStartupAuthCheck()
        {
            try
            {
                AuthGate.Ensure(force: true);
                if (AuthGate.IsAuthorized())
                {
                    Log.Message("[RimKit] pAuth gate OPEN (Lua may load on play).");
                }
                else
                {
                    Log.Error("[RimKit] pAuth gate CLOSED. Lua will not load.\n" + AuthGate.FailureReason() +
                              "\nFull report:\n" + AuthGate.Report());
                }
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] startup pAuth check failed: " + e.Message);
            }
        }

        private static void EnsureNativePath()
        {
            string assembliesDir = NativeAbi.ResolveDllDirectory();
            if (string.IsNullOrEmpty(assembliesDir))
            {
                throw new InvalidOperationException("Cannot resolve Assemblies directory");
            }

            // Native DLL lives under Native/, not Assemblies/.
            string modRoot = Directory.GetParent(assembliesDir)?.FullName;
            string nativeDir = Path.Combine(modRoot ?? assembliesDir, "Native");
            string nativeDll = Path.Combine(nativeDir, "rimlua_core.dll");
            if (!File.Exists(nativeDll))
            {
                throw new FileNotFoundException("Missing native runtime (keep out of Assemblies/)", nativeDll);
            }

            SetDllDirectory(nativeDir);
            IntPtr handle = LoadLibrary(nativeDll);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("LoadLibrary failed for " + nativeDll + " err=" + Marshal.GetLastWin32Error());
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (pathEnv.IndexOf(nativeDir, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Environment.SetEnvironmentVariable("PATH", nativeDir + Path.PathSeparator + pathEnv);
            }

            Log.Message("[RimKit] Native loaded from " + nativeDll);
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static void BindCallbacksAndInit()
        {
            NativeAbi.KeepLog = msg => Log.Message("[Lua] " + NativeAbi.ReadCString(msg));
            NativeAbi.KeepMessage = msg =>
            {
                string text = NativeAbi.ReadCString(msg);
                try
                {
                    // Messages.Message can NRE during early init.
                    if (Current.ProgramState == ProgramState.Playing)
                    {
                        Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
                    }
                    else
                    {
                        Log.Message("[RimKit][msg] " + text);
                    }
                }
                catch (Exception e)
                {
                    Log.Message("[RimKit][msg] " + text + " (fallback: " + e.GetType().Name + ")");
                }
            };
            NativeAbi.KeepRegister = HarmonyBridge.RegisterHook;
            NativeAbi.KeepUnregister = HarmonyBridge.UnregisterHook;
            NativeAbi.KeepInvoke = (op, args) =>
            {
                string result = GameApi.Invoke(op ?? "", args ?? "{}");
                byte[] bytes = Encoding.UTF8.GetBytes(result ?? "");
                IntPtr mem = Marshal.AllocHGlobal(bytes.Length + 1);
                Marshal.Copy(bytes, 0, mem, bytes.Length);
                Marshal.WriteByte(mem, bytes.Length, 0);
                return mem;
            };
            NativeAbi.KeepFree = p =>
            {
                if (p != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(p);
                }
            };

            var cb = new RimLuaCallbacks
            {
                log = Marshal.GetFunctionPointerForDelegate(NativeAbi.KeepLog),
                message = Marshal.GetFunctionPointerForDelegate(NativeAbi.KeepMessage),
                register_hook = Marshal.GetFunctionPointerForDelegate(NativeAbi.KeepRegister),
                unregister_hook = Marshal.GetFunctionPointerForDelegate(NativeAbi.KeepUnregister),
                host_invoke = Marshal.GetFunctionPointerForDelegate(NativeAbi.KeepInvoke),
                host_free = Marshal.GetFunctionPointerForDelegate(NativeAbi.KeepFree)
            };

            // Host and native are built from one version file. A mismatch means a stale DLL was copied.
            string nativeVersion = NativeAbi.NativeVersion();
            if (nativeVersion != RimKitVersion.Version)
            {
                Log.Error("[RimKit] version mismatch: host " + RimKitVersion.Version + " but native core " +
                          (string.IsNullOrEmpty(nativeVersion) ? "unknown" : nativeVersion) +
                          ". Reinstall RimKit so both files come from the same build.");
            }
            else
            {
                Log.Message("[RimKit] version " + RimKitVersion.Version + " (api_level " + RimKitVersion.ApiLevel + ")");
            }

            int rc = NativeAbi.rimlua_init(ref cb);
            if (rc != 0)
            {
                throw new InvalidOperationException("rimlua_init returned " + rc);
            }
        }

        internal static void TryLoadLuaModsGated()
        {
            if (SafeMode.IsActive(out string safeReason))
            {
                Log.Warning("[RimKit] SAFE MODE: no Lua will run (" + safeReason + "). Remove it to run Lua mods again.");
                try
                {
                    Messages.Message("[RimKit] Safe mode is on: Lua mods are not running.", MessageTypeDefOf.CautionInput, false);
                }
                catch (Exception)
                {
                }

                return;
            }

            if (!PAuthProbe.AllowLuaLoad(out string reason))
            {
                string shortReason = reason ?? "unauthorized";
                if (shortReason.Length > 180)
                {
                    shortReason = shortReason.Substring(0, 177) + "...";
                }
                Log.Error("[RimKit] LUA BLOCKED by builtin pAuth: " + shortReason);
                Log.Error("[RimKit] Lua mods blocked (full):\n" + (reason ?? ""));
                try
                {
                    Messages.Message("[RimKit] LUA BLOCKED: " + shortReason, MessageTypeDefOf.ThreatBig, true);
                    if (Find.LetterStack != null)
                    {
                        Find.LetterStack.ReceiveLetter(
                            "RimKit: Lua blocked",
                            "Builtin pAuth refused to load Lua mods.\n\n" +
                            (reason ?? "unauthorized") +
                            "\n\nDisable hostile Lua mods (for example Bad Probe), or fix Auth/allowlist.json after a rebuild. Then restart.",
                            LetterDefOf.ThreatBig);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning("[RimKit] could not show block UI: " + e.Message);
                }
                return;
            }

            LoadAllLuaMods();
        }

        // Lua/ folders of a mod, oldest folder first. A mod with LoadFolders.xml can keep a Lua/ in each version folder.
        private static List<string> LuaFolders(ModContentPack pack)
        {
            var dirs = new List<string>();
            try
            {
                var field = HarmonyLib.AccessTools.Field(typeof(ModContentPack), "foldersToLoadDescendingOrder");
                if (field?.GetValue(pack) is List<string> folders)
                {
                    for (int i = folders.Count - 1; i >= 0; i--)
                    {
                        string d = Path.Combine(folders[i], "Lua");
                        if (Directory.Exists(d) && !dirs.Contains(d)) dirs.Add(d);
                    }
                }
            }
            catch (Exception)
            {
            }

            string root = Path.Combine(pack.RootDir, "Lua");
            if (dirs.Count == 0 && Directory.Exists(root)) dirs.Add(root);
            return dirs;
        }

        private static void LoadAllLuaMods()
        {
            int loaded = 0;
            int skipped = 0;
            foreach (ModContentPack pack in LoadedModManager.RunningMods)
            {
                if (pack?.RootDir == null)
                {
                    continue;
                }

                List<string> luaDirs = LuaFolders(pack);
                if (luaDirs.Count == 0)
                {
                    continue;
                }

                if (LuaThreatScanner.IsQuarantined(pack.PackageId))
                {
                    skipped++;
                    Log.Warning("[RimKit] Quarantined Lua skipped: " + pack.PackageId + " -> " + luaDirs[0]);
                    continue;
                }

                NativeAbi.rimlua_set_mod_context(pack.PackageId);
                try
                {
                    foreach (string luaDir in luaDirs)
                    {
                        Log.Message("[RimKit] Loading Lua from " + pack.PackageId + " -> " + luaDir);
                        NativeAbi.rimlua_load_directory(luaDir);
                    }
                }
                finally
                {
                    NativeAbi.rimlua_set_mod_context(null);
                }

                loaded++;
            }

            if (skipped > 0)
            {
                try
                {
                    Messages.Message(
                        "[RimKit] Loaded " + loaded + " Lua pack(s); quarantined " + skipped + " (see log / Hub).",
                        MessageTypeDefOf.NeutralEvent,
                        false);
                }
                catch
                {
                }
            }
        }
    }

    public sealed class RimLuaMod : Mod
    {
        public RimLuaMod(ModContentPack content) : base(content)
        {
            LuaConfigBridge.Settings = GetSettings<RimLuaSettings>();
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            float topH = 170f;
            var listing = new Listing_Standard();
            listing.Begin(new UnityEngine.Rect(inRect.x, inRect.y, inRect.width, topH));
            AuthenticityWatermark.DrawSettings(listing);
            listing.End();

            UnityEngine.Rect rest = new UnityEngine.Rect(inRect.x, inRect.y + topH + 8f, inRect.width, inRect.height - topH - 8f);
            // Pages that Lua mods define with game.options.page appear as tabs. The general page is the one below.
            if (!OptionsPages.Draw(rest, out UnityEngine.Rect general))
            {
                LuaConfigBridge.DrawSettings(general);
            }
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimKit";
        }
    }
}
