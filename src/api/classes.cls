# Lua-backed game classes (P4-06). See infrastructure/tools/gen-lua-classes.js for the syntax.
# XML names the proxy class and the Lua class: <li Class="RimKit.CompProperties_Lua"><luaClass>my_comp</luaClass></li>, or for defs a
# <modExtensions><li Class="RimKit.DefModExtension_Lua"><luaClass>my_class</luaClass></li></modExtensions>.

# ---- priority 1: behavior on things, work, health, events, stats

@class comp ThingComp_Lua : ThingComp key=((CompProperties_Lua)props).luaClass
@props CompProperties_Lua : CompProperties ctor=compClass = typeof(ThingComp_Lua);
@field public Dictionary<string, string> luaData = new Dictionary<string, string>();
@raw public override void PostExposeData() { base.PostExposeData(); Scribe_Collections.Look(ref luaData, "luaData", LookMode.Value, LookMode.Value); if (luaData == null) luaData = new Dictionary<string, string>(); }
void PostSpawnSetup(bool respawningAfterLoad) : spawn(parent, respawningAfterLoad) !base
void CompTick() : tick(parent) !base
void CompTickRare() : tick_rare(parent) !base
void CompTickLong() : tick_long(parent) !base
string CompInspectStringExtra() : inspect_string(parent) -> null
void PostDestroy(DestroyMode mode, Map previousMap) : destroyed(parent, mode.ToString()) !base
void PostDeSpawn(Map map, DestroyMode mode) : despawned(parent) !base

@class hediff_comp HediffComp_Lua : HediffComp key=((HediffCompProperties_Lua)props).luaClass
@props HediffCompProperties_Lua : HediffCompProperties ctor=compClass = typeof(HediffComp_Lua);
void CompPostPostAdd(DamageInfo? dinfo) : post_add(parent.pawn, parent) !base
void CompPostTick(ref float severityAdjustment) : tick(parent.pawn, parent) !base
void CompPostPostRemoved() : post_removed(parent.pawn, parent) !base
@raw public override string CompTipStringExtra => LuaClasses.Str(L("tip", parent.pawn, parent), null);

@class incident IncidentWorker_Lua : IncidentWorker key=LuaClasses.OfDef(def)
protected bool CanFireNowSub(IncidentParms parms) : can_fire(LuaClasses.Parms(parms)) -> base
protected bool TryExecuteWorker(IncidentParms parms) : execute(LuaClasses.Parms(parms)) -> base

@class condition GameCondition_Lua : GameCondition key=LuaClasses.OfDef(def)
void Init() : start(SingleMap) !base
void GameConditionTick() : tick(SingleMap) !base
void End() : stop(SingleMap) !base
float TemperatureOffset() : temperature_offset(SingleMap) -> base

@class storyteller StorytellerComp_Lua : StorytellerComp key=((StorytellerCompProperties_Lua)props).luaClass
@props StorytellerCompProperties_Lua : StorytellerCompProperties ctor=compClass = typeof(StorytellerComp_Lua);
@raw public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target) { object r = L("incidents", target as Map, Find.TickManager.TicksGame); if (!(r is List<object> list)) yield break; foreach (object o in list) { var d = Json.AsObject(o); if (d == null) continue; IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(Json.GetString(d, "incident")); if (def == null) continue; IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, target); if (d.ContainsKey("points")) parms.points = Convert.ToSingle(d["points"], System.Globalization.CultureInfo.InvariantCulture); yield return new FiringIncident(def, this, parms); } }

@class stat_part StatPart_Lua : StatPart key=luaClass
@field public string luaClass;
@raw public override void TransformValue(StatRequest req, ref float val) { object r = L("transform", req.Thing, val, parentStat != null ? parentStat.defName : null); if (LuaClasses.IsNumber(r)) val = LuaClasses.Float(r, val); }
@raw public override string ExplanationPart(StatRequest req) { return LuaClasses.Str(L("explain", req.Thing, parentStat != null ? parentStat.defName : null), null); }

@class stat_worker StatWorker_Lua : StatWorker key=LuaClasses.OfDef(stat)
@raw public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true) { object r = L("value", req.Thing, stat.defName); return LuaClasses.IsNumber(r) ? LuaClasses.Float(r, 0f) : base.GetValueUnfinalized(req, applyPostProcess); }

