using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.maps kit: cells, rooms, zones, designations, lords, reachability. Ops use the singular domain map.*.
    // Older functions (current, list, width, height, pawns, colonists, things, spawn, ...) stay in GameApi.
    internal static class ApiMaps
    {
        public static void Register()
        {
            R("map.info", Info);
            R("map.terrain", Terrain);
            R("map.set_terrain", SetTerrain);
            R("map.roof", Roof);
            R("map.set_roof", SetRoof);
            R("map.fogged", Fogged);
            R("map.unfog", Unfog);
            R("map.temperature", Temperature);
            R("map.light", Light);
            R("map.snow", Snow);
            R("map.set_snow", SetSnow);
            R("map.filth", Filth);
            R("map.clear_filth", ClearFilth);
            R("map.walkable", Walkable);
            R("map.standable", Standable);
            R("map.in_bounds", InBoundsOp);
            R("map.things_at", ThingsAt);
            R("map.room", RoomAt);
            R("map.rooms", Rooms);
            R("map.reachable", Reachable);
            R("map.zones", Zones);
            R("map.zone_at", ZoneAt);
            R("map.areas", Areas);
            R("map.designations", Designations);
            R("map.add_designation", AddDesignation);
            R("map.remove_designations", RemoveDesignations);
            R("map.lords", Lords);
            R("map.components", Components);
            R("map.edge_cell", EdgeCell);
            R("map.drop_spot", DropSpot);
            R("map.cell_near", CellNear);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string BadMap() => Fail("RK2001", "map handle is stale or null");

        // Resolves the map and the cell from h, x, z. Returns an error string or null.
        private static string Where(Dictionary<string, string> a, out Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            map = MapOf(a);
            if (map == null) return BadMap();
            cell = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!cell.InBounds(map)) return Fail("RK1001", "cell " + cell.x + "," + cell.z + " is outside the map");
            return null;
        }

        private static Jb Cell(IntVec3 c) => Jb.Obj().I("x", c.x).I("z", c.z);

        private static string Info(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return BadMap();
            var j = Jb.Obj().I("id", map.uniqueID).I("tile", map.Tile).I("width", map.Size.x).I("height", map.Size.z)
                .I("cells", map.Area).S("biome", map.Biome?.defName).B("is_player_home", map.IsPlayerHome)
                .B("is_pocket", map.IsPocketMap).F("outdoor_temperature", map.mapTemperature.OutdoorTemp)
                .F("seasonal_temperature", map.mapTemperature.SeasonalTemp).S("weather", map.weatherManager?.curWeather?.defName)
                .F("wealth", map.wealthWatcher?.WealthTotal ?? 0);
            if (map.ParentFaction != null) j.H("faction", map.ParentFaction);
            return j.Ok();
        }

        private static string Terrain(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkStr(m.terrainGrid.TerrainAt(c).defName);
        }

        private static string SetTerrain(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            TerrainDef def = DefDatabase<TerrainDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown terrain " + Str(a, "def"));
            m.terrainGrid.SetTerrain(c, def);
            return OkStr(def.defName);
        }

        private static string Roof(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            RoofDef r = m.roofGrid.RoofAt(c);
            return r == null ? OkJson("null") : OkStr(r.defName);
        }

        // def: a RoofDef name, or empty to remove the roof.
        private static string SetRoof(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            RoofDef def = null;
            if (!string.IsNullOrEmpty(Str(a, "def")))
            {
                def = DefDatabase<RoofDef>.GetNamedSilentFail(Str(a, "def"));
                if (def == null) return Fail("RK3001", "unknown roof " + Str(a, "def"));
            }

            m.roofGrid.SetRoof(c, def);
            return OkBool(true);
        }

        private static string Fogged(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkBool(m.fogGrid.IsFogged(c));
        }

        private static string Unfog(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            m.fogGrid.FloodUnfogAdjacent(c);
            return OkBool(!m.fogGrid.IsFogged(c));
        }

        private static string Temperature(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkFloat(GenTemperature.GetTemperatureForCell(c, m));
        }

        private static string Light(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkFloat(m.glowGrid.GroundGlowAt(c));
        }

        private static string Snow(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkFloat(m.snowGrid.GetDepth(c));
        }

        private static string SetSnow(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            float depth = Math.Max(0f, Math.Min(1f, Float(a, "depth")));
            m.snowGrid.SetDepth(c, depth);
            return OkFloat(m.snowGrid.GetDepth(c));
        }

        private static string Filth(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            return OkStringList(c.GetThingList(m).OfType<RimWorld.Filth>().Select(f => f.def.defName).ToList());
        }

        private static string ClearFilth(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            var filth = c.GetThingList(m).OfType<RimWorld.Filth>().ToList();
            foreach (var f in filth) f.Destroy();
            return OkInt(filth.Count);
        }

        private static string Walkable(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkBool(c.Walkable(m));
        }

        private static string Standable(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkBool(c.Standable(m));
        }

        private static string InBoundsOp(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            return OkBool(new IntVec3(Int(a, "x"), 0, Int(a, "z")).InBounds(m));
        }

        private static string ThingsAt(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            return err ?? OkHandles(c.GetThingList(m).ToList());
        }

        private static Jb RoomJson(Room r, bool withCells)
        {
            var j = Jb.Obj().I("id", r.ID).S("role", r.Role?.defName).I("cells", r.CellCount).B("outdoors", r.PsychologicallyOutdoors)
                .F("temperature", r.Temperature).B("proper", r.ProperRoom).I("open_roof", r.OpenRoofCount);
            try { j.F("impressiveness", r.GetStat(RoomStatDefOf.Impressiveness)); } catch (Exception) { }
            if (withCells)
            {
                var cells = Jb.Arr();
                foreach (IntVec3 c in r.Cells.Take(400)) cells.Add(Cell(c));
                j.Raw("cell_list", cells.ToString());
            }

            return j;
        }

        private static string RoomAt(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            Room r = c.GetRoom(m);
            return r == null ? OkJson("null") : RoomJson(r, Bool(a, "cells")).Ok();
        }

        private static string Rooms(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            var arr = Jb.Arr();
            foreach (Room r in m.regionGrid.AllRooms.Where(r => r.ProperRoom && !r.PsychologicallyOutdoors)) arr.Add(RoomJson(r, false));
            return arr.Ok();
        }

        // mode: ByPawn is not needed. Doors are passable. Returns whether a walker can get from one cell to the other.
        private static string Reachable(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            var from = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            var to = new IntVec3(Int(a, "x2"), 0, Int(a, "z2"));
            if (!from.InBounds(m) || !to.InBounds(m)) return Fail("RK1001", "cell is outside the map");
            return OkBool(m.reachability.CanReach(from, to, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false)));
        }

        private static Jb ZoneJson(Zone z)
        {
            return Jb.Obj().I("id", z.ID).S("label", z.label).S("type", z.GetType().Name).I("cells", z.cells.Count);
        }

        private static string Zones(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            var arr = Jb.Arr();
            foreach (Zone z in m.zoneManager.AllZones) arr.Add(ZoneJson(z));
            return arr.Ok();
        }

        private static string ZoneAt(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            Zone z = m.zoneManager.ZoneAt(c);
            return z == null ? OkJson("null") : ZoneJson(z).Ok();
        }

        private static string Areas(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            var arr = Jb.Arr();
            foreach (Area ar in m.areaManager.AllAreas) arr.Add(Jb.Obj().I("id", ar.ID).S("label", ar.Label).I("cells", ar.TrueCount));
            return arr.Ok();
        }

        private static string Designations(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            string only = Str(a, "def");
            var arr = Jb.Arr();
            foreach (Designation d in m.designationManager.AllDesignations)
            {
                if (!string.IsNullOrEmpty(only) && d.def.defName != only) continue;
                var j = Jb.Obj().S("def", d.def.defName);
                if (d.target.HasThing) j.H("thing", d.target.Thing); else j.I("x", d.target.Cell.x).I("z", d.target.Cell.z);
                arr.Add(j);
            }

            return arr.Ok();
        }

        // Targets a thing (thing handle) or a cell (x, z).
        private static string AddDesignation(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            DesignationDef def = DefDatabase<DesignationDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown designation " + Str(a, "def"));
            Thing t = Str(a, "thing") != "" ? ObjectHandles.Get<Thing>(Int(a, "thing")) : null;
            LocalTargetInfo target;
            if (t != null) target = t;
            else
            {
                var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
                if (!c.InBounds(m)) return Fail("RK1001", "cell is outside the map");
                target = c;
            }

            if (m.designationManager.DesignationAt(target.Cell, def) != null && !target.HasThing) return OkBool(false);
            m.designationManager.AddDesignation(new Designation(target, def));
            return OkBool(true);
        }

        private static string RemoveDesignations(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            string only = Str(a, "def");
            int removed = 0;
            foreach (Designation d in m.designationManager.AllDesignations.ToList())
            {
                if (!string.IsNullOrEmpty(only) && d.def.defName != only) continue;
                m.designationManager.RemoveDesignation(d);
                removed++;
            }

            return OkInt(removed);
        }

        private static string Lords(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            var arr = Jb.Arr();
            foreach (Lord l in m.lordManager.lords)
            {
                var j = Jb.Obj().S("job", l.LordJob?.GetType().Name).S("toil", l.CurLordToil?.GetType().Name).I("pawns", l.ownedPawns.Count);
                if (l.faction != null) j.H("faction", l.faction);
                arr.Add(j);
            }

            return arr.Ok();
        }

        private static string Components(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            return OkStringList(m.components.Select(c => c.GetType().Name).ToList());
        }

        private static string EdgeCell(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            return Cell(CellFinder.RandomEdgeCell(m)).Ok();
        }

        private static string DropSpot(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m == null) return BadMap();
            return Cell(DropCellFinder.RandomDropSpot(m)).Ok();
        }

        private static string CellNear(Dictionary<string, string> a)
        {
            string err = Where(a, out Map m, out IntVec3 c);
            if (err != null) return err;
            int radius = Math.Max(1, Int(a, "radius"));
            return CellFinder.TryFindRandomCellNear(c, m, radius, cell => cell.Standable(m), out IntVec3 found)
                ? Cell(found).Ok()
                : OkJson("null");
        }
    }
}
