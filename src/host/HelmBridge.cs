using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimKit
{
    // The RimKit side of Helm (https://helm, docs in helm/docs): lets a Helm client call every op, run Lua, step time and watch events.
    // Helm is a separate product. This file is the whole adapter: it loads the Helm library and answers its requests on the main thread.
    //
    // Off unless the launcher set HELM_ENDPOINT and HELM_TOKEN, and only after pAuth said the RimKit binaries are the released ones
    // and the Helm library itself is on the allowlist. A normal game never opens a channel.
    internal static class HelmBridge
    {
        private const int AbiVersion = 1;

        public static bool Active { get; private set; }

        private static bool tried;
        private static string endpoint;

        // ---- the Helm C ABI ----

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ReplyFn(IntPtr ctx, IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void InvokeFn(IntPtr user, IntPtr op, IntPtr args, IntPtr reply, IntPtr ctx);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void EvalFn(IntPtr user, IntPtr lang, IntPtr code, IntPtr reply, IntPtr ctx);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void StepFn(IntPtr user, int ticks, IntPtr reply, IntPtr ctx);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void NoArgFn(IntPtr user, IntPtr reply, IntPtr ctx);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void WatchFn(IntPtr user, IntPtr pattern, int on);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ShutdownFn(IntPtr user, int code);

        [StructLayout(LayoutKind.Sequential)]
        private struct Adapter
        {
            public int abi_version;
            public IntPtr user;
            public IntPtr invoke, eval, step, info, ops, watch, shutdown;
        }

        [DllImport("helm", CallingConvention = CallingConvention.Cdecl)] private static extern int helm_start([MarshalAs(UnmanagedType.LPStr)] string endpoint, [MarshalAs(UnmanagedType.LPStr)] string token, ref Adapter adapter);
        [DllImport("helm", CallingConvention = CallingConvention.Cdecl)] private static extern int helm_pump();
        [DllImport("helm", CallingConvention = CallingConvention.Cdecl)] private static extern void helm_publish_event(byte[] name, byte[] payload);
        [DllImport("helm", CallingConvention = CallingConvention.Cdecl)] private static extern void helm_stop();
        [DllImport("helm", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr helm_last_error();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string path);
        [DllImport("libdl.so.2", EntryPoint = "dlopen")] private static extern IntPtr dlopen(string path, int flags);

        // Kept alive for as long as the library may call them.
        private static readonly List<Delegate> keep = new List<Delegate>();

        // ---- start and pump ----

        public static void TryStart()
        {
            if (tried) return;
            tried = true;
            endpoint = Environment.GetEnvironmentVariable("HELM_ENDPOINT");
            string token = Environment.GetEnvironmentVariable("HELM_TOKEN");
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(token)) return;   // a normal game: nothing opens

            try
            {
                if (!AuthGate.IsAuthorized())
                {
                    Log.Error("[RimKit/helm] refused: pAuth did not authorize this RimKit build (" + AuthGate.FailureReason() + ")");
                    return;
                }

                string library = LibraryPath();
                if (!AuthGate.VerifyHelmLibrary(library, out string why))
                {
                    Log.Error("[RimKit/helm] refused: " + why);
                    return;
                }

                IntPtr handle = Environment.OSVersion.Platform == PlatformID.Win32NT ? LoadLibraryW(library) : dlopen(library, 2);
                if (handle == IntPtr.Zero)
                {
                    Log.Error("[RimKit/helm] could not load " + library);
                    return;
                }

                var adapter = new Adapter { abi_version = AbiVersion, user = IntPtr.Zero };
                adapter.invoke = Pin(new InvokeFn(OnInvoke));
                adapter.eval = Pin(new EvalFn(OnEval));
                adapter.step = Pin(new StepFn(OnStep));
                adapter.info = Pin(new NoArgFn(OnInfo));
                adapter.ops = Pin(new NoArgFn(OnOps));
                adapter.watch = Pin(new WatchFn(OnWatch));
                adapter.shutdown = Pin(new ShutdownFn(OnShutdown));
                int rc = helm_start(endpoint, token, ref adapter);
                if (rc != 0)
                {
                    Log.Error("[RimKit/helm] could not start: " + Text(helm_last_error()) + " (code " + rc + ")");
                    return;
                }

                Active = true;
                Log.Message("[RimKit/helm] listening on " + endpoint + " (batch mode: " + Application.isBatchMode + "). Every command is logged.");
            }
            catch (Exception e)
            {
                Log.Error("[RimKit/helm] failed to start: " + e);
            }
        }

        public static void Pump()
        {
            if (!Active) return;
            try { helm_pump(); }
            catch (Exception e) { Log.Error("[RimKit/helm] pump failed: " + e.Message); }
        }

        public static void Shutdown()
        {
            if (!Active) return;
            Active = false;
            try { helm_stop(); } catch { }
        }

        /// <summary>Sends an event to the clients that asked for it. Used by game.dev.helm_publish.</summary>
        public static bool Publish(string name, string payloadJson)
        {
            if (!Active) return false;
            helm_publish_event(Utf8(name), Utf8(payloadJson ?? "null"));
            return true;
        }

        private static string LibraryPath()
        {
            string assemblies = NativeAbi.ResolveDllDirectory();
            string root = Directory.GetParent(assemblies ?? ".")?.FullName ?? ".";
            string file = Environment.OSVersion.Platform == PlatformID.Win32NT ? "helm.dll" : "libhelm.so";
            return Path.Combine(root, "Native", file);
        }

        private static IntPtr Pin(Delegate d)
        {
            keep.Add(d);
            return Marshal.GetFunctionPointerForDelegate(d);
        }

        // ---- text across the boundary ----

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes((s ?? "") + "\0");

        private static string Text(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            int n = 0;
            while (Marshal.ReadByte(p, n) != 0) n++;
            var bytes = new byte[n];
            Marshal.Copy(p, bytes, 0, n);
            return Encoding.UTF8.GetString(bytes);
        }

        private static void Reply(IntPtr reply, IntPtr ctx, string text)
        {
            ReplyFn fn = (ReplyFn)Marshal.GetDelegateForFunctionPointer(reply, typeof(ReplyFn));
            if (text == null) { fn(ctx, IntPtr.Zero); return; }
            byte[] bytes = Utf8(text);
            GCHandle h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { fn(ctx, h.AddrOfPinnedObject()); }
            finally { h.Free(); }
        }

        private static string Q(string s) => JsonLite.Quote(s ?? "");

        // With a window (helm launch --gui) each action Helm runs is shown on screen, so a person can follow it.
        private static void Show(string text)
        {
            if (Application.isBatchMode) return;
            try
            {
                if (Current.ProgramState == ProgramState.Playing)
                    Messages.Message("Helm: " + (text.Length > 90 ? text.Substring(0, 90) + "..." : text), MessageTypeDefOf.SilentInput, false);
            }
            catch (Exception) { }
        }

        // ---- the adapter ----

        private static void OnInvoke(IntPtr user, IntPtr op, IntPtr args, IntPtr reply, IntPtr ctx)
        {
            string name = Text(op);
            string result;
            try
            {
                Log.Message("[RimKit/helm] call " + name);
                Show("call " + name);
                result = name == "batch.run" ? RunBatch(Text(args)) : GameApi.Invoke(name, FlatArgs(Text(args)));
            }
            catch (Exception e) { result = "{\"ok\":false,\"e\":" + Q("RK5000: " + e.Message) + "}"; }
            Reply(reply, ctx, result);
        }

        // batch.run { "calls": [ { "op": "...", "args": {...} }, ... ], "stop_on_error": false }
        // Runs every call in this one frame, so a blueprint with hundreds of cells costs one round trip. Answer:
        // { "ok": true, "t": "j", "v": { "n": calls run, "failed": count, "errors": [ { "i": index, "op": name, "e": text } ] } }
        private static string RunBatch(string argsJson)
        {
            if (!Json.TryParse(argsJson, out object parsed) || !(parsed is Dictionary<string, object> root))
                return "{\"ok\":false,\"e\":\"RK1001: batch.run needs a JSON object\"}";
            var calls = Json.AsArray(root.TryGetValue("calls", out object c) ? c : null);
            if (calls == null) return "{\"ok\":false,\"e\":\"RK1001: batch.run needs a calls array\"}";
            bool stop = Json.GetBool(root, "stop_on_error", false);
            int ran = 0, failed = 0;
            var errors = new StringBuilder();
            for (int i = 0; i < calls.Count; i++)
            {
                var call = Json.AsObject(calls[i]);
                string op = call == null ? null : Json.GetString(call, "op");
                if (string.IsNullOrEmpty(op)) { failed++; AddError(errors, i, "", "RK1001: call has no op"); if (stop) break; continue; }
                ran++;
                string answer;
                try
                {
                    var sb = new StringBuilder();
                    WriteFlat(sb, call.TryGetValue("args", out object a) ? a as Dictionary<string, object> : null);
                    answer = GameApi.Invoke(op, sb.ToString());
                }
                catch (Exception e) { answer = "{\"ok\":false,\"e\":" + Q("RK5000: " + e.Message) + "}"; }
                if (answer != null && answer.StartsWith("{\"ok\":false"))
                {
                    failed++;
                    AddError(errors, i, op, ErrorText(answer));
                    if (stop) break;
                }
            }

            return "{\"ok\":true,\"t\":\"j\",\"v\":{\"n\":" + ran + ",\"failed\":" + failed + ",\"errors\":[" + errors + "]}}";
        }

        private static void AddError(StringBuilder errors, int index, string op, string text)
        {
            if (errors.Length > 0) errors.Append(',');
            if (errors.Length > 20000) return;
            errors.Append("{\"i\":").Append(index).Append(",\"op\":").Append(Q(op)).Append(",\"e\":").Append(Q(text)).Append('}');
        }

        private static string ErrorText(string answer)
        {
            return Json.TryParse(answer, out object o) && o is Dictionary<string, object> d ? Json.GetString(d, "e", "failed") : "failed";
        }

        // Ops read flat arguments. A table or list argument (opts, cells) travels as a JSON string, as it does from Lua.
        private static string FlatArgs(string argsJson)
        {
            if (string.IsNullOrWhiteSpace(argsJson) || !Json.TryParse(argsJson, out object parsed) || !(parsed is Dictionary<string, object> d)) return argsJson;
            var sb = new StringBuilder();
            WriteFlat(sb, d);
            return sb.ToString();
        }

        private static void WriteFlat(StringBuilder sb, Dictionary<string, object> d)
        {
            sb.Append('{');
            bool first = true;
            if (d != null)
            {
                foreach (var kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Json.WriteString(sb, kv.Key);
                    sb.Append(':');
                    if (kv.Value is Dictionary<string, object> || kv.Value is List<object>)
                    {
                        var inner = new StringBuilder();
                        WriteJson(inner, kv.Value);
                        Json.WriteString(sb, inner.ToString());
                    }
                    else WriteJson(sb, kv.Value);
                }
            }

            sb.Append('}');
        }

        private static void WriteJson(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: Json.WriteString(sb, s); break;
                case IFormattable f: sb.Append(f.ToString(null, System.Globalization.CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> d:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in d)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Json.WriteString(sb, kv.Key);
                        sb.Append(':');
                        WriteJson(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case List<object> l:
                    sb.Append('[');
                    for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(','); WriteJson(sb, l[i]); }
                    sb.Append(']');
                    break;
                default: Json.WriteString(sb, v.ToString()); break;
            }
        }

        private static void OnEval(IntPtr user, IntPtr lang, IntPtr code, IntPtr reply, IntPtr ctx)
        {
            string language = Text(lang);
            if (!string.Equals(language, "lua", StringComparison.OrdinalIgnoreCase) && !string.Equals(language, "luau", StringComparison.OrdinalIgnoreCase))
            {
                Reply(reply, ctx, "error: RimKit evaluates Lua only, got '" + language + "'");
                return;
            }

            string source = Text(code);
            Log.Message("[RimKit/helm] eval " + (source.Length > 120 ? source.Substring(0, 120) + "..." : source));
            Show("eval " + source);
            string result;
            try { result = Text(NativeAbi.rimlua_eval_raw(source)); }
            catch (Exception e) { result = "error: " + e.Message; }
            Reply(reply, ctx, result);
        }

        private static void OnStep(IntPtr user, int ticks, IntPtr reply, IntPtr ctx)
        {
            string result;
            try
            {
                Log.Message("[RimKit/helm] step " + ticks);
                Show("step " + ticks + " ticks");
                result = ApiTime.StepTicks(ticks);
            }
            catch (Exception e) { result = "{\"ok\":false,\"e\":" + Q("RK5000: " + e.Message) + "}"; }
            Reply(reply, ctx, result);
        }

        private static void OnInfo(IntPtr user, IntPtr reply, IntPtr ctx)
        {
            var sb = new StringBuilder("{\"game\":\"RimWorld\",\"adapter\":\"rimkit\"");
            try
            {
                sb.Append(",\"version\":").Append(Q(VersionControl.CurrentVersionString));
                sb.Append(",\"rimkit\":").Append(Q(RimKitVersion.Version));
                sb.Append(",\"batch\":").Append(Application.isBatchMode ? "true" : "false");
                sb.Append(",\"platform\":").Append(Q(Application.platform.ToString()));
                bool playing = Current.ProgramState == ProgramState.Playing && Find.TickManager != null;
                sb.Append(",\"playing\":").Append(playing ? "true" : "false");
                sb.Append(",\"tick\":").Append(playing ? Find.TickManager.TicksGame : -1);
                sb.Append(",\"paused\":").Append(playing && Find.TickManager.Paused ? "true" : "false");
            }
            catch (Exception e) { sb.Append(",\"error\":").Append(Q(e.Message)); }
            sb.Append('}');
            Reply(reply, ctx, sb.ToString());
        }

        private static void OnOps(IntPtr user, IntPtr reply, IntPtr ctx)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var (op, tier, since) in ApiRegistry.Describe())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"op\":").Append(Q(op)).Append(",\"tier\":").Append(Q(tier)).Append(",\"since\":").Append(Q(since)).Append('}');
            }

            if (!first) sb.Append(',');
            sb.Append("{\"op\":\"batch.run\",\"tier\":\"advanced\",\"since\":\"0.11.0\"}");
            sb.Append(']');
            Reply(reply, ctx, sb.ToString());
        }

        private static void OnWatch(IntPtr user, IntPtr pattern, int on)
        {
            try { NativeAbi.rimlua_eval_raw("game.dev.helm_watch(" + LuaQuote(Text(pattern)) + ", " + (on != 0 ? "true" : "false") + ")"); }
            catch (Exception e) { Log.Error("[RimKit/helm] watch failed: " + e.Message); }
        }

        private static void OnShutdown(IntPtr user, int code)
        {
            Log.Message("[RimKit/helm] shutdown requested, exit code " + code);
            Application.Quit(code);
        }

        private static string LuaQuote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append(' ');
                else sb.Append(c);
            }

            return sb.Append('"').ToString();
        }
    }
}
