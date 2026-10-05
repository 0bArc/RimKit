using System.Collections.Generic;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    internal static class ApiBuildings
    {
        public static void Register()
        {
            ApiRegistry.Register("building.is_building", IsBuilding, "gameplay");
            ApiRegistry.Register("building.power_on", PowerOn, "gameplay");
            ApiRegistry.Register("building.set_power", SetPower, "gameplay");
            ApiRegistry.Register("building.flickable", IsFlickable, "gameplay");
            ApiRegistry.Register("building.flick", Flick, "gameplay");
            ApiRegistry.Register("building.room_role", RoomRole, "gameplay");
        }

        private static Building AsBuilding(Dictionary<string, string> args) => ThingOf(args) as Building;

        private static string IsBuilding(Dictionary<string, string> args) => OkBool(AsBuilding(args) != null);

        private static CompPowerTrader PowerComp(Building b) => b?.TryGetComp<CompPowerTrader>();

        private static string PowerOn(Dictionary<string, string> args)
        {
            CompPowerTrader c = PowerComp(AsBuilding(args));
            if (c == null) return OkBool(false);
            return OkBool(c.PowerOn);
        }

        private static string SetPower(Dictionary<string, string> args)
        {
            CompPowerTrader c = PowerComp(AsBuilding(args));
            if (c == null) return Err("no power trader");
            c.PowerOn = Bool(args, "v");
            return OkBool(c.PowerOn);
        }

        private static string IsFlickable(Dictionary<string, string> args)
        {
            Building b = AsBuilding(args);
            return OkBool(b?.TryGetComp<CompFlickable>() != null);
        }

        private static string Flick(Dictionary<string, string> args)
        {
            Building b = AsBuilding(args);
            CompFlickable flick = b?.TryGetComp<CompFlickable>();
            if (flick == null) return Err("not flickable");
            bool want = Bool(args, "v");
            if (flick.SwitchIsOn != want)
            {
                flick.DoFlick();
            }
            return OkBool(flick.SwitchIsOn);
        }

        private static string RoomRole(Dictionary<string, string> args)
        {
            Building b = AsBuilding(args);
            Room room = b?.GetRoom();
            return OkStr(room?.Role?.defName ?? "");
        }
    }
}
