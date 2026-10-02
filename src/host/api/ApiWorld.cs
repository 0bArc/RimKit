using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;
using static RimLuaKit.ApiHelpers;

namespace RimLuaKit
{
    internal static class ApiWorld
    {
        public static void Register()
        {
            ApiRegistry.Register("world.weather", Weather, "gameplay");
            ApiRegistry.Register("world.set_weather", SetWeather, "gameplay");
            ApiRegistry.Register("world.temperature", Temperature, "gameplay");
            ApiRegistry.Register("incident.try_fire", TryFire, "gameplay");
            ApiRegistry.Register("incident.list", ListIncidents, "gameplay");
        }

        private static string Weather(Dictionary<string, string> args)
        {
            Map map = MapOf(args) ?? Find.CurrentMap;
            return OkStr(map?.weatherManager?.curWeather?.defName ?? "");
        }

        private static string SetWeather(Dictionary<string, string> args)
        {
            Map map = MapOf(args) ?? Find.CurrentMap;
            WeatherDef w = DefDatabase<WeatherDef>.GetNamedSilentFail(Str(args, "def"));
            if (map?.weatherManager == null || w == null) return Err("bad map/weather");
            map.weatherManager.TransitionTo(w);
            return OkBool(true);
        }

        private static string Temperature(Dictionary<string, string> args)
        {
            Map map = MapOf(args) ?? Find.CurrentMap;
            if (map == null) return OkFloat(0f);
            IntVec3 cell = new IntVec3(Int(args, "x"), 0, Int(args, "z"));
            if (!cell.InBounds(map)) cell = map.Center;
            return OkFloat(cell.GetTemperature(map));
        }

        private static string TryFire(Dictionary<string, string> args)
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(Str(args, "def"));
            if (def == null) return Err("bad incident");
            Map map = MapOf(args) ?? Find.CurrentMap;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map ?? (IIncidentTarget)Find.World);
            if (map != null) parms.target = map;
            bool ok = def.Worker.TryExecute(parms);
            return OkBool(ok);
        }

        private static string ListIncidents(Dictionary<string, string> args)
        {
            var list = new List<string>();
            foreach (IncidentDef d in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (d != null) list.Add(d.defName);
            }
            return OkStringList(list);
        }
    }

    internal static class ApiAudio
    {
        public static void Register()
        {
            ApiRegistry.Register("audio.play", Play, "gameplay");
        }

        private static string Play(Dictionary<string, string> args)
        {
            SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail(Str(args, "def"));
            if (def == null) return Err("bad sound");
            def.PlayOneShotOnCamera();
            return OkBool(true);
        }
    }

    internal static class ApiMapQuery
    {
        public static void Register()
        {
            ApiRegistry.Register("map.pawns_of_faction", PawnsOfFaction, "gameplay");
            ApiRegistry.Register("map.things_of_def_count", ThingsOfDefCount, "gameplay");
        }

        private static string PawnsOfFaction(Dictionary<string, string> args)
        {
            Map map = MapOf(args) ?? Find.CurrentMap;
            Faction f = ObjectHandles.Get<Faction>(Int(args, "faction"));
            var list = new List<object>();
            if (map?.mapPawns?.AllPawns != null)
            {
                foreach (Pawn p in map.mapPawns.AllPawns)
                {
                    if (p != null && (f == null || p.Faction == f)) list.Add(p);
                }
            }
            return OkHandles(list);
        }

        private static string ThingsOfDefCount(Dictionary<string, string> args)
        {
            Map map = MapOf(args) ?? Find.CurrentMap;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(args, "def"));
            if (map == null || def == null) return OkInt(0);
            return OkInt(map.listerThings.ThingsOfDef(def)?.Count ?? 0);
        }
    }
}
