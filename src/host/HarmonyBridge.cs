using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimKit
{
    /// <summary>One Lua hook attached to a game method.</summary>
    internal sealed class HookRecord
    {
        public int Id;
        public int Kind;
        public int PatchPriority = HarmonyLib.Priority.Normal;
        public MethodBase Target;
        public string TypeName;
        public string MethodName;
        public string[] Before = new string[0];
        public string[] After = new string[0];

        // Call redirect only.
        public MethodBase CallTarget;
        public int Nth;

        // Hooks registered together by game.hooks.patch share one state slot per call.
        public int Group;

        // Scalar-only hooks skip JSON (option fast = true).
        public bool Fast;

        // Cost so far, for game.hooks.list and the cost warning.
        public long Calls;
        public long Ticks;
    }

    /// <summary>All hooks on one game method plus what is currently patched in.</summary>
    internal sealed class MethodHooks
    {
        public MethodBase Method;
        public ParameterInfo[] Params;
        public Type ReturnType;
        public bool HasResult;
        public string DisplayName;

        // Copy on write: patched methods may run on worker threads.
        public HookRecord[] Prefixes = new HookRecord[0];
        public HookRecord[] Postfixes = new HookRecord[0];
        public HookRecord[] Finalizers = new HookRecord[0];
        public HookRecord[] Redirects = new HookRecord[0];

        public bool PatchedPrefix;
        public bool PatchedPostfix;
        public bool PatchedFinalizer;
        public bool PatchedTranspiler;
        public string AppliedOrderKey = "";

        public int Count => Prefixes.Length + Postfixes.Length + Finalizers.Length + Redirects.Length;
    }

    /// <summary>Per call scratch shared by the prefix, postfix and finalizer of one invocation.</summary>
    internal sealed class HookCallState
    {
        public readonly Dictionary<int, string> StateJson = new Dictionary<int, string>();
    }

    internal static class HarmonyBridge
    {
        private const int KindPostfix = 0;
        private const int KindPrefix = 1;
        private const int KindFinalizer = 2;
        private const int KindRedirect = 3;
        private const int MaxNesting = 8;

        private static Harmony harmony;
        private static readonly object Gate = new object();
        private static readonly Dictionary<int, HookRecord> Hooks = new Dictionary<int, HookRecord>();
        private static readonly Dictionary<MethodBase, MethodHooks> Methods = new Dictionary<MethodBase, MethodHooks>();

        [ThreadStatic] private static int nesting;

        public static void Init(Harmony harmonyInstance)
        {
            harmony = harmonyInstance;
        }

        // ------------------------------------------------------------------ registration

        public static int RegisterHook(string typeName, string methodName, int kind, int hookId, string optsJson)
        {
            try
            {
                Json.TryParse(string.IsNullOrEmpty(optsJson) ? "{}" : optsJson, out object parsed);
                Dictionary<string, object> opts = Json.AsObject(parsed) ?? new Dictionary<string, object>();

                Type type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    Log.Error("[RimKit] Hook type not found: " + typeName);
                    return 0;
                }

                MethodBase method = FindMethod(type, methodName, Json.GetStringList(opts, "sig"), out string problem);
                if (method == null)
                {
                    Log.Error("[RimKit] " + problem);
                    return 0;
                }

                if (method.IsAbstract)
                {
                    Log.Error("[RimKit] Cannot patch abstract method " + typeName + "." + methodName + ". Patch an override instead.");
                    return 0;
                }

                if (IsExtern(method))
                {
                    Log.Error("[RimKit] Cannot patch extern method " + typeName + "." + methodName);
                    return 0;
                }

                var record = new HookRecord
                {
                    Id = hookId,
                    Kind = kind,
                    Target = method,
                    TypeName = typeName,
                    MethodName = methodName,
                    PatchPriority = (int)Json.GetLong(opts, "priority", HarmonyLib.Priority.Normal),
                    Before = Json.GetStringList(opts, "before").ToArray(),
                    After = Json.GetStringList(opts, "after").ToArray(),
                    Nth = (int)Json.GetLong(opts, "nth", 0),
                    Group = (int)Json.GetLong(opts, "group", hookId)
                };

                if (kind == KindRedirect)
                {
                    string call = Json.GetString(opts, "call");
                    if (string.IsNullOrEmpty(call))
                    {
                        Log.Error("[RimKit] replace_call needs a call target");
                        return 0;
                    }

                    int dot = call.LastIndexOf('.');
                    Type callType = dot > 0 ? AccessTools.TypeByName(call.Substring(0, dot)) : null;
                    if (callType == null)
                    {
                        Log.Error("[RimKit] replace_call type not found: " + call);
                        return 0;
                    }

                    record.CallTarget = FindMethod(callType, call.Substring(dot + 1), Json.GetStringList(opts, "call_sig"), out problem);
                    if (record.CallTarget == null)
                    {
                        Log.Error("[RimKit] " + problem);
                        return 0;
                    }

                    if (record.CallTarget.DeclaringType != null && record.CallTarget.DeclaringType.IsValueType && !record.CallTarget.IsStatic)
                    {
                        Log.Error("[RimKit] replace_call does not support instance methods on structs: " + call);
                        return 0;
                    }

                    if (record.CallTarget.GetParameters().Any(p => p.ParameterType.IsByRef))
                    {
                        Log.Error("[RimKit] replace_call does not support ref or out parameters: " + call);
                        return 0;
                    }
                }

                if (Json.GetBool(opts, "fast") && (kind == KindPrefix || kind == KindPostfix))
                {
                    string why = FastIneligible(method);
                    if (why == null)
                    {
                        record.Fast = true;
                    }
                    else
                    {
                        Log.Warning("[RimKit] hook " + hookId + " on " + typeName + "." + methodName + " cannot use fast = true (" + why + "). Using the normal path.");
                    }
                }

                lock (Gate)
                {
                    if (!Methods.TryGetValue(method, out MethodHooks m))
                    {
                        m = new MethodHooks
                        {
                            Method = method,
                            Params = method.GetParameters(),
                            ReturnType = method is MethodInfo info ? info.ReturnType : typeof(void),
                            DisplayName = (method.DeclaringType != null ? method.DeclaringType.FullName : typeName) + "." + method.Name
                        };
                        m.HasResult = m.ReturnType != typeof(void);
                        Methods[method] = m;
                    }

                    Hooks[hookId] = record;
                    AddRecord(m, record);
                    EnsurePatched(m);
                }

                if (record.Fast)
                {
                    // Outside the lock: the native core calls into the host while holding its own lock, so the host
                    // must never hold Gate while calling the native core.
                    MethodHooks fastMethod;
                    lock (Gate)
                    {
                        Methods.TryGetValue(method, out fastMethod);
                    }

                    if (fastMethod != null)
                    {
                        var names = new StringBuilder("[");
                        for (int i = 0; i < fastMethod.Params.Length; i++)
                        {
                            if (i > 0) names.Append(',');
                            Json.WriteString(names, fastMethod.Params[i].Name);
                        }

                        names.Append(']');
                        NativeAbi.SetHookInfo(hookId, fastMethod.DisplayName, KindName(kind), names.ToString());
                    }
                }

                ReflectAudit.Record("hooks." + KindName(kind), typeName + "." + methodName, "ok");
                Log.Message("[RimKit] Hooked " + KindName(kind) + " " + typeName + "." + methodName + " id=" + hookId);
                return 1;
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] RegisterHook failed: " + e);
                return 0;
            }
        }

        public static void UnregisterHook(int hookId)
        {
            lock (Gate)
            {
                if (!Hooks.TryGetValue(hookId, out HookRecord record))
                {
                    return;
                }

                Hooks.Remove(hookId);
                if (!Methods.TryGetValue(record.Target, out MethodHooks m))
                {
                    return;
                }

                RemoveRecord(m, record);
                try
                {
                    EnsurePatched(m);
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] UnregisterHook repatch failed: " + e);
                }

                if (m.Count == 0)
                {
                    Methods.Remove(record.Target);
                }
            }
        }

        public static IEnumerable<string> DescribeHooks()
        {
            lock (Gate)
            {
                return Hooks.Values.OrderBy(h => h.Id)
                    .Select(h => h.Id + " " + KindName(h.Kind) + " " + h.TypeName + "." + h.MethodName)
                    .ToList();
            }
        }

        private static string KindName(int kind)
        {
            switch (kind)
            {
                case KindPrefix: return "prefix";
                case KindPostfix: return "postfix";
                case KindFinalizer: return "finalizer";
                default: return "replace_call";
            }
        }

        private static bool IsExtern(MethodBase method)
        {
            return (method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0;
        }

        private static void AddRecord(MethodHooks m, HookRecord r)
        {
            switch (r.Kind)
            {
                case KindPrefix: m.Prefixes = Ordered(m.Prefixes, r); break;
                case KindPostfix: m.Postfixes = Ordered(m.Postfixes, r); break;
                case KindFinalizer: m.Finalizers = Ordered(m.Finalizers, r); break;
                default: m.Redirects = m.Redirects.Concat(new[] { r }).ToArray(); break;
            }
        }

        private static void RemoveRecord(MethodHooks m, HookRecord r)
        {
            m.Prefixes = m.Prefixes.Where(x => x.Id != r.Id).ToArray();
            m.Postfixes = m.Postfixes.Where(x => x.Id != r.Id).ToArray();
            m.Finalizers = m.Finalizers.Where(x => x.Id != r.Id).ToArray();
            m.Redirects = m.Redirects.Where(x => x.Id != r.Id).ToArray();
        }

        // Highest priority first, then registration order.
        private static HookRecord[] Ordered(HookRecord[] current, HookRecord added)
        {
            return current.Concat(new[] { added }).OrderByDescending(h => h.PatchPriority).ThenBy(h => h.Id).ToArray();
        }

        internal static MethodBase FindMethod(Type type, string name, List<string> sig, out string problem)
        {
            problem = null;
            if (name == ".ctor" || name == ".cctor")
            {
                Type[] ctorTypes = sig.Count > 0 ? ResolveTypes(sig, out problem) : null;
                if (problem != null)
                {
                    return null;
                }

                ConstructorInfo ctor = ctorTypes != null ? AccessTools.Constructor(type, ctorTypes, name == ".cctor") : AccessTools.Constructor(type, null, name == ".cctor");
                if (ctor == null)
                {
                    problem = "Constructor not found: " + type.FullName;
                }

                return ctor;
            }

            if (sig.Count > 0)
            {
                Type[] types = ResolveTypes(sig, out problem);
                if (problem != null)
                {
                    return null;
                }

                MethodInfo exact = AccessTools.Method(type, name, types);
                if (exact == null)
                {
                    problem = "Method not found: " + type.FullName + "." + name + "(" + string.Join(", ", sig) + ")";
                }

                return exact;
            }

            // No signature: accept when exactly one method has this name on the type (or its bases).
            for (Type t = type; t != null; t = t.BaseType)
            {
                List<MethodInfo> found = AccessTools.GetDeclaredMethods(t).Where(x => x.Name == name).ToList();
                if (found.Count == 1)
                {
                    return found[0];
                }

                if (found.Count > 1)
                {
                    problem = "Ambiguous method " + t.FullName + "." + name + ". Pass sig to choose one of: " +
                              string.Join(" | ", found.Select(DescribeSignature));
                    return null;
                }
            }

            // Property accessor shorthand, e.g. "CurTimeSpeed" for the getter.
            PropertyInfo prop = AccessTools.Property(type, name);
            if (prop?.GetGetMethod(true) != null)
            {
                return prop.GetGetMethod(true);
            }

            problem = "Method not found: " + type.FullName + "." + name;
            return null;
        }

        private static string DescribeSignature(MethodInfo m)
        {
            return m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)) + ")";
        }

        private static Type[] ResolveTypes(List<string> names, out string problem)
        {
            problem = null;
            var result = new Type[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                bool byRef = n.EndsWith("&", StringComparison.Ordinal);
                if (byRef)
                {
                    n = n.Substring(0, n.Length - 1);
                }

                Type t = TypeAlias(n) ?? AccessTools.TypeByName(n);
                if (t == null)
                {
                    problem = "Signature type not found: " + n;
                    return null;
                }

                result[i] = byRef ? t.MakeByRefType() : t;
            }

            return result;
        }

        private static Type TypeAlias(string n)
        {
            switch (n)
            {
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "long": return typeof(long);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                case "string": return typeof(string);
                case "byte": return typeof(byte);
                case "short": return typeof(short);
                case "object": return typeof(object);
                default: return null;
            }
        }

        // ------------------------------------------------------------------ patching

        private static string OrderKey(MethodHooks m)
        {
            int priority = m.Prefixes.Concat(m.Postfixes).Concat(m.Finalizers).Concat(m.Redirects)
                .Select(h => h.PatchPriority).DefaultIfEmpty(HarmonyLib.Priority.Normal).Max();
            string[] before = m.Prefixes.Concat(m.Postfixes).Concat(m.Finalizers).SelectMany(h => h.Before).Distinct().OrderBy(s => s).ToArray();
            string[] after = m.Prefixes.Concat(m.Postfixes).Concat(m.Finalizers).SelectMany(h => h.After).Distinct().OrderBy(s => s).ToArray();
            return priority + "|" + string.Join(",", before) + "|" + string.Join(",", after);
        }

        private static HarmonyMethod Patch(string name, MethodHooks m)
        {
            var hm = new HarmonyMethod(typeof(HarmonyBridge), name);
            int priority = m.Prefixes.Concat(m.Postfixes).Concat(m.Finalizers).Concat(m.Redirects)
                .Select(h => h.PatchPriority).DefaultIfEmpty(HarmonyLib.Priority.Normal).Max();
            hm.priority = priority;
            string[] before = m.Prefixes.Concat(m.Postfixes).Concat(m.Finalizers).SelectMany(h => h.Before).Distinct().ToArray();
            string[] after = m.Prefixes.Concat(m.Postfixes).Concat(m.Finalizers).SelectMany(h => h.After).Distinct().ToArray();
            if (before.Length > 0) hm.before = before;
            if (after.Length > 0) hm.after = after;
            return hm;
        }

        private static MethodInfo SharedMethod(string name) => AccessTools.Method(typeof(HarmonyBridge), name);

        private static string PrefixName(MethodHooks m) => m.HasResult ? nameof(PrefixR) : nameof(PrefixV);
        private static string PostfixName(MethodHooks m) => m.HasResult ? nameof(PostfixR) : nameof(PostfixV);

        // Must be called under Gate.
        private static void EnsurePatched(MethodHooks m)
        {
            if (harmony == null)
            {
                throw new InvalidOperationException("HarmonyBridge not initialised");
            }

            if (m.Count == 0)
            {
                Unpatch(m, true, true, true, true);
                return;
            }

            bool needPrefix = true;  // Always present: it owns the per call state that postfix and finalizer read.
            bool needPostfix = m.Postfixes.Length > 0;
            bool needFinalizer = m.Finalizers.Length > 0;
            bool needTranspiler = m.Redirects.Length > 0;
            string key = OrderKey(m);
            bool orderChanged = key != m.AppliedOrderKey && (m.PatchedPrefix || m.PatchedPostfix || m.PatchedFinalizer);

            if (orderChanged)
            {
                // Harmony fixes priority and ordering per patch, so a change means re-applying ours.
                Unpatch(m, true, true, true, false);
            }

            if (needPrefix && !m.PatchedPrefix)
            {
                harmony.Patch(m.Method, prefix: Patch(PrefixName(m), m));
                m.PatchedPrefix = true;
            }

            if (needPostfix && !m.PatchedPostfix)
            {
                harmony.Patch(m.Method, postfix: Patch(PostfixName(m), m));
                m.PatchedPostfix = true;
            }
            else if (!needPostfix && m.PatchedPostfix)
            {
                Unpatch(m, false, true, false, false);
            }

            if (needFinalizer && !m.PatchedFinalizer)
            {
                harmony.Patch(m.Method, finalizer: Patch(nameof(Finalizer), m));
                m.PatchedFinalizer = true;
            }
            else if (!needFinalizer && m.PatchedFinalizer)
            {
                Unpatch(m, false, false, true, false);
            }

            // The transpiler reads the redirect list while Harmony rebuilds the method, so rebuild on any change.
            if (m.PatchedTranspiler)
            {
                Unpatch(m, false, false, false, true);
            }

            if (needTranspiler)
            {
                harmony.Patch(m.Method, transpiler: new HarmonyMethod(typeof(HarmonyBridge), nameof(Transpiler)));
                m.PatchedTranspiler = true;
            }

            m.AppliedOrderKey = key;
        }

        private static void Unpatch(MethodHooks m, bool prefix, bool postfix, bool finalizer, bool transpiler)
        {
            if (prefix && m.PatchedPrefix)
            {
                harmony.Unpatch(m.Method, SharedMethod(PrefixName(m)));
                m.PatchedPrefix = false;
            }

            if (postfix && m.PatchedPostfix)
            {
                harmony.Unpatch(m.Method, SharedMethod(PostfixName(m)));
                m.PatchedPostfix = false;
            }

            if (finalizer && m.PatchedFinalizer)
            {
                harmony.Unpatch(m.Method, SharedMethod(nameof(Finalizer)));
                m.PatchedFinalizer = false;
            }

            if (transpiler && m.PatchedTranspiler)
            {
                harmony.Unpatch(m.Method, SharedMethod(nameof(Transpiler)));
                m.PatchedTranspiler = false;
            }
        }

        // ------------------------------------------------------------------ shared patches

        // Harmony picks a void or non-void variant because __result cannot be injected into a void method.

        public static bool PrefixV(MethodBase __originalMethod, object __instance, object[] __args, ref object __state)
        {
            object unused = null;
            return RunPrefix(__originalMethod, __instance, __args, ref unused, ref __state);
        }

        public static bool PrefixR(MethodBase __originalMethod, object __instance, object[] __args, ref object __result, ref object __state)
        {
            return RunPrefix(__originalMethod, __instance, __args, ref __result, ref __state);
        }

        public static void PostfixV(MethodBase __originalMethod, object __instance, object[] __args, object __state)
        {
            object unused = null;
            RunPostfix(__originalMethod, __instance, __args, ref unused, __state);
        }

        public static void PostfixR(MethodBase __originalMethod, object __instance, object[] __args, ref object __result, object __state)
        {
            RunPostfix(__originalMethod, __instance, __args, ref __result, __state);
        }

        public static Exception Finalizer(MethodBase __originalMethod, object __instance, object[] __args, Exception __exception, object __state)
        {
            if (!TryGet(__originalMethod, out MethodHooks m) || m.Finalizers.Length == 0 || nesting >= MaxNesting)
            {
                return __exception;
            }

            nesting++;
            try
            {
                bool suppress = false;
                var state = __state as HookCallState;
                foreach (HookRecord hook in m.Finalizers)
                {
                    HookResponse resp = Call(m, hook, "finalizer", __instance, __args, false, null, __exception, state);
                    if (resp != null && resp.Suppress)
                    {
                        suppress = true;
                    }

                    ApplyArgs(m, __args, resp);
                }

                return suppress ? null : __exception;
            }
            finally
            {
                nesting--;
            }
        }

        private static bool RunPrefix(MethodBase original, object instance, object[] args, ref object result, ref object stateRef)
        {
            if (!TryGet(original, out MethodHooks m) || m.Prefixes.Length == 0 || nesting >= MaxNesting)
            {
                return true;
            }

            nesting++;
            try
            {
                var state = stateRef as HookCallState;
                bool cont = true;
                foreach (HookRecord hook in m.Prefixes)
                {
                    HookResponse resp = Call(m, hook, "prefix", instance, args, m.HasResult, result, null, state ?? (state = NewState(ref stateRef)));
                    if (resp == null)
                    {
                        continue;
                    }

                    ApplyArgs(m, args, resp);
                    if (resp.HasResult && m.HasResult)
                    {
                        // A prefix that sets a result replaces the original call.
                        if (HookCodec.TryCoerce(resp.Result, m.ReturnType, out object coerced))
                        {
                            result = coerced;
                            cont = false;
                        }
                        else
                        {
                            Log.Warning("[RimKit] hook " + hook.Id + " result does not fit " + m.ReturnType.Name + " on " + m.DisplayName);
                        }
                    }

                    if (!resp.Cont)
                    {
                        cont = false;
                    }
                }

                if (!cont && m.HasResult && result == null && m.ReturnType.IsValueType)
                {
                    result = Activator.CreateInstance(m.ReturnType);
                }

                return cont;
            }
            finally
            {
                nesting--;
            }
        }

        private static void RunPostfix(MethodBase original, object instance, object[] args, ref object result, object stateObj)
        {
            if (!TryGet(original, out MethodHooks m) || m.Postfixes.Length == 0 || nesting >= MaxNesting)
            {
                return;
            }

            nesting++;
            try
            {
                var state = stateObj as HookCallState;
                foreach (HookRecord hook in m.Postfixes)
                {
                    HookResponse resp = Call(m, hook, "postfix", instance, args, m.HasResult, result, null, state);
                    if (resp == null)
                    {
                        continue;
                    }

                    ApplyArgs(m, args, resp);
                    if (resp.HasResult && m.HasResult)
                    {
                        if (HookCodec.TryCoerce(resp.Result, m.ReturnType, out object coerced) && (coerced != null || !m.ReturnType.IsValueType))
                        {
                            result = coerced;
                        }
                        else
                        {
                            Log.Warning("[RimKit] hook " + hook.Id + " result does not fit " + m.ReturnType.Name + " on " + m.DisplayName);
                        }
                    }
                }
            }
            finally
            {
                nesting--;
            }
        }

        private static HookCallState NewState(ref object stateRef)
        {
            var s = new HookCallState();
            stateRef = s;
            return s;
        }

        private static bool TryGet(MethodBase original, out MethodHooks m)
        {
            lock (Gate)
            {
                return Methods.TryGetValue(original, out m);
            }
        }

        // ------------------------------------------------------------------ context and response

        internal sealed class HookResponse
        {
            public bool Cont = true;
            public bool HasResult;
            public object Result;
            public bool Suppress;
            public Dictionary<int, object> Args = new Dictionary<int, object>();
        }

        // Times every hook call so game.hooks.list can show what a hook costs, and warns once about an expensive one.
        private static HookResponse Call(MethodHooks m, HookRecord hook, string phase, object instance, object[] args,
            bool hasResult, object result, Exception exception, HookCallState state)
        {
            long start = Stopwatch.GetTimestamp();
            HookResponse response = hook.Fast
                ? CallFast(m, hook, instance, args, hasResult, result)
                : CallJson(m, hook, phase, instance, args, hasResult, result, exception, state);
            long calls = Interlocked.Increment(ref hook.Calls);
            Interlocked.Add(ref hook.Ticks, Stopwatch.GetTimestamp() - start);
            if (calls == 2000 || calls == 20000)
            {
                double avgUs = hook.Ticks * 1e6 / Stopwatch.Frequency / calls;
                if (avgUs > 100)
                {
                    Log.Warning("[RimKit] hook " + hook.Id + " on " + m.DisplayName + " averages " + avgUs.ToString("0") + " us per call over " +
                                calls + " calls. Hooks on hot methods slow the game. Narrow it, use an event, or pass fast = true if its arguments are numbers.");
                }
            }

            return response;
        }

        // Why a hook cannot use the JSON-free path, or null when it can.
        private static string FastIneligible(MethodBase method)
        {
            foreach (ParameterInfo p in method.GetParameters())
            {
                if (p.ParameterType.IsByRef) return "parameter " + p.Name + " is ref or out";
                if (ScalarCode(p.ParameterType) < 0) return "parameter " + p.Name + " is " + p.ParameterType.Name + ", not a number or bool";
            }

            Type ret = method is MethodInfo mi ? mi.ReturnType : typeof(void);
            if (ret != typeof(void) && ScalarCode(ret) < 0) return "the method returns " + ret.Name + ", not a number or bool";
            return null;
        }

        // 0 float, 1 bool, 2 integer, -1 not a scalar.
        private static int ScalarCode(Type t)
        {
            if (t == typeof(bool)) return 1;
            if (t == typeof(float) || t == typeof(double)) return 0;
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return 2;
            return -1;
        }

        private static double ToDouble(object v)
        {
            if (v is bool b) return b ? 1.0 : 0.0;
            return v == null ? 0.0 : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        private static HookResponse CallFast(MethodHooks m, HookRecord hook, object instance, object[] args,
            bool hasResult, object result)
        {
            int n = args?.Length ?? 0;
            var values = new double[n];
            var types = new byte[n];
            for (int i = 0; i < n; i++)
            {
                values[i] = ToDouble(args[i]);
                types[i] = (byte)Math.Max(0, ScalarCode(args[i]?.GetType() ?? typeof(double)));
            }

            int resultType = hasResult ? Math.Max(0, ScalarCode(m.ReturnType)) : 0;
            int rc;
            double outResult;
            int flags;
            try
            {
                rc = NativeAbi.rimlua_invoke_hook_fast(hook.Id, ResolvePrimaryHandle(instance, args), n, values, types,
                    hasResult ? 1 : 0, hasResult ? ToDouble(result) : 0.0, (byte)resultType, out outResult, out flags);
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] hook " + hook.Id + " failed: " + e.Message);
                return null;
            }

            if (rc != 0)
            {
                return null;
            }

            var resp = new HookResponse { Cont = (flags & 2) == 0, HasResult = (flags & 1) != 0 };
            if (resp.HasResult)
            {
                resp.Result = resultType == 1 ? (object)(outResult != 0.0) : resultType == 2 ? (object)(long)outResult : outResult;
            }

            return resp;
        }

        internal static List<HookRecord> SnapshotHooks()
        {
            lock (Gate)
            {
                return Hooks.Values.OrderBy(x => x.Id).ToList();
            }
        }

        private static HookResponse CallJson(MethodHooks m, HookRecord hook, string phase, object instance, object[] args,
            bool hasResult, object result, Exception exception, HookCallState state)
        {
            string stateJson = null;
            state?.StateJson.TryGetValue(hook.Group, out stateJson);
            string ctx = BuildContext(m, phase, instance, args, hasResult, result, exception, stateJson);
            string raw;
            try
            {
                raw = NativeAbi.InvokeHookEx(hook.Id, ctx);
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] hook " + hook.Id + " failed: " + e.Message);
                return null;
            }

            if (string.IsNullOrEmpty(raw) || !Json.TryParse(raw, out object parsed) || !(parsed is Dictionary<string, object> obj))
            {
                return null;
            }

            var resp = new HookResponse
            {
                Cont = Json.GetBool(obj, "cont", true),
                HasResult = Json.GetBool(obj, "has_result", false),
                Suppress = Json.GetBool(obj, "suppress", false)
            };

            if (resp.HasResult)
            {
                obj.TryGetValue("result", out resp.Result);
            }

            if (obj.TryGetValue("args", out object argsObj) && argsObj is Dictionary<string, object> changed)
            {
                foreach (KeyValuePair<string, object> kv in changed)
                {
                    if (int.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        resp.Args[index] = kv.Value;
                    }
                }
            }

            if (state != null && obj.ContainsKey("state"))
            {
                state.StateJson[hook.Group] = HookCodec.Encode(obj["state"]);
            }

            return resp;
        }

        private static void ApplyArgs(MethodHooks m, object[] args, HookResponse resp)
        {
            if (resp == null || resp.Args.Count == 0 || args == null)
            {
                return;
            }

            foreach (KeyValuePair<int, object> kv in resp.Args)
            {
                int i = kv.Key - 1;
                if (i < 0 || i >= args.Length || i >= m.Params.Length)
                {
                    continue;
                }

                if (HookCodec.TryCoerce(kv.Value, m.Params[i].ParameterType, out object coerced) && (coerced != null || !m.Params[i].ParameterType.IsValueType))
                {
                    args[i] = coerced;
                }
                else
                {
                    Log.Warning("[RimKit] could not set argument " + kv.Key + " of " + m.DisplayName + " to the Lua value");
                }
            }
        }

        private static string BuildContext(MethodHooks m, string phase, object instance, object[] args, bool hasResult,
            object result, Exception exception, string stateJson)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"method\":");
            Json.WriteString(sb, m.DisplayName);
            sb.Append(",\"phase\":\"").Append(phase).Append('"');
            sb.Append(",\"pawn\":").Append(ResolvePrimaryHandle(instance, args).ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"instance\":");
            HookCodec.Encode(sb, instance, 0);
            sb.Append(",\"args\":[");
            AppendArgs(sb, args);
            sb.Append("],\"arg_names\":[");
            for (int i = 0; i < m.Params.Length; i++)
            {
                if (i > 0) sb.Append(',');
                Json.WriteString(sb, m.Params[i].Name);
            }

            sb.Append("],\"arg_modes\":[");
            for (int i = 0; i < m.Params.Length; i++)
            {
                if (i > 0) sb.Append(',');
                ParameterInfo p = m.Params[i];
                sb.Append(p.IsOut ? "\"out\"" : p.ParameterType.IsByRef ? "\"ref\"" : "\"in\"");
            }

            sb.Append(']');
            if (hasResult)
            {
                sb.Append(",\"has_result\":true,\"result\":");
                HookCodec.Encode(sb, result, 0);
            }
            else
            {
                sb.Append(",\"has_result\":false");
            }

            if (exception != null)
            {
                sb.Append(",\"exception\":");
                Json.WriteString(sb, exception.GetType().Name + ": " + exception.Message);
            }

            if (!string.IsNullOrEmpty(stateJson))
            {
                sb.Append(",\"state\":").Append(stateJson);
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendArgs(StringBuilder sb, object[] args)
        {
            if (args == null)
            {
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                HookCodec.Encode(sb, args[i], 0);
            }
        }

        private static int ResolvePrimaryHandle(object instance, object[] args)
        {
            if (instance is Pawn ip)
            {
                return ObjectHandles.GetOrAdd(ip);
            }

            if (args != null)
            {
                foreach (object arg in args)
                {
                    if (arg is Pawn pawn)
                    {
                        return ObjectHandles.GetOrAdd(pawn);
                    }
                }
            }

            return instance is Thing it ? ObjectHandles.GetOrAdd(it) : 0;
        }

        // ------------------------------------------------------------------ call redirect

        // Replaces calls to a chosen method inside a patched method body with a call into Lua.
        // The Lua function returns nil to run the original call, or a value to use instead.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod, ILGenerator generator)
        {
            MethodHooks m;
            lock (Gate)
            {
                if (!Methods.TryGetValue(__originalMethod, out m) || m.Redirects.Length == 0)
                {
                    foreach (CodeInstruction keep in instructions) yield return keep;
                    yield break;
                }
            }

            HookRecord[] redirects = m.Redirects;
            var seen = new Dictionary<int, int>();
            foreach (CodeInstruction ins in instructions)
            {
                HookRecord match = null;
                if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.operand is MethodBase callee)
                {
                    foreach (HookRecord r in redirects)
                    {
                        if (r.CallTarget == callee)
                        {
                            seen.TryGetValue(r.Id, out int count);
                            count++;
                            seen[r.Id] = count;
                            if (r.Nth == 0 || r.Nth == count)
                            {
                                match = r;
                            }

                            break;
                        }
                    }
                }

                if (match == null)
                {
                    yield return ins;
                    continue;
                }

                foreach (CodeInstruction emitted in EmitRedirect(match, ins, generator))
                {
                    yield return emitted;
                }
            }
        }

        private static IEnumerable<CodeInstruction> EmitRedirect(HookRecord rec, CodeInstruction original, ILGenerator il)
        {
            MethodBase callee = rec.CallTarget;
            ParameterInfo[] ps = callee.GetParameters();
            bool hasThis = !callee.IsStatic;
            var types = new List<Type>();
            if (hasThis) types.Add(callee.DeclaringType);
            types.AddRange(ps.Select(p => p.ParameterType));

            var locals = new LocalBuilder[types.Count];
            for (int i = 0; i < types.Count; i++)
            {
                locals[i] = il.DeclareLocal(types[i]);
            }

            var list = new List<CodeInstruction>();
            for (int i = types.Count - 1; i >= 0; i--)
            {
                list.Add(new CodeInstruction(OpCodes.Stloc, locals[i]));
            }

            list.Add(new CodeInstruction(OpCodes.Ldc_I4, rec.Id));
            list.Add(new CodeInstruction(OpCodes.Ldc_I4, types.Count));
            list.Add(new CodeInstruction(OpCodes.Newarr, typeof(object)));
            for (int i = 0; i < types.Count; i++)
            {
                list.Add(new CodeInstruction(OpCodes.Dup));
                list.Add(new CodeInstruction(OpCodes.Ldc_I4, i));
                list.Add(new CodeInstruction(OpCodes.Ldloc, locals[i]));
                if (types[i].IsValueType)
                {
                    list.Add(new CodeInstruction(OpCodes.Box, types[i]));
                }

                list.Add(new CodeInstruction(OpCodes.Stelem_Ref));
            }

            list.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HarmonyBridge), nameof(Trampoline))));

            Type ret = callee is MethodInfo mi ? mi.ReturnType : typeof(void);
            if (ret == typeof(void))
            {
                list.Add(new CodeInstruction(OpCodes.Pop));
            }
            else if (ret.IsValueType)
            {
                list.Add(new CodeInstruction(OpCodes.Unbox_Any, ret));
            }
            else
            {
                list.Add(new CodeInstruction(OpCodes.Castclass, ret));
            }

            // Keep jump targets and exception blocks that pointed at the original call.
            list[0].labels.AddRange(original.labels);
            list[0].blocks.AddRange(original.blocks);
            return list;
        }

        /// <summary>Entry point for rewritten calls. Returns the replacement value, or runs the original call.</summary>
        public static object Trampoline(int hookId, object[] callArgs)
        {
            HookRecord rec;
            lock (Gate)
            {
                Hooks.TryGetValue(hookId, out rec);
            }

            MethodBase callee = rec?.CallTarget;
            if (callee == null)
            {
                return null;
            }

            Type ret = callee is MethodInfo mi ? mi.ReturnType : typeof(void);
            object fallback = ret != typeof(void) && ret.IsValueType ? Activator.CreateInstance(ret) : null;
            bool hasThis = !callee.IsStatic;

            if (nesting < MaxNesting)
            {
                nesting++;
                try
                {
                    var sb = new StringBuilder(128);
                    sb.Append("{\"method\":");
                    Json.WriteString(sb, rec.TypeName + "." + rec.MethodName);
                    sb.Append(",\"phase\":\"replace_call\",\"has_result\":false,\"pawn\":0,\"instance\":");
                    if (hasThis)
                    {
                        HookCodec.Encode(sb, callArgs[0], 0);
                    }
                    else
                    {
                        sb.Append("null");
                    }

                    sb.Append(",\"args\":[");
                    for (int i = hasThis ? 1 : 0; i < callArgs.Length; i++)
                    {
                        if (i > (hasThis ? 1 : 0)) sb.Append(',');
                        HookCodec.Encode(sb, callArgs[i], 0);
                    }

                    sb.Append("]}");
                    string raw = NativeAbi.InvokeHookEx(hookId, sb.ToString());
                    if (!string.IsNullOrEmpty(raw) && Json.TryParse(raw, out object parsed) && parsed is Dictionary<string, object> obj &&
                        Json.GetBool(obj, "has_result", false))
                    {
                        obj.TryGetValue("result", out object value);
                        if (ret == typeof(void))
                        {
                            return null;
                        }

                        if (HookCodec.TryCoerce(value, ret, out object coerced) && (coerced != null || !ret.IsValueType))
                        {
                            return coerced;
                        }

                        Log.Warning("[RimKit] replace_call " + hookId + " value does not fit " + ret.Name + ", running the original call");
                    }
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] replace_call " + hookId + " failed: " + e.Message);
                }
                finally
                {
                    nesting--;
                }
            }

            // Nothing replaced the call: run it as written.
            try
            {
                object target = hasThis ? callArgs[0] : null;
                object[] rest = callArgs.Skip(hasThis ? 1 : 0).ToArray();
                object value = callee.Invoke(target, rest);
                return ret == typeof(void) ? null : value ?? fallback;
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException ?? tie;
            }
        }
    }

    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    internal static class Patch_Root_Play_Update
    {
        private static int tickCounter;
        private static bool onLoadFired;
        private static bool luaLoadAttempted;

        public static void Postfix()
        {
            if (!luaLoadAttempted)
            {
                luaLoadAttempted = true;
                try
                {
                    HostMain.TryLoadLuaModsGated();
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] gated Lua load failed: " + e);
                }
            }

            if (!onLoadFired)
            {
                onLoadFired = true;
                try
                {
                    NativeAbi.rimlua_call_on_load();
                    Log.Message("[RimKit] Host ready (wide API).");
                }
                catch (Exception e)
                {
                    Log.Error("[RimKit] on_load failed: " + e);
                }
            }

            tickCounter++;
            try
            {
                LuaEventQueue.Drain();
                NativeAbi.rimlua_call_on_tick();
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] on_tick failed: " + e);
            }
        }
    }
}
