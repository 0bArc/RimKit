# RimWorld 1.6 system inventory

This page records the RimWorld systems that RimKit can expose to Lua, and the Harmony targets that carry them. It was produced by decompiling `Assembly-CSharp.dll` from RimWorld 1.6.4871 (revision 590) and verifying every signature below against that source. It is a design input for the unified API, not a stability promise. See [stability](../api/stability.md) for tiers and [kits and domains](../api/domains.md) for the domain plan.

## Scale of the game assembly

| Namespace | Types |
| --- | --- |
| RimWorld | 5913 |
| Verse | 1747 |
| RimWorld.QuestGen | 381 |
| RimWorld.Planet | 302 |
| Verse.AI | 278 |
| Verse.AI.Group | 129 |
| RimWorld.BaseGen | 125 |
| Verse.Sound | 69 |

Family counts (by type name): about 256 `*Def` classes, 962 `Comp*` classes, 303 `JobDriver*`, 229 `Hediff*`, 148 `WorkGiver*`, 96 `IncidentWorker*`, 60 `*Worker`.

DLC content (Royalty, Ideology, Biotech, Anomaly, Odyssey) lives in the same assembly. Gate DLC domains on `ModsConfig.<Dlc>Active`, never on type existence.

## Pawn trackers

Each `Pawn` owns one object per concern. These are the natural targets for `game.pawns` sub-objects.

- Health and body: `Pawn_HealthTracker`, `Pawn_AgeTracker`, `Pawn_GeneTracker`, `Pawn_MutantTracker`
- Needs and mind: `Pawn_NeedsTracker`, `Pawn_StoryTracker`, `Pawn_SkillTracker`, `Pawn_LearningTracker`, `Pawn_GuiltTracker`
- Work and AI: `Pawn_JobTracker`, `Pawn_TimetableTracker`, `Pawn_StanceTracker`, `Pawn_TrainingTracker`
- Social: `Pawn_RelationsTracker`, `Pawn_InteractionsTracker`, `Pawn_GuestTracker`, `Pawn_IdeoTracker`, `Pawn_RoyaltyTracker`, `Pawn_ConnectionsTracker`
- Items: `Pawn_EquipmentTracker`, `Pawn_ApparelTracker`, `Pawn_InventoryTracker`, `Pawn_CarryTracker`, `Pawn_InventoryStockTracker`
- Policies: `Pawn_OutfitTracker`, `Pawn_DrugPolicyTracker`, `Pawn_FoodRestrictionTracker`
- Presentation: `Pawn_DrawTracker`, `Pawn_StyleTracker`, `Pawn_StyleObserverTracker`
- DLC: `Pawn_MechanitorTracker`, `Pawn_PsychicEntropyTracker`, `Pawn_CreepJoinerTracker`, `Pawn_AbilityTracker`
- Other: `Pawn_RecordsTracker`, `Pawn_TraderTracker`, `Pawn_CallTracker`, `Pawn_FilthTracker`, `Pawn_FlightTracker`, `Pawn_RopeTracker`, `Pawn_DuplicateTracker`

Related handlers reached through trackers: `ThoughtHandler` (with `MemoryThoughtHandler`, `SituationalThoughtHandler`), `MentalStateHandler`, `InspirationHandler`, `ImmunityHandler`, `PawnCapacitiesHandler`, `SummaryHealthHandler`.

## Managers (Find.* and Map-level)

| Scope | Managers |
| --- | --- |
| Game | `TickManager`, `FactionManager`, `QuestManager`, `ResearchManager`, `IdeoManager`, `TaleManager`, `HistoryEventsManager`, `LetterStack`, `StudyManager`, `AnalysisManager`, `UniqueIDsManager` |
| Map | `LordManager`, `DesignationManager`, `AreaManager`, `ZoneManager`, `PowerNetManager`, `WeatherManager`, `GameConditionManager`, `ReservationManager`, `HaulDestinationManager`, `StorageGroupManager`, `AnimalPenManager`, `AutoSlaughterManager`, `BreakdownManager`, `PassingShipManager`, `TransportShipManager`, `WindManager`, `SkyManager`, `FleckManager` |
| UI | `DesignatorManager`, `InspectTabManager`, `ActiveLessonHandler`, `TooltipHandler` |
| Meta | `LoadedModManager`, `LongEventHandler`, `SignalManager` |

