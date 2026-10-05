using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RimKit
{
    // Phase 2 event rows: the events popular mods listen for (docs/missing.md, "Events still missing").
    // Targets are looked up by method name so a different overload in another game version still resolves. Payload methods read
    // __instance and __args, which Harmony fills by position, so they do not depend on parameter names.
    internal static partial class EventCatalog
    {
        private static MethodBase M(Type type, string name, int args = -1)
        {
            return AccessTools.GetDeclaredMethods(type).FirstOrDefault(m => m.Name == name && (args < 0 || m.GetParameters().Length == args));
        }

        private static MethodBase Setter(Type type, string property) => AccessTools.PropertySetter(type, property);

        private static void AddMore()
        {
            // pawn lifecycle
            Add("pawn.joined_colony", "A pawn joined the player faction (recruited, rescued, born, tamed). Payload: pawn, from.", false,
                () => M(typeof(Pawn), nameof(Pawn.SetFaction)), nameof(EventPatches.PawnFaction), true);
            Add("pawn.downed", "A pawn was downed. Payload: pawn.", false, () => M(typeof(Pawn_HealthTracker), "MakeDowned"), nameof(EventPatches.PawnDowned), false);
            Add("pawn.stood_up", "A pawn is no longer downed. Payload: pawn.", false, () => M(typeof(Pawn_HealthTracker), "MakeUndowned"), nameof(EventPatches.PawnStoodUp), false);
            Add("pawn.drafted_changed", "A pawn was drafted or undrafted. Payload: pawn, drafted.", false, () => Setter(typeof(Pawn_DraftController), "Drafted"), nameof(EventPatches.PawnDrafted), false);
            Add("pawn.born", "A pawn was born or hatched. Payload: pawn.", false, () => M(typeof(PawnUtility), "TrySpawnHatchedOrBornPawn"), nameof(EventPatches.PawnBorn), false);
            Add("pawn.birthday", "A pawn had a biological birthday. Payload: pawn, age.", false, () => M(typeof(Pawn_AgeTracker), "BirthdayBiological"), nameof(EventPatches.PawnBirthday), false);
            Add("pawn.equipped", "A pawn equipped a weapon or tool. Payload: pawn, thing.", false, () => M(typeof(Pawn_EquipmentTracker), "AddEquipment"), nameof(EventPatches.PawnEquipped), false);
            Add("pawn.wore", "A pawn put on apparel. Payload: pawn, thing.", false, () => M(typeof(Pawn_ApparelTracker), "Wear"), nameof(EventPatches.PawnWore), false);

            // work
            Add("bill.completed", "A bill finished one repetition. Payload: pawn, recipe.", false, () => M(typeof(Bill_Production), "Notify_IterationCompleted"), nameof(EventPatches.BillCompleted), false);
            Add("plant.harvested", "A plant was harvested. Payload: plant, pawn.", false, () => M(typeof(Plant), "PlantCollected"), nameof(EventPatches.PlantHarvested), true);
            Add("construction.finished", "A building frame is about to complete. Payload: frame, def, pawn.", false, () => M(typeof(Frame), "CompleteConstruction"), nameof(EventPatches.ConstructionFinished), true);
            Add("recipe.applied", "A recipe (surgery, medical operation) was applied to a pawn. Payload: pawn, recipe, doer.", false, () => M(typeof(RecipeWorker), "ApplyOnPawn"), nameof(EventPatches.RecipeApplied), false);

            // health
            Add("hediff.tended", "A hediff was tended. Payload: pawn, hediff, quality.", false, () => M(typeof(Hediff), "Tended"), nameof(EventPatches.HediffTended), false);

            // mind and social
            Add("mental_state.ended", "A mental state ended. Payload: pawn, state.", false, () => M(typeof(MentalState), "PostEnd"), nameof(EventPatches.MentalStateEnded), false);
            Add("inspiration.gained", "An inspiration was requested. Payload: pawn, started.", false, () => M(typeof(InspirationHandler), "TryStartInspiration"), nameof(EventPatches.InspirationGained), false);
            Add("thought.gained", "A memory thought was gained. Payload: pawn, thought.", false, () => M(typeof(MemoryThoughtHandler), "TryGainMemory"), nameof(EventPatches.ThoughtGained), false);
            Add("relation.formed", "A direct relation formed (marriage, bond, rival). Payload: pawn, relation, other.", false, () => M(typeof(Pawn_RelationsTracker), "AddDirectRelation"), nameof(EventPatches.RelationFormed), false);
            Add("relation.ended", "A direct relation is ending (breakup, divorce). Payload: pawn, relation, other.", false, () => M(typeof(Pawn_RelationsTracker), "RemoveDirectRelation", 1), nameof(EventPatches.RelationEnded), true);
            Add("interaction.done", "A pawn tried a social interaction. Payload: pawn, recipient, interaction, success.", false, () => M(typeof(Pawn_InteractionsTracker), "TryInteractWith"), nameof(EventPatches.InteractionDone), false);

            // skills
            Add("skill.learned", "A pawn gained skill experience. Hot. Payload: pawn, skill, xp, level.", true, () => M(typeof(SkillRecord), "Learn"), nameof(EventPatches.SkillLearned), false);

            // combat
            Add("projectile.hit", "A projectile is hitting something. Payload: projectile, target, launcher.", false, () => M(typeof(Projectile), "Impact"), nameof(EventPatches.ProjectileHit), true);
            Add("explosion.occurred", "An explosion is starting. Payload: map, x, z, radius, damage_def.", false, () => M(typeof(GenExplosion), "DoExplosion"), nameof(EventPatches.Explosion), true);
            Add("melee.hit", "A melee attack landed. Payload: attacker, target.", false, () => M(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget"), nameof(EventPatches.MeleeHit), false);

            // world and map
            Add("map.generated", "A map was generated. Payload: map.", false, () => M(typeof(MapGenerator), "GenerateMap"), nameof(EventPatches.MapGenerated), false);
            Add("world_object.added", "A world object was added (settlement, site, caravan). Payload: object, def, tile.", false, () => M(typeof(WorldObjectsHolder), "Add"), nameof(EventPatches.WorldObjectAdded), false);
            Add("world_object.removed", "A world object is being removed. Payload: object, def, tile.", false, () => M(typeof(WorldObjectsHolder), "Remove"), nameof(EventPatches.WorldObjectRemoved), true);
            Add("caravan.formed", "A caravan was formed. Payload: caravan.", false, () => M(typeof(CaravanMaker), "MakeCaravan"), nameof(EventPatches.CaravanFormed), false);
            Add("weather.changed", "The weather is changing. Payload: map, weather.", false, () => M(typeof(WeatherManager), "TransitionTo"), nameof(EventPatches.WeatherChanged), false);
            Add("condition.started", "A game condition started. Payload: condition, label.", false, () => M(typeof(GameConditionManager), "RegisterCondition"), nameof(EventPatches.ConditionStarted), false);
            Add("condition.ended", "A game condition is ending. Payload: condition, label.", false, () => M(typeof(GameCondition), "End"), nameof(EventPatches.ConditionEnded), true);

            // time: cheap boundary checks on the tick, nothing is queued between boundaries
            Add("time.hour_changed", "A new game hour began. Payload: ticks.", false, () => M(typeof(TickManager), "DoSingleTick"), nameof(EventPatches.HourChanged), false);
            Add("time.day_changed", "A new game day began. Payload: ticks.", false, () => M(typeof(TickManager), "DoSingleTick"), nameof(EventPatches.DayChanged), false);
            Add("time.quadrum_changed", "A new quadrum began. Payload: ticks.", false, () => M(typeof(TickManager), "DoSingleTick"), nameof(EventPatches.QuadrumChanged), false);
            Add("time.year_changed", "A new year began. Payload: ticks.", false, () => M(typeof(TickManager), "DoSingleTick"), nameof(EventPatches.YearChanged), false);

            // economy
            Add("trade.completed", "A trade was executed. Payload: traded.", false, () => M(typeof(TradeDeal), "TryExecute"), nameof(EventPatches.TradeCompleted), false);

            // quests and research
            Add("quest.accepted", "A quest was accepted. Payload: quest, name, id.", false, () => M(typeof(Quest), "Accept"), nameof(EventPatches.QuestAccepted), false);
            Add("quest.ended", "A quest ended. Payload: quest, name, id, outcome.", false, () => M(typeof(Quest), "End"), nameof(EventPatches.QuestEnded), false);
            Add("research.started", "A research project became the current one. Payload: project.", false, () => M(typeof(ResearchManager), "SetCurrentProject"), nameof(EventPatches.ResearchStarted), false);

            // buildings, designations, zones
            Add("power.changed", "A building was powered on or off. Payload: thing, on.", false, () => Setter(typeof(CompPowerTrader), "PowerOn"), nameof(EventPatches.PowerChanged), false);
            Add("designation.added", "A designation was added. Payload: def, thing, x, z.", false, () => M(typeof(DesignationManager), "AddDesignation"), nameof(EventPatches.DesignationAdded), false);
            Add("designation.removed", "A designation is being removed. Payload: def, thing, x, z.", false, () => M(typeof(DesignationManager), "RemoveDesignation"), nameof(EventPatches.DesignationRemoved), true);
            Add("zone.created", "A zone was created. Payload: id, label, type.", false, () => M(typeof(ZoneManager), "RegisterZone"), nameof(EventPatches.ZoneCreated), false);
            Add("zone.removed", "A zone is being removed. Payload: id, label, type.", false, () => M(typeof(ZoneManager), "DeregisterZone"), nameof(EventPatches.ZoneRemoved), true);

            // factions
            Add("faction.goodwill_changed", "Goodwill between two factions changed. Payload: faction, other, goodwill.", false, () => M(typeof(Faction), "TryAffectGoodwillWith"), nameof(EventPatches.GoodwillChanged), false);

            // DLC events
            Add("pawn.ideo_changed", "A pawn converted to another ideoligion. Payload: pawn, ideo.", false, () => M(typeof(Pawn_IdeoTracker), "SetIdeo"), nameof(EventPatches.IdeoChanged), false);
            Add("ideo.precept_added", "A precept was added to an ideoligion. Payload: ideo, precept.", false, () => M(typeof(Ideo), "AddPrecept"), nameof(EventPatches.PreceptAdded), false);
            Add("royalty.title_changed", "A pawn's royal title changed. Payload: pawn, faction, title.", false, () => M(typeof(Pawn_RoyaltyTracker), "SetTitle"), nameof(EventPatches.TitleChanged), false);
            Add("anomaly.level_changed", "The monolith level changed. Payload: level.", false, () => M(typeof(GameComponent_Anomaly), "SetLevel"), nameof(EventPatches.MonolithLevel), false);

            // more combat, movement and letters
            Add("ability.used", "A pawn used an ability (a psycast, a gene ability, a ritual power). Payload: pawn, ability, target.", false, () => AccessTools.Method(typeof(Ability), "Activate", new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }), nameof(EventPatches.AbilityUsed), false);
            Add("shot.fired", "A verb fired one shot of a burst. Hot. Payload: shooter, target, verb.", true, () => M(typeof(Verb), "TryCastNextBurstShot"), nameof(EventPatches.ShotFired), false);
            Add("pawn.left_map", "A pawn is leaving a map (caravan, exit grid, shuttle). Payload: pawn, map.", false, () => M(typeof(Pawn), "ExitMap"), nameof(EventPatches.PawnLeftMap), true);
            Add("letter.opened", "The player opened a letter. Payload: label.", false, () => M(typeof(ChoiceLetter), "OpenLetter"), nameof(EventPatches.LetterOpened), false);

            // raised by the host itself, no patch
            Add("letter.choice", "The player picked an answer on a letter made with game.hud.letter. Payload: tag, index, label.", false, null, null, false);
            Add("mod.disabled", "A mod was switched off after too many Lua errors. Payload: mod, where.", false, null, null, false);
            Add("mod.over_budget", "A mod is using more time per tick than its budget. Payload: mod, avg_us.", false, null, null, false);
            Add("options.changed", "A value on a game.options page changed. Payload: package_id, key, value.", false, null, null, false);

            // ui
            Add("selection.changed", "Something was selected. Payload: count.", false, () => M(typeof(Selector), "Select"), nameof(EventPatches.SelectionChanged), false);
            Add("selection.cleared", "The selection was cleared.", false, () => M(typeof(Selector), "ClearSelection"), nameof(EventPatches.SelectionCleared), false);
            Add("window.opened", "A window was opened. Payload: class.", false, () => M(typeof(WindowStack), "Add"), nameof(EventPatches.WindowOpened), false);
        }
    }

    internal static partial class EventPatches
    {
        public static void IdeoChanged(Pawn_IdeoTracker __instance, object[] __args)
        {
            Pawn p = IdeoPawn?.GetValue(__instance) as Pawn;
            if (p == null || !Playing) return;
            Emit("pawn.ideo_changed", sb => { Field(sb, "pawn", p, true); Field(sb, "ideo", __args != null && __args.Length > 0 && __args[0] is Ideo i ? (object)i.name : null); });
        }

        public static void PreceptAdded(Ideo __instance, object[] __args)
        {
            if (!Playing) return;
            Emit("ideo.precept_added", sb => { Field(sb, "ideo", __instance.name, true); Field(sb, "precept", __args != null && __args.Length > 0 && __args[0] is Precept p ? p.def.defName : null); });
        }

        public static void TitleChanged(Pawn_RoyaltyTracker __instance, object[] __args)
        {
            Pawn p = RoyaltyPawn?.GetValue(__instance) as Pawn;
            if (p == null || !Playing) return;
            Emit("royalty.title_changed", sb => { Field(sb, "pawn", p, true); Field(sb, "faction", __args != null && __args.Length > 0 ? __args[0] as Faction : null); Field(sb, "title", __args != null && __args.Length > 1 && __args[1] is RoyalTitleDef d ? d.defName : null); });
        }

        public static void MonolithLevel()
        {
            if (!Playing || Find.Anomaly == null) return;
            Emit("anomaly.level_changed", sb => Field(sb, "level", Find.Anomaly.Level, true));
        }

        private static readonly FieldInfo IdeoPawn = AccessTools.Field(typeof(Pawn_IdeoTracker), "pawn");
        private static readonly FieldInfo RoyaltyPawn = AccessTools.Field(typeof(Pawn_RoyaltyTracker), "pawn");

        private static T Arg<T>(object[] args, int i) where T : class => args != null && i < args.Length ? args[i] as T : null;

        private static readonly FieldInfo DraftPawn = AccessTools.Field(typeof(Pawn_DraftController), "pawn");
        private static readonly FieldInfo AgePawn = AccessTools.Field(typeof(Pawn_AgeTracker), "pawn");
        private static readonly FieldInfo EquipPawn = AccessTools.Field(typeof(Pawn_EquipmentTracker), "pawn");
        private static readonly FieldInfo ApparelPawn = AccessTools.Field(typeof(Pawn_ApparelTracker), "pawn");
        private static readonly FieldInfo MemoryPawn = AccessTools.Field(typeof(MemoryThoughtHandler), "pawn");
        private static readonly FieldInfo RelationPawn = AccessTools.Field(typeof(Pawn_RelationsTracker), "pawn");
        private static readonly FieldInfo InteractPawn = AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn");
        private static readonly FieldInfo InspirationPawn = AccessTools.Field(typeof(InspirationHandler), "pawn");
        private static readonly FieldInfo SkillPawn = AccessTools.Field(typeof(SkillRecord), "pawn");

        private static Pawn Of(FieldInfo f, object o) => o == null ? null : f?.GetValue(o) as Pawn;

        public static void PawnFaction(Pawn __instance, object[] __args)
        {
            Faction next = Arg<Faction>(__args, 0);
            if (__instance == null || !Playing || next == __instance.Faction) return;
            if (next != null && next.IsPlayer) Emit("pawn.joined_colony", sb => { Field(sb, "pawn", __instance, true); Field(sb, "from", __instance.Faction); });
        }

        public static void PawnDowned(Pawn_HealthTracker __instance)
        {
            Pawn p = PawnOf(__instance);
            if (p == null) return;
            Emit("pawn.downed", sb => Field(sb, "pawn", p, true));
        }

        public static void PawnStoodUp(Pawn_HealthTracker __instance)
        {
            Pawn p = PawnOf(__instance);
            if (p == null) return;
            Emit("pawn.stood_up", sb => Field(sb, "pawn", p, true));
        }

        public static void PawnDrafted(Pawn_DraftController __instance, bool value)
        {
            Pawn p = Of(DraftPawn, __instance);
            if (p == null || !Playing) return;
            Emit("pawn.drafted_changed", sb => { Field(sb, "pawn", p, true); Field(sb, "drafted", value); });
        }

        public static void PawnBorn(object[] __args)
        {
            Pawn p = Arg<Pawn>(__args, 0);
            if (p == null) return;
            Emit("pawn.born", sb => Field(sb, "pawn", p, true));
        }

        public static void PawnBirthday(Pawn_AgeTracker __instance, int birthdayAge)
        {
            Pawn p = Of(AgePawn, __instance);
            if (p == null) return;
            Emit("pawn.birthday", sb => { Field(sb, "pawn", p, true); Field(sb, "age", birthdayAge); });
        }

        public static void PawnEquipped(Pawn_EquipmentTracker __instance, object[] __args)
        {
            Pawn p = Of(EquipPawn, __instance);
            Thing t = Arg<Thing>(__args, 0);
            if (p == null || t == null || !Playing) return;
            Emit("pawn.equipped", sb => { Field(sb, "pawn", p, true); Field(sb, "thing", t); });
        }

        public static void PawnWore(Pawn_ApparelTracker __instance, object[] __args)
        {
            Pawn p = Of(ApparelPawn, __instance);
            Thing t = Arg<Thing>(__args, 0);
            if (p == null || t == null || !Playing) return;
            Emit("pawn.wore", sb => { Field(sb, "pawn", p, true); Field(sb, "thing", t); });
        }

        public static void BillCompleted(Bill_Production __instance, object[] __args)
        {
            Pawn p = Arg<Pawn>(__args, 0);
            Emit("bill.completed", sb => { Field(sb, "pawn", p, true); Field(sb, "recipe", __instance.recipe); });
        }

        public static void PlantHarvested(Plant __instance, object[] __args)
        {
            Emit("plant.harvested", sb => { Field(sb, "plant", __instance, true); Field(sb, "pawn", Arg<Pawn>(__args, 0)); });
        }

        public static void ConstructionFinished(Frame __instance, object[] __args)
        {
            Emit("construction.finished", sb => { Field(sb, "frame", __instance, true); Field(sb, "def", __instance.def.entityDefToBuild); Field(sb, "pawn", Arg<Pawn>(__args, 0)); });
        }

        public static void RecipeApplied(RecipeWorker __instance, object[] __args)
        {
            Emit("recipe.applied", sb => { Field(sb, "pawn", Arg<Pawn>(__args, 0), true); Field(sb, "recipe", __instance.recipe); Field(sb, "doer", Arg<Pawn>(__args, 2)); });
        }

        public static void HediffTended(Hediff __instance, float quality)
        {
            Emit("hediff.tended", sb => { Field(sb, "pawn", __instance.pawn, true); Field(sb, "hediff", __instance); Field(sb, "quality", quality); });
        }

        public static void MentalStateEnded(MentalState __instance)
        {
            Emit("mental_state.ended", sb => { Field(sb, "pawn", __instance.pawn, true); Field(sb, "state", __instance.def); });
        }

        public static void InspirationGained(InspirationHandler __instance, bool __result)
        {
            Pawn p = Of(InspirationPawn, __instance);
            if (p == null) return;
            Emit("inspiration.gained", sb => { Field(sb, "pawn", p, true); Field(sb, "started", __result); });
        }

        public static void ThoughtGained(MemoryThoughtHandler __instance, object[] __args)
        {
            Pawn p = Of(MemoryPawn, __instance);
            var th = Arg<Thought_Memory>(__args, 0);
            if (p == null || th == null || !Playing) return;
            Emit("thought.gained", sb => { Field(sb, "pawn", p, true); Field(sb, "thought", th.def); });
        }

        public static void RelationFormed(Pawn_RelationsTracker __instance, object[] __args)
        {
            Pawn p = Of(RelationPawn, __instance);
            if (p == null || !Playing) return;
            Emit("relation.formed", sb => { Field(sb, "pawn", p, true); Field(sb, "relation", __args != null && __args.Length > 0 ? __args[0] : null); Field(sb, "other", Arg<Pawn>(__args, 1)); });
        }

        public static void RelationEnded(Pawn_RelationsTracker __instance, object[] __args)
        {
            Pawn p = Of(RelationPawn, __instance);
            if (p == null || !Playing) return;
            var rel = __args != null && __args.Length > 0 ? __args[0] as DirectPawnRelation : null;
            Emit("relation.ended", sb => { Field(sb, "pawn", p, true); Field(sb, "relation", rel?.def); Field(sb, "other", rel?.otherPawn); });
        }

        public static void InteractionDone(Pawn_InteractionsTracker __instance, object[] __args, bool __result)
        {
            Pawn p = Of(InteractPawn, __instance);
            if (p == null) return;
            Emit("interaction.done", sb => { Field(sb, "pawn", p, true); Field(sb, "recipient", Arg<Pawn>(__args, 0)); Field(sb, "interaction", Arg<InteractionDef>(__args, 1)); Field(sb, "success", __result); });
        }

        public static void SkillLearned(SkillRecord __instance, float xp)
        {
            Pawn p = Of(SkillPawn, __instance);
            if (p == null || xp == 0f || !Playing) return;
            Emit("skill.learned", sb => { Field(sb, "pawn", p, true); Field(sb, "skill", __instance.def); Field(sb, "xp", xp); Field(sb, "level", __instance.Level); });
        }

        public static void ProjectileHit(Projectile __instance, object[] __args)
        {
            if (!Playing) return;
            Emit("projectile.hit", sb => { Field(sb, "projectile", __instance.def, true); Field(sb, "target", Arg<Thing>(__args, 0)); Field(sb, "launcher", AccessTools.Field(typeof(Projectile), "launcher")?.GetValue(__instance)); });
        }

        public static void Explosion(object[] __args)
        {
            if (!Playing || __args == null || __args.Length < 4) return;
            var c = (IntVec3)__args[0];
            Emit("explosion.occurred", sb => { Field(sb, "map", __args[1] as Map, true); Field(sb, "x", c.x); Field(sb, "z", c.z); Field(sb, "radius", __args[2]); Field(sb, "damage_def", __args[3] as DamageDef); });
        }

        public static void MeleeHit(Verb_MeleeAttackDamage __instance, object[] __args)
        {
            if (!Playing) return;
            LocalTargetInfo target = __args != null && __args.Length > 0 && __args[0] is LocalTargetInfo t ? t : LocalTargetInfo.Invalid;
            Emit("melee.hit", sb => { Field(sb, "attacker", __instance.CasterPawn, true); Field(sb, "target", target.HasThing ? target.Thing : null); });
        }

        public static void AbilityUsed(Ability __instance, object[] __args)
        {
            if (!Playing || __instance?.pawn == null) return;
            LocalTargetInfo target = __args != null && __args.Length > 0 && __args[0] is LocalTargetInfo t ? t : LocalTargetInfo.Invalid;
            Emit("ability.used", sb => { Field(sb, "pawn", __instance.pawn, true); Field(sb, "ability", __instance.def); Field(sb, "target", target.HasThing ? target.Thing : null); });
        }

        public static void ShotFired(Verb __instance)
        {
            if (!Playing || __instance == null) return;
            LocalTargetInfo target = __instance.CurrentTarget;
            Emit("shot.fired", sb => { Field(sb, "shooter", __instance.Caster, true); Field(sb, "target", target.HasThing ? target.Thing : null); Field(sb, "verb", __instance.verbProps?.label); });
        }

        public static void PawnLeftMap(Pawn __instance)
        {
            if (!Playing || __instance == null) return;
            Emit("pawn.left_map", sb => { Field(sb, "pawn", __instance, true); Field(sb, "map", __instance.MapHeld); });
        }

        public static void LetterOpened(ChoiceLetter __instance)
        {
            if (!Playing || __instance == null) return;
            Emit("letter.opened", sb => Field(sb, "label", __instance.Label.ToString(), true));
        }

        public static void MapGenerated(Map __result)
        {
            if (__result == null) return;
            Emit("map.generated", sb => Field(sb, "map", __result, true));
        }

        public static void WorldObjectAdded(object[] __args)
        {
            var o = Arg<WorldObject>(__args, 0);
            if (o == null || !Playing) return;
            Emit("world_object.added", sb => { Field(sb, "object", o, true); Field(sb, "def", o.def); Field(sb, "tile", (int)o.Tile); });
        }

        public static void WorldObjectRemoved(object[] __args)
        {
            var o = Arg<WorldObject>(__args, 0);
            if (o == null || !Playing) return;
            Emit("world_object.removed", sb => { Field(sb, "object", o, true); Field(sb, "def", o.def); Field(sb, "tile", (int)o.Tile); });
        }

        public static void CaravanFormed(Caravan __result)
        {
            if (__result == null) return;
            Emit("caravan.formed", sb => Field(sb, "caravan", __result, true));
        }

        public static void WeatherChanged(WeatherManager __instance, object[] __args)
        {
            Map map = AccessTools.Field(typeof(WeatherManager), "map")?.GetValue(__instance) as Map;
            Emit("weather.changed", sb => { Field(sb, "map", map, true); Field(sb, "weather", Arg<WeatherDef>(__args, 0)); });
        }

        public static void ConditionStarted(object[] __args)
        {
            var c = Arg<GameCondition>(__args, 0);
            if (c == null) return;
            Emit("condition.started", sb => { Field(sb, "condition", c.def, true); Field(sb, "label", c.LabelCap.ToString()); });
        }

        public static void ConditionEnded(GameCondition __instance)
        {
            Emit("condition.ended", sb => { Field(sb, "condition", __instance.def, true); Field(sb, "label", __instance.LabelCap.ToString()); });
        }

        private static bool Boundary(int length) => Playing && Find.TickManager != null && Find.TickManager.TicksGame % length == 0;

        public static void HourChanged() { if (Boundary(GenDate.TicksPerHour)) Emit("time.hour_changed", sb => Field(sb, "ticks", Find.TickManager.TicksGame, true)); }

        public static void DayChanged() { if (Boundary(GenDate.TicksPerDay)) Emit("time.day_changed", sb => Field(sb, "ticks", Find.TickManager.TicksGame, true)); }

        public static void QuadrumChanged() { if (Boundary(GenDate.TicksPerQuadrum)) Emit("time.quadrum_changed", sb => Field(sb, "ticks", Find.TickManager.TicksGame, true)); }

        public static void YearChanged() { if (Boundary(GenDate.TicksPerYear)) Emit("time.year_changed", sb => Field(sb, "ticks", Find.TickManager.TicksGame, true)); }

        public static void TradeCompleted(bool actuallyTraded)
        {
            Emit("trade.completed", sb => Field(sb, "traded", actuallyTraded, true));
        }

        public static void QuestAccepted(Quest __instance)
        {
            Emit("quest.accepted", sb => { Field(sb, "quest", __instance.name, true); Field(sb, "name", __instance.name); Field(sb, "id", __instance.id); });
        }

        public static void QuestEnded(Quest __instance, object[] __args)
        {
            Emit("quest.ended", sb => { Field(sb, "quest", __instance.name, true); Field(sb, "name", __instance.name); Field(sb, "id", __instance.id); Field(sb, "outcome", __args != null && __args.Length > 0 ? __args[0]?.ToString() : null); });
        }

        public static void ResearchStarted(object[] __args)
        {
            var p = Arg<ResearchProjectDef>(__args, 0);
            if (p == null) return;
            Emit("research.started", sb => Field(sb, "project", p, true));
        }

        public static void PowerChanged(CompPowerTrader __instance, bool value)
        {
            if (!Playing) return;
            Emit("power.changed", sb => { Field(sb, "thing", __instance.parent, true); Field(sb, "on", value); });
        }

        private static void DesignationPayload(string name, Designation d)
        {
            if (d == null || !Playing) return;
            Emit(name, sb =>
            {
                Field(sb, "def", d.def, true);
                Field(sb, "thing", d.target.HasThing ? d.target.Thing : null);
                Field(sb, "x", d.target.Cell.x);
                Field(sb, "z", d.target.Cell.z);
            });
        }

        public static void DesignationAdded(object[] __args) => DesignationPayload("designation.added", Arg<Designation>(__args, 0));

        public static void DesignationRemoved(object[] __args) => DesignationPayload("designation.removed", Arg<Designation>(__args, 0));

        private static void ZonePayload(string name, Zone z)
        {
            if (z == null || !Playing) return;
            Emit(name, sb => { Field(sb, "id", z.ID, true); Field(sb, "label", z.label); Field(sb, "type", z.GetType().Name); });
        }

        public static void ZoneCreated(object[] __args) => ZonePayload("zone.created", Arg<Zone>(__args, 0));

        public static void ZoneRemoved(object[] __args) => ZonePayload("zone.removed", Arg<Zone>(__args, 0));

        public static void GoodwillChanged(Faction __instance, object[] __args)
        {
            var other = Arg<Faction>(__args, 0);
            if (other == null || !Playing) return;
            Emit("faction.goodwill_changed", sb => { Field(sb, "faction", __instance, true); Field(sb, "other", other); Field(sb, "goodwill", __instance.GoodwillWith(other)); });
        }

        public static void SelectionChanged()
        {
            if (!Playing || Find.Selector == null) return;
            Emit("selection.changed", sb => Field(sb, "count", Find.Selector.NumSelected, true));
        }

        public static void SelectionCleared()
        {
            if (!Playing) return;
            Emit("selection.cleared", sb => { });
        }

        public static void WindowOpened(object[] __args)
        {
            var w = __args != null && __args.Length > 0 ? __args[0] : null;
            if (w == null || !Playing) return;
            Emit("window.opened", sb => Field(sb, "class", w.GetType().Name, true));
        }
    }
}
