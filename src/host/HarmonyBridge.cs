using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimLuaKit
{
    internal static class HarmonyBridge
    {
        private static Harmony harmony;
        private static readonly Dictionary<int, MethodBase> HookToMethod = new Dictionary<int, MethodBase>();
        private static readonly Dictionary<MethodBase, List<int>> MethodToPrefixHooks = new Dictionary<MethodBase, List<int>>();
        private static readonly Dictionary<MethodBase, List<int>> MethodToPostfixHooks = new Dictionary<MethodBase, List<int>>();

        public static void Init(Harmony harmonyInstance)
        {
            harmony = harmonyInstance;
        }

        public static int RegisterHook(string typeName, string methodName, int isPrefix, int hookId)
        {
            try
            {
                Type type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    Log.Error("[RimKit] Type not found: " + typeName);
                    return 0;
                }

                MethodInfo method = AccessTools.Method(type, methodName);
                if (method == null)
                {
                    Log.Error("[RimKit] Method not found: " + typeName + "." + methodName);
                    return 0;
                }

                bool prefix = isPrefix != 0;
                if (prefix)
                {
                    if (!MethodToPrefixHooks.ContainsKey(method))
                    {
                        MethodToPrefixHooks[method] = new List<int>();
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(HarmonyBridge), nameof(SharedPrefix)));
                    }

                    MethodToPrefixHooks[method].Add(hookId);
                }
                else
                {
                    if (!MethodToPostfixHooks.ContainsKey(method))
                    {
                        MethodToPostfixHooks[method] = new List<int>();
                        harmony.Patch(method, postfix: new HarmonyMethod(typeof(HarmonyBridge), nameof(SharedPostfix)));
                    }

                    MethodToPostfixHooks[method].Add(hookId);
                }

                HookToMethod[hookId] = method;
                Log.Message("[RimKit] Hooked " + (prefix ? "prefix" : "postfix") + " " + typeName + "." + methodName + " id=" + hookId);
                return 1;
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] RegisterHook failed: " + e);
                return 0;
            }
        }

        public static void UnregisterHook(int hookId)
        {
            if (!HookToMethod.TryGetValue(hookId, out MethodBase method))
            {
                return;
            }

            HookToMethod.Remove(hookId);
            if (MethodToPrefixHooks.TryGetValue(method, out List<int> prefixes))
            {
                prefixes.Remove(hookId);
            }

            if (MethodToPostfixHooks.TryGetValue(method, out List<int> postfixes))
            {
                postfixes.Remove(hookId);
            }
        }

        // First object arg that is a Pawn becomes the Lua handle; JobGiver_GetFood.TryGiveJob pattern.
        public static bool SharedPrefix(MethodBase __originalMethod, object[] __args, ref object __result)
        {
            if (!MethodToPrefixHooks.TryGetValue(__originalMethod, out List<int> hooks) || hooks.Count == 0)
            {
                return true;
            }

            int pawnHandle = FindPawnHandle(__args);
            bool continueOriginal = true;
            foreach (int hookId in hooks)
            {
                if (NativeAbi.rimlua_invoke_hook(hookId, pawnHandle, out int cont) == 0 && cont == 0)
                {
                    continueOriginal = false;
                }
            }

            if (!continueOriginal)
            {
                // Common for TryGiveJob-style: null means no job.
                if (__result == null || __result is Job)
                {
                    __result = null;
                }

                return false;
            }

            return true;
        }

        public static void SharedPostfix(MethodBase __originalMethod, object[] __args)
        {
            if (!MethodToPostfixHooks.TryGetValue(__originalMethod, out List<int> hooks) || hooks.Count == 0)
            {
                return;
            }

            int pawnHandle = FindPawnHandle(__args);
            foreach (int hookId in hooks)
            {
                NativeAbi.rimlua_invoke_hook(hookId, pawnHandle, out _);
            }
        }

        private static int FindPawnHandle(object[] args)
        {
            if (args == null)
            {
                return 0;
            }

            foreach (object arg in args)
            {
                if (arg is Pawn pawn)
                {
                    return ObjectHandles.GetOrAdd(pawn);
                }
            }

            return 0;
        }
    }

    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    internal static class Patch_Root_Play_Update
    {
        private static int tickCounter;
        private static bool onLoadFired;
        private static bool luaLoadAttempted;

        public static void Postfix()
        {
            if (!luaLoadAttempted)
            {
                luaLoadAttempted = true;
                try
                {
                    HostMain.TryLoadLuaModsGated();
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] gated Lua load failed: " + e);
                }
            }

            if (!onLoadFired)
            {
                onLoadFired = true;
                try
                {
                    NativeAbi.rimlua_call_on_load();
                    Log.Message("[RimKit] Host ready (wide API).");
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] on_load failed: " + e);
                }
            }

            tickCounter++;
            try
            {
                LuaEventQueue.Drain();
                NativeAbi.rimlua_call_on_tick();
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] on_tick failed: " + e);
            }
        }
    }
}
