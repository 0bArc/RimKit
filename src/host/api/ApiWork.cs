using System.Collections.Generic;
using RimWorld;
using Verse;
using static RimLuaKit.ApiHelpers;

namespace RimLuaKit
{
    internal static class ApiWork
    {
        public static void Register()
        {
            ApiRegistry.Register("work.get_priority", GetPriority, "gameplay");
            ApiRegistry.Register("work.set_priority", SetPriority, "gameplay");
            ApiRegistry.Register("work.list_types", ListTypes, "gameplay");
        }

        private static string GetPriority(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(Str(args, "work"));
            if (p?.workSettings == null || wt == null) return OkInt(0);
            return OkInt(p.workSettings.GetPriority(wt));
        }

        private static string SetPriority(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(Str(args, "work"));
            if (p?.workSettings == null || wt == null) return Err("bad pawn/work");
            int pri = Int(args, "priority");
            if (pri < 0) pri = 0;
            if (pri > 4) pri = 4;
            p.workSettings.SetPriority(wt, pri);
            return OkBool(true);
        }

        private static string ListTypes(Dictionary<string, string> args)
        {
            var list = new List<string>();
            foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (w != null) list.Add(w.defName);
            }
            return OkStringList(list);
        }
    }

    internal static class ApiInventory
    {
        public static void Register()
        {
            ApiRegistry.Register("inventory.count_def", CountDef, "gameplay");
            ApiRegistry.Register("inventory.drop_carried", DropCarried, "gameplay");
        }

        private static string CountDef(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(args, "def"));
            if (p == null || def == null) return OkInt(0);
            int n = 0;
            if (p.inventory?.innerContainer != null)
            {
                foreach (Thing t in p.inventory.innerContainer)
                {
                    if (t?.def == def) n += t.stackCount;
                }
            }
            if (p.equipment?.Primary?.def == def) n += p.equipment.Primary.stackCount;
            if (p.apparel?.WornApparel != null)
            {
                foreach (Apparel a in p.apparel.WornApparel)
                {
                    if (a?.def == def) n += a.stackCount;
                }
            }
            return OkInt(n);
        }

        private static string DropCarried(Dictionary<string, string> args)
        {
            Pawn p = PawnOf(args);
            if (p?.carryTracker?.CarriedThing == null) return OkBool(false);
            p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            return OkBool(true);
        }
    }
}
