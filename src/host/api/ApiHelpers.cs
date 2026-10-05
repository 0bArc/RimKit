using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Verse;

namespace RimKit
{
    internal static class ApiHelpers
    {
        public static Map MapOf(Dictionary<string, string> args) => ObjectHandles.Get<Map>(Int(args, "h"));
        public static Thing ThingOf(Dictionary<string, string> args) => ObjectHandles.Get<Thing>(Int(args, "h"));
        public static Pawn PawnOf(Dictionary<string, string> args) => ObjectHandles.Get<Pawn>(Int(args, "h"));

        public static string Str(Dictionary<string, string> a, string k) =>
            a != null && a.TryGetValue(k, out string v) ? v : "";

        public static int Int(Dictionary<string, string> a, string k) =>
            int.TryParse(Str(a, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        public static float Float(Dictionary<string, string> a, string k) =>
            float.TryParse(Str(a, k), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        public static bool Bool(Dictionary<string, string> a, string k)
        {
            string s = Str(a, k);
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static string OkStr(string s) => "{\"ok\":true,\"t\":\"s\",\"v\":" + JsonLite.Quote(s ?? "") + "}";
        public static string OkInt(int v) => "{\"ok\":true,\"t\":\"i\",\"v\":" + v.ToString(CultureInfo.InvariantCulture) + "}";
        public static string OkFloat(float v) => "{\"ok\":true,\"t\":\"f\",\"v\":" + v.ToString(CultureInfo.InvariantCulture) + "}";
        public static string OkBool(bool v) => "{\"ok\":true,\"t\":\"b\",\"v\":" + (v ? "true" : "false") + "}";
        public static string OkHandle(object o) =>
            "{\"ok\":true,\"t\":\"h\",\"v\":" + ObjectHandles.GetOrAdd(o).ToString(CultureInfo.InvariantCulture) + "}";

        public static string OkHandles(System.Collections.IEnumerable list)
        {
            var sb = new StringBuilder("{\"ok\":true,\"t\":\"a\",\"v\":[");
            bool first = true;
            if (list != null)
            {
                foreach (object o in list)
                {
                    if (o == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(ObjectHandles.GetOrAdd(o).ToString(CultureInfo.InvariantCulture));
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static string OkStringList(List<string> list)
        {
            var sb = new StringBuilder("{\"ok\":true,\"t\":\"sa\",\"v\":[");
            bool first = true;
            if (list != null)
            {
                foreach (string s in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(JsonLite.Quote(s));
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static string Err(string msg) => "{\"ok\":false,\"e\":" + JsonLite.Quote(msg ?? "") + "}";

        /// <summary>Structured result. The JSON is decoded to Lua tables, with handles wrapped as RimPawn and friends.</summary>
        public static string OkJson(string json) => "{\"ok\":true,\"t\":\"j\",\"v\":" + json + "}";

        /// <summary>Error with a stable RKS code (see docs/standard/rks.md).</summary>
        public static string Fail(string code, string message) => Err(code + ": " + message);
    }
}
