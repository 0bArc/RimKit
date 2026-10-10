using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimKit
{
    /// <summary>
    /// Named game events delivered to Lua with a payload table. A Harmony patch is installed on the first
    /// subscription and removed on the last unsubscribe, so unused events cost nothing.
    /// Names follow UNC: &lt;domain&gt;.&lt;past_tense_verb&gt;.
    /// </summary>
    internal static partial class EventCatalog
    {
        internal sealed class EventDef
        {
            public string Name;
            public string Description;
            public bool Hot;
            public Func<MethodBase> Target;
            public string PatchMethod;
            public bool IsPrefix;
            public int Subscribers;
            public MethodBase Installed;
        }

        private static readonly List<EventDef> All = new List<EventDef>();
        private static readonly Dictionary<string, EventDef> ByName = new Dictionary<string, EventDef>(StringComparer.Ordinal);
        private static Harmony harmony;

        public static void Init(Harmony harmonyInstance)
        {
            harmony = harmonyInstance;
            if (All.Count > 0)
            {
                return;
            }

            // thing
            Add("thing.spawned", "A thing was spawned on a map after load. Payload: thing, map, def.", false,
                () => AccessTools.Method(typeof(Thing), nameof(Thing.SpawnSetup), new[] { typeof(Map), typeof(bool) }), nameof(EventPatches.ThingSpawned), false);
            Add("thing.despawned", "A thing is about to leave the map. Payload: thing, mode.", false,
                () => AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn), new[] { typeof(DestroyMode) }), nameof(EventPatches.ThingDespawned), true);
            Add("thing.destroyed", "A thing is about to be destroyed. Payload: thing, mode.", false,
                () => AccessTools.Method(typeof(Thing), nameof(Thing.Destroy), new[] { typeof(DestroyMode) }), nameof(EventPatches.ThingDestroyed), true);
            Add("thing.damaged", "A thing took damage. Hot. Payload: thing, damage, dealt.", true,
                () => AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage), new[] { typeof(DamageInfo) }), nameof(EventPatches.ThingDamaged), false);

            // pawn
            Add("pawn.spawned", "A pawn was spawned after load. Payload: pawn, map.", false,
                () => AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup), new[] { typeof(Map), typeof(bool) }), nameof(EventPatches.PawnSpawned), false);
            Add("pawn.died", "A pawn is dying. Payload: pawn, damage, culprit.", false,
                () => AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) }), nameof(EventPatches.PawnDied), true);
            Add("pawn.damaged", "A pawn took damage. Hot. Payload: pawn, damage, dealt. Use a filter, for example { humanlike = true }.", true,
                () => AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage), new[] { typeof(DamageInfo) }), nameof(EventPatches.PawnDamaged), false);
            Add("pawn.resurrected", "A pawn was resurrected. Payload: pawn.", false,
                () => AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_Resurrected), new[] { typeof(bool), typeof(float) }), nameof(EventPatches.PawnResurrected), false);

            // health
            Add("hediff.added", "A hediff was added to a pawn. Payload: pawn, hediff, part.", false,
                () => AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
                    new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) }), nameof(EventPatches.HediffAdded), false);
            Add("hediff.removed", "A hediff is about to be removed. Payload: pawn, hediff.", false,
                () => AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff), new[] { typeof(Hediff) }), nameof(EventPatches.HediffRemoved), true);

            // jobs and mind
            Add("job.started", "A pawn started a job. Payload: pawn, job, target.", false,
                () => AccessTools.GetDeclaredMethods(typeof(Pawn_JobTracker)).FirstOrDefault(m => m.Name == nameof(Pawn_JobTracker.StartJob)), nameof(EventPatches.JobStarted), false);
            Add("job.ended", "A pawn is ending its current job. Payload: pawn, job, condition.", false,
                () => AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob), new[] { typeof(JobCondition), typeof(bool), typeof(bool) }), nameof(EventPatches.JobEnded), true);
            Add("mental_state.started", "A mental state was requested. Payload: pawn, state, started.", false,
                () => AccessTools.GetDeclaredMethods(typeof(MentalStateHandler)).FirstOrDefault(m => m.Name == nameof(MentalStateHandler.TryStartMentalState)), nameof(EventPatches.MentalStateStarted), false);

            // time and game lifecycle
            Add("time.speed_changed", "The game speed changed. Payload: speed.", false,
                () => AccessTools.PropertySetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed)), nameof(EventPatches.SpeedChanged), false);
            Add("time.ticked", "One game tick ran. Hot. Payload: ticks.", true,
                () => AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)), nameof(EventPatches.Ticked), false);
            Add("game.new", "A new game was initialised.", false,
                () => AccessTools.Method(typeof(Game), nameof(Game.InitNewGame)), nameof(EventPatches.GameNew), false);
            Add("game.loaded", "A save was loaded.", false,
                () => AccessTools.Method(typeof(Game), nameof(Game.LoadGame)), nameof(EventPatches.GameLoaded), false);
            Add("game.ready", "The game finished initialising and is playable.", false,
                () => AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)), nameof(EventPatches.GameReady), false);
            Add("game.saving", "The game is about to save. Payload: file.", false,
                () => AccessTools.Method(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame), new[] { typeof(string) }), nameof(EventPatches.GameSaving), true);

            // map
            Add("map.ready", "A map finished initialising. Payload: map.", false,
                () => AccessTools.Method(typeof(Map), nameof(Map.FinalizeInit)), nameof(EventPatches.MapReady), false);
            Add("map.removed", "A map is being removed. Payload: map.", false,
                () => AccessTools.Method(typeof(Game), nameof(Game.DeinitAndRemoveMap), new[] { typeof(Map), typeof(bool) }), nameof(EventPatches.MapRemoved), true);

            // storyteller, quests, research
            Add("incident.fired", "An incident was attempted. Payload: incident, success, points, map.", false,
                () => AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute), new[] { typeof(IncidentParms) }), nameof(EventPatches.IncidentFired), false);
            Add("quest.added", "A quest was added. Payload: quest, name, id.", false,
                () => AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Add), new[] { typeof(Quest) }), nameof(EventPatches.QuestAdded), false);
            Add("quest.removed", "A quest is being removed. Payload: quest, name, id.", false,
                () => AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Remove), new[] { typeof(Quest) }), nameof(EventPatches.QuestRemoved), true);
            Add("research.finished", "A research project finished. Payload: project, researcher.", false,
                () => AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.FinishProject), new[] { typeof(ResearchProjectDef), typeof(bool), typeof(Pawn), typeof(bool) }), nameof(EventPatches.ResearchFinished), false);
            Add("research.progressed", "Research progress was made. Hot. Payload: amount, researcher.", true,
                () => AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.ResearchPerformed), new[] { typeof(float), typeof(Pawn) }), nameof(EventPatches.ResearchProgressed), false);

            // ui feeds and factions
            Add("letter.received", "A letter arrived. Payload: label, def.", false,
                () => AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) }), nameof(EventPatches.LetterReceived), false);
            Add("message.shown", "A message was shown. Payload: text, def.", false,
                () => AccessTools.Method(typeof(Messages), nameof(Messages.Message), new[] { typeof(Message), typeof(bool) }), nameof(EventPatches.MessageShown), false);
            Add("faction.added", "A faction was added to the world. Payload: faction, def.", false,
                () => AccessTools.Method(typeof(FactionManager), nameof(FactionManager.Add), new[] { typeof(Faction) }), nameof(EventPatches.FactionAdded), false);

            AddMore();
            AddWork();
        }

        private static void Add(string name, string description, bool hot, Func<MethodBase> target, string patchMethod, bool prefix)
        {
            var def = new EventDef { Name = name, Description = description, Hot = hot, Target = target, PatchMethod = patchMethod, IsPrefix = prefix };
            All.Add(def);
            ByName[name] = def;
        }

        public static IReadOnlyList<EventDef> List() => All;

        public static bool Exists(string name) => ByName.ContainsKey(name);

        public static string Subscribe(string name)
        {
            if (!ByName.TryGetValue(name, out EventDef def))
            {
                return "unknown event: " + name;
            }

            lock (def)
            {
                def.Subscribers++;
                if (def.Target == null)
                {
                    return null;   // virtual event: raised by the host itself, nothing to patch
                }

                if (def.Installed != null)
                {
                    return null;
                }

                try
                {
                    MethodBase target = def.Target();
                    if (target == null)
                    {
                        def.Subscribers--;
                        return "event target not found in this game version: " + name;
                    }

                    var hm = new HarmonyMethod(typeof(EventPatches), def.PatchMethod);
                    if (def.IsPrefix)
                    {
                        harmony.Patch(target, prefix: hm);
                    }
                    else
                    {
                        harmony.Patch(target, postfix: hm);
                    }

                    def.Installed = target;
                    return null;
                }
                catch (Exception e)
                {
                    def.Subscribers--;
                    Log.Error("[RimKit] event " + name + " install failed: " + e);
                    return "event install failed: " + name;
                }
            }
        }

        public static void Unsubscribe(string name)
        {
            if (!ByName.TryGetValue(name, out EventDef def))
            {
                return;
            }

            lock (def)
            {
                if (def.Subscribers > 0)
                {
                    def.Subscribers--;
                }

                if (def.Subscribers > 0 || def.Installed == null)
                {
                    return;
                }

                try
                {
                    harmony.Unpatch(def.Installed, AccessTools.Method(typeof(EventPatches), def.PatchMethod));
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] event " + name + " uninstall failed: " + e);
                }

                def.Installed = null;
            }
        }

        public static string ListJson()
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < All.Count; i++)
            {
                EventDef d = All[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":");
                Json.WriteString(sb, d.Name);
                sb.Append(",\"description\":");
                Json.WriteString(sb, d.Description);
                sb.Append(",\"hot\":").Append(d.Hot ? "true" : "false");
                sb.Append(",\"installed\":").Append(d.Installed != null ? "true" : "false");
                sb.Append('}');
            }

            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>Harmony patch bodies for the catalog. Each queues one payload for the main thread.</summary>
    internal static partial class EventPatches
    {
        private static readonly FieldInfo HealthPawn = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");
        private static readonly FieldInfo MentalPawn = AccessTools.Field(typeof(MentalStateHandler), "pawn");

        private static bool Playing => Current.ProgramState == ProgramState.Playing;

        private static void Emit(string name, Action<StringBuilder> body)
        {
            try
            {
                var sb = new StringBuilder(128);
                sb.Append('{');
                body(sb);
                sb.Append('}');
                LuaEventQueue.EnqueuePayload(name, sb.ToString());
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] event " + name + " payload failed: " + e.Message);
            }
        }

        private static void Field(StringBuilder sb, string key, object value, bool first = false)
        {
            if (!first) sb.Append(',');
            Json.WriteString(sb, key);
            sb.Append(':');
            HookCodec.Encode(sb, value, 0);
        }

        private static readonly FieldInfo JobTrackerPawn = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

        private static Pawn PawnOf(Pawn_HealthTracker tracker) => HealthPawn?.GetValue(tracker) as Pawn;

        private static Pawn JobPawn(Pawn_JobTracker tracker) => JobTrackerPawn?.GetValue(tracker) as Pawn;

        public static void ThingSpawned(Thing __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || !Playing || __instance == null) return;
            Emit("thing.spawned", sb => { Field(sb, "thing", __instance, true); Field(sb, "map", map); Field(sb, "def", __instance.def); });
        }

        public static void ThingDespawned(Thing __instance, DestroyMode mode)
        {
            if (!Playing || __instance == null) return;
            Emit("thing.despawned", sb => { Field(sb, "thing", __instance, true); Field(sb, "mode", mode); });
        }

        public static void ThingDestroyed(Thing __instance, DestroyMode mode)
        {
            if (!Playing || __instance == null) return;
            Emit("thing.destroyed", sb => { Field(sb, "thing", __instance, true); Field(sb, "mode", mode); });
        }

        public static void ThingDamaged(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (!Playing || __instance == null) return;
            Emit("thing.damaged", sb =>
            {
                Field(sb, "thing", __instance, true);
                Field(sb, "damage", dinfo);
                Field(sb, "dealt", __result != null ? __result.totalDamageDealt : 0f);
            });
        }

        public static void PawnSpawned(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || !Playing || __instance == null) return;
            Emit("pawn.spawned", sb => { Field(sb, "pawn", __instance, true); Field(sb, "map", map); });
        }

        public static void PawnDied(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (__instance == null) return;
            Emit("pawn.died", sb =>
            {
                Field(sb, "pawn", __instance, true);
                Field(sb, "damage", dinfo.HasValue ? (object)dinfo.Value : null);
                Field(sb, "culprit", exactCulprit);
            });
        }

        // Pawn does not override TakeDamage, so the patch sits on Thing.TakeDamage and keeps only pawns.
        public static void PawnDamaged(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (!Playing || !(__instance is Pawn pawn)) return;
            Emit("pawn.damaged", sb =>
            {
                Field(sb, "pawn", pawn, true);
                Field(sb, "damage", dinfo);
                Field(sb, "dealt", __result != null ? __result.totalDamageDealt : 0f);
            });
        }

        public static void PawnResurrected(Pawn_HealthTracker __instance)
        {
            Pawn pawn = PawnOf(__instance);
            if (pawn == null) return;
            Emit("pawn.resurrected", sb => Field(sb, "pawn", pawn, true));
        }

        public static void HediffAdded(Pawn_HealthTracker __instance, Hediff hediff, BodyPartRecord part)
        {
            Pawn pawn = PawnOf(__instance);
            if (pawn == null || hediff == null) return;
            Emit("hediff.added", sb =>
            {
                Field(sb, "pawn", pawn, true);
                Field(sb, "hediff", hediff);
                Field(sb, "part", part != null ? part.def.defName : null);
            });
        }

        public static void HediffRemoved(Pawn_HealthTracker __instance, Hediff hediff)
        {
            Pawn pawn = PawnOf(__instance);
            if (pawn == null || hediff == null) return;
            Emit("hediff.removed", sb => { Field(sb, "pawn", pawn, true); Field(sb, "hediff", hediff); });
        }

        public static void JobStarted(Pawn_JobTracker __instance, Job newJob)
        {
            if (JobPawn(__instance) == null || newJob == null) return;
            Emit("job.started", sb =>
            {
                Field(sb, "pawn", JobPawn(__instance), true);
                Field(sb, "job", newJob.def);
                Field(sb, "target", newJob.targetA.HasThing ? (object)newJob.targetA.Thing : null);
            });
        }

        public static void JobEnded(Pawn_JobTracker __instance, JobCondition condition)
        {
            if (JobPawn(__instance) == null) return;
            Job cur = __instance.curJob;
            Emit("job.ended", sb =>
            {
                Field(sb, "pawn", JobPawn(__instance), true);
                Field(sb, "job", cur != null ? cur.def : null);
                Field(sb, "condition", condition);
            });
        }

        public static void MentalStateStarted(MentalStateHandler __instance, MentalStateDef stateDef, bool __result)
        {
            Pawn pawn = MentalPawn?.GetValue(__instance) as Pawn;
            if (pawn == null) return;
            Emit("mental_state.started", sb => { Field(sb, "pawn", pawn, true); Field(sb, "state", stateDef); Field(sb, "started", __result); });
        }

        public static void SpeedChanged(TimeSpeed value)
        {
            Emit("time.speed_changed", sb => Field(sb, "speed", value, true));
        }

        public static void Ticked()
        {
            Emit("time.ticked", sb => Field(sb, "ticks", Find.TickManager != null ? Find.TickManager.TicksGame : 0, true));
        }

        public static void GameNew()
        {
            ObjectHandles.Clear();
            Emit("game.new", sb => { });
        }

        public static void GameLoaded()
        {
            ObjectHandles.Clear();
            Emit("game.loaded", sb => { });
        }

        public static void GameReady()
        {
            Emit("game.ready", sb => { });
        }

        public static void GameSaving(string fileName)
        {
            Emit("game.saving", sb => Field(sb, "file", fileName, true));
        }

        public static void MapReady(Map __instance)
        {
            if (__instance == null) return;
            Emit("map.ready", sb => Field(sb, "map", __instance, true));
        }

        public static void MapRemoved(Map map)
        {
            if (map == null) return;
            Emit("map.removed", sb => Field(sb, "map", map, true));
        }

        public static void IncidentFired(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (__instance == null) return;
            Emit("incident.fired", sb =>
            {
                Field(sb, "incident", __instance.def, true);
                Field(sb, "success", __result);
                Field(sb, "points", parms != null ? parms.points : 0f);
                Field(sb, "map", parms?.target as Map);
            });
        }

        public static void QuestAdded(Quest quest)
        {
            if (quest == null) return;
            Emit("quest.added", sb => { Field(sb, "quest", quest, true); Field(sb, "name", quest.name); Field(sb, "id", quest.id); });
        }

        public static void QuestRemoved(Quest quest)
        {
            if (quest == null) return;
            Emit("quest.removed", sb => { Field(sb, "quest", quest, true); Field(sb, "name", quest.name); Field(sb, "id", quest.id); });
        }

        public static void ResearchFinished(ResearchProjectDef proj, Pawn researcher)
        {
            if (proj == null) return;
            Emit("research.finished", sb => { Field(sb, "project", proj, true); Field(sb, "researcher", researcher); });
        }

        public static void ResearchProgressed(float amount, Pawn researcher)
        {
            Emit("research.progressed", sb => { Field(sb, "amount", amount, true); Field(sb, "researcher", researcher); });
        }

        public static void LetterReceived(Letter let)
        {
            if (let == null) return;
            Emit("letter.received", sb => { Field(sb, "label", let.Label.ToString(), true); Field(sb, "def", let.def); });
        }

        public static void MessageShown(Message msg)
        {
            if (msg == null) return;
            Emit("message.shown", sb => { Field(sb, "text", msg.text, true); Field(sb, "def", msg.def); });
        }

        public static void FactionAdded(Faction faction)
        {
            if (faction == null) return;
            Emit("faction.added", sb => { Field(sb, "faction", faction, true); Field(sb, "def", faction.def); });
        }
    }
}
