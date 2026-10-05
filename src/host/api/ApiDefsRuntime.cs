using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // A def whose data is a table the mod defines in XML and reads from Lua (P4-10). XML:
    //   <RimKit.LuaDataDef><defName>Foo</defName><table>my_table</table><values><speed>3</speed></values><list><li>a</li></list></RimKit.LuaDataDef>
    public class LuaDataDef : Def
    {
        public string table;
        public Dictionary<string, string> values = new Dictionary<string, string>();
        public List<string> list = new List<string>();
    }

    // game.defs: change defs at runtime with a journal, read def fields, look defs up across mods and read custom data defs. Ops: defs.*.
    // The older defs.get, exists, list, label, register_thing, write_hediff, write_recipe and mod_root keep working.
    internal static class ApiDefsRuntime
    {
        private sealed class Change
        {
            public string Kind, Def, Path;
            public object Def_, Old;
            public Type DefType;
            public string OldText, NewText;
            public int Tick;
        }

        private static readonly List<Change> Journal = new List<Change>();

        public static void Register()
        {
            R("defs.set", SetField);
            R("defs.value", ValueOf);
            R("defs.fields", Fields);
            R("defs.restore", Restore);
            R("defs.changes", Changes);
            R("defs.of", Of);
            R("defs.of_mod", OfMod);
            R("defs.kinds", Kinds);
            R("defs.data_rows", DataRows);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "advanced", "0.9.0");

        // ---- lookup

        internal static Type DefType(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            Type t = GenTypes.GetTypeInAnyAssembly(kind) ?? GenTypes.GetTypeInAnyAssembly("RimWorld." + kind) ?? GenTypes.GetTypeInAnyAssembly("Verse." + kind);
            return t != null && typeof(Def).IsAssignableFrom(t) ? t : null;
        }

        internal static Def FindDef(Type type, string name)
        {
            var db = typeof(DefDatabase<>).MakeGenericType(type);
            return AccessTools.Method(db, "GetNamedSilentFail")?.Invoke(null, new object[] { name }) as Def;
        }

        private static IEnumerable<Def> AllDefs(Type type)
        {
            var db = typeof(DefDatabase<>).MakeGenericType(type);
            var list = AccessTools.Property(db, "AllDefsListForReading")?.GetValue(null, null) as IEnumerable;
            return list == null ? Enumerable.Empty<Def>() : list.Cast<Def>();
        }

        private static string Resolve(Dictionary<string, string> a, out Type type, out Def def)
        {
            def = null;
            type = DefType(Str(a, "kind"));
            if (type == null) return Fail("RK3001", Str(a, "kind") + " is not a def type, see game.defs.kinds()");
            def = FindDef(type, Str(a, "def"));
            return def == null ? Fail("RK3001", "unknown " + type.Name + " " + Str(a, "def")) : null;
        }

        // ---- reading and writing a field by path: "stackLimit", "plant.growDays", "statBases[0].value"

        private static List<(string name, int index)> ParsePath(string path)
        {
            var parts = new List<(string, int)>();
            foreach (string seg in path.Split('.'))
            {
                int br = seg.IndexOf('[');
                if (br < 0) { parts.Add((seg, -1)); continue; }
                string name = seg.Substring(0, br);
                if (!int.TryParse(seg.Substring(br + 1).TrimEnd(']'), out int idx)) return null;
                parts.Add((name, idx));
            }

            return parts;
        }

        private static MemberInfo Member(Type t, string name)
        {
            return (MemberInfo)AccessTools.Field(t, name) ?? AccessTools.Property(t, name);
        }

        private static object Get(MemberInfo m, object target) => m is FieldInfo f ? f.GetValue(target) : ((PropertyInfo)m).GetValue(target, null);

        private static void Put(MemberInfo m, object target, object value)
        {
            if (m is FieldInfo f) f.SetValue(target, value);
            else ((PropertyInfo)m).SetValue(target, value, null);
        }

        private static Type TypeOf(MemberInfo m) => m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;

        // Walks to the owner of the last segment. Returns the member, the object that holds it and an optional list index.
        private static string Walk(object root, string path, out MemberInfo member, out object owner, out int index)
        {
            member = null; owner = null; index = -1;
            var parts = ParsePath(path);
            if (parts == null || parts.Count == 0) return Fail("RK1001", "bad field path " + path);
            object cur = root;
            for (int i = 0; i < parts.Count; i++)
            {
                if (cur == null) return Fail("RK3001", "the path " + path + " passes through an empty value");
                MemberInfo m = Member(cur.GetType(), parts[i].name);
                if (m == null) return Fail("RK3001", cur.GetType().Name + " has no field " + parts[i].name);
                bool last = i == parts.Count - 1;
                if (last) { member = m; owner = cur; index = parts[i].index; return null; }
                object next = Get(m, cur);
                if (parts[i].index >= 0)
                {
                    var list = next as IList;
                    if (list == null || parts[i].index >= list.Count) return Fail("RK1001", "index " + parts[i].index + " is out of range in " + parts[i].name);
                    next = list[parts[i].index];
                }

                cur = next;
            }

            return Fail("RK1001", "bad field path " + path);
        }

        private static object Coerce(Type t, object v, out string err)
        {
            err = null;
            try
            {
                Type u = Nullable.GetUnderlyingType(t) ?? t;
                if (v == null) { if (!u.IsValueType) return null; err = "null is not valid for " + u.Name; return null; }
                if (u == typeof(string)) return Convert.ToString(v, CultureInfo.InvariantCulture);
                if (u == typeof(bool)) return v is bool b ? b : bool.Parse(Convert.ToString(v, CultureInfo.InvariantCulture));
                if (u == typeof(int)) return Convert.ToInt32(Convert.ToDouble(v, CultureInfo.InvariantCulture));
                if (u == typeof(long)) return Convert.ToInt64(Convert.ToDouble(v, CultureInfo.InvariantCulture));
                if (u == typeof(float)) return Convert.ToSingle(v, CultureInfo.InvariantCulture);
                if (u == typeof(double)) return Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (u == typeof(ushort)) return Convert.ToUInt16(Convert.ToDouble(v, CultureInfo.InvariantCulture));
                if (u.IsEnum) return Enum.Parse(u, Convert.ToString(v, CultureInfo.InvariantCulture), true);
                if (u == typeof(Color) && v is string hex && ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
                if (u == typeof(Type)) return GenTypes.GetTypeInAnyAssembly(Convert.ToString(v, CultureInfo.InvariantCulture)) ?? throw new ArgumentException("unknown type " + v);
                if (u == typeof(IntRange) && Json.AsObject(v) is Dictionary<string, object> ir) return new IntRange((int)Json.GetLong(ir, "min"), (int)Json.GetLong(ir, "max"));
                if (u == typeof(FloatRange) && Json.AsObject(v) is Dictionary<string, object> fr) return new FloatRange(Convert.ToSingle(fr["min"], CultureInfo.InvariantCulture), Convert.ToSingle(fr["max"], CultureInfo.InvariantCulture));
                if (typeof(Def).IsAssignableFrom(u))
                {
                    Def d = FindDef(u, Convert.ToString(v, CultureInfo.InvariantCulture));
                    if (d == null) err = "unknown " + u.Name + " " + v;
                    return d;
                }

                err = "a " + u.Name + " cannot be set from Lua";
            }
            catch (Exception e)
            {
                err = "cannot convert " + v + " to " + t.Name + ": " + e.Message;
            }

            return null;
        }

        private static string Show(object v)
        {
            switch (v)
            {
                case null: return "null";
                case Def d: return d.defName;
                case Color c: return "#" + ColorUtility.ToHtmlStringRGBA(c);
                case IntRange r: return r.min + "~" + r.max;
                case FloatRange fr: return fr.min.ToString(CultureInfo.InvariantCulture) + "~" + fr.max.ToString(CultureInfo.InvariantCulture);
                case Type t: return t.FullName;
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case IList l: return "[" + l.Count + " items]";
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        // ---- ops

        // game.defs.set(kind, def, path, value): changes one field. The old value is recorded, game.defs.restore puts it back.
        private static string SetField(Dictionary<string, string> a)
        {
            string err = Resolve(a, out Type type, out Def def);
            if (err != null) return err;
            if (string.IsNullOrEmpty(Str(a, "path"))) return Fail("RK1001", "path is required");
            err = Walk(def, Str(a, "path"), out MemberInfo m, out object owner, out int index);
            if (err != null) return err;
            object parsed = null;
            string raw = a.TryGetValue("value", out string v) ? v : null;
            if (raw != null) { if (!Json.TryParse(raw, out parsed)) parsed = raw; }
            if (index >= 0)
            {
                var list = Get(m, owner) as IList;
                if (list == null || index >= list.Count) return Fail("RK1001", "index out of range");
                Type elem = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
                object val = Coerce(elem, parsed ?? raw, out err);
                if (err != null) return Fail("RK1001", err);
                string old = Show(list[index]);
                Journal.Add(new Change { Kind = type.Name, Def = def.defName, Path = Str(a, "path"), Def_ = def, DefType = type, Old = list[index], OldText = old, NewText = Show(val), Tick = (Current.Game?.tickManager?.TicksGame ?? 0) });
                list[index] = val;
                return OkStr(Show(val));
            }

            object coerced = Coerce(TypeOf(m), parsed ?? raw, out err);
            if (err != null) return Fail("RK1001", err);
            object before = Get(m, owner);
            Journal.Add(new Change { Kind = type.Name, Def = def.defName, Path = Str(a, "path"), Def_ = def, DefType = type, Old = before, OldText = Show(before), NewText = Show(coerced), Tick = (Current.Game?.tickManager?.TicksGame ?? 0) });
            Put(m, owner, coerced);
            return OkStr(Show(coerced));
        }

        private static string ValueOf(Dictionary<string, string> a)
        {
            string err = Resolve(a, out Type type, out Def def);
            if (err != null) return err;
            err = Walk(def, Str(a, "path"), out MemberInfo m, out object owner, out int index);
            if (err != null) return err;
            object val = Get(m, owner);
            if (index >= 0 && val is IList l && index < l.Count) val = l[index];
            switch (val)
            {
                case null: return OkJson("null");
                case bool b: return OkBool(b);
                case int i: return OkInt(i);
                case float f: return OkFloat(f);
                case double d: return OkFloat((float)d);
                default: return OkStr(Show(val));
            }
        }

        // Public and private fields of a def type that Lua can read or set, with their types.
        private static string Fields(Dictionary<string, string> a)
        {
            Type type = DefType(Str(a, "kind"));
            if (type == null) return Fail("RK3001", Str(a, "kind") + " is not a def type");
            var arr = Jb.Arr();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    arr.Add(Jb.Obj().S("name", f.Name).S("type", f.FieldType.Name).S("declared_in", t.Name).B("public", f.IsPublic));
            return arr.Ok();
        }

        private static string Kinds(Dictionary<string, string> a)
        {
            var names = typeof(Def).AllSubclasses().Where(t => !t.IsAbstract).Select(t => t.Namespace == "Verse" || t.Namespace == "RimWorld" ? t.Name : t.FullName).OrderBy(n => n).ToList();
            return OkStringList(names);
        }

        // Puts back changes, newest first. With no arguments it undoes everything. Filter by kind, def and path.
        private static string Restore(Dictionary<string, string> a)
        {
            int n = 0;
            foreach (Change c in Journal.AsEnumerable().Reverse().ToList())
            {
                if (!string.IsNullOrEmpty(Str(a, "kind")) && c.Kind != Str(a, "kind")) continue;
                if (!string.IsNullOrEmpty(Str(a, "def")) && c.Def != Str(a, "def")) continue;
                if (!string.IsNullOrEmpty(Str(a, "path")) && c.Path != Str(a, "path")) continue;
                if (Walk(c.Def_, c.Path, out MemberInfo m, out object owner, out int index) == null)
                {
                    if (index >= 0 && Get(m, owner) is IList l) l[index] = c.Old; else Put(m, owner, c.Old);
                    n++;
                }

                Journal.Remove(c);
            }

            return OkInt(n);
        }

        private static string Changes(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (Change c in Journal) arr.Add(Jb.Obj().S("kind", c.Kind).S("def", c.Def).S("path", c.Path).S("old", c.OldText).S("new", c.NewText).I("tick", c.Tick));
            return arr.Ok();
        }

        private static Jb DefInfo(Def d)
        {
            var j = Jb.Obj().B("exists", true).S("kind", d.GetType().Name).S("def", d.defName).S("label", d.label);
            if (d.modContentPack != null) j.S("package_id", d.modContentPack.PackageId).S("mod", d.modContentPack.Name);
            return j;
        }

        // game.defs.of(kind, name): one def and the mod it came from, or { exists = false }. Works for defs of any mod.
        private static string Of(Dictionary<string, string> a)
        {
            Type type = DefType(Str(a, "kind"));
            if (type == null) return Fail("RK3001", Str(a, "kind") + " is not a def type, see game.defs.kinds()");
            Def d = FindDef(type, Str(a, "def"));
            if (d == null || (!string.IsNullOrEmpty(Str(a, "package_id")) && !string.Equals(d.modContentPack?.PackageId, Str(a, "package_id"), StringComparison.OrdinalIgnoreCase)))
                return Jb.Obj().B("exists", false).Ok();
            return DefInfo(d).Ok();
        }

        private static string OfMod(Dictionary<string, string> a)
        {
            Type type = DefType(Str(a, "kind"));
            if (type == null) return Fail("RK3001", Str(a, "kind") + " is not a def type");
            var arr = Jb.Arr();
            foreach (Def d in AllDefs(type).Where(x => string.Equals(x.modContentPack?.PackageId, Str(a, "package_id"), StringComparison.OrdinalIgnoreCase))) arr.Add(DefInfo(d));
            return arr.Ok();
        }

        private static string DataRows(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (LuaDataDef d in DefDatabase<LuaDataDef>.AllDefsListForReading.Where(x => x.table == Str(a, "table")))
            {
                var values = Jb.Obj();
                foreach (var kv in d.values) values.S(kv.Key, kv.Value);
                var list = Jb.Arr();
                foreach (string s in d.list) list.AddS(s);
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).Raw("values", values.ToString()).Raw("list", list.ToString()));
            }

            return arr.Ok();
        }
    }
}
