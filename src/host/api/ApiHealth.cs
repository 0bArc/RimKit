using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using static RimKit.ApiHelpers;

namespace RimKit
{
    internal static class ApiHealth
    {
        public static void Register()
        {
            ApiRegistry.Register("health.hediff_severity", HediffSeverity, "gameplay");
            ApiRegistry.Register("health.set_hediff_severity", SetHediffSeverity, "gameplay");
            ApiRegistry.Register("health.tend", Tend, "gameplay");
            ApiRegistry.Register("health.has_hediff", HasHediff, "gameplay");
            ApiRegistry.Register("surgery.queue_operation", QueueOperation, "gameplay");
        }

        private static string HediffSeverity(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(args, "def"));
            if (p?.health?.hediffSet == null || def == null) return OkFloat(0f);
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            return OkFloat(h?.Severity ?? 0f);
        }

        private static string SetHediffSeverity(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(args, "def"));
            if (p?.health?.hediffSet == null || def == null) return Err("bad pawn/hediff");
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (h == null)
            {
                h = HediffMaker.MakeHediff(def, p);
                p.health.AddHediff(h);
            }
            h.Severity = Float(args, "severity");
            return OkBool(true);
        }

        private static string Tend(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p?.health?.hediffSet == null) return Err("no pawn");
            float quality = Float(args, "quality");
            if (quality <= 0f) quality = 0.5f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury inj && inj.TendableNow())
                {
                    inj.Tended(quality, quality);
                }
            }
            return OkBool(true);
        }

        private static string HasHediff(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(args, "def"));
            if (p?.health?.hediffSet == null || def == null) return OkBool(false);
            return OkBool(p.health.hediffSet.HasHediff(def));
        }

        private static string QueueOperation(Dictionary<string, string> args)
        {
            Pawn patient = PawnOf(args);
            string recipeName = Str(args, "recipe");
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
            if (patient?.BillStack == null || recipe == null) return Err("bad patient/recipe");
            if (!recipe.IsSurgery) return Err("not a surgery recipe");
            Bill_Medical bill = new Bill_Medical(recipe, null);
            patient.BillStack.AddBill(bill);
            return OkBool(true);
        }
    }
}
