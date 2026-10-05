using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.classes: Lua-backed game classes (see src/api/classes.cls). Ops are classes.*.
    internal static class ApiClasses
    {
        // family -> (def type name, field that holds the class). Used to swap a def's class for the Lua proxy.
        private static readonly Dictionary<string, (string defType, string field, Type proxy)> Replaceable = new Dictionary<string, (string, string, Type)>
        {
            ["incident"] = ("IncidentDef", "workerClass", typeof(IncidentWorker_Lua)),
            ["work_giver"] = ("WorkGiverDef", "giverClass", typeof(WorkGiver_Lua)),
            ["stat_worker"] = ("StatDef", "workerClass", typeof(StatWorker_Lua)),
            ["recipe_worker"] = ("RecipeDef", "workerClass", typeof(RecipeWorker_Lua)),
            ["thought_worker"] = ("ThoughtDef", "workerClass", typeof(ThoughtWorker_Lua)),
            ["mental_state_worker"] = ("MentalStateDef", "workerClass", typeof(MentalStateWorker_Lua)),
            ["need"] = ("NeedDef", "needClass", typeof(Need_Lua)),
            ["damage_worker"] = ("DamageDef", "workerClass", typeof(DamageWorker_Lua)),
            ["condition"] = ("GameConditionDef", "conditionClass", typeof(GameCondition_Lua)),
            ["gene"] = ("GeneDef", "geneClass", typeof(Gene_Lua)),
            ["projectile"] = ("ThingDef", "thingClass", typeof(Projectile_Lua)),
            ["building"] = ("ThingDef", "thingClass", typeof(Building_Lua)),
            ["door"] = ("ThingDef", "thingClass", typeof(Building_Door_Lua)),
            ["storage"] = ("ThingDef", "thingClass", typeof(Building_Storage_Lua)),
            ["scen_part"] = ("ScenPartDef", "scenPartClass", typeof(ScenPart_Lua)),
            ["ritual_outcome"] = ("RitualOutcomeEffectDef", "workerClass", typeof(RitualOutcomeEffectWorker_Lua)),
        };

        public static void Register()
        {
            R("classes.register_fn", RegisterFn);
            R("classes.list", List);
            R("classes.families", Families);
            R("classes.data_get", DataGet);
            R("classes.data_set", DataSet);
            R("classes.replace", Replace);
            R("classes.replaceable", ReplaceableList);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "advanced", "0.9.0");

        private static string RegisterFn(Dictionary<string, string> a)
        {
            string family = Str(a, "family"), name = Str(a, "name"), fn = Str(a, "fn");
            if (string.IsNullOrEmpty(family) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fn)) return Fail("RK1001", "family, name and fn are required");
            if (!LuaClassFamilies.All.Contains(family)) return Fail("RK1001", "unknown family " + family + ", see game.classes.families()");
            if (Int(a, "callback") <= 0) return Fail("RK1001", "callback must be a function");
            LuaClasses.Register(family, name, fn, Int(a, "callback"));
            return OkBool(true);
        }

        private static string List(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (var kv in LuaClasses.All)
            {
                string[] parts = kv.Key.Split(new[] { ':' }, 2);
                var fns = Jb.Arr();
                foreach (string f in kv.Value.Keys.OrderBy(x => x)) fns.AddS(f);
                arr.Add(Jb.Obj().S("family", parts[0]).S("name", parts[1]).Raw("functions", fns.ToString()));
            }

            return arr.Ok();
        }

        private static string Families(Dictionary<string, string> a) => OkStringList(LuaClassFamilies.All.ToList());

        // ---- data saved with a comp

        private static ThingComp_Lua CompOf(Thing t, string cls)
        {
            return (t as ThingWithComps)?.AllComps.OfType<ThingComp_Lua>().FirstOrDefault(c => string.IsNullOrEmpty(cls) || ((CompProperties_Lua)c.props).luaClass == cls);
        }

        private static string DataGet(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            ThingComp_Lua c = CompOf(t, Str(a, "class"));
            if (c == null) return Fail("RK3003", "the thing has no Lua comp" + (string.IsNullOrEmpty(Str(a, "class")) ? "" : " of class " + Str(a, "class")));
            return c.luaData.TryGetValue(Str(a, "key"), out string v) ? OkStr(v) : OkJson("null");
        }

        private static string DataSet(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            ThingComp_Lua c = CompOf(t, Str(a, "class"));
            if (c == null) return Fail("RK3003", "the thing has no Lua comp" + (string.IsNullOrEmpty(Str(a, "class")) ? "" : " of class " + Str(a, "class")));
            if (string.IsNullOrEmpty(Str(a, "key"))) return Fail("RK1001", "key is required");
            if (a.TryGetValue("value", out string v) && v != null) c.luaData[Str(a, "key")] = v; else c.luaData.Remove(Str(a, "key"));
            return OkBool(true);
        }

        // ---- replacing a def's class

        private static Type DefType(string name) => GenTypes.GetTypeInAnyAssembly(name) ?? GenTypes.GetTypeInAnyAssembly("RimWorld." + name) ?? GenTypes.GetTypeInAnyAssembly("Verse." + name);

        private static string ReplaceableList(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (var kv in Replaceable.OrderBy(k => k.Key)) arr.Add(Jb.Obj().S("family", kv.Key).S("def_type", kv.Value.defType).S("field", kv.Value.field).S("proxy", kv.Value.proxy.Name));
            return arr.Ok();
        }

        // Swaps the class of one def for the Lua proxy of a family and names the Lua class, so the def runs your functions instead.
        // The change lasts until the game restarts, like any def change at runtime.
        private static string Replace(Dictionary<string, string> a)
        {
            string family = Str(a, "family");
            if (!Replaceable.TryGetValue(family, out var info)) return Fail("RK1001", family + " has no def class to replace, see game.classes.replaceable()");
            Type defType = DefType(info.defType);
            if (defType == null) return Fail("RK3003", "the game has no " + info.defType);
            object def = AccessTools.Method(typeof(DefDatabase<>).MakeGenericType(defType), "GetNamedSilentFail")?.Invoke(null, new object[] { Str(a, "def") });
            if (def == null) return Fail("RK3001", "unknown " + info.defType + " " + Str(a, "def"));
            FieldInfo field = AccessTools.Field(defType, info.field);
            if (field == null) return Fail("RK3003", info.defType + "." + info.field + " does not exist on this game version");
            if (string.IsNullOrEmpty(Str(a, "lua_class"))) return Fail("RK1001", "lua_class is required");
            var d = (Def)def;
            field.SetValue(def, info.proxy);
            if (d.modExtensions == null) d.modExtensions = new List<DefModExtension>();
            d.modExtensions.RemoveAll(x => x is DefModExtension_Lua);
            d.modExtensions.Add(new DefModExtension_Lua { luaClass = Str(a, "lua_class") });
            // Workers are created once and cached on the def, so drop the cached instance.
            foreach (FieldInfo f in defType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.Name.EndsWith("Int", StringComparison.Ordinal) && !f.FieldType.IsPrimitive && f.FieldType != typeof(string) && f.FieldType.Name.IndexOf("Worker", StringComparison.Ordinal) >= 0 || f.Name == "workerInt" || f.Name == "giverInt")
                    f.SetValue(def, null);
            return OkBool(true);
        }
    }

    // A group of pawns controlled by Lua: one toil that asks the Lua class for duties, ending when its finished function returns true.
    public class LordJob_Lua : LordJob
    {
        public string luaClass;

        public LordJob_Lua()
        {
        }

        public LordJob_Lua(string luaClass)
        {
            this.luaClass = luaClass;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var toil = new LordToil_Lua(luaClass);
            graph.AddToil(toil);
            graph.StartingToil = toil;
            var end = new LordToil_End();
            graph.AddToil(end);
            var t = new Transition(toil, end);
            t.AddTrigger(new Trigger_TickCondition(() => LuaClasses.Bool(LuaClasses.Call("lord_job", luaClass, "finished", lord != null ? (object)lord.ownedPawns.Count : 0), false), 60));
            graph.AddTransition(t);
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref luaClass, "luaClass");
        }
    }
}
