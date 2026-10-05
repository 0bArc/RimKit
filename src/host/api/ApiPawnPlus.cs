using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using static RimKit.ApiHelpers;

namespace RimKit
{
    internal static class ApiPawnPlus
    {
        public static void Register()
        {
            ApiRegistry.Register("pawn.mental_state", MentalState, "gameplay");
            ApiRegistry.Register("pawn.set_mental_state", SetMentalState, "gameplay");
            ApiRegistry.Register("pawn.clear_mental_state", ClearMentalState, "gameplay");
            ApiRegistry.Register("pawn.capable", Capable, "gameplay");
            ApiRegistry.Register("pawn.relations_count", RelationsCount, "gameplay");
            // Draftable control for any non-Humanlike pawn (animals, entities, ToolUsers).
            ApiRegistry.Register("pawn.make_controllable", MakeControllable, "gameplay");
            ApiRegistry.Register("pawn.release_control", ReleaseControl, "gameplay");
            ApiRegistry.Register("pawn.is_controllable", IsControllable, "gameplay");
        }

        private static string MakeControllable(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p == null) return Err("no pawn");
            PawnControl.MakeControllable(p);
            return OkBool(p.drafter != null && PawnControl.IsControlled(p));
        }

        private static string ReleaseControl(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p == null) return Err("no pawn");
            PawnControl.ReleaseControl(p);
            return OkBool(true);
        }

        private static string IsControllable(Dictionary<string, string> args)
        {
            return OkBool(PawnControl.IsControlled(PawnOf(args)));
        }

        private static string MentalState(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            return OkStr(p?.MentalStateDef?.defName ?? "");
        }

        private static string SetMentalState(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            var def = DefDatabase<MentalStateDef>.GetNamedSilentFail(Str(args, "def"));
            if (p?.mindState?.mentalStateHandler == null || def == null) return Err("bad pawn/mental");
            p.mindState.mentalStateHandler.TryStartMentalState(def, null, true, false, false, null, false, false, false);
            return OkBool(true);
        }

        private static string ClearMentalState(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p?.mindState?.mentalStateHandler == null) return Err("no pawn");
            p.mindState.mentalStateHandler.CurState?.RecoverFromState();
            return OkBool(true);
        }

        private static string Capable(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            string cap = Str(args, "capacity");
            if (p?.health?.capacities == null) return OkBool(false);
            PawnCapacityDef def = DefDatabase<PawnCapacityDef>.GetNamedSilentFail(cap);
            if (def == null) return Err("bad capacity");
            return OkBool(p.health.capacities.CapableOf(def));
        }

        private static string RelationsCount(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            return OkInt(p?.relations?.DirectRelations?.Count ?? 0);
        }
    }
}
