using System;
using Verse;

namespace RimKit
{
    // Calls Lua functions that a kit function received as an argument (a "function" key). The native layer registers the
    // function and gives the host an id. Always call from the main thread, and never while holding the host Gate.
    internal static class LuaCallbacks
    {
        public static object Call(int id, string argJson = null)
        {
            if (id <= 0) return null;
            try
            {
                string text = NativeAbi.UiCall(id, argJson);
                if (string.IsNullOrEmpty(text)) return null;
                return Json.TryParse(text, out object v) ? v : null;
            }
            catch (Exception e)
            {
                Log.Error("[RimKit] Lua callback " + id + " failed: " + e.Message);
                return null;
            }
        }

        public static void Fire(int id, string argJson = null) => Call(id, argJson);

        public static bool CallBool(int id, string argJson, bool fallback)
        {
            object v = Call(id, argJson);
            return v is bool b ? b : fallback;
        }

        public static string CallString(int id, string argJson, string fallback = null)
        {
            object v = Call(id, argJson);
            return v is string s ? s : fallback;
        }

        public static double CallNumber(int id, string argJson, double fallback)
        {
            object v = Call(id, argJson);
            if (v is double d) return d;
            if (v is long l) return l;
            return fallback;
        }

        public static string Arg(object o) => HookCodec.Encode(o);
    }
}
