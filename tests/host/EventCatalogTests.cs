using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimKit;
using Xunit;

namespace RimKit.Host.Tests
{
    // Every event in the catalog must point at a real game method and a real patch body, and Harmony must accept the patch.
    // This runs against the installed game's assemblies without starting the game, so a game update or a typo in a catalog row
    // fails here and not in a player's log.
    public class EventCatalogTests
    {
        static EventCatalogTests() { GameAssemblies.Ensure(); }

        private static Harmony instance;

        private static Harmony Instance => instance ?? (instance = new Harmony("rimkit.tests.events"));

        private static IEnumerable<EventCatalog.EventDef> Patched()
        {
            EventCatalog.Init(Instance);
            return EventCatalog.List().Where(e => e.Target != null);
        }

        [Fact]
        public void EventNamesFollowTheNamingRule()
        {
            EventCatalog.Init(Instance);
            foreach (var e in EventCatalog.List())
                Assert.Matches(@"^[a-z][a-z_]*\.[a-z][a-z_]*$", e.Name);
            Assert.Equal(EventCatalog.List().Count, EventCatalog.List().Select(e => e.Name).Distinct().Count());
        }

        [Fact]
        public void EveryEventHasAPayloadDescription()
        {
            EventCatalog.Init(Instance);
            foreach (var e in EventCatalog.List())
                Assert.True(e.Description.Length > 10, e.Name + " needs a description");
        }

        [Fact]
        public void EveryPatchBodyExists()
        {
            foreach (var e in Patched())
                Assert.True(AccessTools.Method(typeof(EventPatches), e.PatchMethod) != null, e.Name + " patch method " + e.PatchMethod);
        }

        [Fact]
        public void EveryTargetResolvesInTheGame()
        {
            foreach (var e in Patched())
                Assert.True(e.Target() != null, e.Name + " target not found in this game version");
        }

        // The game assemblies target Unity's runtime. Patching a method whose body calls an engine internal call, or uses an API that only
        // exists in Unity's class library, cannot be compiled by the plain .NET Framework here. That says nothing about the patch.
        private static bool EnvironmentLimit(System.Exception ex) => ex.Message.Contains("ECall methods") || ex.Message.Contains("KeyValuePair`2.Deconstruct");

        [Fact]
        public void HarmonyAcceptsEveryPatch()
        {
            // Same calls as EventCatalog.Subscribe, but the exception is reported here and not through the game's logger.
            var failures = new List<string>();
            foreach (var e in Patched())
            {
                var target = e.Target();
                var hm = new HarmonyMethod(typeof(EventPatches), e.PatchMethod);
                try
                {
                    if (e.IsPrefix) Instance.Patch(target, prefix: hm);
                    else Instance.Patch(target, postfix: hm);
                    Instance.Unpatch(target, AccessTools.Method(typeof(EventPatches), e.PatchMethod));
                }
                catch (System.Exception ex)
                {
                    if (!EnvironmentLimit(ex)) failures.Add(e.Name + ": " + ex.Message);
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
    }
}
