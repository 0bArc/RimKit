using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;
using static RimKit.Dlc;

namespace RimKit
{
    // game.royalty (Royalty DLC): titles, favor, permits, psylink, abilities, the empire, throne rooms. Ops are royalty.*.
    // game.biotech (Biotech DLC): xenotypes, mechanitors and mechs, pregnancy, growth, hemogen, gene packs. Ops are biotech.*.
    internal static class ApiRoyaltyBiotech
    {
        public static void Register()
        {
            const string Roy = "Royalty";
            D(Roy, "royalty.titles", Titles);
            D(Roy, "royalty.title", TitleOf);
            D(Roy, "royalty.set_title", SetTitle);
            D(Roy, "royalty.favor", Favor);
            D(Roy, "royalty.set_favor", SetFavor);
            D(Roy, "royalty.permits", Permits);
            D(Roy, "royalty.permit_defs", PermitDefs);
            D(Roy, "royalty.add_permit", AddPermit);
            D(Roy, "royalty.psylink_level", PsylinkLevel);
            D(Roy, "royalty.set_psylink_level", SetPsylinkLevel);
            D(Roy, "royalty.abilities", Abilities);
            D(Roy, "royalty.gain_ability", GainAbility);
            D(Roy, "royalty.entropy", Entropy);
            D(Roy, "royalty.empire", Empire);
            D(Roy, "royalty.throne_rooms", ThroneRooms);

            const string Bio = "Biotech";
            D(Bio, "biotech.xenotypes", Xenotypes);
            D(Bio, "biotech.set_xenotype", SetXenotype);
            D(Bio, "biotech.gene_defs", GeneDefs);
            D(Bio, "biotech.mechanitor", Mechanitor);
            D(Bio, "biotech.mechs", Mechs);
            D(Bio, "biotech.control_mech", ControlMech);
            D(Bio, "biotech.pregnancy", Pregnancy);
            D(Bio, "biotech.start_pregnancy", StartPregnancy);
            D(Bio, "biotech.end_pregnancy", EndPregnancy);
            D(Bio, "biotech.growth", Growth);
            D(Bio, "biotech.set_growth_points", SetGrowthPoints);
            D(Bio, "biotech.hemogen", Hemogen);
            D(Bio, "biotech.set_hemogen", SetHemogen);
            D(Bio, "biotech.genepack", Genepack);
        }

        private static string BadPawn() => Fail("RK2001", "pawn handle is stale or null");

        // ---- Royalty

        private static Faction FactionArg(Dictionary<string, string> a)
        {
            if (string.IsNullOrEmpty(Str(a, "faction"))) return Faction.OfEmpire;
            return ObjectHandles.Get<Faction>(Int(a, "faction"));
        }

        private static string Titles(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (RoyalTitleDef t in DefDatabase<RoyalTitleDef>.AllDefsListForReading.OrderBy(x => x.seniority))
                arr.Add(Jb.Obj().S("def", t.defName).S("label", t.label).I("seniority", (int)t.seniority).I("favor_cost", t.favorCost).B("awardable", t.Awardable));
            return arr.Ok();
        }

