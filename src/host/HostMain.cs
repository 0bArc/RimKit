using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimLuaKit
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
                harmony.PatchAll();

                BindCallbacksAndInit();
                // Wait until play + auth.
                BrandOptionsCategory();
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

        private static void BrandOptionsCategory()
        {
            try
            {
                OptionCategoryDef mods = DefDatabase<OptionCategoryDef>.GetNamedSilentFail("Mods");
                if (mods != null)
                {
                    mods.label = "RimKit";
                }
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] could not brand Options category: " + e.Message);
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
            NativeAbi.KeepLog = msg => Log.Message("[RimLua] " + NativeAbi.ReadCString(msg));
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
                        Log.Message("[RimLua][msg] " + text);
                    }
                }
                catch (Exception e)
                {
                    Log.Message("[RimLua][msg] " + text + " (fallback: " + e.GetType().Name + ")");
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

            int rc = NativeAbi.rimlua_init(ref cb);
            if (rc != 0)
            {
                throw new InvalidOperationException("rimlua_init returned " + rc);
            }
        }

        internal static void TryLoadLuaModsGated()
        {
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

                string luaDir = Path.Combine(pack.RootDir, "Lua");
                if (!Directory.Exists(luaDir))
                {
                    continue;
                }

                if (LuaThreatScanner.IsQuarantined(pack.PackageId))
                {
                    skipped++;
                    Log.Warning("[RimKit] Quarantined Lua skipped: " + pack.PackageId + " -> " + luaDir);
                    continue;
                }

                Log.Message("[RimKit] Loading Lua from " + pack.PackageId + " -> " + luaDir);
                NativeAbi.rimlua_load_directory(luaDir);
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
            LuaConfigBridge.DrawSettings(rest);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimKit";
        }
    }
}
