using System.Collections.Generic;

namespace RimKit
{
    // events.subscribe / events.unsubscribe / events.list / events.stats. Lua calls these from events.on and events.off.
    internal static class ApiEvents
    {
        public static void Register()
        {
            ApiRegistry.Register("events.subscribe", Subscribe);
            ApiRegistry.Register("events.unsubscribe", Unsubscribe);
            ApiRegistry.Register("events.list", List);
            ApiRegistry.Register("events.stats", Stats);
        }

        private static string Subscribe(Dictionary<string, string> args)
        {
            string name = ApiHelpers.Str(args, "name");
            string error = EventCatalog.Subscribe(name);
            return error == null ? ApiHelpers.OkBool(true) : ApiHelpers.Err(error);
        }

        private static string Unsubscribe(Dictionary<string, string> args)
        {
            EventCatalog.Unsubscribe(ApiHelpers.Str(args, "name"));
            return ApiHelpers.OkBool(true);
        }

        private static string List(Dictionary<string, string> args)
        {
            return "{\"ok\":true,\"t\":\"j\",\"v\":" + EventCatalog.ListJson() + "}";
        }

        private static string Stats(Dictionary<string, string> args)
        {
            return "{\"ok\":true,\"t\":\"j\",\"v\":{\"pending\":" + LuaEventQueue.PendingCount +
                   ",\"dropped\":" + LuaEventQueue.Dropped + "}}";
        }
    }
}
