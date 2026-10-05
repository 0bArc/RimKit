using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.generation, game.scenarios, game.plants, game.conditions, game.weather (more). Ops: generation.*, scenario.*, plant.*, condition.*, weather.*.
    internal static class ApiWorldSys
    {
        public static void Register()
        {
            R("generation.terrains", Terrains);
            R("generation.map_generators", MapGenerators);
            R("generation.map_steps", MapSteps);
            R("generation.world_steps", WorldSteps);
            R("generation.features", Features);
            R("generation.rivers", Rivers);
            R("generation.roads", Roads);
            R("generation.base_gen", BaseGenRun);
            R("scenario.list", ScenarioList);
            R("scenario.current", ScenarioCurrent);
            R("scenario.part_defs", ScenarioPartDefs);
            R("scenario.disallowed_buildings", DisallowedBuildings);
            R("scenario.set_building_allowed", SetBuildingAllowed);
            R("plant.info", PlantInfo);
            R("plant.set_growth", PlantSetGrowth);
            R("plant.defs", PlantDefs);
            R("plant.can_grow", CanGrow);
            R("plant.sow", Sow);
            R("plant.harvest", Harvest);
            R("plant.fertility", Fertility);
            R("plant.zones", GrowingZones);
            R("plant.set_zone_plant", SetZonePlant);
            R("condition.list", ConditionList);
            R("condition.defs", ConditionDefs);
            R("condition.start", ConditionStart);
            R("condition.stop", ConditionEnd);
            R("condition.temperature_offset", TemperatureOffset);
            R("weather.defs", WeatherDefs);
            R("weather.sky", Sky);
            R("weather.season", SeasonInfo);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static Map MapOrCurrent(Dictionary<string, string> a) => string.IsNullOrEmpty(Str(a, "h")) ? Find.CurrentMap : MapOf(a);

        private static string BadMap() => Fail("RK2001", "map handle is stale or null");

        // ---- generation

        private static string Terrains(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (TerrainDef t in DefDatabase<TerrainDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", t.defName).S("label", t.label).F("fertility", t.fertility).I("path_cost", t.pathCost).B("water", t.IsWater)
                    .B("smoothable", t.smoothedTerrain != null).Raw("affordances", Jb.Arr().Also(x => { foreach (var af in t.affordances) x.AddS(af.defName); }).ToString()));
            return arr.Ok();
        }

        private static string MapGenerators(Dictionary<string, string> a) => OkStringList(DefDatabase<MapGeneratorDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string MapSteps(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            string only = Str(a, "generator");
            if (!string.IsNullOrEmpty(only))
            {
                MapGeneratorDef g = DefDatabase<MapGeneratorDef>.GetNamedSilentFail(only);
                if (g == null) return Fail("RK3001", "unknown map generator " + only);
                foreach (GenStepDef s in g.genSteps) arr.Add(Jb.Obj().S("def", s.defName).F("order", s.order).S("class", s.genStep?.GetType().Name));
                return arr.Ok();
            }

            foreach (GenStepDef s in DefDatabase<GenStepDef>.AllDefsListForReading) arr.Add(Jb.Obj().S("def", s.defName).F("order", s.order).S("class", s.genStep?.GetType().Name));
            return arr.Ok();
        }

        private static string WorldSteps(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (WorldGenStepDef s in DefDatabase<WorldGenStepDef>.AllDefsListForReading) arr.Add(Jb.Obj().S("def", s.defName).F("order", s.order).S("class", s.worldGenStep?.GetType().Name));
            return arr.Ok();
        }

        private static string Features(Dictionary<string, string> a)
        {
            if (Find.World?.features == null) return Fail("RK3001", "no world is loaded");
            var arr = Jb.Arr();
            foreach (WorldFeature f in Find.World.features.features)
                arr.Add(Jb.Obj().S("name", f.name).S("def", f.def?.defName).I("id", f.uniqueID).F("max_draw_size", f.maxDrawSizeInTiles));
            return arr.Ok();
        }

        private static string Rivers(Dictionary<string, string> a) => OkStringList(DefDatabase<RiverDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string Roads(Dictionary<string, string> a) => OkStringList(DefDatabase<RoadDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        // Runs a base generation symbol on a rectangle. Known symbols include basePart_outdoors, ancientRuins, settlement.
        // opts: faction, rotation, stuff.
        private static string BaseGenRun(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            string symbol = Str(a, "symbol");
            if (string.IsNullOrEmpty(symbol)) return Fail("RK1001", "symbol is required");
            int x1 = Int(a, "x"), z1 = Int(a, "z"), x2 = Int(a, "x2"), z2 = Int(a, "z2");
            var rect = new CellRect(Math.Min(x1, x2), Math.Min(z1, z2), Math.Abs(x2 - x1) + 1, Math.Abs(z2 - z1) + 1);
            if (!rect.InBounds(map)) return Fail("RK1001", "the rectangle is outside the map");
            Opts o = Opts.From(a, "opts");
            var rp = new ResolveParams { rect = rect, faction = o.Handle<Faction>("faction") };
            if (!string.IsNullOrEmpty(o.Str("stuff"))) rp.wallStuff = DefDatabase<ThingDef>.GetNamedSilentFail(o.Str("stuff"));
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push(symbol, rp);
            BaseGen.Generate();
            return OkBool(true);
        }

        // ---- scenarios

        private static string ScenarioList(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ScenarioDef d in DefDatabase<ScenarioDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("name", d.scenario?.name).S("summary", d.scenario?.summary));
            return arr.Ok();
        }

        private static string ScenarioCurrent(Dictionary<string, string> a)
        {
            Scenario s = Find.Scenario;
            if (s == null) return Fail("RK3001", "no game is loaded");
            var parts = Jb.Arr();
            foreach (ScenPart p in s.AllParts) parts.Add(Jb.Obj().S("class", p.GetType().Name).S("label", p.Label).S("def", p.def?.defName));
            return Jb.Obj().S("name", s.name).S("summary", s.summary).S("description", s.description).Raw("parts", parts.ToString()).Ok();
        }

        private static string ScenarioPartDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ScenPartDef d in DefDatabase<ScenPartDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("category", d.category.ToString()).S("class", d.scenPartClass?.Name));
            return arr.Ok();
        }

        private static string DisallowedBuildings(Dictionary<string, string> a)
        {
            if (Current.Game?.Rules == null) return Fail("RK3001", "no game is loaded");
            var names = new List<string>();
            if (AccessTools.Field(typeof(GameRules), "disallowedBuildings")?.GetValue(Current.Game.Rules) is System.Collections.IEnumerable items)
            {
                foreach (object o in items) names.Add(o is Def d ? d.defName : o?.ToString() ?? "");
            }

            return OkStringList(names);
        }

        private static string SetBuildingAllowed(Dictionary<string, string> a)
        {
            if (Current.Game?.Rules == null) return Fail("RK3001", "no game is loaded");
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            Current.Game.Rules.SetAllowBuilding(def, Bool(a, "allowed"));
            return OkBool(true);
        }

        // ---- plants

        private static Plant PlantOf(Dictionary<string, string> a, out string err)
        {
            err = null;
            Thing t = ThingOf(a);
            if (t == null) { err = Fail("RK2001", "thing handle is stale or null"); return null; }
            if (!(t is Plant p)) { err = Fail("RK3003", t.def.defName + " is not a plant"); return null; }
            return p;
        }

        private static string PlantInfo(Dictionary<string, string> a)
        {
            Plant p = PlantOf(a, out string err);
            if (err != null) return err;
            return Jb.Obj().S("def", p.def.defName).F("growth", p.Growth).S("stage", p.LifeStage.ToString()).B("harvestable", p.HarvestableNow).I("yield", p.YieldNow())
                .F("grow_days", p.def.plant.growDays).F("rate", p.GrowthRate).B("sown", p.sown).B("dying", p.Dying).Ok();
        }

        private static string PlantSetGrowth(Dictionary<string, string> a)
        {
            Plant p = PlantOf(a, out string err);
            if (err != null) return err;
            p.Growth = Math.Max(0f, Math.Min(1f, Float(a, "growth")));
            return OkFloat(p.Growth);
        }

        private static string PlantDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x.plant != null))
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).F("grow_days", d.plant.growDays).F("fertility_min", d.plant.fertilityMin).F("fertility_sensitivity", d.plant.fertilitySensitivity)
                    .S("harvest", d.plant.harvestedThingDef?.defName).F("yield", d.plant.harvestYield).B("sowable", d.plant.Sowable).F("sow_work", d.plant.sowWork));
            return arr.Ok();
        }

        private static string CanGrow(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def?.plant == null) return Fail("RK3001", Str(a, "def") + " is not a plant def");
            bool fertile = map.fertilityGrid.FertilityAt(c) >= def.plant.fertilityMin;
            bool terrain = c.GetTerrain(map).fertility > 0f;
            return Jb.Obj().B("fertile", fertile).B("terrain", terrain).B("season", PlantUtility.GrowthSeasonNow(map, def)).B("free", c.GetPlant(map) == null)
                .B("ok", fertile && terrain && c.GetPlant(map) == null).Ok();
        }

        private static string Sow(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def?.plant == null) return Fail("RK3001", Str(a, "def") + " is not a plant def");
            if (c.GetPlant(map) != null) return Fail("RK3003", "the cell already has a plant");
            var plant = (Plant)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(plant, c, map);
            plant.Growth = string.IsNullOrEmpty(Str(a, "growth")) ? 0.05f : Math.Max(0f, Math.Min(1f, Float(a, "growth")));
            return OkHandle(plant);
        }

        // Harvests a plant at once: drops its yield next to it. A plant that is not harvestable yields nothing.
        private static string Harvest(Dictionary<string, string> a)
        {
            Plant p = PlantOf(a, out string err);
            if (err != null) return err;
            if (!p.HarvestableNow || p.def.plant.harvestedThingDef == null) return OkInt(0);
            int n = p.YieldNow();
            Thing yield = ThingMaker.MakeThing(p.def.plant.harvestedThingDef);
            yield.stackCount = Math.Max(1, n);
            GenPlace.TryPlaceThing(yield, p.Position, p.Map, ThingPlaceMode.Near);
            if (p.def.plant.HarvestDestroys) p.Destroy(); else p.Growth = 0.08f;
            return OkInt(n);
        }

        private static string Fertility(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            return OkFloat(map.fertilityGrid.FertilityAt(c));
        }

        private static string GrowingZones(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            var arr = Jb.Arr();
            foreach (Zone_Growing z in map.zoneManager.AllZones.OfType<Zone_Growing>())
                arr.Add(Jb.Obj().I("id", z.ID).S("label", z.label).S("plant", z.GetPlantDefToGrow()?.defName).I("cells", z.cells.Count).B("sow", z.allowSow));
            return arr.Ok();
        }

        private static string SetZonePlant(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            Zone_Growing z = map.zoneManager.AllZones.OfType<Zone_Growing>().FirstOrDefault(x => x.ID == Int(a, "id"));
            if (z == null) return Fail("RK3001", "no growing zone with id " + Str(a, "id"));
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def?.plant == null) return Fail("RK3001", Str(a, "def") + " is not a plant def");
            z.SetPlantDefToGrow(def);
            return OkStr(z.GetPlantDefToGrow().defName);
        }

        // ---- conditions and weather

        private static string ConditionList(Dictionary<string, string> a)
        {
            GameConditionManager gcm = string.IsNullOrEmpty(Str(a, "h")) && Find.CurrentMap == null ? Find.World?.gameConditionManager : MapOrCurrent(a)?.gameConditionManager;
            if (gcm == null) return Fail("RK3001", "no game is loaded");
            var arr = Jb.Arr();
            foreach (GameCondition c in gcm.ActiveConditions)
                arr.Add(Jb.Obj().S("def", c.def.defName).S("label", c.LabelCap).B("permanent", c.Permanent).I("ticks_left", c.Permanent ? -1 : c.TicksLeft).I("age", c.TicksPassed));
            return arr.Ok();
        }

        private static string ConditionDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (GameConditionDef d in DefDatabase<GameConditionDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).F("temperature_offset", d.temperatureOffset));
            return arr.Ok();
        }

        // opts: duration (ticks, default 2 days), world (true for a world-wide condition).
        private static string ConditionStart(Dictionary<string, string> a)
        {
            GameConditionDef def = DefDatabase<GameConditionDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown game condition " + Str(a, "def"));
            Opts o = Opts.From(a, "opts");
            int duration = o.Has("duration") ? Math.Max(60, o.Int("duration")) : 120000;
            GameCondition cond = GameConditionMaker.MakeCondition(def, duration);
            GameConditionManager gcm = o.Bool("world") ? Find.World?.gameConditionManager : MapOrCurrent(a)?.gameConditionManager;
            if (gcm == null) return Fail("RK3001", "no map or world to start the condition on");
            gcm.RegisterCondition(cond);
            return OkBool(true);
        }

        private static string ConditionEnd(Dictionary<string, string> a)
        {
            GameConditionDef def = DefDatabase<GameConditionDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown game condition " + Str(a, "def"));
            Map map = MapOrCurrent(a);
            int n = 0;
            foreach (GameConditionManager gcm in new[] { map?.gameConditionManager, Find.World?.gameConditionManager })
            {
                if (gcm == null) continue;
                foreach (GameCondition c in gcm.ActiveConditions.Where(x => x.def == def).ToList()) { c.End(); n++; }
            }

            return OkInt(n);
        }

        private static string TemperatureOffset(Dictionary<string, string> a)
        {
            Map map = MapOrCurrent(a);
            if (map == null) return BadMap();
            return OkFloat(map.gameConditionManager.ActiveConditions.Sum(c => c.TemperatureOffset()));
        }

        private static string WeatherDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (WeatherDef d in DefDatabase<WeatherDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).F("rain", d.rainRate).F("wind_factor", d.windSpeedFactor).F("accuracy", d.accuracyMultiplier)
                    .F("temperature_offset", d.temperatureRange.TrueMin));
            return arr.Ok();
        }

        private static string Sky(Dictionary<string, string> a)
        {
            Map map = MapOrCurrent(a);
            if (map == null) return BadMap();
            return Jb.Obj().F("glow", map.skyManager.CurSkyGlow).F("wind", map.windManager.WindSpeed).S("weather", map.weatherManager.curWeather.defName)
                .S("last_weather", map.weatherManager.lastWeather.defName).F("transition", map.weatherManager.TransitionLerpFactor).F("outdoor_temperature", map.mapTemperature.OutdoorTemp)
                .F("seasonal_temperature", map.mapTemperature.SeasonalTemp).Ok();
        }

        private static string SeasonInfo(Dictionary<string, string> a)
        {
            Map map = MapOrCurrent(a);
            if (map == null) return BadMap();
            Season s = GenLocalDate.Season(map);
            return Jb.Obj().S("season", s.ToString()).S("label", s.Label()).F("year_percent", GenLocalDate.YearPercent(map)).I("day_of_season", GenLocalDate.DayOfSeason(map)).Ok();
        }
    }
}
