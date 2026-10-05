using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static RimKit.ApiHelpers;
using static RimKit.Dlc;

namespace RimKit
{
    // game.odyssey, the part that reads gravship engines and space maps with direct game types. Ops: odyssey.has_engine, engine_info, components, space_maps.
    // The older odyssey.layers, engines and gravship ops are in ApiAnomalyOdyssey.cs.
    internal static class ApiOdysseyDepth
    {
        private const string Od = "Odyssey";

        public static void Register()
        {
            D(Od, "odyssey.has_engine", HasEngine);
            D(Od, "odyssey.engine_info", EngineInfo);
            D(Od, "odyssey.components", Components);
            D(Od, "odyssey.space_maps", SpaceMaps);
        }

        private static string HasEngine(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            return OkBool(GravshipUtility.PlayerHasGravEngine(map));
        }

        private static Building_GravEngine EngineOf(Dictionary<string, string> a) => ThingOf(a) as Building_GravEngine;

        private static string EngineInfo(Dictionary<string, string> a)
        {
            Building_GravEngine e = EngineOf(a);
            if (e == null) return Fail("RK2001", "the handle is stale, or it is not a gravship engine");
            var missing = Jb.Arr();
            if (e.MissingComponents != null) foreach (GravshipComponentTypeDef d in e.MissingComponents) missing.AddS(d.defName);
            int cooldown = System.Math.Max(0, e.cooldownCompleteTick - Find.TickManager.TicksGame);
            return Jb.Obj().S("name", e.RenamableLabel).F("fuel", e.TotalFuel).F("max_fuel", e.MaxFuel).F("fuel_per_tile", e.FuelPerTile)
                .F("fuel_savings_percent", e.FuelSavingsPercent).I("max_launch_distance", e.MaxLaunchDistance).I("cooldown_ticks", cooldown)
                .I("substructure_cells", e.ValidSubstructure?.Count ?? 0).I("connected_cells", e.AllConnectedSubstructure?.Count ?? 0)
                .I("components", e.GravshipComponents?.Count ?? 0).Raw("missing_components", missing.ToString()).B("signal_jammer", e.HasSignalJammer).Ok();
        }

        private static string Components(Dictionary<string, string> a)
        {
            Building_GravEngine e = EngineOf(a);
            if (e == null) return Fail("RK2001", "the handle is stale, or it is not a gravship engine");
            var arr = Jb.Arr();
            if (e.GravshipComponents != null)
                foreach (CompGravshipFacility c in e.GravshipComponents)
                    arr.Add(Jb.Obj().H("thing", c.parent).S("def", c.parent.def.defName).B("active", c.CanBeActive));
            return arr.Ok();
        }

        private static string SpaceMaps(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (Map m in Find.Maps)
            {
                if (!(m.Parent is SpaceMapParent space)) continue;
                arr.Add(Jb.Obj().H("map", m).S("name", space.Label).S("layer", m.Tile.LayerDef?.defName).S("precious_resource", space.PreciousResource?.defName).B("gravship_can_land", space.GravShipCanLandOn));
            }
            return arr.Ok();
        }
    }
}
