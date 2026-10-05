using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.query: efficient queries over many objects with filters, so Lua does not loop over everything. Ops are query.*.
    internal static class ApiQuery
    {
        public static void Register()
        {
            R("query.things", Things);
            R("query.pawns", Pawns);
            R("query.count_by_def", CountByDef);
            R("query.radius", Radius);
            R("query.nearest", Nearest);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static bool InArea(Thing t, Opts o)
        {
            Dictionary<string, object> area = o.Obj("area");
            if (area == null) return true;
            int x1 = (int)Json.GetLong(area, "x1", int.MinValue), x2 = (int)Json.GetLong(area, "x2", int.MaxValue);
            int z1 = (int)Json.GetLong(area, "z1", int.MinValue), z2 = (int)Json.GetLong(area, "z2", int.MaxValue);
            IntVec3 p = t.Position;
            return p.x >= Math.Min(x1, x2) && p.x <= Math.Max(x1, x2) && p.z >= Math.Min(z1, z2) && p.z <= Math.Max(z1, z2);
        }

        private static IEnumerable<Thing> Source(Map map, Opts o, out string err)
        {
            err = null;
            string group = o.Str("group");
            if (!string.IsNullOrEmpty(group))
            {
                if (!Enum.TryParse(group, true, out ThingRequestGroup g)) { err = Fail("RK1001", "unknown thing group " + group); return null; }
                return map.listerThings.ThingsInGroup(g);
            }

            List<string> defs = o.Strings("defs");
            if (!string.IsNullOrEmpty(o.Str("def"))) defs.Add(o.Str("def"));
            if (defs.Count > 0)
            {
                var all = new List<Thing>();
                foreach (string name in defs)
                {
                    ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (d == null) { err = Fail("RK3001", "unknown thing def " + name); return null; }
                    all.AddRange(map.listerThings.ThingsOfDef(d));
                }

                return all;
            }

            return map.listerThings.AllThings;
        }

        private static Faction FactionFilter(Opts o, out bool set)
        {
            set = o.Has("faction");
            Faction f = o.Handle<Faction>("faction");
            if (f == null && string.Equals(o.Str("faction"), "player", StringComparison.OrdinalIgnoreCase)) f = Faction.OfPlayer;
            return f;
        }

        // opts: map (required), def, defs[], group (ThingRequestGroup name such as HaulableEver, Weapon, Plant), faction,
        // area {x1,z1,x2,z2}, spawned_only (default true), limit.
        private static string Things(Dictionary<string, string> a)
        {
            Opts o = Opts.From(a, "opts");
            Map map = o.Handle<Map>("map");
            if (map == null) return Fail("RK2001", "opts.map is required");
            IEnumerable<Thing> src = Source(map, o, out string err);
            if (err != null) return err;
            Faction f = FactionFilter(o, out bool hasFaction);
            bool spawnedOnly = o.Bool("spawned_only", true);
            int limit = o.Has("limit") ? o.Int("limit") : int.MaxValue;
            var result = new List<Thing>();
            foreach (Thing t in src)
            {
                if (spawnedOnly && !t.Spawned) continue;
                if (hasFaction && t.Faction != f) continue;
                if (!InArea(t, o)) continue;
                result.Add(t);
                if (result.Count >= limit) break;
            }

            return OkHandles(result);
        }

        // opts: map (all maps when omitted), faction ("player", "hostile", "neutral" or a faction), kind, humanlike, animal,
        // colonist, prisoner, slave, downed, dead, drafted, area {x1,z1,x2,z2}, limit.
        private static string Pawns(Dictionary<string, string> a)
        {
            Opts o = Opts.From(a, "opts");
            Map only = o.Handle<Map>("map");
            IEnumerable<Pawn> src = only != null ? only.mapPawns.AllPawns : Find.Maps.SelectMany(m => m.mapPawns.AllPawns);
            string factionText = o.Str("faction");
            Faction f = o.Handle<Faction>("faction");
            string kind = o.Str("kind");
            int limit = o.Has("limit") ? o.Int("limit") : int.MaxValue;
            var result = new List<Pawn>();
            foreach (Pawn p in src)
            {
                if (!p.Spawned && !o.Bool("include_unspawned")) continue;
                if (f != null && p.Faction != f) continue;
                if (f == null && !string.IsNullOrEmpty(factionText))
                {
                    switch (factionText.ToLowerInvariant())
                    {
                        case "player": if (p.Faction != Faction.OfPlayer) continue; break;
                        case "hostile": if (p.Faction == null || !p.Faction.HostileTo(Faction.OfPlayer)) continue; break;
                        case "neutral": if (p.Faction == null || p.Faction.IsPlayer || p.Faction.HostileTo(Faction.OfPlayer)) continue; break;
                        default: return Fail("RK1001", "faction must be player, hostile, neutral or a faction");
                    }
                }

                if (!string.IsNullOrEmpty(kind) && p.kindDef?.defName != kind) continue;
                if (o.Has("humanlike") && p.RaceProps.Humanlike != o.Bool("humanlike")) continue;
                if (o.Has("animal") && p.RaceProps.Animal != o.Bool("animal")) continue;
                if (o.Has("colonist") && p.IsColonist != o.Bool("colonist")) continue;
                if (o.Has("prisoner") && p.IsPrisoner != o.Bool("prisoner")) continue;
                if (o.Has("slave") && p.IsSlave != o.Bool("slave")) continue;
                if (o.Has("downed") && p.Downed != o.Bool("downed")) continue;
                if (o.Has("dead") && p.Dead != o.Bool("dead")) continue;
                if (o.Has("drafted") && (p.Drafted) != o.Bool("drafted")) continue;
                if (!InArea(p, o)) continue;
                result.Add(p);
                if (result.Count >= limit) break;
            }

            return OkHandles(result);
        }

        // Counts spawned things per def in one call. opts: map (required), group, faction, area.
        private static string CountByDef(Dictionary<string, string> a)
        {
            Opts o = Opts.From(a, "opts");
            Map map = o.Handle<Map>("map");
            if (map == null) return Fail("RK2001", "opts.map is required");
            IEnumerable<Thing> src = Source(map, o, out string err);
            if (err != null) return err;
            Faction f = FactionFilter(o, out bool hasFaction);
            var counts = new Dictionary<string, long>();
            foreach (Thing t in src)
            {
                if (!t.Spawned) continue;
                if (hasFaction && t.Faction != f) continue;
                if (!InArea(t, o)) continue;
                counts.TryGetValue(t.def.defName, out long n);
                counts[t.def.defName] = n + Math.Max(1, t.stackCount);
            }

            var j = Jb.Obj();
            foreach (var kv in counts.OrderBy(k => k.Key)) j.I(kv.Key, kv.Value);
            return j.Ok();
        }

        // Things within a radius of a cell. opts: def, group, faction, limit.
        private static string Radius(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var center = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!center.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            float radius = Math.Max(0.5f, Float(a, "radius"));
            Opts o = Opts.From(a, "opts");
            Faction f = FactionFilter(o, out bool hasFaction);
            string defName = o.Str("def");
            int limit = o.Has("limit") ? o.Int("limit") : int.MaxValue;
            var result = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(center, map, radius, true))
            {
                if (!string.IsNullOrEmpty(defName) && t.def.defName != defName) continue;
                if (hasFaction && t.Faction != f) continue;
                result.Add(t);
                if (result.Count >= limit) break;
            }

            return OkHandles(result);
        }

        // The closest spawned thing to a cell. opts: def or group (one of them), faction, max_distance.
        private static string Nearest(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var center = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            Opts o = Opts.From(a, "opts");
            IEnumerable<Thing> src = Source(map, o, out string err);
            if (err != null) return err;
            Faction f = FactionFilter(o, out bool hasFaction);
            float max = o.Has("max_distance") ? (float)o.Num("max_distance") : float.MaxValue;
            Thing best = null;
            float bestDist = float.MaxValue;
            foreach (Thing t in src)
            {
                if (!t.Spawned) continue;
                if (hasFaction && t.Faction != f) continue;
                float d = (t.Position - center).LengthHorizontal;
                if (d < bestDist && d <= max) { best = t; bestDist = d; }
            }

            return best == null ? OkJson("null") : OkHandle(best);
        }
    }
}
