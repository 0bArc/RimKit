using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace DontEatMod
{
    [StaticConstructorOnStartup]
    public static class DontEatMain
    {
        private const string HarmonyId = "com.xyn.donteat";

        static DontEatMain()
        {
            _ = LoadedModManager.GetMod<DontEatMod>();
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message("[DontEat] Harmony patches applied.");
        }
    }

    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    public static class Patch_Root_Play_Update
    {
        public static void Postfix()
        {
            DontEatDebugUI.CheckHotkeyAndToggle();
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Patch_JobGiver_GetFood
    {
        private static int lastDebugMessageTick = -999999;

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn?.RaceProps?.Humanlike != true)
            {
                return true;
            }

            if (pawn.Faction != Faction.OfPlayer || pawn.Map == null)
            {
                return true;
            }

            var counter = pawn.Map.resourceCounter;
            if (counter == null)
            {
                return true;
            }

            var settings = DontEatMod.Settings;
            float threshold = settings?.foodThreshold ?? DontEatSettings.DefaultFoodThreshold;
            float emergencyHunger = settings?.emergencyHungerPercent ?? DontEatSettings.DefaultEmergencyHungerPercent;
            float totalFood = counter.TotalHumanEdibleNutrition;
            float pawnFoodPercent = pawn.needs?.food?.CurLevelPercentage ?? 1f;

            if (totalFood < threshold && pawnFoodPercent <= emergencyHunger)
            {
                DebugLog($"Emergency override for {pawn.LabelShort}: needs food now ({pawnFoodPercent:P0}, reserves {totalFood:0.0}/{threshold:0.0}).", pawn);
                return true;
            }

            if (totalFood < threshold)
            {
                __result = null;
                DebugLog($"Blocked food job for {pawn.LabelShort}: reserves low ({totalFood:0.0} < {threshold:0.0}).", pawn);
                return false;
            }

            DebugLog($"Allowed food job for {pawn.LabelShort}: reserves ok ({totalFood:0.0} >= {threshold:0.0}).", pawn);

            return true;
        }

        private static void DebugLog(string message, Pawn pawn)
        {
            var settings = DontEatMod.Settings;
            if (settings?.debugLoggingEnabled == true)
            {
                Log.Message("[DontEat] " + message);
            }

            if (settings?.debugInGameMessages != true)
            {
                return;
            }

            int cooldownTicks = (int)Math.Round((settings.debugMessageCooldownSeconds <= 0f ? 0f : settings.debugMessageCooldownSeconds) * 60f);
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick - lastDebugMessageTick < cooldownTicks)
            {
                return;
            }

            lastDebugMessageTick = currentTick;
            Messages.Message("[DontEat] " + message, pawn, MessageTypeDefOf.TaskCompletion, false);
        }
    }
}