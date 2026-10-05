using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.ai: work givers, think trees, duties, lords, path costs, danger. Ops are ai.*.
    internal static class ApiAi
    {
        public static void Register()
        {
            R("ai.work_givers", WorkGivers);
            R("ai.set_work_giver_priority", SetWorkGiverPriority);
            R("ai.work_types", WorkTypes);
            R("ai.think_trees", ThinkTrees);
            R("ai.think_nodes", ThinkNodes);
            R("ai.think_remove", ThinkRemove);
            R("ai.think_insert", ThinkInsert);
            R("ai.duty", DutyOf);
            R("ai.set_duty", SetDuty);
            R("ai.duty_defs", DutyDefs);
            R("ai.lord_of", LordOf);
            R("ai.lord_make", MakeLord);
            R("ai.remove_from_lord", RemoveFromLord);
            R("ai.path_cost", PathCost);
            R("ai.danger", DangerAt);
            R("ai.max_danger", MaxDanger);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static string Bad() => Fail("RK2001", "pawn handle is stale or null");

        private static string WorkGivers(Dictionary<string, string> a)
        {
            string only = Str(a, "work_type");
            var arr = Jb.Arr();
            foreach (WorkGiverDef g in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (!string.IsNullOrEmpty(only) && g.workType?.defName != only) continue;
                arr.Add(Jb.Obj().S("def", g.defName).S("label", g.label).S("work_type", g.workType?.defName).I("priority", g.priorityInType)
                    .B("emergency", g.emergency).S("class", g.giverClass?.Name));
            }

            return arr.Ok();
        }

        // Changes the order of a work giver inside its work type. Higher priority runs first.
        private static string SetWorkGiverPriority(Dictionary<string, string> a)
        {
            WorkGiverDef g = DefDatabase<WorkGiverDef>.GetNamedSilentFail(Str(a, "def"));
            if (g == null) return Fail("RK3001", "unknown work giver " + Str(a, "def"));
            g.priorityInType = Int(a, "priority");
            WorkTypeDef wt = g.workType;
            if (wt != null)
            {
                wt.workGiversByPriority.Sort((x, y) => y.priorityInType.CompareTo(x.priorityInType));
            }

            return OkInt(g.priorityInType);
        }

        private static string WorkTypes(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", w.defName).S("label", w.labelShort).I("order", w.naturalPriority).B("always_on", w.alwaysStartActive)
                    .Raw("givers", Jb.Arr().Also(x => { foreach (var g in w.workGiversByPriority) x.AddS(g.defName); }).ToString()));
            return arr.Ok();
        }

        private static string ThinkTrees(Dictionary<string, string> a) => OkStringList(DefDatabase<ThinkTreeDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static void Dump(ThinkNode n, int depth, Jb arr)
        {
            arr.Add(Jb.Obj().I("depth", depth).S("class", n.GetType().Name).I("children", n.subNodes?.Count ?? 0));
            if (n.subNodes != null) foreach (ThinkNode c in n.subNodes) Dump(c, depth + 1, arr);
        }

        private static string ThinkNodes(Dictionary<string, string> a)
        {
            ThinkTreeDef def = DefDatabase<ThinkTreeDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown think tree " + Str(a, "def"));
            var arr = Jb.Arr();
            if (def.thinkRoot != null) Dump(def.thinkRoot, 0, arr);
            return arr.Ok();
        }

        private static int RemoveClass(ThinkNode node, string cls)
        {
            int n = 0;
            if (node.subNodes == null) return 0;
            for (int i = node.subNodes.Count - 1; i >= 0; i--)
            {
                if (node.subNodes[i].GetType().Name == cls) { node.subNodes.RemoveAt(i); n++; }
                else n += RemoveClass(node.subNodes[i], cls);
            }

            return n;
        }

        private static ThinkNode FindClass(ThinkNode node, string cls)
        {
            if (node.GetType().Name == cls) return node;
            if (node.subNodes != null)
                foreach (ThinkNode c in node.subNodes) { ThinkNode f = FindClass(c, cls); if (f != null) return f; }
            return null;
        }

        // Removes every node of a class (for example JobGiver_Wander) from a think tree. Returns how many.
        private static string ThinkRemove(Dictionary<string, string> a)
        {
            ThinkTreeDef def = DefDatabase<ThinkTreeDef>.GetNamedSilentFail(Str(a, "def"));
            if (def?.thinkRoot == null) return Fail("RK3001", "unknown think tree " + Str(a, "def"));
            return OkInt(RemoveClass(def.thinkRoot, Str(a, "class")));
        }

        // Adds a node of a class with a parameterless constructor under a parent (default the root) at an index (default first).
        private static string ThinkInsert(Dictionary<string, string> a)
        {
            ThinkTreeDef def = DefDatabase<ThinkTreeDef>.GetNamedSilentFail(Str(a, "def"));
            if (def?.thinkRoot == null) return Fail("RK3001", "unknown think tree " + Str(a, "def"));
            Type type = HarmonyLib.AccessTools.TypeByName(Str(a, "class")) ?? HarmonyLib.AccessTools.TypeByName("RimWorld." + Str(a, "class")) ?? HarmonyLib.AccessTools.TypeByName("Verse.AI." + Str(a, "class"));
            if (type == null || !typeof(ThinkNode).IsAssignableFrom(type)) return Fail("RK3001", Str(a, "class") + " is not a think node class");
            ThinkNode parent = string.IsNullOrEmpty(Str(a, "parent")) ? def.thinkRoot : FindClass(def.thinkRoot, Str(a, "parent"));
            if (parent == null) return Fail("RK3001", "parent node " + Str(a, "parent") + " not found");
            var node = (ThinkNode)Activator.CreateInstance(type);
            if (parent.subNodes == null) parent.subNodes = new List<ThinkNode>();
            int index = string.IsNullOrEmpty(Str(a, "index")) ? 0 : Math.Max(0, Math.Min(parent.subNodes.Count, Int(a, "index")));
            parent.subNodes.Insert(index, node);
            return OkBool(true);
        }

        private static string DutyOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState == null) return Bad();
            PawnDuty d = p.mindState.duty;
            if (d == null) return OkJson("null");
            var j = Jb.Obj().S("def", d.def.defName);
            if (d.focus.IsValid) { if (d.focus.HasThing) j.H("thing", d.focus.Thing); else j.I("x", d.focus.Cell.x).I("z", d.focus.Cell.z); }
            return j.Ok();
        }

        private static string SetDuty(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.mindState == null) return Bad();
            DutyDef def = DefDatabase<DutyDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown duty " + Str(a, "def"));
            var duty = new PawnDuty(def);
            if (!string.IsNullOrEmpty(Str(a, "x")) && p.Map != null) duty.focus = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            p.mindState.duty = duty;
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            return OkBool(true);
        }

        private static string DutyDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<DutyDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string LordOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            Lord l = p.GetLord();
            if (l == null) return OkJson("null");
            return Jb.Obj().S("job", l.LordJob?.GetType().Name).S("toil", l.CurLordToil?.GetType().Name).I("pawns", l.ownedPawns.Count).H("faction", l.faction).Ok();
        }

        // Puts pawns under a new lord running a simple lord job. job: ExitMap, ExitMapBest, DefendPoint (x, z), TravelAndExit.
        private static string MakeLord(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            Opts o = Opts.From(a, "opts");
            Faction f = o.Handle<Faction>("faction") ?? Faction.OfPlayer;
            List<Pawn> pawns = o.Handles<Pawn>("pawns");
            if (pawns.Count == 0) return Fail("RK1001", "opts.pawns must list at least one pawn");
            LordJob job;
            switch (o.Str("job", "ExitMapBest"))
            {
                case "ExitMap": job = new LordJob_ExitMapBest(LocomotionUrgency.Walk); break;
                case "ExitMapBest": job = new LordJob_ExitMapBest(LocomotionUrgency.Jog); break;
                case "DefendPoint": job = new LordJob_DefendPoint(new IntVec3(o.Int("x"), 0, o.Int("z")), null, 12f, false, true); break;
                case "Lua":
                    if (string.IsNullOrEmpty(o.Str("class"))) return Fail("RK1001", "job Lua needs a class, the name of a lord_toil Lua class");
                    job = new LordJob_Lua(o.Str("class")); break;
                default: return Fail("RK1001", "job must be ExitMap, ExitMapBest, DefendPoint or Lua");
            }

            foreach (Pawn p in pawns) p.GetLord()?.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
            Lord lord = LordMaker.MakeNewLord(f, job, map, pawns);
            return OkBool(lord != null);
        }

        private static string RemoveFromLord(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            Lord l = p.GetLord();
            if (l == null) return OkBool(false);
            l.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
            return OkBool(true);
        }

        private static string PathCost(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.Map == null) return Bad();
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(p.Map)) return Fail("RK1001", "cell is outside the map");
            return OkInt(p.Map.pathing.For(p).pathGrid.CalculatedCostAt(c, true, IntVec3.Invalid));
        }

        private static string DangerAt(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.Map == null) return Bad();
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(p.Map)) return Fail("RK1001", "cell is outside the map");
            return OkStr(c.GetDangerFor(p, p.Map).ToString());
        }

        private static string MaxDanger(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            return OkStr(p.NormalMaxDanger().ToString());
        }
    }

    internal static class JbExt
    {
        public static Jb Also(this Jb j, Action<Jb> fn)
        {
            fn(j);
            return j;
        }
    }
}
