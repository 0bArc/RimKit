using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;

namespace RimKit
{
    /// <summary>
    /// Records Advanced tier calls (reflection and Harmony hooks). Entries use ISO 8601 UTC timestamps.
    /// Denied calls are always written to the game log. Allowed calls are written only when the setting
    /// rimkit.audit_verbose is on. The last entries stay available through reflect.audit.
    /// </summary>
    internal static class ReflectAudit
    {
        private const int Capacity = 500;
        private static readonly Queue<string> Ring = new Queue<string>();

        private static bool Verbose =>
            LuaConfigBridge.Settings?.Bools != null &&
            LuaConfigBridge.Settings.Bools.TryGetValue("rimkit.audit_verbose", out bool v) && v;

        public static void Record(string op, string target, string outcome)
        {
            string line = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) +
                          " " + op + " " + target + " " + outcome;
            lock (Ring)
            {
                Ring.Enqueue(line);
                while (Ring.Count > Capacity)
                {
                    Ring.Dequeue();
                }
            }

            if (outcome != "ok" || Verbose)
            {
                Log.Message("[RimKit][audit] " + line);
            }
        }

        public static List<string> Recent(int count)
        {
            lock (Ring)
            {
                return Ring.Skip(Math.Max(0, Ring.Count - Math.Max(1, count))).ToList();
            }
        }
    }

    /// <summary>Which types and members typed reflection may touch.</summary>
    internal static class ReflectPolicy
    {
        private static readonly string[] BlockedUnityPrefixes =
        {
            "UnityEngine.Networking", "UnityEngine.Diagnostics", "UnityEngine.Profiling", "UnityEngine.Windows",
            "UnityEngine.SceneManagement", "UnityEngine.XR", "UnityEngine.Playables", "UnityEngine.Analytics",
            "UnityEngine.Scripting", "UnityEngine.Rendering", "UnityEngine.Experimental",
        };

        private static readonly string[] BlockedGameTypes =
        {
            "Verse.DirectXmlSaver", "Verse.GenCommandLine", "Verse.Root", "Verse.Root_Entry", "Verse.Root_Play",
            "Verse.ModAssemblyHandler", "Verse.LoadedModManager", "Verse.ModContentPack", "Verse.PlayDataLoader",
            "Verse.ScribeSaver", "Verse.ScribeLoader", "Verse.GenFilePaths", "Verse.SteamManager", "Verse.Steam.",
            "RimWorld.SteamUtility", "RimWorld.ScenarioFiles", "RimWorld.Planet.WorldFileUtility",
        };

        private static readonly string[] BlockedNamePieces =
        {
            "OpenUrl", "OpenURL", "OpenFile", "OpenDir", "OpenLog", "OpenWorkshop", "Shutdown", "Restart", "Quit",
            "Browse", "Process", "WriteAll", "ExecuteMenuItem", "RunInTextEditor",
        };

        public static string CheckType(Type t)
        {
            if (t == null) return "type not found";
            if (t.IsByRef) t = t.GetElementType();
            if (t.IsArray) return CheckType(t.GetElementType());
            Type nullable = Nullable.GetUnderlyingType(t);
            if (nullable != null) return CheckType(nullable);
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(object)) return null;

            if (t.IsGenericType)
            {
                string ns0 = t.Namespace ?? "";
                if (ns0 == "System.Collections.Generic" || ns0 == "System")
                {
                    foreach (Type arg in t.GetGenericArguments())
                    {
                        string inner = CheckType(arg);
                        if (inner != null) return inner;
                    }

                    return null;
                }
            }

            string ns = t.Namespace ?? "";
            string full = t.FullName ?? t.Name;
            bool game = ns.StartsWith("Verse", StringComparison.Ordinal) ||
                        ns.StartsWith("RimWorld", StringComparison.Ordinal) ||
                        ns.StartsWith("UnityEngine", StringComparison.Ordinal);
            if (!game)
            {
                return "type outside the game namespaces (Verse, RimWorld, UnityEngine): " + full;
            }

            foreach (string prefix in BlockedUnityPrefixes)
            {
                if (full.StartsWith(prefix, StringComparison.Ordinal)) return "type blocked: " + full;
            }

            foreach (string blocked in BlockedGameTypes)
            {
                if (blocked.EndsWith(".", StringComparison.Ordinal)
                        ? full.StartsWith(blocked, StringComparison.Ordinal)
                        : full == blocked)
                {
                    return "type blocked: " + full;
                }
            }

            return LuaSandbox.DenyIfReflectBlocked(t, null, false);
        }

        public static string CheckMember(Type declaring, string name, bool isCall)
        {
            if (string.IsNullOrEmpty(name)) return "member name is empty";
            foreach (string piece in BlockedNamePieces)
            {
                if (name.IndexOf(piece, StringComparison.OrdinalIgnoreCase) >= 0) return "member blocked: " + name;
            }

            string meta = LuaSandbox.DenyIfReflectBlocked(declaring, name, isCall);
            if (meta != null) return meta;
            if (name == "GetType") return "member blocked: GetType";
            return null;
        }

        public static string CheckValue(object value)
        {
            if (value == null) return null;
            Type t = value.GetType();
            if (value is Type || value is Assembly || value is MemberInfo || value is Delegate || value is Module)
            {
                return "value blocked: " + t.Name;
            }

            return null;
        }
    }

    // Typed reflection: JSON typed arguments and results, overload resolution by coercion, stable error codes.
    // Lua entry points live under game.reflect. All ops need rimkit.developer_reflect except type, is_a and release.
    internal static class ApiReflect
    {
        private const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const int MaxMembers = 800;

        public static void Register()
        {
            ApiRegistry.Register("reflect.type", TypeOf, "advanced");
            ApiRegistry.Register("reflect.is_a", IsA, "advanced");
            ApiRegistry.Register("reflect.release", Release, "advanced");
            ApiRegistry.Register("reflect.get", Get, "advanced");
            ApiRegistry.Register("reflect.set", Set, "advanced");
            ApiRegistry.Register("reflect.call", Call, "advanced");
            ApiRegistry.Register("reflect.static_get", StaticGet, "advanced");
            ApiRegistry.Register("reflect.static_set", StaticSet, "advanced");
            ApiRegistry.Register("reflect.static_call", StaticCall, "advanced");
            ApiRegistry.Register("reflect.new", New, "advanced");
            ApiRegistry.Register("reflect.members", Members, "advanced");
            ApiRegistry.Register("reflect.enum_names", EnumNames, "advanced");
            ApiRegistry.Register("reflect.audit", Audit, "advanced");
        }

        private static string Fail(string code, string message) =>
            "{\"ok\":false,\"code\":\"" + code + "\",\"e\":" + JsonLite.Quote(code + ": " + message) + "}";

        private static string OkJson(string json) => "{\"ok\":true,\"t\":\"j\",\"v\":" + json + "}";

        private static string Gate()
        {
            return LuaSandbox.DeveloperReflectEnabled
                ? null
                : Fail("RK4002", "typed reflection is off. Enable the RimKit setting developer_reflect.");
        }

        private static bool TryParseJson(string raw, out object value, out string error)
        {
            error = null;
            value = null;
            if (string.IsNullOrEmpty(raw))
            {
                return true;
            }

            if (!Json.TryParse(raw, out value))
            {
                error = Fail("RK1001", "argument is not valid JSON");
                return false;
            }

            return true;
        }

        private static Type ResolveType(string name, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(name))
            {
                error = Fail("RK1001", "type is required");
                return null;
            }

            Type t = AccessTools.TypeByName(name);
            if (t == null)
            {
                error = Fail("RK3001", "type not found: " + name);
                return null;
            }

            string deny = ReflectPolicy.CheckType(t);
            if (deny != null)
            {
                ReflectAudit.Record("reflect", name, "denied: " + deny);
                error = Fail("RK4001", deny);
                return null;
            }

            return t;
        }

        private static object ResolveInstance(Dictionary<string, string> args, out Type type, out string error)
        {
            error = null;
            type = null;
            int h = ApiHelpers.Int(args, "h");
            object obj = ObjectHandles.Get<object>(h);
            if (obj == null)
            {
                error = Fail("RK2001", "handle " + h + " is stale or null");
                return null;
            }

            type = obj.GetType();
            string deny = ReflectPolicy.CheckType(type);
            if (deny != null)
            {
                ReflectAudit.Record("reflect", type.FullName, "denied: " + deny);
                error = Fail("RK4001", deny);
                return null;
            }

            return obj;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(name, AnyMember);
                if (p != null && p.GetIndexParameters().Length == 0) return p;
                FieldInfo f = t.GetField(name, AnyMember);
                if (f != null) return f;
            }

            return null;
        }

        private static string Encoded(object value, string op, string target)
        {
            string deny = ReflectPolicy.CheckValue(value);
            if (deny != null)
            {
                ReflectAudit.Record(op, target, "denied: " + deny);
                return Fail("RK4001", deny);
            }

            ReflectAudit.Record(op, target, "ok");
            return OkJson(EncodePinned(value));
        }

        // A result that Lua just received may be an object nothing else owns yet, so its handle pins it.
        private static string EncodePinned(object value)
        {
            ObjectHandles.PinNewHandles = true;
            try
            {
                return HookCodec.Encode(value);
            }
            finally
            {
                ObjectHandles.PinNewHandles = false;
            }
        }

        // ---------------------------------------------------------------- simple ops

        private static string TypeOf(Dictionary<string, string> args)
        {
            object obj = ObjectHandles.Get<object>(ApiHelpers.Int(args, "h"));
            return obj == null ? Fail("RK2001", "handle is stale or null") : ApiHelpers.OkStr(obj.GetType().FullName ?? obj.GetType().Name);
        }

        private static string IsA(Dictionary<string, string> args)
        {
            object obj = ObjectHandles.Get<object>(ApiHelpers.Int(args, "h"));
            if (obj == null) return Fail("RK2001", "handle is stale or null");
            Type t = AccessTools.TypeByName(ApiHelpers.Str(args, "type"));
            return ApiHelpers.OkBool(t != null && t.IsInstanceOfType(obj));
        }

        private static string Release(Dictionary<string, string> args)
        {
            ObjectHandles.Remove(ObjectHandles.Get<object>(ApiHelpers.Int(args, "h")));
            return ApiHelpers.OkBool(true);
        }

        private static string Audit(Dictionary<string, string> args)
        {
            int n = ApiHelpers.Int(args, "n");
            var sb = new StringBuilder("[");
            List<string> lines = ReflectAudit.Recent(n <= 0 ? 50 : n);
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Json.WriteString(sb, lines[i]);
            }

            return OkJson(sb.Append(']').ToString());
        }

        private static string EnumNames(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            Type t = ResolveType(ApiHelpers.Str(args, "type"), out string error);
            if (t == null) return error;
            if (!t.IsEnum) return Fail("RK1001", t.FullName + " is not an enum");
            return OkJson(HookCodec.Encode(Enum.GetNames(t).ToList()));
        }

        // ---------------------------------------------------------------- fields and properties

        private static string Get(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            object obj = ResolveInstance(args, out Type type, out string error);
            if (obj == null) return error;
            return ReadMember(type, obj, ApiHelpers.Str(args, "member"), "reflect.get");
        }

        private static string StaticGet(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            Type type = ResolveType(ApiHelpers.Str(args, "type"), out string error);
            if (type == null) return error;
            return ReadMember(type, null, ApiHelpers.Str(args, "member"), "reflect.static_get");
        }

        private static string ReadMember(Type type, object obj, string name, string op)
        {
            string target = type.FullName + "." + name;
            string deny = ReflectPolicy.CheckMember(type, name, false);
            if (deny != null)
            {
                ReflectAudit.Record(op, target, "denied: " + deny);
                return Fail("RK4001", deny);
            }

            MemberInfo member = FindMember(type, name);
            try
            {
                if (member is PropertyInfo pi && pi.GetGetMethod(true) != null)
                {
                    if (obj == null && !pi.GetGetMethod(true).IsStatic) return Fail("RK1001", name + " is an instance member");
                    return Encoded(pi.GetValue(obj, null), op, target);
                }

                if (member is FieldInfo fi)
                {
                    if (obj == null && !fi.IsStatic) return Fail("RK1001", name + " is an instance member");
                    return Encoded(fi.GetValue(obj), op, target);
                }
            }
            catch (TargetInvocationException tie)
            {
                return Fail("RK5001", (tie.InnerException ?? tie).Message);
            }

            return Fail("RK3001", "member not found: " + target);
        }

        private static string Set(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            object obj = ResolveInstance(args, out Type type, out string error);
            if (obj == null) return error;
            return WriteMember(type, obj, args, "reflect.set");
        }

        private static string StaticSet(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            Type type = ResolveType(ApiHelpers.Str(args, "type"), out string error);
            if (type == null) return error;
            return WriteMember(type, null, args, "reflect.static_set");
        }

        private static string WriteMember(Type type, object obj, Dictionary<string, string> args, string op)
        {
            string name = ApiHelpers.Str(args, "member");
            string target = type.FullName + "." + name;
            string deny = ReflectPolicy.CheckMember(type, name, false);
            if (deny != null)
            {
                ReflectAudit.Record(op, target, "denied: " + deny);
                return Fail("RK4001", deny);
            }

            if (!TryParseJson(ApiHelpers.Str(args, "value_json"), out object json, out string jsonError)) return jsonError;
            MemberInfo member = FindMember(type, name);
            Type memberType = member is PropertyInfo p ? p.PropertyType : member is FieldInfo f ? f.FieldType : null;
            if (memberType == null) return Fail("RK3001", "member not found: " + target);
            string typeDeny = ReflectPolicy.CheckType(memberType);
            if (typeDeny != null) return Fail("RK4001", typeDeny);
            if (!HookCodec.TryCoerce(json, memberType, out object value))
            {
                return Fail("RK1001", "value does not fit " + memberType.Name + " for " + target);
            }

            try
            {
                if (member is PropertyInfo pi)
                {
                    if (pi.GetSetMethod(true) == null) return Fail("RK3001", target + " is read only");
                    pi.SetValue(obj, value, null);
                }
                else
                {
                    ((FieldInfo)member).SetValue(obj, value);
                }
            }
            catch (TargetInvocationException tie)
            {
                return Fail("RK5001", (tie.InnerException ?? tie).Message);
            }

            ReflectAudit.Record(op, target, "ok");
            return ApiHelpers.OkBool(true);
        }

        // ---------------------------------------------------------------- calls and construction

        private static string Call(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            object obj = ResolveInstance(args, out Type type, out string error);
            if (obj == null) return error;
            return Invoke(type, obj, args, "reflect.call");
        }

        private static string StaticCall(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            Type type = ResolveType(ApiHelpers.Str(args, "type"), out string error);
            if (type == null) return error;
            return Invoke(type, null, args, "reflect.static_call");
        }

        private static string New(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            Type type = ResolveType(ApiHelpers.Str(args, "type"), out string error);
            if (type == null) return error;
            if (type.IsAbstract || type.IsInterface) return Fail("RK1001", type.FullName + " cannot be constructed");
            if (!TryParseJson(ApiHelpers.Str(args, "args_json"), out object argsJson, out string jsonError)) return jsonError;
            List<object> list = argsJson as List<object> ?? new List<object>();
            string target = type.FullName + ".ctor";

            if (list.Count == 0 && type.IsValueType)
            {
                return Encoded(Activator.CreateInstance(type), "reflect.new", target);
            }

            IEnumerable<MethodBase> ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return InvokeBest(ctors, null, list, null, "reflect.new", target, isCtor: true);
        }

        private static string Invoke(Type type, object obj, Dictionary<string, string> args, string op)
        {
            string name = ApiHelpers.Str(args, "method");
            string target = type.FullName + "." + name;
            string deny = ReflectPolicy.CheckMember(type, name, true);
            if (deny != null)
            {
                ReflectAudit.Record(op, target, "denied: " + deny);
                return Fail("RK4001", deny);
            }

            if (!TryParseJson(ApiHelpers.Str(args, "args_json"), out object argsJson, out string e1)) return e1;
            if (!TryParseJson(ApiHelpers.Str(args, "sig_json"), out object sigJson, out string e2)) return e2;
            List<object> list = argsJson as List<object> ?? new List<object>();
            List<string> sig = (sigJson as List<object>)?.OfType<string>().ToList();

            var methods = new List<MethodBase>();
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (MethodInfo m in t.GetMethods(AnyMember))
                {
                    if (m.Name == name && !m.IsGenericMethodDefinition && (obj != null || m.IsStatic) &&
                        !methods.Any(x => SameSignature(x, m)))
                    {
                        methods.Add(m);
                    }
                }
            }

            if (methods.Count == 0)
            {
                return Fail("RK3001", "method not found: " + target + (obj == null ? " (static)" : ""));
            }

            return InvokeBest(methods, obj, list, sig, op, target, isCtor: false);
        }

        private static bool SameSignature(MethodBase a, MethodBase b)
        {
            ParameterInfo[] pa = a.GetParameters();
            ParameterInfo[] pb = b.GetParameters();
            if (pa.Length != pb.Length) return false;
            for (int i = 0; i < pa.Length; i++)
            {
                if (pa[i].ParameterType != pb[i].ParameterType) return false;
            }

            return true;
        }

        private static string InvokeBest(IEnumerable<MethodBase> candidates, object obj, List<object> argValues,
            List<string> sig, string op, string target, bool isCtor)
        {
            // Fewest unused optional parameters first, so an exact arity match wins.
            var ordered = candidates
                .Select(c => new { Method = c, Params = c.GetParameters() })
                .Where(c => sig == null || SigMatches(c.Params, sig))
                .OrderBy(c => c.Params.Count(p => !p.IsOut) - argValues.Count < 0 ? 99 : c.Params.Count(p => !p.IsOut) - argValues.Count)
                .ToList();

            foreach (var c in ordered)
            {
                if (!TryBuildArguments(c.Params, argValues, out object[] values, out List<int> outIndexes))
                {
                    continue;
                }

                string paramDeny = c.Params.Select(p => ReflectPolicy.CheckType(p.ParameterType)).FirstOrDefault(d => d != null);
                if (paramDeny != null)
                {
                    ReflectAudit.Record(op, target, "denied: " + paramDeny);
                    return Fail("RK4001", paramDeny);
                }

                try
                {
                    object result = isCtor ? ((ConstructorInfo)c.Method).Invoke(values) : ((MethodInfo)c.Method).Invoke(obj, values);
                    if (outIndexes.Count == 0)
                    {
                        return Encoded(result, op, target);
                    }

                    ObjectHandles.PinNewHandles = true;
                    var sb = new StringBuilder("{\"result\":");
                    HookCodec.Encode(sb, result, 0);
                    sb.Append(",\"out\":{");
                    for (int i = 0; i < outIndexes.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Json.WriteString(sb, c.Params[outIndexes[i]].Name);
                        sb.Append(':');
                        HookCodec.Encode(sb, values[outIndexes[i]], 0);
                    }

                    sb.Append("}}");
                    ObjectHandles.PinNewHandles = false;
                    ReflectAudit.Record(op, target, "ok");
                    return OkJson(sb.ToString());
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    ReflectAudit.Record(op, target, "error: " + inner.GetType().Name);
                    return Fail("RK5001", inner.GetType().Name + ": " + inner.Message);
                }
            }

            string overloads = string.Join(" | ", ordered.Select(c => "(" + string.Join(", ", c.Params.Select(p => p.ParameterType.Name + " " + p.Name)) + ")"));
            ReflectAudit.Record(op, target, "no overload");
            return Fail("RK3002", "no overload of " + target + " accepts these " + argValues.Count + " arguments. Candidates: " +
                                  (overloads.Length == 0 ? "none" : overloads));
        }

        private static bool SigMatches(ParameterInfo[] ps, List<string> sig)
        {
            if (ps.Length != sig.Count) return false;
            for (int i = 0; i < ps.Length; i++)
            {
                Type t = ps[i].ParameterType;
                string s = sig[i];
                bool byRef = s.EndsWith("&", StringComparison.Ordinal);
                if (byRef) s = s.Substring(0, s.Length - 1);
                if (byRef != t.IsByRef) return false;
                Type element = t.IsByRef ? t.GetElementType() : t;
                if (!(string.Equals(element.FullName, s, StringComparison.Ordinal) || string.Equals(element.Name, s, StringComparison.Ordinal) ||
                      string.Equals(Alias(element), s, StringComparison.Ordinal)))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Alias(Type t)
        {
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(double)) return "double";
            if (t == typeof(long)) return "long";
            return null;
        }

        // Lua supplies values for the non-out parameters in order. Optional parameters may be left off.
        private static bool TryBuildArguments(ParameterInfo[] ps, List<object> given, out object[] values, out List<int> outIndexes)
        {
            values = new object[ps.Length];
            outIndexes = new List<int>();
            int next = 0;
            for (int i = 0; i < ps.Length; i++)
            {
                ParameterInfo p = ps[i];
                if (p.IsOut)
                {
                    outIndexes.Add(i);
                    Type el = p.ParameterType.GetElementType();
                    values[i] = el != null && el.IsValueType ? Activator.CreateInstance(el) : null;
                    continue;
                }

                if (next < given.Count)
                {
                    if (!HookCodec.TryCoerce(given[next], p.ParameterType, out object coerced) ||
                        (coerced == null && p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) == null && !p.ParameterType.IsByRef))
                    {
                        return false;
                    }

                    values[i] = coerced;
                    if (p.ParameterType.IsByRef) outIndexes.Add(i);
                    next++;
                    continue;
                }

                if (p.HasDefaultValue)
                {
                    values[i] = p.DefaultValue == DBNull.Value || p.DefaultValue == null
                        ? (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                        : p.DefaultValue;
                    continue;
                }

                if (p.IsOptional)
                {
                    values[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                    continue;
                }

                return false;
            }

            return next == given.Count;
        }

        // ---------------------------------------------------------------- discovery

        private static string Members(Dictionary<string, string> args)
        {
            string gate = Gate();
            if (gate != null) return gate;
            Type type;
            string error;
            if (ApiHelpers.Int(args, "h") != 0)
            {
                if (ResolveInstance(args, out type, out error) == null) return error;
            }
            else
            {
                type = ResolveType(ApiHelpers.Str(args, "type"), out error);
                if (type == null) return error;
            }

            var sb = new StringBuilder("[");
            int n = 0;
            for (Type t = type; t != null && n < MaxMembers; t = t.BaseType)
            {
                if (ReflectPolicy.CheckType(t) != null) break;
                foreach (MemberInfo m in t.GetMembers(AnyMember))
                {
                    if (n >= MaxMembers) break;
                    string row = DescribeMember(m, t);
                    if (row == null) continue;
                    if (n++ > 0) sb.Append(',');
                    sb.Append(row);
                }
            }

            ReflectAudit.Record("reflect.members", type.FullName, "ok");
            return OkJson(sb.Append(']').ToString());
        }

        private static string DescribeMember(MemberInfo m, Type declaring)
        {
            var sb = new StringBuilder("{\"name\":");
            Json.WriteString(sb, m.Name);
            sb.Append(",\"declaring\":");
            Json.WriteString(sb, declaring.Name);
            switch (m)
            {
                case PropertyInfo p when p.GetIndexParameters().Length == 0:
                    sb.Append(",\"kind\":\"property\",\"type\":");
                    Json.WriteString(sb, p.PropertyType.Name);
                    sb.Append(",\"readonly\":").Append(p.GetSetMethod(true) == null ? "true" : "false");
                    sb.Append(",\"static\":").Append((p.GetGetMethod(true) ?? p.GetSetMethod(true))?.IsStatic == true ? "true" : "false");
                    break;
                case FieldInfo f:
                    if (f.Name.IndexOf('<') >= 0) return null;
                    sb.Append(",\"kind\":\"field\",\"type\":");
                    Json.WriteString(sb, f.FieldType.Name);
                    sb.Append(",\"readonly\":").Append(f.IsInitOnly ? "true" : "false");
                    sb.Append(",\"static\":").Append(f.IsStatic ? "true" : "false");
                    break;
                case MethodInfo mi when !mi.IsSpecialName:
                    sb.Append(",\"kind\":\"method\",\"type\":");
                    Json.WriteString(sb, mi.ReturnType.Name);
                    sb.Append(",\"static\":").Append(mi.IsStatic ? "true" : "false");
                    AppendParams(sb, mi.GetParameters());
                    break;
                case ConstructorInfo ci when !ci.IsStatic:
                    sb.Append(",\"kind\":\"ctor\",\"type\":");
                    Json.WriteString(sb, declaring.Name);
                    sb.Append(",\"static\":false");
                    AppendParams(sb, ci.GetParameters());
                    break;
                default:
                    return null;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendParams(StringBuilder sb, ParameterInfo[] ps)
        {
            sb.Append(",\"params\":[");
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":");
                Json.WriteString(sb, ps[i].Name);
                sb.Append(",\"type\":");
                Json.WriteString(sb, ps[i].ParameterType.Name);
                sb.Append(",\"optional\":").Append(ps[i].IsOptional ? "true" : "false");
                sb.Append(",\"mode\":\"").Append(ps[i].IsOut ? "out" : ps[i].ParameterType.IsByRef ? "ref" : "in").Append("\"}");
            }

            sb.Append(']');
        }
    }
}
