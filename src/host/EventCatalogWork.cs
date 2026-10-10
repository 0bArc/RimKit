using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimKit
{
    // Events for work, health, mind, combat, world, economy, research, rooms, factions and input. These were the "events still
    // missing" groups in docs/missing.md. Work events listen to the game's own record counters (Pawn_RecordsTracker.Increment),
    // because the game bumps them at exactly the moment a hauling, repair, mining or deconstruction step finishes.
    internal static partial class EventCatalog
    {
        private static void AddWork()
        {
            // pawn lifecycle
            Add("pawn.left_colony", "A pawn is leaving the player faction (released, kicked out, turned hostile). Payload: pawn, to.", false, () => M(typeof(Pawn), nameof(Pawn.SetFaction)), nameof(EventPatches.PawnLeftColony), true);
            Add("pawn.escaped", "A prisoner took part in a prison break. Payload: pawn, initiator.", false, () => M(typeof(PrisonBreakUtility), "StartPrisonBreak", 5), nameof(EventPatches.PawnEscaped), false);

            // work and jobs
            Add("work.completed", "A pawn finished a work job (anything started by a work giver). Payload: pawn, job, work_type.", false, () => M(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob)), nameof(EventPatches.WorkCompleted), true);
            Add("item.crafted", "A bill produced one finished item. Payload: thing, recipe, pawn.", false, () => M(typeof(GenRecipe), "PostProcessProduct"), nameof(EventPatches.ItemCrafted), false);
            Add("mining.completed", "A pawn mined out a rock or ore cell. Payload: def, x, z, pawn.", false, () => M(typeof(Mineable), "DestroyMined"), nameof(EventPatches.MiningCompleted), true);
            Add("construction.started", "A blueprint became a frame (or a finished building) because a pawn began building. Payload: frame, def, pawn.", false, () => M(typeof(Blueprint), "TryReplaceWithSolidThing"), nameof(EventPatches.ConstructionStarted), false);
            Add("building.deconstructed", "A pawn finished deconstructing a building. Payload: pawn, thing, def, x, z.", false, () => M(typeof(Pawn_RecordsTracker), "Increment"), nameof(EventPatches.Deconstructed), false);
            Add("thing.repaired", "A pawn finished repairing a building. Payload: pawn, thing, def.", false, () => M(typeof(Pawn_RecordsTracker), "Increment"), nameof(EventPatches.Repaired), false);
            Add("thing.hauled", "A pawn picked up a thing to haul it. Payload: pawn, thing, def.", false, () => M(typeof(Pawn_RecordsTracker), "Increment"), nameof(EventPatches.Hauled), false);
            Add("thing.dropped", "A pawn put down what it was carrying. Payload: pawn, thing, def, x, z.", false, () => M(typeof(Pawn_CarryTracker), "TryDropCarriedThing", 5), nameof(EventPatches.Dropped), false);

            // health
            Add("hediff.healed", "An injury was healed away completely. Payload: pawn, hediff.", false, () => M(typeof(Hediff_Injury), "Heal"), nameof(EventPatches.HediffHealed), false);
            Add("immunity.gained", "A pawn became fully immune to a disease it was fighting. Payload: pawn, disease.", false, () => M(typeof(ImmunityRecord), "ImmunityTickInterval"), nameof(EventPatches.ImmunityGained), false);

            // mind and social
            Add("thought.lost", "A memory thought was removed. Payload: pawn, thought.", false, () => M(typeof(MemoryThoughtHandler), "RemoveMemory"), nameof(EventPatches.ThoughtLost), true);
            Add("social_fight.started", "Two pawns started a social fight. Payload: pawn, other.", false, () => M(typeof(MentalState), "PostStart"), nameof(EventPatches.SocialFightStarted), false);

            // skills
            Add("skill.learning_saturated", "A pawn reached the daily full-rate learning limit for a skill. Payload: pawn, skill.", false, () => M(typeof(SkillRecord), "Learn"), nameof(EventPatches.LearningSaturated), false);

            // combat
            Add("fire.started", "A fire started burning. Payload: fire, map, x, z.", false, () => M(typeof(Fire), "SpawnSetup"), nameof(EventPatches.FireStarted), false);
            Add("fire.ended", "A fire went out. Payload: fire, map, x, z.", false, () => M(typeof(Fire), "DeSpawn"), nameof(EventPatches.FireEnded), true);

            // world and map
            Add("world.generated", "A new world was generated. Payload: seed.", false, () => M(typeof(WorldGenerator), "GenerateWorld"), nameof(EventPatches.WorldGenerated), false);
            Add("caravan.arrived", "A caravan reached the end of its path. Payload: caravan, tile.", false, () => M(typeof(Caravan_PathFollower), "PatherArrived"), nameof(EventPatches.CaravanArrived), false);
            Add("site.visited", "A caravan arrived at a site it was sent to. Payload: caravan, site, def.", false, () => M(typeof(CaravanArrivalAction_VisitSite), "Arrived"), nameof(EventPatches.SiteVisited), false);

            // economy
            Add("goods.delivered", "A trade ship or order dropped goods onto the map. Payload: thing, map, x, z.", false, () => M(typeof(TradeUtility), "SpawnDropPod"), nameof(EventPatches.GoodsDelivered), false);
            Add("silver.changed", "The colony's silver changed since the last game hour. Payload: silver, delta.", false, () => M(typeof(TickManager), "DoSingleTick"), nameof(EventPatches.SilverChanged), false);

            // research
            Add("research.milestone", "The current research project passed 25, 50 or 75 percent. Payload: project, percent.", false, () => M(typeof(ResearchManager), "ResearchPerformed"), nameof(EventPatches.ResearchMilestone), false);

            // buildings
            Add("room.changed", "Rooms on a map were rebuilt after a wall, door or roof changed. Payload: map.", false, () => M(typeof(RegionAndRoomUpdater), "NotifyAffectedDistrictsAndRoomsAndUpdateTemperatureVacuum"), nameof(EventPatches.RoomChanged), false);

            // factions
            Add("faction.relation_changed", "The relation kind between two factions changed (neutral, hostile, ally). Payload: faction, other, previous, kind.", false, () => M(typeof(Faction), "Notify_RelationKindChanged"), nameof(EventPatches.RelationChanged), false);
            Add("faction.leader_changed", "A faction got a new leader. Payload: faction, leader.", false, () => M(typeof(Faction), "TryGenerateNewLeader"), nameof(EventPatches.LeaderChanged), false);

            // ui
            Add("gizmo.clicked", "The player clicked a gizmo. Payload: label, class.", false, () => M(typeof(Command), "ProcessInput"), nameof(EventPatches.GizmoClicked), false);
            Add("key.pressed", "A key went down in the game window. Hot. Payload: key, shift, control, alt.", true, () => M(typeof(UIRoot), "UIRootOnGUI"), nameof(EventPatches.KeyPressed), false);
        }
    }

    internal static partial class EventPatches
    {
        private static readonly FieldInfo RecordsPawn = AccessTools.Field(typeof(Pawn_RecordsTracker), "pawn");
        private static readonly FieldInfo CarryPawn = AccessTools.Field(typeof(Pawn_CarryTracker), "pawn");
        private static readonly FieldInfo JobTrackerCurJob = AccessTools.Field(typeof(Pawn_JobTracker), "curJob");
        private static readonly FieldInfo UpdaterMap = AccessTools.Field(typeof(RegionAndRoomUpdater), "map");
        private static readonly FieldInfo PathCaravan = AccessTools.Field(typeof(Caravan_PathFollower), "caravan");
        private static readonly FieldInfo SkillXpToday = AccessTools.Field(typeof(SkillRecord), "xpSinceMidnight");
        private static readonly FieldInfo SkillDailyLimit = AccessTools.Field(typeof(SkillRecord), "MaxFullRateXpPerDay");
        private static readonly FieldInfo FightOther = AccessTools.Field(typeof(MentalState_SocialFighting), "otherPawn");
        private static readonly FieldInfo VisitSite = AccessTools.Field(typeof(CaravanArrivalAction_VisitSite), "site");

        public static void PawnLeftColony(Pawn __instance, object[] __args)
        {
            Faction next = Arg<Faction>(__args, 0);
            if (__instance == null || !Playing || __instance.Faction == null || !__instance.Faction.IsPlayer || next == null || next.IsPlayer) return;
            Emit("pawn.left_colony", sb => { Field(sb, "pawn", __instance, true); Field(sb, "to", next); });
        }

        public static void PawnEscaped(Pawn initiator, List<Pawn> escapingPrisoners)
        {
            if (escapingPrisoners == null || escapingPrisoners.Count == 0 || !Playing) return;
            foreach (Pawn p in escapingPrisoners.ToList())
                Emit("pawn.escaped", sb => { Field(sb, "pawn", p, true); Field(sb, "initiator", initiator); });
        }

        public static void WorkCompleted(Pawn_JobTracker __instance, JobCondition condition)
        {
            if (condition != JobCondition.Succeeded || !Playing) return;
            Job job = JobTrackerCurJob?.GetValue(__instance) as Job;
            Pawn pawn = JobPawn(__instance);
            if (job == null || pawn == null || job.workGiverDef == null) return;
            Emit("work.completed", sb => { Field(sb, "pawn", pawn, true); Field(sb, "job", job.def); Field(sb, "work_type", job.workGiverDef.workType); });
        }

        public static void ItemCrafted(Thing __result, object[] __args)
        {
            if (__result == null || !Playing) return;
            Emit("item.crafted", sb => { Field(sb, "thing", __result, true); Field(sb, "recipe", Arg<RecipeDef>(__args, 1)); Field(sb, "pawn", Arg<Pawn>(__args, 2)); });
        }

        public static void MiningCompleted(Mineable __instance, object[] __args)
        {
            if (__instance == null || !Playing) return;
            IntVec3 c = __instance.Position;
            Emit("mining.completed", sb => { Field(sb, "def", __instance.def, true); Field(sb, "x", c.x); Field(sb, "z", c.z); Field(sb, "pawn", Arg<Pawn>(__args, 0)); });
        }

        public static void ConstructionStarted(Pawn workerPawn, Thing createdThing)
        {
            if (createdThing == null || !Playing) return;
            Emit("construction.started", sb =>
            {
                Field(sb, "frame", createdThing, true);
                Field(sb, "def", createdThing is Frame f ? f.def.entityDefToBuild : createdThing.def);
                Field(sb, "pawn", workerPawn);
            });
        }

        // The pawn's current job target is the thing the record step is about.
        private static void RecordStep(string name, Pawn_RecordsTracker tracker, object[] args, RecordDef wanted, bool withCell)
        {
            if (!Playing || wanted == null || Arg<RecordDef>(args, 0) != wanted) return;
            Pawn pawn = Of(RecordsPawn, tracker);
            Job job = pawn?.CurJob;
            if (pawn == null || job == null) return;
            Thing thing = job.targetA.HasThing ? job.targetA.Thing : null;
            IntVec3 cell = job.targetA.Cell;
            Emit(name, sb =>
            {
                Field(sb, "pawn", pawn, true);
                Field(sb, "thing", thing);
                Field(sb, "def", thing?.def);
                if (withCell) { Field(sb, "x", cell.x); Field(sb, "z", cell.z); }
            });
        }

        public static void Deconstructed(Pawn_RecordsTracker __instance, object[] __args) => RecordStep("building.deconstructed", __instance, __args, RecordDefOf.ThingsDeconstructed, true);

        public static void Repaired(Pawn_RecordsTracker __instance, object[] __args) => RecordStep("thing.repaired", __instance, __args, RecordDefOf.ThingsRepaired, false);

        public static void Hauled(Pawn_RecordsTracker __instance, object[] __args) => RecordStep("thing.hauled", __instance, __args, RecordDefOf.ThingsHauled, false);

        public static void Dropped(Pawn_CarryTracker __instance, Thing resultingThing, bool __result)
        {
            if (!__result || !Playing) return;
            Pawn pawn = Of(CarryPawn, __instance);
            if (pawn == null) return;
            IntVec3 c = resultingThing != null && resultingThing.Spawned ? resultingThing.Position : pawn.Position;
            Emit("thing.dropped", sb => { Field(sb, "pawn", pawn, true); Field(sb, "thing", resultingThing); Field(sb, "def", resultingThing?.def); Field(sb, "x", c.x); Field(sb, "z", c.z); });
        }

        public static void HediffHealed(Hediff_Injury __instance)
        {
            if (__instance == null || __instance.pawn == null || __instance.Severity > 0.0001f || !Playing) return;
            Emit("hediff.healed", sb => { Field(sb, "pawn", __instance.pawn, true); Field(sb, "hediff", __instance); });
        }

        // true means the record was last seen below full immunity, so crossing 1 now is a real gain and not a save that loaded already immune
        private static readonly ConditionalWeakTable<ImmunityRecord, object> ImmunityBelow = new ConditionalWeakTable<ImmunityRecord, object>();
        private static readonly object BelowMark = new object();

        public static void ImmunityGained(ImmunityRecord __instance, Pawn pawn)
        {
            if (__instance == null || pawn == null || !Playing) return;
            bool wasBelow = ImmunityBelow.TryGetValue(__instance, out _);
            if (__instance.immunity < 1f)
            {
                if (!wasBelow) ImmunityBelow.Add(__instance, BelowMark);
                return;
            }

            if (!wasBelow) return;
            ImmunityBelow.Remove(__instance);
            Emit("immunity.gained", sb => { Field(sb, "pawn", pawn, true); Field(sb, "disease", __instance.hediffDef); });
        }

        public static void ThoughtLost(MemoryThoughtHandler __instance, object[] __args)
        {
            Pawn p = Of(MemoryPawn, __instance);
            var th = Arg<Thought_Memory>(__args, 0);
            if (p == null || th == null || !Playing) return;
            Emit("thought.lost", sb => { Field(sb, "pawn", p, true); Field(sb, "thought", th.def); });
        }

        public static void SocialFightStarted(MentalState __instance)
        {
            if (!(__instance is MentalState_SocialFighting fight) || __instance.pawn == null || !Playing) return;
            Emit("social_fight.started", sb => { Field(sb, "pawn", fight.pawn, true); Field(sb, "other", FightOther?.GetValue(fight) as Pawn); });
        }

        private static readonly ConditionalWeakTable<SkillRecord, object> SkillSaturated = new ConditionalWeakTable<SkillRecord, object>();

        public static void LearningSaturated(SkillRecord __instance)
        {
            Pawn p = Of(SkillPawn, __instance);
            if (p == null || SkillXpToday == null || SkillDailyLimit == null || !Playing) return;
            bool saturated = (float)SkillXpToday.GetValue(__instance) > Convert.ToSingle(SkillDailyLimit.GetValue(null));
            bool was = SkillSaturated.TryGetValue(__instance, out _);
            if (!saturated) { if (was) SkillSaturated.Remove(__instance); return; }
            if (was) return;
            SkillSaturated.Add(__instance, BelowMark);
            Emit("skill.learning_saturated", sb => { Field(sb, "pawn", p, true); Field(sb, "skill", __instance.def); });
        }

        private static void FirePayload(string name, Fire f)
        {
            if (f == null || !Playing) return;
            Emit(name, sb => { Field(sb, "fire", f, true); Field(sb, "map", f.Map); Field(sb, "x", f.Position.x); Field(sb, "z", f.Position.z); });
        }

        public static void FireStarted(Fire __instance, bool respawningAfterLoad)
        {
            if (!respawningAfterLoad) FirePayload("fire.started", __instance);
        }

        public static void FireEnded(Fire __instance) => FirePayload("fire.ended", __instance);

        public static void WorldGenerated(World __result)
        {
            if (__result == null) return;
            Emit("world.generated", sb => Field(sb, "seed", __result.info?.seedString, true));
        }

        public static void CaravanArrived(Caravan_PathFollower __instance)
        {
            Caravan c = PathCaravan?.GetValue(__instance) as Caravan;
            if (c == null || !Playing) return;
            Emit("caravan.arrived", sb => { Field(sb, "caravan", c, true); Field(sb, "tile", (int)c.Tile); });
        }

        public static void SiteVisited(CaravanArrivalAction_VisitSite __instance, Caravan caravan)
        {
            Site site = VisitSite?.GetValue(__instance) as Site;
            if (site == null || caravan == null || !Playing) return;
            Emit("site.visited", sb => { Field(sb, "caravan", caravan, true); Field(sb, "site", site); Field(sb, "def", site.def); });
        }

        public static void GoodsDelivered(Map map, Thing t, object[] __args)
        {
            if (t == null || !Playing) return;
            IntVec3 c = __args != null && __args.Length > 0 && __args[0] is IntVec3 v ? v : IntVec3.Invalid;
            Emit("goods.delivered", sb => { Field(sb, "thing", t, true); Field(sb, "map", map); Field(sb, "x", c.x); Field(sb, "z", c.z); });
        }

        private static int lastSilver = -1;

        public static void SilverChanged()
        {
            if (!Boundary(GenDate.TicksPerHour)) return;
            int now = 0;
            foreach (Map m in Find.Maps)
                if (m.IsPlayerHome && m.resourceCounter != null) now += m.resourceCounter.Silver;
            int before = lastSilver;
            lastSilver = now;
            if (before < 0 || before == now) return;
            Emit("silver.changed", sb => { Field(sb, "silver", now, true); Field(sb, "delta", now - before); });
        }

        private static readonly Dictionary<string, int> ResearchBucket = new Dictionary<string, int>();

        public static void ResearchMilestone()
        {
            if (!Playing || Find.ResearchManager == null) return;
            ResearchProjectDef p = Find.ResearchManager.GetProject();
            if (p == null) return;
            int bucket = (int)(p.ProgressPercent * 4f);
            ResearchBucket.TryGetValue(p.defName, out int last);
            if (bucket <= last || bucket >= 4) { if (bucket < last) ResearchBucket[p.defName] = bucket; return; }
            ResearchBucket[p.defName] = bucket;
            Emit("research.milestone", sb => { Field(sb, "project", p, true); Field(sb, "percent", bucket * 25); });
        }

        public static void RoomChanged(RegionAndRoomUpdater __instance)
        {
            Map map = UpdaterMap?.GetValue(__instance) as Map;
            if (map == null || !Playing) return;
            Emit("room.changed", sb => Field(sb, "map", map, true));
        }

        public static void RelationChanged(Faction __instance, object[] __args)
        {
            Faction other = Arg<Faction>(__args, 0);
            if (other == null || !Playing) return;
            object previous = __args.Length > 1 ? __args[1] : null;
            Emit("faction.relation_changed", sb => { Field(sb, "faction", __instance, true); Field(sb, "other", other); Field(sb, "previous", previous?.ToString()); Field(sb, "kind", __instance.RelationKindWith(other).ToString()); });
        }

        public static void LeaderChanged(Faction __instance)
        {
            if (__instance?.leader == null || !Playing) return;
            Emit("faction.leader_changed", sb => { Field(sb, "faction", __instance, true); Field(sb, "leader", __instance.leader); });
        }

        public static void GizmoClicked(Command __instance)
        {
            if (!Playing || __instance == null) return;
            Emit("gizmo.clicked", sb => { Field(sb, "label", __instance.Label, true); Field(sb, "class", __instance.GetType().Name); });
        }

        public static void KeyPressed()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None || !Playing) return;
            Emit("key.pressed", sb => { Field(sb, "key", e.keyCode.ToString(), true); Field(sb, "shift", e.shift); Field(sb, "control", e.control); Field(sb, "alt", e.alt); });
        }
    }
}
