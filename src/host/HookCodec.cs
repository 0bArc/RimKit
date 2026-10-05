using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimKit
{
    /// <summary>
    /// Converts between game values and the JSON used on the hook and event channels.
    /// Encoding: primitives map directly, enums and defs become names, structs of known shape become objects,
    /// and reference types become handles ({"$h":n,"$k":kind,"$t":typeName}). Decoding coerces by the declared type.
    /// </summary>
    internal static class HookCodec
    {
        private const int MaxDepth = 6;
        private const int MaxListItems = 128;
        private static readonly Dictionary<Type, MemberInfo> DefMemberCache = new Dictionary<Type, MemberInfo>();

        public static string Encode(object value)
        {
            var sb = new StringBuilder(64);
            Encode(sb, value, 0);
            return sb.ToString();
        }

        public static void Encode(StringBuilder sb, object value, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (depth > MaxDepth)
            {
                Json.WriteString(sb, value.ToString());
                return;
            }

            switch (value)
            {
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case string s:
                    Json.WriteString(sb, s);
                    return;
                case int _:
                case long _:
                case short _:
                case byte _:
                case sbyte _:
                case ushort _:
                case uint _:
                case ulong _:
                    sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                    return;
                case float f:
                    AppendDouble(sb, f);
                    return;
                case double d:
                    AppendDouble(sb, d);
                    return;
                case TaggedString ts:
                    Json.WriteString(sb, ts.ToString());
                    return;
                case Def def:
                    Json.WriteString(sb, def.defName);
                    return;
                case IntVec3 c:
                    sb.Append("{\"x\":").Append(c.x).Append(",\"y\":").Append(c.y).Append(",\"z\":").Append(c.z).Append('}');
                    return;
                case Vector3 v3:
                    sb.Append("{\"x\":");
                    AppendDouble(sb, v3.x);
                    sb.Append(",\"y\":");
                    AppendDouble(sb, v3.y);
                    sb.Append(",\"z\":");
                    AppendDouble(sb, v3.z);
                    sb.Append('}');
                    return;
                case Vector2 v2:
                    sb.Append("{\"x\":");
                    AppendDouble(sb, v2.x);
                    sb.Append(",\"y\":");
                    AppendDouble(sb, v2.y);
                    sb.Append('}');
                    return;
                case Rot4 rot:
                    sb.Append(rot.AsInt);
                    return;
                case DamageInfo dinfo:
                    EncodeDamageInfo(sb, dinfo, depth);
                    return;
                case Pawn pawn:
                    AppendHandle(sb, pawn, "pawn");
                    return;
                case Thing thing:
                    AppendHandle(sb, thing, "thing");
                    return;
                case Map map:
                    AppendHandle(sb, map, "map");
                    return;
                case Faction faction:
                    AppendHandle(sb, faction, "faction");
                    return;
            }

            Type type = value.GetType();
            if (type.IsEnum)
            {
                Json.WriteString(sb, value.ToString());
                return;
            }

            // Parsed JSON objects (hook state, nested payloads) go back out as objects.
            if (value is Dictionary<string, object> dict)
            {
                sb.Append('{');
                bool firstKey = true;
                foreach (KeyValuePair<string, object> kv in dict)
                {
                    if (!firstKey) sb.Append(',');
                    firstKey = false;
                    Json.WriteString(sb, kv.Key);
                    sb.Append(':');
                    Encode(sb, kv.Value, depth + 1);
                }

                sb.Append('}');
                return;
            }

            if (value is IEnumerable list && !(value is IDictionary))
            {
                sb.Append('[');
                int n = 0;
                foreach (object item in list)
                {
                    if (n >= MaxListItems)
                    {
                        break;
                    }

                    if (n > 0) sb.Append(',');
                    Encode(sb, item, depth + 1);
                    n++;
                }

                sb.Append(']');
                return;
            }

            if (type.IsValueType)
            {
                Json.WriteString(sb, value.ToString());
                return;
            }

            AppendHandle(sb, value, "object");
        }

        private static void EncodeDamageInfo(StringBuilder sb, DamageInfo dinfo, int depth)
        {
            sb.Append("{\"def\":");
            Json.WriteString(sb, dinfo.Def != null ? dinfo.Def.defName : "");
            sb.Append(",\"amount\":");
            AppendDouble(sb, dinfo.Amount);
            sb.Append(",\"angle\":");
            AppendDouble(sb, dinfo.Angle);
            sb.Append(",\"instigator\":");
            Encode(sb, dinfo.Instigator, depth + 1);
            sb.Append(",\"weapon\":");
            Encode(sb, dinfo.Weapon, depth + 1);
            sb.Append(",\"hit_part\":");
            Json.WriteString(sb, dinfo.HitPart != null ? dinfo.HitPart.def.defName : "");
            sb.Append('}');
        }

        private static void AppendHandle(StringBuilder sb, object o, string kind)
        {
            int h = ObjectHandles.GetOrAdd(o);
            sb.Append("{\"$h\":").Append(h.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"$k\":\"").Append(kind).Append('"');
            Type t = o.GetType();
            sb.Append(",\"$t\":");
            Json.WriteString(sb, t.FullName ?? t.Name);
            if (kind == "object")
            {
                string defName = TryReadDefName(o, t);
                if (defName != null)
                {
                    sb.Append(",\"def\":");
                    Json.WriteString(sb, defName);
                }
            }

            sb.Append('}');
        }

        private static string TryReadDefName(object o, Type t)
        {
            MemberInfo member;
            lock (DefMemberCache)
            {
                if (!DefMemberCache.TryGetValue(t, out member))
                {
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                    FieldInfo field = t.GetField("def", flags);
                    PropertyInfo prop = field == null ? t.GetProperty("def", flags) : null;
                    if (field != null && typeof(Def).IsAssignableFrom(field.FieldType))
                    {
                        member = field;
                    }
                    else if (prop != null && prop.GetIndexParameters().Length == 0 && typeof(Def).IsAssignableFrom(prop.PropertyType))
                    {
                        member = prop;
                    }

                    DefMemberCache[t] = member;
                }
            }

            try
            {
                object v = member is FieldInfo fi ? fi.GetValue(o) : member is PropertyInfo pi ? pi.GetValue(o, null) : null;
                return (v as Def)?.defName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AppendDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                sb.Append("null");
                return;
            }

            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>Coerces a parsed JSON value to the declared parameter or return type.</summary>
        public static bool TryCoerce(object json, Type target, out object result)
        {
            result = null;
            if (target == null)
            {
                return false;
            }

            if (target.IsByRef)
            {
                target = target.GetElementType();
            }

            Type nullable = Nullable.GetUnderlyingType(target);
            if (json == null)
            {
                if (!target.IsValueType || nullable != null)
                {
                    return true;
                }

                return false;
            }

            if (nullable != null)
            {
                target = nullable;
            }

            if (json is Dictionary<string, object> obj)
            {
                if (obj.ContainsKey("$h"))
                {
                    object found = ObjectHandles.Get<object>((int)Json.GetLong(obj, "$h"));
                    if (found != null && target.IsInstanceOfType(found))
                    {
                        result = found;
                        return true;
                    }

                    if (found is Thing thing)
                    {
                        if (target == typeof(LocalTargetInfo)) { result = new LocalTargetInfo(thing); return true; }
                        if (target == typeof(TargetInfo)) { result = new TargetInfo(thing); return true; }
                    }

                    return false;
                }

                if (target == typeof(IntVec3) && obj.ContainsKey("x"))
                {
                    result = new IntVec3((int)Json.GetLong(obj, "x"), (int)Json.GetLong(obj, "y"), (int)Json.GetLong(obj, "z"));
                    return true;
                }

                if (target == typeof(LocalTargetInfo) && obj.ContainsKey("x"))
                {
                    result = new LocalTargetInfo(new IntVec3((int)Json.GetLong(obj, "x"), (int)Json.GetLong(obj, "y"), (int)Json.GetLong(obj, "z")));
                    return true;
                }

                if (target == typeof(Vector3) && obj.ContainsKey("x"))
                {
                    result = new Vector3(ToFloat(obj, "x"), ToFloat(obj, "y"), ToFloat(obj, "z"));
                    return true;
                }

                if (target == typeof(Vector2) && obj.ContainsKey("x"))
                {
                    result = new Vector2(ToFloat(obj, "x"), ToFloat(obj, "y"));
                    return true;
                }

                return false;
            }

            if (json is List<object> list)
            {
                return TryCoerceList(list, target, out result);
            }

            if (target == typeof(object))
            {
                result = json;
                return true;
            }

            if (target == typeof(string))
            {
                result = json is string s0 ? s0 : Convert.ToString(json, CultureInfo.InvariantCulture);
                return true;
            }

            if (target == typeof(TaggedString))
            {
                result = new TaggedString(Convert.ToString(json, CultureInfo.InvariantCulture));
                return true;
            }

            if (target == typeof(bool))
            {
                if (json is bool b) { result = b; return true; }
                if (json is long lb) { result = lb != 0; return true; }
                return false;
            }

            if (target.IsEnum)
            {
                try
                {
                    result = json is string name
                        ? Enum.Parse(target, name, true)
                        : Enum.ToObject(target, Convert.ToInt64(json, CultureInfo.InvariantCulture));
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (typeof(Def).IsAssignableFrom(target))
            {
                if (!(json is string defName)) return false;
                MethodInfo getNamed = typeof(DefDatabase<>).MakeGenericType(target)
                    .GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static);
                result = getNamed?.Invoke(null, new object[] { defName });
                return result != null;
            }

            if (target == typeof(Rot4) && json is long rot)
            {
                result = new Rot4((int)rot);
                return true;
            }

            if (IsNumeric(target) && (json is long || json is double))
            {
                try
                {
                    result = Convert.ChangeType(json, target, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryCoerceList(List<object> list, Type target, out object result)
        {
            result = null;
            if (target == typeof(IntVec3) && list.Count >= 3)
            {
                result = new IntVec3(Convert.ToInt32(list[0]), Convert.ToInt32(list[1]), Convert.ToInt32(list[2]));
                return true;
            }

            Type element = null;
            if (target.IsArray)
            {
                element = target.GetElementType();
            }
            else if (target.IsGenericType && target.GetGenericArguments().Length == 1 &&
                     target.IsAssignableFrom(typeof(List<>).MakeGenericType(target.GetGenericArguments()[0])))
            {
                element = target.GetGenericArguments()[0];
            }

            if (element == null)
            {
                return false;
            }

            Array values = Array.CreateInstance(element, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!TryCoerce(list[i], element, out object item))
                {
                    return false;
                }

                values.SetValue(item, i);
            }

            if (target.IsArray)
            {
                result = values;
                return true;
            }

            result = Activator.CreateInstance(typeof(List<>).MakeGenericType(element), new object[] { values });
            return true;
        }

        private static bool IsNumeric(Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                    return true;
                default:
                    return false;
            }
        }

        private static float ToFloat(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object v)) return 0f;
            return v is long l ? l : v is double d ? (float)d : 0f;
        }
    }
}
