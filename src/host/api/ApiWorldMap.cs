using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.world: the world map, tiles, settlements, world objects, caravans, world pawns, map transitions. Ops are world.*.
    // world.weather, set_weather, temperature and current are older ops and keep working.
    internal static class ApiWorldMap
    {
        public static void Register()
        {
            R("world.info", Info);
            R("world.tile_info", TileInfo);
            R("world.distance", Distance);
            R("world.settlements", Settlements);
            R("world.objects", Objects);
            R("world.object_info", ObjectInfo);
            R("world.create_object", CreateObject);
            R("world.remove_object", RemoveObject);
            R("world.caravans", Caravans);
            R("world.travel", Travel);
            R("world.pawns", WorldPawns);
            R("world.map_at", MapAt);
            R("world.generate_map", GenerateMap);
            R("world.biomes", Biomes);
            R("world.find_tile", FindTile);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static string NoWorld() => Fail("RK3001", "no world is loaded");

        private static bool ValidTile(int tile) => Find.WorldGrid != null && tile >= 0 && tile < Find.WorldGrid.TilesCount;

        private static string Info(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            WorldGrid g = Find.WorldGrid;
            return Jb.Obj().I("tiles", g.TilesCount).F("coverage", Find.World.info.planetCoverage).S("seed", Find.World.info.seedString)
                .I("objects", Find.WorldObjects.AllWorldObjects.Count).I("settlements", Find.WorldObjects.Settlements.Count)
                .I("caravans", Find.WorldObjects.Caravans.Count).I("maps", Find.Maps.Count).Ok();
        }

        private static string TileInfo(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            int tile = Int(a, "tile");
            if (!ValidTile(tile)) return Fail("RK1001", "tile is outside the world grid");
            WorldGrid g = Find.WorldGrid;
            Tile t = g[tile];
            var j = Jb.Obj().I("tile", tile).S("biome", t.PrimaryBiome?.defName).S("hilliness", t.hilliness.ToString()).F("elevation", t.elevation)
                .F("temperature", t.temperature).F("rainfall", t.rainfall).B("water", t.WaterCovered).F("pollution", t.pollution);
            Map m = Current.Game.FindMap(tile);
            if (m != null) j.H("map", m);
            WorldObject o = Find.WorldObjects.ObjectsAt(tile).FirstOrDefault();
            if (o != null) j.H("object", o);
            return j.Ok();
        }

        private static string Distance(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            int x = Int(a, "from"), y = Int(a, "to");
            if (!ValidTile(x) || !ValidTile(y)) return Fail("RK1001", "tile is outside the world grid");
            return OkFloat(Find.WorldGrid.ApproxDistanceInTiles(x, y));
        }

        private static Jb ObjectJson(WorldObject o)
        {
            var j = Jb.Obj().H("object", o).S("def", o.def.defName).S("label", o.LabelCap).I("tile", o.Tile);
            if (o.Faction != null) j.H("faction", o.Faction);
            return j;
        }

        private static string Settlements(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            Faction f = string.IsNullOrEmpty(Str(a, "faction")) ? null : ObjectHandles.Get<Faction>(Int(a, "faction"));
            var arr = Jb.Arr();
            foreach (Settlement s in Find.WorldObjects.Settlements)
            {
                if (f != null && s.Faction != f) continue;
                arr.Add(ObjectJson(s).S("name", s.Name));
            }

            return arr.Ok();
        }

        private static string Objects(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            string def = Str(a, "def");
            var arr = Jb.Arr();
            foreach (WorldObject o in Find.WorldObjects.AllWorldObjects)
            {
                if (!string.IsNullOrEmpty(def) && o.def.defName != def) continue;
                arr.Add(ObjectJson(o));
            }

            return arr.Ok();
        }

        private static WorldObject ObjectOf(Dictionary<string, string> a) => ObjectHandles.Get<WorldObject>(Int(a, "h"));

        private static string ObjectInfo(Dictionary<string, string> a)
        {
            WorldObject o = ObjectOf(a);
            if (o == null) return Fail("RK2001", "world object handle is stale or null");
            var j = ObjectJson(o).B("has_map", (o as MapParent)?.HasMap ?? false).B("destroyed", o.Destroyed);
            if (o is Settlement s) j.S("name", s.Name).B("can_trade", s.CanTradeNow);
            return j.Ok();
        }

        private static string CreateObject(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            WorldObjectDef def = DefDatabase<WorldObjectDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown world object def " + Str(a, "def"));
            int tile = Int(a, "tile");
            if (!ValidTile(tile)) return Fail("RK1001", "tile is outside the world grid");
            WorldObject o = WorldObjectMaker.MakeWorldObject(def);
            o.Tile = tile;
            if (!string.IsNullOrEmpty(Str(a, "faction"))) o.SetFaction(ObjectHandles.Get<Faction>(Int(a, "faction")));
            Find.WorldObjects.Add(o);
            return OkHandle(o);
        }

        private static string RemoveObject(Dictionary<string, string> a)
        {
            WorldObject o = ObjectOf(a);
            if (o == null) return Fail("RK2001", "world object handle is stale or null");
            if ((o as MapParent)?.HasMap ?? false) return Fail("RK3003", "the object has a loaded map");
            o.Destroy();
            return OkBool(o.Destroyed);
        }

        private static string Caravans(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            var arr = Jb.Arr();
            foreach (Caravan c in Find.WorldObjects.Caravans)
            {
                var j = ObjectJson(c).S("name", c.Name).I("pawns", c.PawnsListForReading.Count).B("player", c.IsPlayerControlled).B("moving", c.pather.Moving);
                if (c.pather.Moving) j.I("destination", c.pather.Destination);
                arr.Add(j);
            }

            return arr.Ok();
        }

        private static string Travel(Dictionary<string, string> a)
        {
            Caravan c = ObjectOf(a) as Caravan;
            if (c == null) return Fail("RK2001", "caravan handle is stale or not a caravan");
            int tile = Int(a, "tile");
            if (!ValidTile(tile)) return Fail("RK1001", "tile is outside the world grid");
            c.pather.StartPath(tile, null, true);
            return OkBool(c.pather.Moving);
        }

        private static string WorldPawns(Dictionary<string, string> a)
        {
            if (Find.WorldPawns == null) return NoWorld();
            Opts o = Opts.From(a, "opts");
            Faction f = o.Handle<Faction>("faction");
            int limit = o.Has("limit") ? o.Int("limit") : int.MaxValue;
            var list = new List<Pawn>();
            foreach (Pawn p in Find.WorldPawns.AllPawnsAlive)
            {
                if (f != null && p.Faction != f) continue;
                if (o.Has("humanlike") && p.RaceProps.Humanlike != o.Bool("humanlike")) continue;
                list.Add(p);
                if (list.Count >= limit) break;
            }

            return OkHandles(list);
        }

        private static string MapAt(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            return OkHandle(Current.Game.FindMap(Int(a, "tile")));
        }

        // Generates (or returns) the map for a tile. Slow: it builds a whole map. size defaults to 75.
        private static string GenerateMap(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            int tile = Int(a, "tile");
            if (!ValidTile(tile)) return Fail("RK1001", "tile is outside the world grid");
            int size = string.IsNullOrEmpty(Str(a, "size")) ? 75 : Math.Max(25, Math.Min(250, Int(a, "size")));
            Map m = GetOrGenerateMapUtility.GetOrGenerateMap(tile, new IntVec3(size, 1, size), null);
            return OkHandle(m);
        }

        private static string Biomes(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (BiomeDef b in DefDatabase<BiomeDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", b.defName).S("label", b.label).B("playable", b.canBuildBase).F("animal_density", b.animalDensity).F("plant_density", b.plantDensity));
            return arr.Ok();
        }

        // opts: biome, near (tile), min_distance, max_distance (in tiles), allow_settled. Returns the nearest matching tile or nil.
        private static string FindTile(Dictionary<string, string> a)
        {
            if (Find.World == null) return NoWorld();
            Opts o = Opts.From(a, "opts");
            string biome = o.Str("biome");
            int near = o.Has("near") ? o.Int("near") : (Find.CurrentMap?.Tile ?? 0);
            double min = o.Num("min_distance", 0), max = o.Num("max_distance", double.MaxValue);
            WorldGrid g = Find.WorldGrid;
            int best = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < g.TilesCount; i++)
            {
                Tile t = g[i];
                if (t.WaterCovered || !t.PrimaryBiome.canBuildBase) continue;
                if (!string.IsNullOrEmpty(biome) && t.PrimaryBiome.defName != biome) continue;
                if (!o.Bool("allow_settled") && Find.WorldObjects.AnyWorldObjectAt(i)) continue;
                double d = g.ApproxDistanceInTiles(near, i);
                if (d < min || d > max || d >= bestDist) continue;
                best = i;
                bestDist = d;
            }

            return best < 0 ? OkJson("null") : OkInt(best);
        }
    }
}
