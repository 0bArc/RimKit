using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimLuaKit
{
    // Saves which pawns we made draftable (hediff + this component).
    public class GameComponent_PawnControl : GameComponent
    {
        public List<int> ControlledIds = new List<int>();

        public GameComponent_PawnControl(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ControlledIds, "rimlua_controlled_pawns", LookMode.Value);
            ControlledIds ??= new List<int>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PawnControl.RestoreFromSave(ControlledIds);
        }

        public static GameComponent_PawnControl GetOrCreate()
        {
            Game g = Current.Game;
            if (g == null)
            {
                return null;
            }

            var c = g.GetComponent<GameComponent_PawnControl>();
            if (c == null)
            {
                c = new GameComponent_PawnControl(g);
                g.components.Add(c);
            }

            return c;
        }

        public void WriteIds(HashSet<int> ids)
        {
            ControlledIds = ids.ToList();
        }
    }

    // Draft/control for non-humanlike pawns (entities, etc.).
    internal static class PawnControl
    {
        private static readonly HashSet<int> ControlledIds = new HashSet<int>();
        private static HediffDef markerDef;

        public static bool IsControlled(Pawn p)
        {
            if (p == null)
            {
                return false;
            }

            if (ControlledIds.Contains(p.thingIDNumber))
            {
                return true;
            }

            if (HasMarker(p))
            {
                ControlledIds.Add(p.thingIDNumber);
                return true;
            }

            // Player ToolUsers without a marker (older saves).
            if (p.Faction?.IsPlayer == true
                && p.RaceProps != null
                && !p.RaceProps.Humanlike
                && p.RaceProps.intelligence == Intelligence.ToolUser)
            {
                EnsureDrafter(p);
                EnsureMarker(p);
                if (p.playerSettings == null)
                {
                    p.playerSettings = new Pawn_PlayerSettings(p);
                }

                ControlledIds.Add(p.thingIDNumber);
                Persist();
                return true;
            }

            return false;
        }

        public static void MakeControllable(Pawn p)
        {
            if (p == null || p.Destroyed)
            {
                return;
            }

            TryLeaveLord(p);
            if (p.Faction != Faction.OfPlayer)
            {
                p.SetFaction(Faction.OfPlayer);
            }

            EnsureDrafter(p);
            if (p.playerSettings == null)
            {
                p.playerSettings = new Pawn_PlayerSettings(p);
            }

            EnsureMarker(p);
            ControlledIds.Add(p.thingIDNumber);
            Persist();

            try
            {
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
                p.jobs?.ClearQueuedJobs();
            }
            catch
            {
            }

            if (p.drafter != null)
            {
                p.drafter.Drafted = true;
            }
        }

        public static void ReleaseControl(Pawn p)
        {
            if (p == null)
            {
                return;
            }

            ControlledIds.Remove(p.thingIDNumber);
            RemoveMarker(p);
            Persist();
            if (p.drafter != null)
            {
                p.drafter.Drafted = false;
            }
        }

        public static void RestoreFromSave(List<int> ids)
        {
            ControlledIds.Clear();
            if (ids != null)
            {
                foreach (int id in ids)
                {
                    ControlledIds.Add(id);
                }
            }

            // Older saves with marker hediff.
            if (Current.Game?.Maps == null)
            {
                return;
            }

            foreach (Map map in Current.Game.Maps)
            {
                if (map?.mapPawns?.AllPawns == null)
                {
                    continue;
                }

                foreach (Pawn p in map.mapPawns.AllPawns)
                {
                    if (p == null)
                    {
                        continue;
                    }

                    bool marked = HasMarker(p);
                    bool listed = ControlledIds.Contains(p.thingIDNumber);
                    if (!marked && !listed)
                    {
                        continue;
                    }

                    TryLeaveLord(p);
                    if (p.Faction != Faction.OfPlayer)
                    {
                        p.SetFaction(Faction.OfPlayer);
                    }

                    EnsureDrafter(p);
                    if (p.playerSettings == null)
                    {
                        p.playerSettings = new Pawn_PlayerSettings(p);
                    }

                    EnsureMarker(p);
                    ControlledIds.Add(p.thingIDNumber);
                    if (p.drafter != null)
                    {
                        p.drafter.Drafted = true;
                    }
                }
            }

            Persist();
            Log.Message("[RimKit] Restored controllable pawns: " + ControlledIds.Count);
        }

        private static void Persist()
        {
            GameComponent_PawnControl.GetOrCreate()?.WriteIds(ControlledIds);
        }

        private static HediffDef MarkerDef()
        {
            if (markerDef != null)
            {
                return markerDef;
            }

            markerDef = DefDatabase<HediffDef>.GetNamedSilentFail("RimLua_Controllable");
            if (markerDef == null)
            {
                markerDef = new HediffDef
                {
                    defName = "RimLua_Controllable",
                    label = "rimlua controlled",
                    description = "RimKit marker: player can draft and order this pawn.",
                    hediffClass = typeof(Hediff),
                    defaultLabelColor = new Color(0.6f, 0.8f, 1f),
                    isBad = false,
                    everCurableByItem = false,
                    tendable = false,
                    displayWound = false,
                    maxSeverity = 1f,
                    initialSeverity = 1f,
                };
                DefDatabase<HediffDef>.Add(markerDef);
            }

            return markerDef;
        }

        private static bool HasMarker(Pawn p)
        {
            HediffDef def = MarkerDef();
            return p.health?.hediffSet?.HasHediff(def) == true;
        }

        private static void EnsureMarker(Pawn p)
        {
            HediffDef def = MarkerDef();
            if (p.health?.hediffSet == null)
            {
                return;
            }

            if (!p.health.hediffSet.HasHediff(def))
            {
                p.health.AddHediff(def);
            }
        }

        private static void RemoveMarker(Pawn p)
        {
            HediffDef def = MarkerDef();
            Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (h != null)
            {
                p.health.RemoveHediff(h);
            }
        }

        private static void EnsureDrafter(Pawn p)
        {
            if (p.drafter == null)
            {
                p.drafter = new Pawn_DraftController(p);
            }
        }

        private static void TryLeaveLord(Pawn p)
        {
            try
            {
                Lord lord = p.GetLord();
                if (lord != null)
                {
                    lord.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
                }
            }
            catch
            {
            }
        }

        public static bool IsCastingOrWarming(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.stances?.curStance is Stance_Warmup)
            {
                return true;
            }

            JobDef cur = pawn.CurJobDef;
            if (cur != null && cur.abilityCasting)
            {
                return true;
            }

            if (pawn.abilities?.abilities != null)
            {
                for (int i = 0; i < pawn.abilities.abilities.Count; i++)
                {
                    Ability a = pawn.abilities.abilities[i];
                    if (a != null && a.Casting)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Thing FindHostileTarget(Pawn pawn, float maxDist)
        {
            if (pawn?.Map == null)
            {
                return null;
            }

            Thing best = null;
            float bestDist = maxDist * maxDist;
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return null;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other.Dead || other.Downed || other == pawn)
                {
                    continue;
                }

                if (other.Faction == null || other.Faction.IsPlayer)
                {
                    continue;
                }

                bool hostile = other.HostileTo(pawn);
                if (!hostile && other.Faction.HostileTo(Faction.OfPlayer))
                {
                    hostile = true;
                }

                if (!hostile)
                {
                    continue;
                }

                float d = (other.Position - pawn.Position).LengthHorizontalSquared;
                if (d > bestDist)
                {
                    continue;
                }

                bestDist = d;
                best = other;
            }

            return best;
        }

        public static bool TryEngage(Pawn pawn)
        {
            if (pawn?.jobs == null || !pawn.Spawned || pawn.Downed || pawn.Dead)
            {
                return false;
            }

            if (IsCastingOrWarming(pawn))
            {
                return false;
            }

            EnsureDrafter(pawn);
            if (pawn.drafter != null && !pawn.drafter.Drafted)
            {
                pawn.drafter.Drafted = true;
            }

            Thing hostile = FindHostileTarget(pawn, 55f);
            if (hostile == null)
            {
                return false;
            }

            LocalTargetInfo target = hostile;
            float dist = pawn.Position.DistanceTo(hostile.Position);

            if (pawn.CurJob != null
                && (pawn.CurJob.def == JobDefOf.AttackMelee || pawn.CurJob.def == JobDefOf.AttackStatic)
                && pawn.CurJob.targetA.Thing == hostile)
            {
                return true;
            }

            // Melee if adjacent.
            if (dist <= 2.2f)
            {
                try
                {
                    if (pawn.meleeVerbs != null && pawn.meleeVerbs.TryMeleeAttack(hostile))
                    {
                        return true;
                    }

                    if (pawn.TryStartAttack(target))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            Job job = BuildEngageJob(pawn, hostile, dist);
            if (job == null)
            {
                return false;
            }

            try
            {
                job.playerForced = true;
                pawn.jobs.StartJob(
                    job,
                    JobCondition.InterruptForced,
                    null,
                    false,
                    true,
                    null,
                    JobTag.DraftedOrder);
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] TryEngage StartJob failed: " + e.Message);
                return false;
            }
        }

        public static Job BuildEngageJob(Pawn pawn)
        {
            Thing hostile = FindHostileTarget(pawn, 55f);
            if (hostile == null || pawn == null)
            {
                return null;
            }

            float dist = pawn.Position.DistanceTo(hostile.Position);
            return BuildEngageJob(pawn, hostile, dist);
        }

        private static Job BuildEngageJob(Pawn pawn, Thing hostile, float dist)
        {
            LocalTargetInfo lt = hostile;

            if (dist > 1.5f && pawn.abilities?.abilities != null)
            {
                for (int i = 0; i < pawn.abilities.abilities.Count; i++)
                {
                    Ability ability = pawn.abilities.abilities[i];
                    if (ability?.def == null || !ability.CanCast)
                    {
                        continue;
                    }

                    try
                    {
                        if (!ability.CanApplyOn(lt))
                        {
                            continue;
                        }

                        Job abilityJob = ability.GetJob(lt, lt);
                        if (abilityJob != null)
                        {
                            return abilityJob;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (dist <= 14f)
            {
                return JobMaker.MakeJob(JobDefOf.AttackMelee, hostile);
            }

            IntVec3 cell = hostile.Position;
            if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
            {
                if (!RCellFinder.TryFindRandomCellNearWith(
                        cell,
                        c => c.Walkable(pawn.Map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly),
                        pawn.Map,
                        out cell,
                        1,
                        8))
                {
                    cell = hostile.Position;
                }
            }

            Job go = JobMaker.MakeJob(JobDefOf.Goto, cell);
            go.locomotionUrgency = LocomotionUrgency.Sprint;
            return go;
        }

        public static void OrderSelectedToCell(IntVec3 cell)
        {
            List<Pawn> sel = Find.Selector?.SelectedPawns;
            if (sel == null || sel.Count == 0)
            {
                return;
            }

            Map map = Find.CurrentMap;
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            for (int i = 0; i < sel.Count; i++)
            {
                Pawn p = sel[i];
                if (p == null || !IsControlled(p) || p.drafter == null || !p.Drafted)
                {
                    continue;
                }

                if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    continue;
                }

                if (p.drafter != null)
                {
                    p.drafter.Drafted = true;
                }

                Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.locomotionUrgency = LocomotionUrgency.Sprint;
                p.jobs?.TryTakeOrderedJob(job, JobTag.DraftedOrder);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonistPlayerControlled), MethodType.Getter)]
    internal static class Patch_PawnControl_IsColonistPlayerControlled
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || __instance == null || !__instance.Spawned)
            {
                return;
            }

            if (PawnControl.IsControlled(__instance) && __instance.Faction?.IsPlayer == true)
            {
                if (__instance.drafter == null)
                {
                    __instance.drafter = new Pawn_DraftController(__instance);
                }

                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonist), MethodType.Getter)]
    internal static class Patch_PawnControl_IsColonist
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || __instance == null)
            {
                return;
            }

            if (PawnControl.IsControlled(__instance) && __instance.Faction?.IsPlayer == true)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "Tick")]
    internal static class Patch_PawnControl_Tick
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance == null || !__instance.Spawned || __instance.Dead)
            {
                return;
            }

            if ((Find.TickManager.TicksGame + __instance.thingIDNumber) % 20 != 0)
            {
                return;
            }

            if (!PawnControl.IsControlled(__instance))
            {
                return;
            }

            PawnControl.TryEngage(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TickRare))]
    internal static class Patch_PawnControl_TickRare
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance == null || !__instance.Spawned || __instance.Dead)
            {
                return;
            }

            if (!PawnControl.IsControlled(__instance))
            {
                return;
            }

            PawnControl.TryEngage(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    internal static class Patch_PawnControl_DetermineNextJob
    {
        public static bool Prefix(Pawn_JobTracker __instance, ref ThinkResult __result, bool ignoreQueue)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || !PawnControl.IsControlled(pawn))
            {
                return true;
            }

            if (PawnControl.IsCastingOrWarming(pawn))
            {
                return true;
            }

            JobQueue queue = Traverse.Create(__instance).Field("jobQueue").GetValue<JobQueue>();
            if (!ignoreQueue && queue != null && queue.Count > 0)
            {
                return true;
            }

            Job engage = PawnControl.BuildEngageJob(pawn);
            if (engage != null)
            {
                engage.playerForced = true;
                __result = new ThinkResult(engage, null, null, false);
                return false;
            }

            EnsureDrafterLocal(pawn);
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            __result = new ThinkResult(wait, null, null, false);
            return false;
        }

        private static void EnsureDrafterLocal(Pawn pawn)
        {
            if (pawn.drafter == null)
            {
                pawn.drafter = new Pawn_DraftController(pawn);
            }

            if (pawn.drafter != null)
            {
                pawn.drafter.Drafted = true;
            }
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "ShouldGenerateFloatMenuForPawn")]
    internal static class Patch_PawnControl_FloatMenu
    {
        public static void Postfix(Pawn pawn, ref AcceptanceReport __result)
        {
            if (__result.Accepted || pawn == null)
            {
                return;
            }

            if (PawnControl.IsControlled(pawn) && pawn.Drafted && pawn.Spawned && !pawn.Downed)
            {
                __result = AcceptanceReport.WasAccepted;
            }
        }
    }

    // Drop tend/rescue/carry menus for controlled non-humans.
    [HarmonyPatch(typeof(FloatMenuOptionProvider), nameof(FloatMenuOptionProvider.SelectedPawnValid))]
    internal static class Patch_PawnControl_NoCareFloatMenus
    {
        private static readonly HashSet<Type> BlockedProviders = new HashSet<Type>
        {
            typeof(FloatMenuOptionProvider_DraftedTend),
            typeof(FloatMenuOptionProvider_RescuePawn),
            typeof(FloatMenuOptionProvider_CarryPawn),
            typeof(FloatMenuOptionProvider_CarryDeathrestingToCasket),
        };

        internal static bool IsArmyOnly(Pawn pawn)
        {
            if (pawn?.RaceProps == null)
            {
                return false;
            }

            if (pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (ApiAnomaly.LooksLikeEntity(pawn))
            {
                return true;
            }

            return PawnControl.IsControlled(pawn)
                || (pawn.Faction?.IsPlayer == true && pawn.RaceProps.intelligence == Intelligence.ToolUser);
        }

        public static void Postfix(FloatMenuOptionProvider __instance, Pawn pawn, ref bool __result)
        {
            if (!__result || __instance == null || pawn == null)
            {
                return;
            }

            if (!IsArmyOnly(pawn))
            {
                return;
            }

            if (BlockedProviders.Contains(__instance.GetType()))
            {
                __result = false;
            }
        }
    }

    // Right-click empty ground moves drafted army units.
    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
    internal static class Patch_PawnControl_RightClickMove
    {
        public static void Postfix()
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1)
            {
                return;
            }

            if (Find.WindowStack != null && Find.WindowStack.Count > 0)
            {
                Window top = Find.WindowStack[Find.WindowStack.Count - 1];
                if (top is FloatMenu || (top != null && top.absorbInputAroundWindow))
                {
                    return;
                }
            }

            List<Pawn> sel = Find.Selector?.SelectedPawns;
            if (sel == null || sel.Count == 0)
            {
                return;
            }

            bool anyArmy = false;
            for (int i = 0; i < sel.Count; i++)
            {
                if (PawnControl.IsControlled(sel[i]) && sel[i].Drafted)
                {
                    anyArmy = true;
                    break;
                }
            }

            if (!anyArmy)
            {
                return;
            }

            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }

            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map))
            {
                return;
            }

            // Leave pawn clicks for the float menu.
            List<Thing> at = map.thingGrid.ThingsListAtFast(cell);
            if (at != null)
            {
                for (int i = 0; i < at.Count; i++)
                {
                    if (at[i] is Pawn)
                    {
                        return;
                    }
                }
            }

            PawnControl.OrderSelectedToCell(cell);
            Event.current.Use();
        }
    }
}