@class work_giver WorkGiver_Lua : WorkGiver_Scanner key=LuaClasses.OfDef(def)
@raw public override ThingRequest PotentialWorkThingRequest { get { string g = LuaClasses.Str(L("request_group"), null); return !string.IsNullOrEmpty(g) && Enum.TryParse(g, true, out ThingRequestGroup group) ? ThingRequest.ForGroup(group) : ThingRequest.ForGroup(ThingRequestGroup.Undefined); } }
@raw public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) { object r = L("potential", pawn); return r == null ? base.PotentialWorkThingsGlobal(pawn) : LuaClasses.Things<Thing>(r); }
bool HasJobOnThing(Pawn pawn, Thing t, bool forced) : has_job(pawn, t, forced) -> false
Job JobOnThing(Pawn pawn, Thing t, bool forced) : job(pawn, t, forced) -> null
bool ShouldSkip(Pawn pawn, bool forced) : skip(pawn, forced) -> base

@class job_giver JobGiver_Lua : ThinkNode_JobGiver key=luaClass
@field public string luaClass;
protected Job TryGiveJob(Pawn pawn) : job(pawn) -> null

# ---- priority 2: placement, recipes, mood, powers, weapons

@class place_worker PlaceWorker_Lua : PlaceWorker key=null
@raw public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null) { string cls = LuaClasses.OfDef(checkingDef); object r = LuaClasses.Call("place_worker", cls, "allows", checkingDef.defName, loc.x, loc.z, rot.AsInt, map); return LuaClasses.Report(r, true); }

@class recipe_worker RecipeWorker_Lua : RecipeWorker key=LuaClasses.OfDef(recipe)
void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill) : apply(pawn, billDoer, part != null ? part.Index : -1) !base

@class thought_worker ThoughtWorker_Lua : ThoughtWorker key=LuaClasses.OfDef(def)
@raw protected override ThoughtState CurrentStateInternal(Pawn p) { object r = L("state", p); if (r is bool b) return b ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive; if (LuaClasses.IsNumber(r)) return ThoughtState.ActiveAtStage(Math.Max(0, LuaClasses.Int(r, 0))); return ThoughtState.Inactive; }
@raw protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn) { object r = L("social_state", p, otherPawn); if (r is bool b) return b ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive; if (LuaClasses.IsNumber(r)) return ThoughtState.ActiveAtStage(Math.Max(0, LuaClasses.Int(r, 0))); return ThoughtState.Inactive; }

@class mental_state_worker MentalStateWorker_Lua : MentalStateWorker key=LuaClasses.OfDef(def)
bool StateCanOccur(Pawn pawn) : can_occur(pawn) -> base

@class need Need_Lua : Need key=LuaClasses.OfDef(def)
@ctor Pawn pawn : base(pawn)
@raw public override void NeedInterval() { if (def.fallPerDay > 0f) CurLevel -= def.fallPerDay / 400f; L("interval", pawn, CurLevel); }

@class ability CompAbilityEffect_Lua : CompAbilityEffect key=((CompProperties_AbilityEffect_Lua)props).luaClass
@props CompProperties_AbilityEffect_Lua : CompProperties_AbilityEffect ctor=compClass = typeof(CompAbilityEffect_Lua);
void Apply(LocalTargetInfo target, LocalTargetInfo dest) : apply(parent.pawn, target.Thing, target.Cell.x, target.Cell.z) !base
bool Valid(LocalTargetInfo target, bool throwMessages) : valid(parent.pawn, target.Thing) -> base

@class damage_worker DamageWorker_Lua : DamageWorker key=LuaClasses.OfDef(def)
@raw public override DamageResult Apply(DamageInfo dinfo, Thing victim) { DamageResult r = base.Apply(dinfo, victim); L("applied", victim, dinfo.Amount, r != null ? r.totalDamageDealt : 0f); return r; }

@class projectile Projectile_Lua : Projectile key=LuaClasses.OfDef(def)
@raw protected override void Impact(Thing hitThing, bool blockedByShield = false) { L("impact", this, hitThing, launcher); base.Impact(hitThing, blockedByShield); }

@class verb Verb_Lua : Verb_LaunchProjectile key=LuaClasses.OfDef(EquipmentSource != null ? EquipmentSource.def : null)
@raw protected override bool TryCastShot() { object r = L("shoot", CasterPawn, currentTarget.Thing); return LuaClasses.IsBool(r) ? (bool)r : base.TryCastShot(); }

# ---- priority 3: generation, quests, groups, rituals, genes

@class gen_step GenStep_Lua : GenStep key=LuaClasses.OfDef(def)
@raw public override int SeedPart => 4815162;
void Generate(Map map, GenStepParams parms) : generate(map)

