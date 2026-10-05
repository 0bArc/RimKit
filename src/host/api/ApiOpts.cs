using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RimKit
{
    // Reads a table argument that the native layer sent as a JSON string (a kit key typed "table").
    // Wrapped game objects inside it arrive as {"$h": handle}.
    internal sealed class Opts
    {
        private readonly Dictionary<string, object> d;

        private Opts(Dictionary<string, object> d)
        {
            this.d = d ?? new Dictionary<string, object>();
        }

        public static Opts From(Dictionary<string, string> args, string key)
        {
            if (args != null && args.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text) &&
                Json.TryParse(text, out object v))
            {
                return new Opts(Json.AsObject(v));
            }

            return new Opts(null);
        }

        public bool Has(string key) => d.ContainsKey(key) && d[key] != null;

        public string Str(string key, string fallback = null) => Json.GetString(d, key, fallback);

        public int Int(string key, int fallback = 0) => (int)Json.GetLong(d, key, fallback);

        public double Num(string key, double fallback = 0)
        {
            if (d.TryGetValue(key, out object v))
            {
                if (v is double x) return x;
                if (v is long l) return l;
            }

            return fallback;
        }

        public bool Bool(string key, bool fallback = false) => Json.GetBool(d, key, fallback);

        public List<string> Strings(string key) => Json.GetStringList(d, key);

        public T Handle<T>(string key) where T : class
        {
            if (d.TryGetValue(key, out object v))
            {
                return HandleOf<T>(v);
            }

            return null;
        }

        public List<T> Handles<T>(string key) where T : class
        {
            var list = new List<T>();
            if (d.TryGetValue(key, out object v) && v is List<object> items)
            {
                foreach (object item in items)
                {
                    T t = HandleOf<T>(item);
                    if (t != null) list.Add(t);
                }
            }

            return list;
        }

        public Dictionary<string, object> Obj(string key) => d.TryGetValue(key, out object v) ? Json.AsObject(v) : null;

        public List<object> Array(string key) => d.TryGetValue(key, out object v) ? Json.AsArray(v) : null;

        public static T HandleOf<T>(object v) where T : class
        {
            var o = Json.AsObject(v);
            if (o != null && o.ContainsKey("$h"))
            {
                return ObjectHandles.Get<T>((int)Json.GetLong(o, "$h"));
            }

            return null;
        }
    }

    // Small JSON writer for kit results.
    internal sealed class Jb
    {
        private readonly StringBuilder sb = new StringBuilder();
        private readonly bool array;
        private bool first = true;

        private Jb(bool array)
        {
            this.array = array;
            sb.Append(array ? '[' : '{');
        }

        public static Jb Obj() => new Jb(false);

        public static Jb Arr() => new Jb(true);

        private void Key(string key)
        {
            if (!first) sb.Append(',');
            first = false;
            if (!array) sb.Append(JsonLite.Quote(key)).Append(':');
        }

        public Jb S(string key, string v)
        {
            if (v == null) return this;
            Key(key);
            sb.Append(JsonLite.Quote(v));
            return this;
        }

        public Jb I(string key, long v)
        {
            Key(key);
            sb.Append(v.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public Jb F(string key, double v)
        {
            Key(key);
            sb.Append(double.IsNaN(v) || double.IsInfinity(v) ? "0" : v.ToString("0.####", CultureInfo.InvariantCulture));
            return this;
        }

        public Jb B(string key, bool v)
        {
            Key(key);
            sb.Append(v ? "true" : "false");
            return this;
        }

        // A game object becomes a handle the native layer wraps as RimPawn, RimThing and so on. Null leaves the key out.
        public Jb H(string key, object o)
        {
            if (o == null) return this;
            Key(key);
            sb.Append(HookCodec.Encode(o));
            return this;
        }

        public Jb Raw(string key, string json)
        {
            Key(key);
            sb.Append(json ?? "null");
            return this;
        }

        // Array items. The key argument is ignored.
        public Jb Add(Jb child)
        {
            Key("");
            sb.Append(child);
            return this;
        }

        public Jb AddS(string v)
        {
            Key("");
            sb.Append(JsonLite.Quote(v ?? ""));
            return this;
        }

        public Jb AddH(object o)
        {
            Key("");
            sb.Append(HookCodec.Encode(o));
            return this;
        }

        public override string ToString() => sb.ToString() + (array ? "]" : "}");

        public string Ok() => ApiHelpers.OkJson(ToString());
    }
}
