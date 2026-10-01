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
                LoadAllLuaMods();
                // Defer on_load until play loop: Messages/UI not ready in StaticConstructorOnStartup.
                Log.Message("[RimLuaKit] Host assembled. Waiting for play to fire on_load.");
            }
            catch (Exception e)
            {
                Log.Error("[RimLuaKit] Failed to start: " + e);
            }
        }

        private static void EnsureNativePath()
        {
            string assembliesDir = NativeAbi.ResolveDllDirectory();
            if (string.IsNullOrEmpty(assembliesDir))
            {
                throw new InvalidOperationException("Cannot resolve Assemblies directory");
            }

            // RimWorld loads every Assemblies/*.dll as managed. Native core must live in Native/.
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

            Log.Message("[RimLuaKit] Native loaded from " + nativeDll);
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
                    // Messages.Message NREs during StaticConstructorOnStartup / early init.
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

        private static void LoadAllLuaMods()
        {
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

                Log.Message("[RimLuaKit] Loading Lua from " + pack.PackageId + " -> " + luaDir);
                NativeAbi.rimlua_load_directory(luaDir);
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
            LuaConfigBridge.DrawSettings(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimLuaKit";
        }
    }
}
