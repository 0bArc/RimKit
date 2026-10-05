using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.pawns social and animals: interactions, opinions with reasons, romance, animals, prisoners and slaves. Ops are pawn.*.
    internal static class ApiSocial
    {
        public static void Register()
        {
            R("pawn.interact", Interact);
            R("pawn.interaction_defs", InteractionDefs);
            R("pawn.opinion_reasons", OpinionReasons);
            R("pawn.partner", Partner);
            R("pawn.marry", Marry);
            R("pawn.break_up", BreakUp);
            R("pawn.animal_info", AnimalInfo);
            R("pawn.set_master", SetMaster);
            R("pawn.train", Train);
            R("pawn.tame", Tame);
            R("pawn.guest_info", GuestInfo);
            R("pawn.set_guest_status", SetGuestStatus);
            R("pawn.set_interaction_mode", SetInteractionMode);
            R("pawn.interaction_modes", InteractionModes);
            R("pawn.recruit", Recruit);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string Bad() => Fail("RK2001", "pawn handle is stale or null");

        private static Pawn Other(Dictionary<string, string> a) => ObjectHandles.Get<Pawn>(Int(a, "other"));

        private static string Interact(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            Pawn o = Other(a);
            if (p?.interactions == null || o == null) return Bad();
            InteractionDef def = DefDatabase<InteractionDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown interaction " + Str(a, "def"));
            return OkBool(p.interactions.TryInteractWith(o, def));
        }

        private static string InteractionDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<InteractionDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string OpinionReasons(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            Pawn o = Other(a);
            if (p?.needs?.mood?.thoughts == null || o == null) return Fail("RK3003", "this pawn has no thoughts about others");
            var list = new List<ISocialThought>();
            p.needs.mood.thoughts.GetSocialThoughts(o, list);
            var arr = Jb.Arr();
            foreach (ISocialThought t in list)
            {
                var thought = t as Thought;
                arr.Add(Jb.Obj().S("def", thought?.def?.defName).S("label", thought?.LabelCap).F("offset", t.OpinionOffset()));
            }

            return Jb.Obj().I("opinion", p.relations.OpinionOf(o)).Raw("reasons", arr.ToString()).Ok();
        }

        private static string Partner(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.relations == null) return Bad();
            Pawn partner = p.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse) ??
                           p.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance) ??
                           p.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
            return OkHandle(partner);
        }

        private static string Marry(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            Pawn o = Other(a);
            if (p?.relations == null || o?.relations == null) return Bad();
            foreach (var def in new[] { PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance })
            {
                p.relations.TryRemoveDirectRelation(def, o);
                o.relations.TryRemoveDirectRelation(def, p);
            }

            if (!p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, o)) p.relations.AddDirectRelation(PawnRelationDefOf.Spouse, o);
            return OkBool(true);
        }

        private static string BreakUp(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            Pawn o = Other(a);
            if (p?.relations == null || o?.relations == null) return Bad();
            bool had = false;
            foreach (var def in new[] { PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance, PawnRelationDefOf.Spouse })
            {
                if (p.relations.DirectRelationExists(def, o)) had = true;
                p.relations.TryRemoveDirectRelation(def, o);
                o.relations.TryRemoveDirectRelation(def, p);
            }

            if (had) p.relations.AddDirectRelation(PawnRelationDefOf.ExLover, o);
            return OkBool(had);
        }

        private static string AnimalInfo(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            if (!p.RaceProps.Animal) return Fail("RK3003", p.def.defName + " is not an animal");
            var j = Jb.Obj().S("trainability", p.RaceProps.trainability?.defName).F("wildness", p.GetStatValue(StatDefOf.Wildness))
                .B("tame", p.Faction != null && p.Faction.IsPlayer);
            if (p.playerSettings?.Master != null) j.H("master", p.playerSettings.Master);
            Pawn bond = p.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bond != null) j.H("bonded", bond);
            var tr = Jb.Arr();
            if (p.training != null)
            {
                foreach (TrainableDef td in DefDatabase<TrainableDef>.AllDefsListForReading)
                {
                    AcceptanceReport can = p.training.CanAssignToTrain(td, out bool visible);
                    if (!visible) continue;
                    tr.Add(Jb.Obj().S("def", td.defName).B("learned", p.training.HasLearned(td)).B("can_learn", can.Accepted));
                }
            }

            return j.Raw("trainables", tr.ToString()).Ok();
        }

        private static string SetMaster(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.playerSettings == null) return Bad();
            Pawn master = string.IsNullOrEmpty(Str(a, "master")) ? null : ObjectHandles.Get<Pawn>(Int(a, "master"));
            p.playerSettings.Master = master;
            return OkBool(p.playerSettings.Master == master);
        }

        private static string Train(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.training == null) return Fail("RK3003", "this pawn cannot be trained");
            TrainableDef td = DefDatabase<TrainableDef>.GetNamedSilentFail(Str(a, "def"));
            if (td == null) return Fail("RK3001", "unknown trainable " + Str(a, "def"));
            Pawn trainer = string.IsNullOrEmpty(Str(a, "trainer")) ? null : ObjectHandles.Get<Pawn>(Int(a, "trainer"));
            if (!p.training.CanAssignToTrain(td, out bool visible).Accepted) return OkBool(false);
            p.training.SetWantedRecursive(td, true);
            p.training.Train(td, trainer, true);
            return OkBool(p.training.HasLearned(td));
        }

        private static string Tame(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            if (!p.RaceProps.Animal) return Fail("RK3003", p.def.defName + " is not an animal");
            Pawn tamer = string.IsNullOrEmpty(Str(a, "tamer")) ? null : ObjectHandles.Get<Pawn>(Int(a, "tamer"));
            p.SetFaction(Faction.OfPlayer, tamer);
            return OkBool(p.Faction == Faction.OfPlayer);
        }

        private static string GuestInfo(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.guest == null) return Fail("RK3003", "this pawn has no guest data");
            return Jb.Obj().S("status", p.guest.GuestStatus.ToString()).S("mode", p.guest.ExclusiveInteractionMode?.defName)
                .F("resistance", p.guest.resistance).F("will", p.guest.will).B("recruitable", p.guest.Recruitable).B("is_prisoner", p.IsPrisonerOfColony)
                .B("is_slave", p.IsSlaveOfColony).Ok();
        }

        // status: Guest, Prisoner or Slave.
        private static string SetGuestStatus(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.guest == null) return Fail("RK3003", "this pawn has no guest data");
            if (!Enum.TryParse(Str(a, "status"), true, out GuestStatus status)) return Fail("RK1001", "status must be Guest, Prisoner or Slave");
            p.guest.SetGuestStatus(Faction.OfPlayer, status);
            return OkStr(p.guest.GuestStatus.ToString());
        }

        private static string SetInteractionMode(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.guest == null) return Fail("RK3003", "this pawn has no guest data");
            PrisonerInteractionModeDef def = DefDatabase<PrisonerInteractionModeDef>.GetNamedSilentFail(Str(a, "mode"));
            if (def == null) return Fail("RK3001", "unknown interaction mode " + Str(a, "mode"));
            p.guest.SetExclusiveInteraction(def);
            return OkStr(def.defName);
        }

        private static string InteractionModes(Dictionary<string, string> a) => OkStringList(DefDatabase<PrisonerInteractionModeDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string Recruit(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            Pawn recruiter = string.IsNullOrEmpty(Str(a, "recruiter")) ? null : ObjectHandles.Get<Pawn>(Int(a, "recruiter"));
            if (recruiter == null) { p.SetFaction(Faction.OfPlayer); }
            else InteractionWorker_RecruitAttempt.DoRecruit(recruiter, p, true);
            return OkBool(p.Faction == Faction.OfPlayer);
        }
    }
}