@class thing_set_maker ThingSetMaker_Lua : ThingSetMaker key=luaClass
@field public string luaClass;
@raw protected override bool CanGenerateSub(ThingSetMakerParams parms) { return !LuaClasses.Has("thing_set_maker", luaClass, "can_generate") || LuaClasses.Bool(L("can_generate"), true); }
@raw protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings) { object r = L("generate", parms.totalMarketValueRange.HasValue ? parms.totalMarketValueRange.Value.max : 0f); if (!(r is List<object> list)) return; foreach (object o in list) { var d = Json.AsObject(o); if (d == null) continue; ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Json.GetString(d, "def")); if (def == null) continue; Thing t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null); t.stackCount = Math.Max(1, (int)Json.GetLong(d, "count", 1)); outThings.Add(t); } }
@raw protected override IEnumerable<ThingDef> AllGeneratableThingsDebugSub(ThingSetMakerParams parms) { yield break; }

@class quest_part QuestPart_Lua : QuestPart key=luaClass
@field public string luaClass;
@field public string inSignal;
@raw public override void Notify_QuestSignalReceived(Signal signal) { base.Notify_QuestSignalReceived(signal); if (signal.tag == inSignal) L("signal", quest != null ? quest.id : 0, signal.tag); }
@raw public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref luaClass, "luaClass"); Scribe_Values.Look(ref inSignal, "inSignal"); }

@class lord_toil LordToil_Lua : LordToil key=luaClass
@field public string luaClass;
@raw public LordToil_Lua() { }
@raw public LordToil_Lua(string cls) { luaClass = cls; }
@raw public override void UpdateAllDuties() { L("duties", LuaClasses.Handles(lord.ownedPawns.Cast<object>())); }

@class gene Gene_Lua : Gene key=LuaClasses.OfDef(def)
void PostAdd() : post_add(pawn, def.defName) !base
void Tick() : tick(pawn) !base
void PostRemove() : post_remove(pawn, def.defName) !base

@class quest_node QuestNode_Lua : QuestNode key=luaClass
@field public string luaClass;
@raw protected override void RunInt() { L("run"); }
@raw protected override bool TestRunInt(Slate slate) { Slate previous = LuaClasses.CurrentSlate; LuaClasses.CurrentSlate = slate; try { return LuaClasses.Bool(L("test"), true); } finally { LuaClasses.CurrentSlate = previous; } }

@class world_gen_step WorldGenStep_Lua : WorldGenStep key=LuaClasses.OfDef(def)
@raw public override int SeedPart => 7391042;
@raw public override void GenerateFresh(string seed, PlanetLayer layer) { L("generate", seed); }

@class scen_part ScenPart_Lua : ScenPart key=LuaClasses.OfDef(def)
void PostGameStart() : post_game_start() !base
void PostWorldGenerate() : post_world_generate() !base
void GenerateIntoMap(Map map) : generate_into_map(map) !base
void PostMapGenerate(Map map) : post_map_generate(map) !base
void Tick() : tick() !base
@raw public override IEnumerable<string> GetSummaryListEntries(string tag) { string s = LuaClasses.Str(L("summary", tag), null); if (!string.IsNullOrEmpty(s)) yield return s; }

@class ritual_outcome RitualOutcomeEffectWorker_Lua : RitualOutcomeEffectWorker key=LuaClasses.OfDef(def)
@ctor : base()
@ctor RitualOutcomeEffectDef def : base(def)
void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual) : apply(progress, LuaClasses.Handles(totalPresence.Keys.Cast<object>()))

@class think_node ThinkNode_Conditional_Lua : ThinkNode_Conditional key=luaClass
@field public string luaClass;
protected bool Satisfied(Pawn p) : satisfied(p) -> false

# ---- buildings beyond comps

@class building Building_Lua : Building key=LuaClasses.OfDef(def)
void SpawnSetup(Map map, bool respawningAfterLoad) : spawn(this, respawningAfterLoad) !base
void DeSpawn(DestroyMode mode) : despawn(this, mode.ToString()) !base
void Destroy(DestroyMode mode) : destroy(this, mode.ToString()) !base
protected void Tick() : tick(this) !base
void TickRare() : tick_rare(this) !base
void TickLong() : tick_long(this) !base
void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt) : damaged(this, totalDamageDealt) !base
@raw public override string GetInspectString() { string b = base.GetInspectString(); string s = LuaClasses.Str(L("inspect_string", this), null); return string.IsNullOrEmpty(s) ? b : (string.IsNullOrEmpty(b) ? s : b + "\n" + s); }

@class door Building_Door_Lua : Building_Door key=LuaClasses.OfDef(def)
bool PawnCanOpen(Pawn p) : can_open(this, p) -> base
bool BlocksPawn(Pawn p) : blocks(this, p) -> base
protected void Tick() : tick(this) !base

@class storage Building_Storage_Lua : Building_Storage key=LuaClasses.OfDef(def)
void Notify_ReceivedThing(Thing newItem) : received(this, newItem) !base
void Notify_LostThing(Thing newItem) : lost(this, newItem) !base
