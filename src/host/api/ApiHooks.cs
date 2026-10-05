using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace RimKit
{
    // hooks.stats: what each Lua hook has cost so far. game.hooks.list merges it into its rows.
    internal static class ApiHooks
    {
        public static void Register()
        {
            ApiRegistry.Register("hooks.stats", Stats, "advanced", "0.5.0");
            ApiRegistry.Register("hooks.check_target", CheckTarget, "advanced", "0.5.0");
            ApiRegistry.Register("hooks.check_events", CheckEvents, "advanced", "0.5.0");
        }

        private static string Stats(Dictionary<string, string> args)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (HookRecord h in HarmonyBridge.SnapshotHooks())
            {
                long calls = h.Calls;
                double totalUs = h.Ticks * 1e6 / Stopwatch.Frequency;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(h.Id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"calls\":").Append(calls.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"total_us\":").Append(totalUs.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(",\"avg_us\":").Append((calls == 0 ? 0 : totalUs / calls).ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(",\"fast\":").Append(h.Fast ? "true" : "false").Append('}');
            }

            sb.Append(']');
            return ApiHelpers.OkJson(sb.ToString());
        }

        // Game-update watch: does a hook target still resolve? args: type, method, sig (comma separated type names).
        private static string CheckTarget(Dictionary<string, string> args)
        {
            args.TryGetValue("type", out string typeName);
            args.TryGetValue("method", out string methodName);
            args.TryGetValue("sig", out string sig);
            Type type = string.IsNullOrEmpty(typeName) ? null : AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return ApiHelpers.OkJson("{\"found\":false,\"problem\":\"type not found\"}");
            }

            var list = string.IsNullOrWhiteSpace(sig)
                ? new List<string>()
                : sig.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var method = HarmonyBridge.FindMethod(type, methodName ?? "", list, out string problem);
            return ApiHelpers.OkJson(method != null
                ? "{\"found\":true}"
                : "{\"found\":false,\"problem\":" + JsonLite.Quote(problem ?? "method not found") + "}");
        }

        // Every catalog event: does its patch target still exist in this game build?
        private static string CheckEvents(Dictionary<string, string> args)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (EventCatalog.EventDef e in EventCatalog.List())
            {
                string problem = null;
                bool ok = false;
                try
                {
                    ok = e.Target == null || e.Target() != null;
                    if (!ok) problem = "target resolved to null";
                }
                catch (Exception ex)
                {
                    problem = ex.GetType().Name + ": " + ex.Message;
                }

                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"event\":").Append(JsonLite.Quote(e.Name)).Append(",\"ok\":").Append(ok ? "true" : "false");
                if (problem != null) sb.Append(",\"problem\":").Append(JsonLite.Quote(problem));
                sb.Append('}');
            }

            sb.Append(']');
            return ApiHelpers.OkJson(sb.ToString());
        }
    }
}