        private static string TitleOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.royalty == null) return Fail("RK3003", "this pawn has no royal title data");
            Faction f = FactionArg(a);
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            RoyalTitleDef t = p.royalty.GetCurrentTitle(f);
            return t == null ? OkJson("null") : Jb.Obj().S("def", t.defName).S("label", t.label).I("seniority", (int)t.seniority).Ok();
        }

        // def: a RoyalTitleDef name, or empty to remove the title.
        private static string SetTitle(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.royalty == null) return Fail("RK3003", "this pawn has no royal title data");
            Faction f = FactionArg(a);
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            RoyalTitleDef def = null;
            if (!string.IsNullOrEmpty(Str(a, "def")))
            {
                def = DefDatabase<RoyalTitleDef>.GetNamedSilentFail(Str(a, "def"));
                if (def == null) return Fail("RK3001", "unknown title " + Str(a, "def"));
            }

            p.royalty.SetTitle(f, def, Bool(a, "rewards"), false, Bool(a, "letter"));
            return OkBool(p.royalty.GetCurrentTitle(f) == def);
        }

        private static string Favor(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.royalty == null) return Fail("RK3003", "this pawn has no royal title data");
            Faction f = FactionArg(a);
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            return OkInt(p.royalty.GetFavor(f));
        }

        private static string SetFavor(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.royalty == null) return Fail("RK3003", "this pawn has no royal title data");
            Faction f = FactionArg(a);
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            p.royalty.SetFavor(f, Math.Max(0, Int(a, "value")));
            return OkInt(p.royalty.GetFavor(f));
        }

        private static string Permits(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.royalty == null) return Fail("RK3003", "this pawn has no royal title data");
            var arr = Jb.Arr();
            foreach (FactionPermit fp in p.royalty.AllFactionPermits)
                arr.Add(Jb.Obj().S("def", fp.Permit.defName).S("label", fp.Permit.label).H("faction", fp.Faction).I("last_used_tick", fp.LastUsedTick));
            return arr.Ok();
        }

        private static string PermitDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (RoyalTitlePermitDef d in DefDatabase<RoyalTitlePermitDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("title", d.minTitle?.defName).I("cooldown_days", (int)d.cooldownDays));
            return arr.Ok();
        }

        private static string AddPermit(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.royalty == null) return Fail("RK3003", "this pawn has no royal title data");
            Faction f = FactionArg(a);
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            RoyalTitlePermitDef def = DefDatabase<RoyalTitlePermitDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown permit " + Str(a, "def"));
            p.royalty.AddPermit(def, f);
            return OkBool(p.royalty.HasPermit(def, f));
        }

        private static string PsylinkLevel(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return BadPawn();
            return OkInt(p.GetPsylinkLevel());
        }

        private static string SetPsylinkLevel(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return BadPawn();
            int target = Math.Max(0, Math.Min(6, Int(a, "level")));
            int current = p.GetPsylinkLevel();
            if (target > current && p.GetMainPsylinkSource() == null) return Fail("RK3003", "the pawn has no psylink, give one with a hediff first");
            if (target != current) p.ChangePsylinkLevel(target - current, false);
            return OkInt(p.GetPsylinkLevel());
        }

        private static string Abilities(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.abilities == null) return Fail("RK3003", "this pawn has no abilities");
            var arr = Jb.Arr();
            foreach (Ability ab in p.abilities.abilities)
                arr.Add(Jb.Obj().S("def", ab.def.defName).S("label", ab.def.label).I("level", ab.def.level).B("can_cast", ab.CanCast).I("cooldown_ticks", ab.CooldownTicksRemaining));
            return arr.Ok();
        }

        private static string GainAbility(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.abilities == null) return Fail("RK3003", "this pawn has no abilities");
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown ability " + Str(a, "def"));
            p.abilities.GainAbility(def);
            return OkBool(p.abilities.GetAbility(def) != null);
        }

        private static string Entropy(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.psychicEntropy == null) return Fail("RK3003", "this pawn has no psychic entropy");
            return Jb.Obj().F("entropy", p.psychicEntropy.EntropyValue).F("max", p.psychicEntropy.MaxEntropy).F("psyfocus", p.psychicEntropy.CurrentPsyfocus).B("limited", p.psychicEntropy.IsCurrentlyMeditating).Ok();
        }

        private static string Empire(Dictionary<string, string> a)
        {
            Faction f = Faction.OfEmpire;
            if (f == null) return Fail("RK3001", "this world has no Empire");
            var j = Jb.Obj().H("faction", f).S("name", f.Name).B("hidden", f.Hidden);
            if (!f.IsPlayer) j.I("goodwill", f.GoodwillWith(Faction.OfPlayer)).S("relation", f.RelationKindWith(Faction.OfPlayer).ToString());
            return j.Ok();
        }

        // Rooms the game counts as throne rooms, with the impressiveness a title's throne room requirement is checked against.
        private static string ThroneRooms(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var arr = Jb.Arr();
            foreach (Room r in map.regionGrid.AllRooms.Where(x => x.Role?.defName == "ThroneRoom"))
                arr.Add(Jb.Obj().I("id", r.ID).I("cells", r.CellCount).F("impressiveness", r.GetStat(RoomStatDefOf.Impressiveness)).I("x", r.Cells.First().x).I("z", r.Cells.First().z));
            return arr.Ok();
        }

        // ---- Biotech

        private static string Xenotypes(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (XenotypeDef d in DefDatabase<XenotypeDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).I("genes", d.genes.Count).B("inheritable", d.inheritable).F("archite_cost", 0));
            return arr.Ok();
        }

        private static string SetXenotype(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.genes == null) return Fail("RK3003", "this pawn has no genes");
            XenotypeDef def = DefDatabase<XenotypeDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown xenotype " + Str(a, "def"));
            p.genes.SetXenotype(def);
            return OkBool(p.genes.Xenotype == def);
        }

        private static string GeneDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (GeneDef d in DefDatabase<GeneDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).I("complexity", d.biostatCpx).I("metabolism", d.biostatMet).I("archite", d.biostatArc).S("category", d.displayCategory?.defName));
            return arr.Ok();
        }

        private static string Mechanitor(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return BadPawn();
            Pawn_MechanitorTracker m = p.mechanitor;
            if (m == null) return OkJson("null");
            return Jb.Obj().I("bandwidth", (int)m.TotalBandwidth).I("used_bandwidth", (int)m.UsedBandwidth).I("controlled", m.ControlledPawns.Count).I("control_groups", m.controlGroups.Count).Ok();
        }

        private static string Mechs(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mechanitor == null) return Fail("RK3003", "this pawn is not a mechanitor");
            return OkHandles(p.mechanitor.ControlledPawns.ToList());
        }

        private static string ControlMech(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mechanitor == null) return Fail("RK3003", "this pawn is not a mechanitor");
            Pawn mech = ObjectHandles.Get<Pawn>(Int(a, "mech"));
            if (mech == null || !mech.RaceProps.IsMechanoid) return Fail("RK2001", "mech handle is stale or not a mechanoid");
            mech.relations?.AddDirectRelation(PawnRelationDefOf.Overseer, p);
            return OkBool(mech.GetOverseer() == p);
        }

        private static string Pregnancy(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return BadPawn();
            Hediff_Pregnant h = p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.PregnantHuman) as Hediff_Pregnant;
            if (h == null) return OkJson("null");
            return Jb.Obj().F("progress", h.GestationProgress).H("father", h.Father).Ok();
        }

        private static string StartPregnancy(Dictionary<string, string> a)
        {
            Pawn mother = PawnOf(a);
            if (mother?.health == null) return BadPawn();
            if (!mother.RaceProps.Humanlike || mother.gender != Gender.Female) return Fail("RK3003", "only a female humanlike pawn can be pregnant");
            if (mother.health.hediffSet.HasHediff(HediffDefOf.PregnantHuman)) return OkBool(false);
            Pawn father = string.IsNullOrEmpty(Str(a, "father")) ? null : ObjectHandles.Get<Pawn>(Int(a, "father"));
            var preg = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, mother);
            preg.SetParents(mother, father, null);
            mother.health.AddHediff(preg);
            return OkBool(mother.health.hediffSet.HasHediff(HediffDefOf.PregnantHuman));
        }

        private static string EndPregnancy(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.health == null) return BadPawn();
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman);
            if (h == null) return OkBool(false);
            p.health.RemoveHediff(h);
            return OkBool(true);
        }

        private static string Growth(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ageTracker == null) return BadPawn();
            var t = p.ageTracker;
            return Jb.Obj().F("age", t.AgeBiologicalYearsFloat).S("life_stage", t.CurLifeStage?.defName).F("growth_points", t.growthPoints).B("growing", t.CurLifeStage != null && t.Growth < 1f).F("growth", t.Growth).Ok();
        }

        private static string SetGrowthPoints(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ageTracker == null) return BadPawn();
            p.ageTracker.growthPoints = Math.Max(0f, Float(a, "value"));
            return OkFloat(p.ageTracker.growthPoints);
        }

        private static Gene_Hemogen HemogenGene(Pawn p) => p?.genes?.GetFirstGeneOfType<Gene_Hemogen>();

        private static string Hemogen(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return BadPawn();
            Gene_Hemogen g = HemogenGene(p);
            return g == null ? OkJson("null") : Jb.Obj().F("value", g.Value).F("max", g.Max).B("hungry", g.Value < g.MinLevelForAlert).Ok();
        }

        private static string SetHemogen(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return BadPawn();
            Gene_Hemogen g = HemogenGene(p);
            if (g == null) return Fail("RK3003", "this pawn has no hemogen gene");
            g.Value = Math.Max(0f, Math.Min(g.Max, Float(a, "value")));
            return OkFloat(g.Value);
        }

        private static string Genepack(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            var pack = t as Genepack;
            if (pack == null) return Fail("RK3003", t.def.defName + " is not a gene pack");
            var genes = Jb.Arr();
            foreach (GeneDef g in pack.GeneSet.GenesListForReading) genes.AddS(g.defName);
            return Jb.Obj().Raw("genes", genes.ToString()).I("complexity", pack.GeneSet.ComplexityTotal).I("metabolism", pack.GeneSet.MetabolismTotal).I("archite", pack.GeneSet.ArchitesTotal).Ok();
        }
    }
}
