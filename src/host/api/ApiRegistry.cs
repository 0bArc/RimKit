using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimKit
{
    internal delegate string ApiHandler(Dictionary<string, string> args);

    // Op name -> handler. Tiers: gameplay | util | advanced | denied. Every op also records the version that added it.
    internal static class ApiRegistry
    {
        private static readonly Dictionary<string, ApiHandler> Handlers =
            new Dictionary<string, ApiHandler>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Tiers =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> SinceVersions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static readonly string[] KnownTiers = { "gameplay", "util", "advanced", "denied" };

        // The version recorded for ops registered without an explicit one. Bootstrap sets it per module.
        private static string defaultSince = "0.3.0";

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
            defaultSince = "0.4.0";
            ApiEvents.Register();
            ApiReflect.Register();
            defaultSince = "0.5.0";
            ApiPawns.Register();
            ApiHooks.Register();
            ApiTime.Register();
            ApiFactions.Register();
            ApiThings.Register();
            ApiMaps.Register();
            ApiPawnHealth.Register();
            ApiJobs.Register();
            ApiGear.Register();
            ApiSocial.Register();
            ApiMind.Register();
            ApiSelection.Register();
            ApiSpawning.Register();
            ApiQuery.Register();
            ApiThingsPlus.Register();
            ApiAi.Register();
            ApiStory.Register();
            ApiQuestNodes.Register();
            ApiOdysseyDepth.Register();
            ApiNeeds.Register();
            ApiWorldMap.Register();
            ApiCombat.Register();
            ApiBuild.Register();
            ApiEconomy.Register();
            ApiWorldSys.Register();
            ApiSave.Register();
            ApiWidgets.Register();
            ApiGizmos.Register();
            ApiTabs.Register();
            ApiGraphics.Register();
            ApiEffects.Register();
            ApiInputAudio.Register();
            ApiHud.Register();
            ApiClasses.Register();
            ApiStatMods.Register();
            ApiDefsRuntime.Register();
            ApiDefsAuthor.Register();
            ApiDlc.Register();
            ApiIdeology.Register();
            ApiRoyaltyBiotech.Register();
            ApiAnomalyOdyssey.Register();
            ApiDev.Register();
            defaultSince = "0.3.0";
            try
            {
                Log.Message("[RimKit] ApiRegistry loaded " + Handlers.Count + " domain ops");
            }
            catch (Exception)
            {
                // Verse.Log needs the game engine. Unit tests run without it.
            }
        }

        public static void Register(string op, ApiHandler handler, string tier = "gameplay", string since = null)
        {
            if (string.IsNullOrEmpty(op) || handler == null) return;
            Handlers[op] = handler;
            Tiers[op] = tier ?? "gameplay";
            SinceVersions[op] = since ?? defaultSince;
        }

        /// <summary>Every registered operation with its tier and the version that added it. Used by the conformance tests.</summary>
        public static List<(string Op, string Tier, string Since)> Describe()
        {
            EnsureBootstrapped();
            return Handlers.Keys.OrderBy(k => k).Select(k => (k, Tiers[k], SinceVersions.TryGetValue(k, out string s) ? s : "")).ToList();
        }

        public static bool TryInvoke(string op, Dictionary<string, string> args, out string result)
        {
            EnsureBootstrapped();
            if (Handlers.TryGetValue(op, out ApiHandler h))
            {
                result = h(args ?? new Dictionary<string, string>());
                if (args != null && args.ContainsKey("_mod")) ModOwnership.Note(op, args, result);
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
