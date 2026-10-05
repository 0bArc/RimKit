using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.pawns mind: mental states, break thresholds, inspirations, drug use. Ops are pawn.*.
    internal static class ApiMind
    {
        public static void Register()
        {
            R("pawn.mental_info", MentalStateOf);
            R("pawn.start_mental_state", StartMentalState);
            R("pawn.stop_mental_state", StopMentalState);
            R("pawn.mental_state_defs", MentalStateDefs);
            R("pawn.break_thresholds", BreakThresholds);
            R("pawn.mind_summary", MindSummary);
            R("pawn.inspiration", InspirationOf);
            R("pawn.give_inspiration", GiveInspiration);
            R("pawn.end_inspiration", EndInspiration);
            R("pawn.use_drug", UseDrug);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string Bad() => Fail("RK2001", "pawn handle is stale or null");

        private static string MentalStateOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            MentalState s = p.MentalState;
            if (s == null) return OkJson("null");
            return Jb.Obj().S("def", s.def.defName).S("label", s.InspectLine).I("age_ticks", s.Age).B("caused_by_mood", s.causedByMood)
                .B("aggro", s.def.IsAggro).S("category", s.def.category.ToString()).Ok();
        }

        // def: a mental state def name such as Wander_Sad, Berserk or Binging_DrugMajor. reason: text shown in the letter.
        private static string StartMentalState(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState?.mentalStateHandler == null) return Bad();
            MentalStateDef def = DefDatabase<MentalStateDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown mental state " + Str(a, "def"));
            string reason = string.IsNullOrEmpty(Str(a, "reason")) ? null : Str(a, "reason");
            bool started = p.mindState.mentalStateHandler.TryStartMentalState(def, reason, true, false, false, null, false, false, false);
            return OkBool(started);
        }

        private static string StopMentalState(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState?.mentalStateHandler == null) return Bad();
            if (p.MentalState == null) return OkBool(false);
            p.mindState.mentalStateHandler.CurState.RecoverFromState();
            return OkBool(true);
        }

        private static string MentalStateDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (MentalStateDef d in DefDatabase<MentalStateDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("category", d.category.ToString()).B("aggro", d.IsAggro).B("recoverable", d.recoverFromSleep || d.minTicksBeforeRecovery > 0));
            return arr.Ok();
        }

        private static string BreakThresholds(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            var breaker = p?.mindState?.mentalBreaker;
            if (breaker == null) return Fail("RK3003", "this pawn has no mental breaker");
            return Jb.Obj().F("minor", breaker.BreakThresholdMinor).F("major", breaker.BreakThresholdMajor).F("extreme", breaker.BreakThresholdExtreme)
                .B("can_break", breaker.CanDoRandomMentalBreaks).F("mood", p.needs?.mood?.CurLevel ?? 0).Ok();
        }

        private static string MindSummary(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            var j = Jb.Obj().B("in_mental_state", p.InMentalState).B("inspired", p.Inspired);
            if (p.needs?.mood != null)
            {
                j.F("mood", p.needs.mood.CurLevel).I("memories", p.needs.mood.thoughts.memories.Memories.Count);
            }

            if (p.MentalState != null) j.S("mental_state", p.MentalState.def.defName);
            if (p.Inspired) j.S("inspiration", p.Inspiration.def.defName);
            return j.Ok();
        }

        private static string InspirationOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState?.inspirationHandler == null) return Bad();
            Inspiration i = p.Inspiration;
            if (i == null) return OkJson("null");
            return Jb.Obj().S("def", i.def.defName).S("label", i.def.LabelCap).I("age_ticks", i.Age).Ok();
        }

        private static string GiveInspiration(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState?.inspirationHandler == null) return Bad();
            InspirationDef def = DefDatabase<InspirationDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown inspiration " + Str(a, "def"));
            return OkBool(p.mindState.inspirationHandler.TryStartInspiration(def));
        }

        private static string EndInspiration(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState?.inspirationHandler == null) return Bad();
            if (p.Inspiration == null) return OkBool(false);
            p.mindState.inspirationHandler.EndInspiration(p.Inspiration);
            return OkBool(true);
        }

        // Uses one unit of a drug def (for example Beer, Flake) on the pawn, with all its effects and tolerance.
        private static string UseDrug(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null || !def.IsDrug) return Fail("RK3001", Str(a, "def") + " is not a drug def");
            Thing drug = ThingMaker.MakeThing(def);
            drug.Ingested(p, 0f);
            if (!drug.Destroyed) drug.Destroy();
            return OkBool(true);
        }
    }
}
