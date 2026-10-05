using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RimKit
{
    // Writes the plain values the JSON reader produces (dictionaries, lists, strings, numbers, booleans) back to text.
    internal static class JsonOut
    {
        public static string Of(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v, int depth)
        {
            if (depth > 24) { sb.Append("null"); return; }
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: sb.Append(JsonLite.Quote(s)); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(double.IsNaN(f) || double.IsInfinity(f) ? "0" : f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString("R", CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append(JsonLite.Quote(kv.Key)).Append(':');
                        Write(sb, kv.Value, depth + 1);
                    }

                    sb.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item, depth + 1);
                    }

                    sb.Append(']');
                    return;
                }
                default:
                    sb.Append(JsonLite.Quote(v.ToString()));
                    return;
            }
        }
    }
}
