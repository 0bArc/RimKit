using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimKit
{
    // XML hook for defs that name a Lua class: <modExtensions><li Class="RimKit.DefModExtension_Lua"><luaClass>my_class</luaClass></li></modExtensions>.
    public class DefModExtension_Lua : DefModExtension
    {
        public string luaClass;
    }

    // A value that is already JSON, passed through to Lua untouched.
    internal sealed class RawJson
    {
        public readonly string Json;

        public RawJson(string json) => Json = json;
    }

    // Registry and call path for the generated proxy classes (src/host/LuaClasses.g.cs). A Lua class is a table of functions registered
    // under a family and a name. Each proxy method calls the Lua function with the game objects as arguments.
    internal static class LuaClasses
    {
        /// <summary>The slate a Lua quest node is running against (set while its test or run function executes).</summary>
        public static RimWorld.QuestGen.Slate CurrentSlate;

        private static readonly Dictionary<string, Dictionary<string, int>> Classes = new Dictionary<string, Dictionary<string, int>>();

        private static string Key(string family, string name) => family + ":" + name;

        public static void Register(string family, string name, string fn, int callbackId)
        {
            string key = Key(family, name);
            if (!Classes.TryGetValue(key, out var fns)) Classes[key] = fns = new Dictionary<string, int>();
            fns[fn] = callbackId;
        }

        public static bool HasClass(string family, string name) => Classes.ContainsKey(Key(family, name));

        public static bool Has(string family, string name, string fn) => name != null && Classes.TryGetValue(Key(family, name), out var fns) && fns.ContainsKey(fn);

        public static IEnumerable<KeyValuePair<string, Dictionary<string, int>>> All => Classes;

        public static string OfDef(Def def) => def?.GetModExtension<DefModExtension_Lua>()?.luaClass;

        // ---- calling

        private static void Enc(StringBuilder sb, object o)
        {
            switch (o)
            {
                case null: sb.Append("null"); return;
                case RawJson raw: sb.Append(raw.Json); return;
                case string s: sb.Append(JsonLite.Quote(s)); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "0" : f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString("R", CultureInfo.InvariantCulture)); return;
                default: sb.Append(HookCodec.Encode(o)); return;
            }
        }

        // Calls a Lua class function with positional arguments. Returns the parsed result, or null when the function is not defined.
        public static object Call(string family, string name, string fn, params object[] args)
        {
            if (name == null || !Classes.TryGetValue(Key(family, name), out var fns) || !fns.TryGetValue(fn, out int id)) return null;
            var sb = new StringBuilder("{\"n\":").Append(args.Length).Append(",\"a\":[");
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                Enc(sb, args[i]);
            }

            sb.Append("]}");
            return LuaCallbacks.Call(id, sb.ToString());
        }

        // ---- result conversion

        public static bool IsBool(object r) => r is bool;

        public static bool Bool(object r, bool fallback) => r is bool b ? b : fallback;

        public static bool IsNumber(object r) => r is double || r is long;

        public static float Float(object r, float fallback) => r is double d ? (float)d : r is long l ? l : fallback;

        public static int Int(object r, int fallback) => r is double d ? (int)Math.Round(d) : r is long l ? (int)l : fallback;

        public static string Str(object r, string fallback) => r is string s ? s : fallback;

        public static AcceptanceReport Report(object r, AcceptanceReport fallback)
        {
            if (r is bool b) return b;
            if (r is string s && s.Length > 0) return new AcceptanceReport(s);
            return fallback;
        }

        public static List<T> Things<T>(object r) where T : class
        {
            var list = new List<T>();
            if (r is List<object> items)
                foreach (object item in items)
                {
                    T t = Opts.HandleOf<T>(item);
                    if (t != null) list.Add(t);
                }

            return list;
        }

        // A Lua result { job = "JobDefName", target = thing, x =, z =, count = } becomes a job for the pawn.
        public static Job MakeJob(object r)
        {
            var d = Json.AsObject(r);
            if (d == null) return null;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(Json.GetString(d, "job"));
            if (def == null) return null;
            LocalTargetInfo a = LocalTargetInfo.Invalid;
            Thing t = Opts.HandleOf<Thing>(d.TryGetValue("target", out object tv) ? tv : null);
            if (t != null) a = t;
            else if (d.ContainsKey("x") && d.ContainsKey("z")) a = new IntVec3((int)Json.GetLong(d, "x"), 0, (int)Json.GetLong(d, "z"));
            LocalTargetInfo b = LocalTargetInfo.Invalid;
            Thing t2 = Opts.HandleOf<Thing>(d.TryGetValue("target2", out object t2v) ? t2v : null);
            if (t2 != null) b = t2;
            Job job = JobMaker.MakeJob(def, a, b);
            if (d.ContainsKey("count")) job.count = (int)Json.GetLong(d, "count");
            return job;
        }

        // A list of game objects as a JSON array of handles.
        public static RawJson Handles(IEnumerable<object> items)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (object o in items)
            {
                if (o == null) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(HookCodec.Encode(o));
            }

            return new RawJson(sb.Append(']').ToString());
        }

        public static RawJson Parms(IncidentParms p)
        {
            var sb = new StringBuilder("{\"points\":").Append(p.points.ToString("R", CultureInfo.InvariantCulture)).Append(",\"forced\":").Append(p.forced ? "true" : "false");
            if (p.faction != null) sb.Append(",\"faction\":").Append(HookCodec.Encode(p.faction));
            if (p.target is Map m) sb.Append(",\"map\":").Append(HookCodec.Encode(m));
            sb.Append('}');
            return new RawJson(sb.ToString());
        }
    }
}
