using System.Collections.Generic;
using static RimLuaKit.ApiHelpers;

namespace RimLuaKit
{
    internal static class ApiData
    {
        public static void Register()
        {
            ApiRegistry.Register("data.get", Get, "gameplay");
            ApiRegistry.Register("data.set", Set, "gameplay");
            ApiRegistry.Register("data.remove", Remove, "gameplay");
            ApiRegistry.Register("data.keys", Keys, "gameplay");
            ApiRegistry.Register("api.list", ListAll, "gameplay");
        }

        private static string KeyOf(Dictionary<string, string> args)
        {
            string pack = Str(args, "package_id");
            string key = Str(args, "key");
            if (string.IsNullOrEmpty(pack) || string.IsNullOrEmpty(key)) return null;
            return pack + "::" + key;
        }

        private static string Get(Dictionary<string, string> args)
        {
            string k = KeyOf(args);
            if (k == null) return Err("package_id and key required");
            var c = RimLuaModDataComponent.GetOrCreate();
            if (c?.Values == null) return OkStr("");
            return OkStr(c.Values.TryGetValue(k, out string v) ? v : "");
        }

        private static string Set(Dictionary<string, string> args)
        {
            string k = KeyOf(args);
            if (k == null) return Err("package_id and key required");
            var c = RimLuaModDataComponent.GetOrCreate();
            if (c == null) return Err("no game");
            c.Values[k] = Str(args, "value");
            return OkBool(true);
        }

        private static string Remove(Dictionary<string, string> args)
        {
            string k = KeyOf(args);
            if (k == null) return Err("package_id and key required");
            var c = RimLuaModDataComponent.GetOrCreate();
            if (c?.Values == null) return OkBool(false);
            return OkBool(c.Values.Remove(k));
        }

        private static string Keys(Dictionary<string, string> args)
        {
            string pack = Str(args, "package_id") + "::";
            var c = RimLuaModDataComponent.GetOrCreate();
            var list = new List<string>();
            if (c?.Values != null)
            {
                foreach (string k in c.Values.Keys)
                {
                    if (k.StartsWith(pack)) list.Add(k.Substring(pack.Length));
                }
            }
            return OkStringList(list);
        }

        private static string ListAll(Dictionary<string, string> args) => OkStringList(ApiRegistry.ListOps());
    }
}
