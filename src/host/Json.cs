using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RimKit
{
    /// <summary>
    /// Strict JSON reader that builds a plain object tree: Dictionary&lt;string, object&gt;, List&lt;object&gt;,
    /// string, long, double, bool or null. Used by the hook and event channels, where values nest.
    /// </summary>
    internal static class Json
    {
        private const int MaxDepth = 64;

        public static bool TryParse(string text, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int p = 0;
            try
            {
                if (!ReadValue(text, ref p, out value, 0))
                {
                    value = null;
                    return false;
                }

                SkipWs(text, ref p);
                if (p != text.Length)
                {
                    value = null;
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        public static Dictionary<string, object> AsObject(object o) => o as Dictionary<string, object>;

        public static List<object> AsArray(object o) => o as List<object>;

        public static string GetString(Dictionary<string, object> obj, string key, string fallback = null)
        {
            return obj != null && obj.TryGetValue(key, out object v) && v is string s ? s : fallback;
        }

        public static long GetLong(Dictionary<string, object> obj, string key, long fallback = 0)
        {
            if (obj == null || !obj.TryGetValue(key, out object v))
            {
                return fallback;
            }

            if (v is long l) return l;
            if (v is double d) return (long)d;
            return fallback;
        }

        public static bool GetBool(Dictionary<string, object> obj, string key, bool fallback = false)
        {
            return obj != null && obj.TryGetValue(key, out object v) && v is bool b ? b : fallback;
        }

        public static List<string> GetStringList(Dictionary<string, object> obj, string key)
        {
            var result = new List<string>();
            if (obj != null && obj.TryGetValue(key, out object v) && v is List<object> list)
            {
                foreach (object item in list)
                {
                    if (item is string s)
                    {
                        result.Add(s);
                    }
                }
            }

            return result;
        }

        private static void SkipWs(string t, ref int p)
        {
            while (p < t.Length && (t[p] == ' ' || t[p] == '\t' || t[p] == '\n' || t[p] == '\r'))
            {
                p++;
            }
        }

        private static bool ReadValue(string t, ref int p, out object value, int depth)
        {
            value = null;
            if (depth > MaxDepth)
            {
                return false;
            }

            SkipWs(t, ref p);
            if (p >= t.Length)
            {
                return false;
            }

            char c = t[p];
            if (c == '{')
            {
                p++;
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWs(t, ref p);
                if (p < t.Length && t[p] == '}')
                {
                    p++;
                    value = obj;
                    return true;
                }

                while (true)
                {
                    SkipWs(t, ref p);
                    if (!ReadString(t, ref p, out string key))
                    {
                        return false;
                    }

                    SkipWs(t, ref p);
                    if (p >= t.Length || t[p] != ':')
                    {
                        return false;
                    }

                    p++;
                    if (!ReadValue(t, ref p, out object item, depth + 1))
                    {
                        return false;
                    }

                    obj[key] = item;
                    SkipWs(t, ref p);
                    if (p < t.Length && t[p] == ',')
                    {
                        p++;
                        continue;
                    }

                    if (p < t.Length && t[p] == '}')
                    {
                        p++;
                        value = obj;
                        return true;
                    }

                    return false;
                }
            }

            if (c == '[')
            {
                p++;
                var list = new List<object>();
                SkipWs(t, ref p);
                if (p < t.Length && t[p] == ']')
                {
                    p++;
                    value = list;
                    return true;
                }

                while (true)
                {
                    if (!ReadValue(t, ref p, out object item, depth + 1))
                    {
                        return false;
                    }

                    list.Add(item);
                    SkipWs(t, ref p);
                    if (p < t.Length && t[p] == ',')
                    {
                        p++;
                        continue;
                    }

                    if (p < t.Length && t[p] == ']')
                    {
                        p++;
                        value = list;
                        return true;
                    }

                    return false;
                }
            }

            if (c == '"')
            {
                if (!ReadString(t, ref p, out string s))
                {
                    return false;
                }

                value = s;
                return true;
            }

            if (string.CompareOrdinal(t, p, "true", 0, 4) == 0)
            {
                p += 4;
                value = true;
                return true;
            }

            if (string.CompareOrdinal(t, p, "false", 0, 5) == 0)
            {
                p += 5;
                value = false;
                return true;
            }

            if (string.CompareOrdinal(t, p, "null", 0, 4) == 0)
            {
                p += 4;
                value = null;
                return true;
            }

            if (c == '-' || (c >= '0' && c <= '9'))
            {
                int start = p;
                bool isFloat = false;
                if (t[p] == '-') p++;
                while (p < t.Length && ((t[p] >= '0' && t[p] <= '9') || t[p] == '.' || t[p] == 'e' || t[p] == 'E' || t[p] == '+' || t[p] == '-'))
                {
                    if (t[p] == '.' || t[p] == 'e' || t[p] == 'E') isFloat = true;
                    p++;
                }

                string num = t.Substring(start, p - start);
                if (isFloat)
                {
                    if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return false;
                    value = d;
                    return true;
                }

                if (!long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return false;
                value = l;
                return true;
            }

            return false;
        }

        private static bool ReadString(string t, ref int p, out string s)
        {
            s = null;
            if (p >= t.Length || t[p] != '"')
            {
                return false;
            }

            p++;
            var sb = new StringBuilder();
            while (p < t.Length)
            {
                char c = t[p++];
                if (c == '"')
                {
                    s = sb.ToString();
                    return true;
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (p >= t.Length)
                {
                    return false;
                }

                char e = t[p++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (p + 4 > t.Length || !int.TryParse(t.Substring(p, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                        {
                            return false;
                        }

                        sb.Append((char)cp);
                        p += 4;
                        break;
                    default:
                        return false;
                }
            }

            return false;
        }

        /// <summary>Writes a JSON string literal with full escaping (control characters included).</summary>
        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < 0x20)
                            {
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
            }

            sb.Append('"');
        }
    }
}
