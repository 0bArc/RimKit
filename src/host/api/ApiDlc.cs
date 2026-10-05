using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // One guard for every DLC function (P5-06). A function registered with D(...) checks that the DLC is active and returns RK3003
    // otherwise, so no DLC op can run on a game without the expansion. tools/check-ops.js fails when a DLC file registers an op any other way.
    internal static class Dlc
    {
        public static readonly string[] Names = { "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey" };

        public static bool Active(string dlc)
        {
            switch (dlc)
            {
                case "Royalty": return ModsConfig.RoyaltyActive;
                case "Ideology": return ModsConfig.IdeologyActive;
                case "Biotech": return ModsConfig.BiotechActive;
                case "Anomaly": return ModsConfig.AnomalyActive;
                case "Odyssey": return ModsConfig.OdysseyActive;
                default: return false;
            }
        }

        // Null when the DLC is active, otherwise the error response.
        public static string Require(string dlc) => Active(dlc) ? null : Fail("RK3003", "the " + dlc + " DLC is not active");

        // Registers an op that only runs when the DLC is active.
        public static void D(string dlc, string op, Func<Dictionary<string, string>, string> fn)
        {
            ApiRegistry.Register(op, a => Require(dlc) ?? fn(a), "advanced", "0.9.0");
        }
    }

    // game.dlc: which expansions are active.
    internal static class ApiDlc
    {
        public static void Register()
        {
            ApiRegistry.Register("dlc.status", Status, "advanced", "0.9.0");
            ApiRegistry.Register("dlc.active", Active, "advanced", "0.9.0");
        }

        private static string Status(Dictionary<string, string> a)
        {
            var j = Jb.Obj();
            foreach (string n in Dlc.Names) j.B(n, Dlc.Active(n));
            return j.Ok();
        }

        private static string Active(Dictionary<string, string> a)
        {
            string name = Str(a, "dlc");
            if (!Dlc.Names.Contains(name)) return Fail("RK1001", "dlc must be one of " + string.Join(", ", Dlc.Names));
            return OkBool(Dlc.Active(name));
        }
    }
}
