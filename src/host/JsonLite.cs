using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RimKit
{
    // Minimal JSON helpers for the host op channel: flat object parsing and string quoting.
    internal static class JsonLite
    {
        public static Dictionary<string, string> ParseObject(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;
            json = json.Trim();
            if (json.Length < 2 || json[0] != '{') return dict;
            int i = 1;
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] == '}') break;
                string key = ReadString(json, ref i);
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ':') i++;
                SkipWs(json, ref i);
                string val = ReadValue(json, ref i);
                dict[key] = val;
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }
            return dict;
        }

        public static string Quote(string s)
        {
            if (s == null) s = "";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (i < s.Length && s[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        i++;
                        char esc = s[i++];
                        switch (esc)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else sb.Append(s[i++]);
                }
                if (i < s.Length && s[i] == '"') i++;
                return sb.ToString();
            }
            return ReadValue(s, ref i);
        }

        private static string ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return "";
            if (s[i] == '"') return ReadString(s, ref i);
            int start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']') i++;
            return s.Substring(start, i - start).Trim();
        }
    }
}
