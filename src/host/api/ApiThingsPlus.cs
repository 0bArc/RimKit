using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // More of game.things and game.factions: minified items, styles, ownership, storage filters, temporary factions, diplomacy.
    internal static class ApiThingsPlus
    {
        public static void Register()
        {
            R("thing.minify", Minify);
            R("thing.inner", Inner);
            R("thing.style", StyleOf);
            R("thing.set_style", SetStyle);
            R("thing.owners", Owners);
            R("thing.assign_owner", AssignOwner);
            R("thing.unassign_owner", UnassignOwner);
            R("thing.storage_allows", StorageAllows);
            R("thing.storage_set_allowed", StorageSetAllowed);
            R("thing.storage_priority", StoragePriority);
            R("thing.set_storage_priority", SetStoragePriority);
            R("faction.make_temporary", MakeTemporary);
            R("faction.remove", RemoveFaction);
            R("faction.make_peace", MakePeace);
            R("faction.declare_war", DeclareWar);
            R("faction.send_gift", SendGift);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string BadThing() => Fail("RK2001", "thing handle is stale or null");

        private static string Minify(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            if (!t.def.Minifiable) return Fail("RK3003", t.def.defName + " cannot be minified");
            Map map = t.Map;
            IntVec3 cell = t.Position;
            MinifiedThing m = MinifyUtility.MakeMinified(t);
            if (m == null) return Fail("RK3001", "the game could not minify it");
            if (map != null && !m.Spawned) GenSpawn.Spawn(m, cell, map);
            return OkHandle(m);
        }

        private static string Inner(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            return OkHandle((t as MinifiedThing)?.InnerThing);
        }

        private static string StyleOf(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            return t.StyleDef == null ? OkJson("null") : OkStr(t.StyleDef.defName);
        }

        private static string SetStyle(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            ThingStyleDef style = null;
            if (!string.IsNullOrEmpty(Str(a, "style")))
            {
                style = DefDatabase<ThingStyleDef>.GetNamedSilentFail(Str(a, "style"));
                if (style == null) return Fail("RK3001", "unknown style " + Str(a, "style"));
            }

            t.StyleDef = style;
            if (t.Spawned) t.Map.mapDrawer.MapMeshDirty(t.Position, MapMeshFlagDefOf.Things);
            return OkBool(t.StyleDef == style);
        }

        private static CompAssignableToPawn Assignable(Thing t) => t.TryGetComp<CompAssignableToPawn>();

        private static string Owners(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            var comp = Assignable(t);
            if (comp == null) return Fail("RK3003", t.def.defName + " cannot be assigned to pawns");
            return OkHandles(comp.AssignedPawnsForReading.ToList());
        }

        private static string AssignOwner(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            var comp = Assignable(t);
            if (comp == null) return Fail("RK3003", t.def.defName + " cannot be assigned to pawns");
            Pawn p = ObjectHandles.Get<Pawn>(Int(a, "pawn"));
            if (p == null) return Fail("RK2001", "pawn handle is stale or null");
            comp.TryAssignPawn(p);
            return OkBool(comp.AssignedPawnsForReading.Contains(p));
        }

        private static string UnassignOwner(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            var comp = Assignable(t);
            if (comp == null) return Fail("RK3003", t.def.defName + " cannot be assigned to pawns");
            Pawn p = ObjectHandles.Get<Pawn>(Int(a, "pawn"));
            if (p == null) return Fail("RK2001", "pawn handle is stale or null");
            comp.TryUnassignPawn(p);
            return OkBool(!comp.AssignedPawnsForReading.Contains(p));
        }

        private static StorageSettings Storage(Thing t, out string err)
        {
            err = null;
            if (t == null) { err = BadThing(); return null; }
            var parent = t as IStoreSettingsParent;
            StorageSettings s = parent?.GetStoreSettings();
            if (s == null) err = Fail("RK3003", t.def.defName + " has no storage settings");
            return s;
        }

        private static string StorageAllows(Dictionary<string, string> a)
        {
            StorageSettings s = Storage(ThingOf(a), out string err);
            if (err != null) return err;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            return OkBool(s.AllowedToAccept(def));
        }

        private static string StorageSetAllowed(Dictionary<string, string> a)
        {
            StorageSettings s = Storage(ThingOf(a), out string err);
            if (err != null) return err;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            s.filter.SetAllow(def, Bool(a, "allowed"));
            return OkBool(s.AllowedToAccept(def) == Bool(a, "allowed"));
        }

        private static string StoragePriority(Dictionary<string, string> a)
        {
            StorageSettings s = Storage(ThingOf(a), out string err);
            return err ?? OkStr(s.Priority.ToString());
        }

        // priority: Unstored, Low, Normal, Preferred, Important, Critical (any case).
        private static string SetStoragePriority(Dictionary<string, string> a)
        {
            StorageSettings s = Storage(ThingOf(a), out string err);
            if (err != null) return err;
            if (!Enum.TryParse(Str(a, "priority"), true, out StoragePriority p)) return Fail("RK1001", "priority must be Unstored, Low, Normal, Preferred, Important or Critical");
            s.Priority = p;
            return OkStr(s.Priority.ToString());
        }

        private static string MakeTemporary(Dictionary<string, string> a)
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown faction def " + Str(a, "def"));
            Faction f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
            if (f == null) return Fail("RK3001", "the game could not generate that faction");
            f.temporary = true;
            if (!string.IsNullOrEmpty(Str(a, "name"))) f.Name = Str(a, "name");
            Find.FactionManager.Add(f);
            return OkHandle(f);
        }

        private static string RemoveFaction(Dictionary<string, string> a)
        {
            Faction f = ObjectHandles.Get<Faction>(Int(a, "h"));
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            if (!f.temporary) return Fail("RK1001", "only temporary factions can be removed");
            var all = AccessTools.Field(typeof(FactionManager), "allFactions")?.GetValue(Find.FactionManager) as List<Faction>;
            if (all == null || !all.Remove(f)) return Fail("RK3001", "the faction could not be removed");
            return OkBool(true);
        }

        private static string Relate(Dictionary<string, string> a, FactionRelationKind kind)
        {
            Faction f = ObjectHandles.Get<Faction>(Int(a, "h"));
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            if (f.IsPlayer) return Fail("RK1001", "pick another faction");
            if (f.def.permanentEnemy) return Fail("RK3003", f.Name + " is a permanent enemy");
            f.SetRelationDirect(Faction.OfPlayer, kind, true, null, null);
            return OkStr(f.RelationKindWith(Faction.OfPlayer).ToString());
        }

        private static string MakePeace(Dictionary<string, string> a) => Relate(a, FactionRelationKind.Neutral);

        private static string DeclareWar(Dictionary<string, string> a) => Relate(a, FactionRelationKind.Hostile);

        // Gives items to a faction and raises goodwill the way an in-game gift does. things: list of thing handles.
        private static string SendGift(Dictionary<string, string> a)
        {
            Faction f = ObjectHandles.Get<Faction>(Int(a, "h"));
            if (f == null) return Fail("RK2001", "faction handle is stale or null");
            if (f.IsPlayer || f.HostileTo(Faction.OfPlayer) && f.def.permanentEnemy) return Fail("RK3003", f.Name + " cannot receive gifts");
            var things = new List<Thing>();
            if (a.TryGetValue("things", out string json) && Json.TryParse(json, out object parsed) && parsed is List<object> list)
            {
                foreach (object item in list)
                {
                    Thing t = Opts.HandleOf<Thing>(item);
                    if (t != null) things.Add(t);
                }
            }

            if (things.Count == 0) return Fail("RK1001", "things must list at least one thing");
            // The items are consumed. Goodwill rises by market value / 40, at most 40 per gift, scaled by the game's goodwill setting.
            float value = things.Sum(x => x.MarketValue * x.stackCount);
            int gain = Math.Max(1, Math.Min(40, (int)(value / 40f)));
            foreach (Thing t in things) t.Destroy();
            f.TryAffectGoodwillWith(Faction.OfPlayer, gain, true, false, null, null);
            return OkInt(f.GoodwillWith(Faction.OfPlayer));
        }
    }
}
