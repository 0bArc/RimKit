using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.pawns health: injuries, body parts, damage, healing, immunity, prosthetics, surgery. Ops are pawn.*.
    internal static class ApiPawnHealth
    {
        public static void Register()
        {
            R("pawn.hediff_list", HediffList);
            R("pawn.injuries", Injuries);
            R("pawn.body_parts", BodyParts);
            R("pawn.damage_part", DamagePart);
            R("pawn.heal_injuries", HealInjuries);
            R("pawn.restore_part", RestorePart);
            R("pawn.remove_part", RemovePart);
            R("pawn.add_hediff_on_part", AddHediffOnPart);
            R("pawn.immunity", Immunity);
            R("pawn.health_summary", HealthSummary);
            R("pawn.surgeries", Surgeries);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string Bad() => Fail("RK2001", "pawn handle is stale or null");

        private static BodyPartRecord Part(Pawn p, string index, out string err)
        {
            err = null;
            if (string.IsNullOrEmpty(index)) return null;
            if (!int.TryParse(index, out int i) || i < 0 || i >= p.RaceProps.body.AllParts.Count)
            {
                err = Fail("RK1001", "part index out of range 0 to " + (p.RaceProps.body.AllParts.Count - 1));
                return null;
            }

            return p.RaceProps.body.GetPartAtIndex(i);
        }

        private static Jb HediffJson(Hediff h)
        {
            var j = Jb.Obj().S("def", h.def.defName).S("label", h.LabelCap).F("severity", h.Severity).F("bleeding", h.BleedRate)
                .B("injury", h is Hediff_Injury).B("permanent", h.IsPermanent()).B("tended", h.IsTended()).B("tendable", h.TendableNow())
                .B("visible", h.Visible).I("age_ticks", h.ageTicks).S("stage", h.CurStage?.label);
            if (h.Part != null) j.I("part", h.Part.Index).S("part_label", h.Part.LabelCap);
            if (h is HediffWithComps hwc && hwc.comps != null)
            {
                var names = Jb.Arr();
                foreach (var c in hwc.comps) names.AddS(c.GetType().Name);
                j.Raw("comps", names.ToString());
            }

            return j;
        }

        private static string HediffList(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            var arr = Jb.Arr();
            foreach (Hediff h in p.health.hediffSet.hediffs) arr.Add(HediffJson(h));
            return arr.Ok();
        }

        private static string Injuries(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            var arr = Jb.Arr();
            foreach (Hediff h in p.health.hediffSet.hediffs.Where(x => x is Hediff_Injury)) arr.Add(HediffJson(h));
            return arr.Ok();
        }

        private static string BodyParts(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            var arr = Jb.Arr();
            foreach (BodyPartRecord part in p.RaceProps.body.AllParts)
            {
                bool missing = p.health.hediffSet.PartIsMissing(part);
                arr.Add(Jb.Obj().I("index", part.Index).S("def", part.def.defName).S("label", part.LabelCap).B("missing", missing)
                    .F("hp", missing ? 0 : p.health.hediffSet.GetPartHealth(part)).F("max_hp", part.def.GetMaxHealth(p))
                    .F("coverage", part.coverage).S("depth", part.depth.ToString()));
            }

            return arr.Ok();
        }

        // part: the index from body_parts. Omit it to hit the whole body.
        private static string DamagePart(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            DamageDef def = DefDatabase<DamageDef>.GetNamedSilentFail(string.IsNullOrEmpty(Str(a, "damage_def")) ? "Cut" : Str(a, "damage_def"));
            if (def == null) return Fail("RK3001", "unknown damage def " + Str(a, "damage_def"));
            float amount = Float(a, "amount");
            if (amount <= 0) return Fail("RK1001", "amount must be positive");
            BodyPartRecord part = Part(p, Str(a, "part"), out string err);
            if (err != null) return err;
            p.TakeDamage(new DamageInfo(def, amount, 0f, -1f, null, part));
            return OkFloat(p.health.summaryHealth.SummaryHealthPercent);
        }

        private static string HealInjuries(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            bool includePermanent = Bool(a, "permanent");
            int n = 0;
            foreach (Hediff h in p.health.hediffSet.hediffs.Where(x => x is Hediff_Injury && (includePermanent || !x.IsPermanent())).ToList())
            {
                p.health.RemoveHediff(h);
                n++;
            }

            return OkInt(n);
        }

        private static string RestorePart(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            BodyPartRecord part = Part(p, Str(a, "part"), out string err);
            if (err != null) return err;
            if (part == null) return Fail("RK1001", "part is required");
            p.health.RestorePart(part);
            return OkBool(!p.health.hediffSet.PartIsMissing(part));
        }

        private static string RemovePart(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            BodyPartRecord part = Part(p, Str(a, "part"), out string err);
            if (err != null) return err;
            if (part == null) return Fail("RK1001", "part is required");
            if (p.health.hediffSet.PartIsMissing(part)) return OkBool(false);
            Hediff missing = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, p, part);
            p.health.AddHediff(missing, part);
            return OkBool(true);
        }

        // Adds a hediff on a body part, for example a prosthetic (SimpleProstheticLeg) on a missing leg.
        private static string AddHediffOnPart(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown hediff " + Str(a, "def"));
            BodyPartRecord part = Part(p, Str(a, "part"), out string err);
            if (err != null) return err;
            if (part != null && p.health.hediffSet.PartIsMissing(part)) p.health.RestorePart(part);
            Hediff h = HediffMaker.MakeHediff(def, p, part);
            if (!string.IsNullOrEmpty(Str(a, "severity"))) h.Severity = Float(a, "severity");
            p.health.AddHediff(h, part);
            return OkBool(true);
        }

        private static string Immunity(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown hediff " + Str(a, "def"));
            return OkFloat(p.health.immunity.GetImmunity(def, false));
        }

        private static string HealthSummary(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return Bad();
            return Jb.Obj().F("health", p.health.summaryHealth.SummaryHealthPercent).F("pain", p.health.hediffSet.PainTotal)
                .F("bleed_rate", p.health.hediffSet.BleedRateTotal).B("downed", p.Downed).B("dead", p.Dead)
                .B("in_pain_shock", p.health.InPainShock).I("hediffs", p.health.hediffSet.hediffs.Count).Ok();
        }

        private static string Surgeries(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            var arr = Jb.Arr();
            foreach (RecipeDef r in p.def.AllRecipes.Where(x => x.IsSurgery))
            {
                arr.Add(Jb.Obj().S("def", r.defName).S("label", r.LabelCap).B("available", r.AvailableNow).B("on_body_part", r.targetsBodyPart));
            }

            return arr.Ok();
        }
    }
}
