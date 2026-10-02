using System;
using System.Collections.Concurrent;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimLuaKit
{
    // SpawnSetup can run off the main thread; queue and drain later.
    internal static class LuaEventQueue
    {
        private static readonly ConcurrentQueue<(string name, int handle)> Pending =
            new ConcurrentQueue<(string, int)>();

        public static void Enqueue(string name, int handle)
        {
            if (string.IsNullOrEmpty(name) || handle == 0)
            {
                return;
            }

            Pending.Enqueue((name, handle));
        }

        public static void Drain()
        {
            int n = 0;
            while (n < 64 && Pending.TryDequeue(out var ev))
            {
                n++;
                try
                {
                    NativeAbi.rimlua_emit_event(ev.name, ev.handle);
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] event drain " + ev.name + " failed: " + e);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class EventBridge_PawnSpawnSetup
    {
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (__instance == null || respawningAfterLoad)
            {
                return;
            }

            // Off main thread during map gen.
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            try
            {
                LuaEventQueue.Enqueue("pawn_spawned", ObjectHandles.GetOrAdd(__instance));
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] pawn_spawned queue failed: " + e);
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
                LuaEventQueue.Enqueue("pawn_died", ObjectHandles.GetOrAdd(__instance));
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] pawn_died queue failed: " + e);
            }
        }
    }
}
