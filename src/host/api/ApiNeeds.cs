using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.needs: needs defined from Lua with no XML. Ops: need.define, need.defs, need.remove.
    // A need made here is a normal NeedDef whose class is Need_Lua, so the game draws its bar, saves its level and adds it to pawns like any
    // other need. It is created when the mod's Lua runs at startup, which is before any save loads, so saves find it.
    internal static class ApiNeeds
    {
        // Defs created here, so need.remove only takes away what Lua made.
        private static readonly HashSet<string> Created = new HashSet<string>(StringComparer.Ordinal);

        public static void Register()
        {
            ApiRegistry.Register("need.define", a => Define(a), "gameplay", "0.10.0");
            ApiRegistry.Register("need.defs", a => Defs(a), "gameplay", "0.10.0");
            ApiRegistry.Register("need.remove", a => Remove(a), "gameplay", "0.10.0");
        }

        private static string Define(Dictionary<string, string> a)
        {
            string name = Str(a, "def");
            if (!LuaSandbox.IsSafeDefName(name)) return Fail("RK1001", "def must be a def name: letters, digits and underscore, not starting with a digit");
            Opts o = Opts.From(a, "opts");
            string luaClass = o.Str("lua_class", name);

            NeedDef existing = DefDatabase<NeedDef>.GetNamedSilentFail(name);
            if (existing != null && !Created.Contains(name)) return Fail("RK1001", name + " is a need from the game or another mod. Pick another name, or replace its class with game.classes.replace");

            NeedDef def = existing ?? new NeedDef { defName = name };
            def.needClass = typeof(Need_Lua);
            def.label = o.Str("label", name);
            def.description = o.Str("description", "");
            def.showOnNeedList = o.Bool("show", true);
            def.major = o.Bool("major", false);
            def.baseLevel = (float)Math.Max(0.0, Math.Min(1.0, o.Num("base_level", 0.5)));
            def.fallPerDay = (float)Math.Max(0.0, o.Num("fall_per_day", 0.0));
            def.listPriority = o.Int("list_priority", 0);
            def.freezeWhileSleeping = o.Bool("freeze_while_sleeping", false);
            def.freezeInMentalState = o.Bool("freeze_in_mental_state", false);
            def.scaleBar = o.Bool("scale_bar", false);
            def.showForCaravanMembers = o.Bool("show_for_caravan", false);

            string who = o.Str("who", "colonists");
            def.colonistsOnly = who == "colonists";
            def.colonistAndPrisonersOnly = who == "colonists_and_prisoners";
            if (who != "colonists" && who != "colonists_and_prisoners" && who != "humanlike" && who != "everyone")
                return Fail("RK1001", "who must be colonists, colonists_and_prisoners, humanlike or everyone");
            string intelligence = o.Str("intelligence", who == "everyone" ? "Animal" : "Humanlike");
            if (!Enum.TryParse(intelligence, true, out Intelligence level)) return Fail("RK1001", "intelligence must be Animal, ToolUser or Humanlike");
            def.minIntelligence = level;

            def.modExtensions = def.modExtensions ?? new List<DefModExtension>();
            def.modExtensions.RemoveAll(x => x is DefModExtension_Lua);
            def.modExtensions.Add(new DefModExtension_Lua { luaClass = luaClass });

            if (existing == null)
            {
                def.shortHash = (ushort)(Math.Abs(def.defName.GetHashCode()) % 60000 + 1);
                DefDatabase<NeedDef>.Add(def);
                Created.Add(name);
            }

            int added = RefreshPawns();
            return Jb.Obj().S("def", name).S("lua_class", luaClass).B("created", existing == null).I("pawns", added).Ok();
        }

        // Asks every pawn's needs tracker to pick up the new definition. Returns how many pawns now have the need.
        private static int RefreshPawns()
        {
            int with = 0;
            try
            {
                foreach (Pawn p in PawnsFinder.AllMapsWorldAndTemporary_Alive.ToList())
                {
                    p.needs?.AddOrRemoveNeedsAsAppropriate();
                    if (p.needs != null && p.needs.AllNeeds.Any(n => n.def != null && Created.Contains(n.def.defName))) with++;
                }
            }
            catch (Exception)
            {
                // No game is loaded yet: pawns pick the need up when they are made.
            }

            return with;
        }

        private static string Defs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (string name in Created.OrderBy(n => n))
            {
                NeedDef d = DefDatabase<NeedDef>.GetNamedSilentFail(name);
                if (d == null) continue;
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("lua_class", d.GetModExtension<DefModExtension_Lua>()?.luaClass).B("major", d.major).F("base_level", d.baseLevel).F("fall_per_day", d.fallPerDay));
            }

            return arr.Ok();
        }

        // Takes a need Lua made away from every pawn and hides it. The def stays in the database until the game restarts.
        private static string Remove(Dictionary<string, string> a)
        {
            string name = Str(a, "def");
            if (!Created.Contains(name)) return OkBool(false);
            NeedDef def = DefDatabase<NeedDef>.GetNamedSilentFail(name);
            if (def == null) return OkBool(false);
            def.showOnNeedList = false;
            def.minIntelligence = (Intelligence)99;
            try
            {
                foreach (Pawn p in PawnsFinder.AllMapsWorldAndTemporary_Alive.ToList()) p.needs?.AddOrRemoveNeedsAsAppropriate();
            }
            catch (Exception)
            {
            }

            Created.Remove(name);
            return OkBool(true);
        }
    }
}
