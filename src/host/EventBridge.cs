using System;
using HarmonyLib;
using Verse;

namespace RimLuaKit
{
    /// <summary>
    /// Fixed Harmony patches that emit semantic Lua events (pawn_spawned, pawn_died).
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class EventBridge_PawnSpawnSetup
    {
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (__instance == null || respawningAfterLoad)
            {
                return;
            }

            try
            {
                NativeAbi.rimlua_emit_event("pawn_spawned", ObjectHandles.GetOrAdd(__instance));
            }
            catch (Exception e)
            {
                Log.Error("[RimLuaKit] pawn_spawned emit failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class EventBridge_PawnKill
    {
        public static void Prefix(Pawn __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                NativeAbi.rimlua_emit_event("pawn_died", ObjectHandles.GetOrAdd(__instance));
            }
            catch (Exception e)
            {
                Log.Error("[RimLuaKit] pawn_died emit failed: " + e);
            }
        }
    }
}