Extension points the game already offers (prefer these over raw patches for persistence): `GameComponent`, `WorldComponent`, `MapComponent`. The host already uses a `GameComponent` for `data.*`.

## Verified Harmony targets

Signatures were read from the decompiled 1.6.4871 source. The "Event" column is the proposed UNC event name (see [naming](../api/naming.md)).

| Domain | Target | Proposed event or use |
| --- | --- | --- |
| Thing | `Thing.SpawnSetup(Map map, bool respawningAfterLoad)` | `thing.spawned` |
| Thing | `Thing.DeSpawn(DestroyMode mode = Vanish)` | `thing.despawned` |
| Thing | `Thing.Destroy(DestroyMode mode = Vanish)` | `thing.destroyed` |
| Thing | `Thing.TakeDamage(DamageInfo dinfo)` returns `DamageWorker.DamageResult` | `thing.damaged`, result readable and writable |
| Thing | `Thing.PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)` | after-damage observation |
| Pawn | `Pawn.Kill(DamageInfo? dinfo, Hediff exactCulprit = null)` | `pawn.died` |
| Pawn | `Pawn.Tick()` (protected) and `Pawn.TickInterval(int delta)` (protected) | per-pawn tick, see note below |
| Pawn | `Pawn.TickRare()` | rare tick |
| Health | `Pawn_HealthTracker.AddHediff(Hediff, BodyPartRecord, DamageInfo?, DamageWorker.DamageResult)` | `hediff.added` |
| Health | `Pawn_HealthTracker.RemoveHediff(Hediff)` | `hediff.removed` |
| Health | `Pawn_HealthTracker.PreApplyDamage(DamageInfo, out bool absorbed)` | damage absorption control |
| Health | `Pawn_HealthTracker.SetDead()` and `Notify_Resurrected(bool, float)` | `pawn.resurrected` |
| Health | `HediffSet.AddDirect(Hediff, DamageInfo?, DamageResult)` | low level add |
| Jobs | `Pawn_JobTracker.StartJob(Job, JobCondition, ThinkNode, ...)` (13 parameters) | `job.started`, overload selection needed |
| Jobs | `Pawn_JobTracker.EndCurrentJob(JobCondition, bool, bool)` | `job.ended` |
| Jobs | `Pawn_JobTracker.ClearQueuedJobs(bool)` | queue control |
| Mind | `MentalStateHandler.TryStartMentalState(MentalStateDef, string, bool, bool, bool, Pawn, bool, bool, bool)` returns `bool` | `mental_state.started` |
| Time | `TickManager.DoSingleTick()`, `TogglePaused()`, `CurTimeSpeed` property | `time.ticked`, `time.speed_changed` |
| Game | `Game.InitNewGame()`, `Game.LoadGame()`, `Game.FinalizeInit()`, `Game.UpdatePlay()` | `game.new`, `game.loaded`, `game.ready` |
| Save | `GameDataSaveLoader.SaveGame(string fileName)` | `game.saving` |
| Map | `Map.ConstructComponents()`, `Map.FinalizeInit()`, `Map.MapPostTick()` | `map.created`, `map.ready` |
| Map | `Game.DeinitAndRemoveMap(Map, bool notifyPlayer)` | `map.removed`, handle invalidation |
| Incidents | `IncidentWorker.TryExecute(IncidentParms parms)` returns `bool` | `incident.fired`, cancel by prefix |
| Quests | `QuestManager.Add(Quest)`, `Remove(Quest)`, `QuestManagerTick()` | `quest.added`, `quest.removed` |
| Research | `ResearchManager.FinishProject(ResearchProjectDef, bool, Pawn, bool)` | `research.finished` |
| Research | `ResearchManager.ResearchPerformed(float amount, Pawn researcher)` | `research.progressed` |
| Letters | `LetterStack.ReceiveLetter(...)` (3 overloads), `RemoveLetter(Letter)` | `letter.received`, `letter.removed` |
| Messages | `Messages.Message(...)` (4 overloads) | `message.shown` |
| Factions | `FactionManager.Add(Faction)` | `faction.added` |
| Storyteller | `Storyteller.StorytellerTick()` | storyteller pacing control |
| Needs | `Pawn_NeedsTracker.TryGetNeed(NeedDef)`, `AddOrRemoveNeedsAsAppropriate()` | need access |
| Construction | `GenConstruct.PlaceBlueprintForBuild(...)` and install and reinstall variants | `building.blueprint_placed` |
| Trade | `TradeUtility.*` (drop pods, launch, trader quest hooks) | trade domain entry points |
| Date | `GenDate.*` (`DaysPassedAt`, `HourOfDay`, `TwelfthsPassedAt`, `YearsPassedAt`, tick conversions) | `game.time` read ops |

