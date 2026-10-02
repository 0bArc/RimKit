using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimLuaKit
{
    internal delegate string ApiHandler(Dictionary<string, string> args);

    // Op name -> handler. Tiers: gameplay | util | denied.
    internal static class ApiRegistry
    {
        private static readonly Dictionary<string, ApiHandler> Handlers =
            new Dictionary<string, ApiHandler>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Tiers =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool bootstrapped;

        public static void EnsureBootstrapped()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            ApiData.Register();
            ApiDefsPlus.Register();
            ApiPawnPlus.Register();
            ApiHealth.Register();
            ApiUiPlus.Register();
            ApiUtil.Register();
            ApiBuildings.Register();
            ApiWork.Register();
            ApiInventory.Register();
            ApiAnomaly.Register();
            ApiWorld.Register();
            ApiAudio.Register();
            ApiMapQuery.Register();
            Log.Message("[RimKit] ApiRegistry loaded " + Handlers.Count + " domain ops");
        }

        public static void Register(string op, ApiHandler handler, string tier = "gameplay")
        {
            if (string.IsNullOrEmpty(op) || handler == null) return;
            Handlers[op] = handler;
            Tiers[op] = tier ?? "gameplay";
        }

        public static bool TryInvoke(string op, Dictionary<string, string> args, out string result)
        {
            EnsureBootstrapped();
            if (Handlers.TryGetValue(op, out ApiHandler h))
            {
                result = h(args ?? new Dictionary<string, string>());
                return true;
            }
            result = null;
            return false;
        }

        public static List<string> ListOps()
        {
            EnsureBootstrapped();
            return Handlers.Keys.OrderBy(k => k).ToList();
        }

        public static string TierOf(string op) =>
            Tiers.TryGetValue(op, out string t) ? t : "legacy";
    }
}
