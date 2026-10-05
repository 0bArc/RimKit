using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.stats.modify, add_offset, add_factor: change any stat for any thing with one patch over the game's stat calculation.
    // Ops: stat.modify, stat.add_offset, stat.add_factor, stat.remove_modifier, stat.modifiers.
    internal static class ApiStatMods
    {
        private sealed class Mod
        {
            public int Id;
            public string Stat;
            public string Kind;      // fn, offset, factor
            public int Callback;
            public double Value;
            public HashSet<string> Defs = new HashSet<string>();
            public string Target = "any";   // any, pawn, building, item
        }

        private static readonly Dictionary<string, List<Mod>> ByStat = new Dictionary<string, List<Mod>>();
        private static readonly FieldInfo StatField = AccessTools.Field(typeof(StatWorker), "stat");
        private static int nextId = 1;
        private static bool patched;

        [ThreadStatic]
        private static bool inside;

        public static void Register()
        {
            R("stat.modify", Modify);
            R("stat.add_offset", AddOffset);
            R("stat.add_factor", AddFactor);
            R("stat.remove_modifier", Remove);
            R("stat.modifiers", List);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "advanced", "0.9.0");

        private static void EnsurePatched()
        {
            if (patched) return;
            patched = true;
            try
            {
                var harmony = new Harmony("rimkit.statmods");
                var post = new HarmonyMethod(typeof(ApiStatMods), nameof(Postfix));
                var workers = new List<Type> { typeof(StatWorker) };
                workers.AddRange(typeof(StatWorker).AllSubclassesNonAbstract());
                workers.AddRange(typeof(StatWorker).AllSubclasses().Where(t => t.IsAbstract));
                int n = 0;
                foreach (Type t in workers.Distinct())
                {
                    foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name != "GetValue" && m.Name != "GetValueAbstract") continue;
                        if (m.ReturnType != typeof(float) || m.IsAbstract) continue;
                        ParameterInfo[] ps = m.GetParameters();
                        bool request = ps.Length > 0 && ps[0].ParameterType == typeof(StatRequest);
                        bool abstractDef = m.Name == "GetValueAbstract" && ps.Length > 0 && ps[0].ParameterType == typeof(ThingDef);
                        if (!request && !abstractDef) continue;
                        harmony.Patch(m, postfix: post);
                        n++;
                    }
                }

                if (n == 0) Log.Warning("[RimKit] no stat worker methods were patched, game.stats.modify will have no effect");
            }
            catch (Exception e)
            {
                patched = false;
                Log.Error("[RimKit] could not patch the stat workers: " + e.Message);
            }
        }

        public static void Postfix(StatWorker __instance, object[] __args, ref float __result)
        {
            if (inside || ByStat.Count == 0) return;
            var stat = StatField?.GetValue(__instance) as StatDef;
            if (stat == null || !ByStat.TryGetValue(stat.defName, out List<Mod> mods) || mods.Count == 0) return;
            Thing thing = null;
            ThingDef def = null;
            if (__args != null && __args.Length > 0)
            {
                if (__args[0] is StatRequest req) { thing = req.Thing; def = req.Def as ThingDef; }
                else if (__args[0] is ThingDef d) def = d;
            }

            if (def == null && thing != null) def = thing.def;
            inside = true;
            try
            {
                float v = __result;
                foreach (Mod m in mods.ToArray())
                {
                    if (m.Defs.Count > 0 && (def == null || !m.Defs.Contains(def.defName))) continue;
                    if (!Matches(m.Target, thing, def)) continue;
                    switch (m.Kind)
                    {
                        case "offset": v += (float)m.Value; break;
                        case "factor": v *= (float)m.Value; break;
                        default:
                        {
                            string arg = "{\"thing\":" + (thing != null ? HookCodec.Encode(thing) : "null") + ",\"def\":" + JsonLite.Quote(def?.defName ?? "") +
                                         ",\"value\":" + v.ToString("R", CultureInfo.InvariantCulture) + ",\"stat\":" + JsonLite.Quote(stat.defName) + "}";
                            object r = LuaCallbacks.Call(m.Callback, arg);
                            if (r is double d) v = (float)d; else if (r is long l) v = l;
                            break;
                        }
                    }
                }

                __result = v;
            }
            finally
            {
                inside = false;
            }
        }

        private static bool Matches(string target, Thing thing, ThingDef def)
        {
            switch (target)
            {
                case "pawn": return thing is Pawn || (def != null && def.category == ThingCategory.Pawn);
                case "building": return thing is Building || (def != null && def.category == ThingCategory.Building);
                case "item": return def != null && def.category == ThingCategory.Item;
                default: return true;
            }
        }

        private static string Add(Dictionary<string, string> a, string kind, double value, int callback)
        {
            string stat = Str(a, "stat");
            if (DefDatabase<StatDef>.GetNamedSilentFail(stat) == null) return Fail("RK3001", "unknown stat " + stat);
            Opts o = Opts.From(a, "opts");
            string target = o.Str("target", "any");
            if (target != "any" && target != "pawn" && target != "building" && target != "item") return Fail("RK1001", "target must be any, pawn, building or item");
            var m = new Mod { Id = nextId++, Stat = stat, Kind = kind, Value = value, Callback = callback, Target = target, Defs = new HashSet<string>(o.Strings("defs")) };
            if (!ByStat.TryGetValue(stat, out var list)) ByStat[stat] = list = new List<Mod>();
            list.Add(m);
            EnsurePatched();
            StatWorkerCache.Clear();
            return OkInt(m.Id);
        }

        // fn gets { thing, def, value, stat } and returns the new value. It runs every time the stat is read, so keep it small.
        private static string Modify(Dictionary<string, string> a)
        {
            if (Int(a, "fn") <= 0) return Fail("RK1001", "fn must be a function");
            return Add(a, "fn", 0, Int(a, "fn"));
        }

        private static string AddOffset(Dictionary<string, string> a) => Add(a, "offset", Float(a, "value"), 0);

        private static string AddFactor(Dictionary<string, string> a) => Add(a, "factor", Float(a, "value"), 0);

        private static string Remove(Dictionary<string, string> a)
        {
            int id = Int(a, "id");
            foreach (var kv in ByStat.ToList())
            {
                if (kv.Value.RemoveAll(m => m.Id == id) > 0)
                {
                    if (kv.Value.Count == 0) ByStat.Remove(kv.Key);
                    StatWorkerCache.Clear();
                    return OkBool(true);
                }
            }

            return OkBool(false);
        }

        private static string List(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (Mod m in ByStat.Values.SelectMany(x => x).OrderBy(x => x.Id))
                arr.Add(Jb.Obj().I("id", m.Id).S("stat", m.Stat).S("kind", m.Kind).F("value", m.Value).S("target", m.Target));
            return arr.Ok();
        }
    }

    // The game caches stat values per thing for a while. Clearing the cache makes a new modifier show up at once.
    internal static class StatWorkerCache
    {
        public static void Clear()
        {
            try
            {
                AccessTools.Method(typeof(StatWorker), "ClearCache")?.Invoke(null, null);
                var cache = AccessTools.Field(typeof(StatWorker), "temporaryStatCache")?.GetValue(null) as System.Collections.IDictionary;
                cache?.Clear();
            }
            catch (Exception)
            {
                // A cache that cannot be cleared only delays the change a few seconds.
            }
        }
    }
}