### Findings that affect existing code

1. In 1.6 `Thing.Tick()` and `Pawn.Tick()` are `protected override`, and a `TickInterval(int delta)` path exists next to them. `PawnControlBridge` patches `Pawn.Tick` and `Pawn.TickRare`. Pawns that tick through `TickInterval` may not reach a `Tick` postfix, so the control bridge needs a verification pass against `TickInterval`.
2. `Pawn.Kill` takes `(DamageInfo? dinfo, Hediff exactCulprit = null)`. The existing `pawn_died` prefix resolves it by name, which works, but overload selection is needed for `Thing.Kill` versus `Pawn.Kill`.
3. `Pawn_JobTracker.StartJob` has 13 parameters. Name only resolution is fine today because there is one overload, but typed argument payloads are required to expose the job and `jobGiver`.
4. `Messages.Message` and `LetterStack.ReceiveLetter` each have several overloads. Hooking them requires explicit signatures (the Harmony layer upgrade).
5. `Verse.Scribe` exposes few static entry points, so custom persistence should go through `GameComponent`, `WorldComponent`, `MapComponent` and `IExposable`, not through patching `Scribe_*`.

## Hook strategy

- Prefer one lazily installed patch per target, shared by all Lua handlers (current `SharedPrefix` and `SharedPostfix` model).
- Events carry a JSON payload, not a single handle. Payload keys follow the UNC argument rules.
- Hot paths (`Pawn.Tick`, `Thing.TakeDamage`, `MapPostTick`) are opt in. They install only when a handler subscribes and are removed when the last handler unsubscribes.
- Handles to a `Map` must be invalidated on `Game.DeinitAndRemoveMap` and on game load.

## Domain candidates

Derived from the tracker and manager lists above. Order follows the plan in the repository docs.

1. `game.pawns` (shipped, see [pawns](../api/pawns.md); gear and policies still open): skills and passions, needs, thoughts, relations, story (backstory, traits), genes, equipment, apparel, inventory, timetable, outfit and policies
2. `game.things`: comps, quality, stuff, stack, hit points, minification
3. `game.maps`: cells, terrain, roof, zones, areas, rooms, designations, lords, weather per map
4. `game.time`: ticks, date, speed, pause, `GenDate` helpers
5. `game.research`: projects, progress, finish
6. `game.incidents`, `game.storyteller`, `game.quests`
7. `game.world`: tiles, world objects, caravans, factions on the world map
8. `game.combat`: damage, verbs, stats
9. `game.economy`: trade, silver, market value
10. `game.buildings`: power nets, bills, storage, doors, blueprints and frames
11. `game.plants`, `game.animals`
12. `game.defs`: runtime Def reads and controlled mutation
13. `game.saves`: custom persistence, save and load events
14. `game.ui`: gizmos, designators, inspect tabs, textures and flecks
15. DLC kits: `game.ideology`, `game.royalty`, `game.biotech`, `game.anomaly`, `game.odyssey`

## Reproducing this inventory

Decompile with `ilspycmd -p -o <outdir> Assembly-CSharp.dll`, then search the output for the signatures in the table. Do not commit decompiled source to this repository.
