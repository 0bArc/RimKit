using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // Spawning and generation: pawn generation requests, pawn kinds, thing sets, stuff and quality rolls.
    // Ops: pawn.generate, pawn.kinds, thing.thing_set, thing.thing_set_defs, thing.stuff_options, thing.random_stuff, thing.roll_quality.
    internal static class ApiSpawning
    {
        public static void Register()
        {
            R("pawn.generate", Generate);
            R("pawn.kinds", Kinds);
            R("thing.thing_set", ThingSet);
            R("thing.thing_set_defs", ThingSetDefs);
            R("thing.stuff_options", StuffOptions);
            R("thing.random_stuff", RandomStuff);
            R("thing.roll_quality", RollQuality);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string Kinds(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (PawnKindDef k in DefDatabase<PawnKindDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", k.defName).S("label", k.label).S("race", k.race?.defName).B("humanlike", k.RaceProps?.Humanlike ?? false).B("animal", k.RaceProps?.Animal ?? false));
            return arr.Ok();
        }

        // opts: kind (def name, default Colonist), faction (a faction, or "player"), gender (Male, Female), age (years),
        // map, x, z (spawn it there), name (nick name).
        private static string Generate(Dictionary<string, string> a)
        {
            Opts o = Opts.From(a, "opts");
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(o.Str("kind", "Colonist"));
            if (kind == null) return Fail("RK3001", "unknown pawn kind " + o.Str("kind"));
            Faction faction = o.Handle<Faction>("faction");
            if (faction == null && string.Equals(o.Str("faction"), "player", StringComparison.OrdinalIgnoreCase)) faction = Faction.OfPlayer;
            Gender? gender = null;
            if (o.Has("gender"))
            {
                if (!Enum.TryParse(o.Str("gender"), true, out Gender g) || g == Gender.None) return Fail("RK1001", "gender must be Male or Female");
                gender = g;
            }

            float? age = o.Has("age") ? (float?)o.Num("age") : null;
            var request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                fixedGender: gender, fixedBiologicalAge: age, fixedChronologicalAge: age);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            if (pawn == null) return Fail("RK3001", "the game could not generate that pawn");
            Map map = o.Handle<Map>("map");
            if (map != null && o.Has("x") && o.Has("z"))
            {
                var cell = new IntVec3(o.Int("x"), 0, o.Int("z"));
                if (!cell.InBounds(map)) return Fail("RK1001", "cell is outside the map");
                GenSpawn.Spawn(pawn, cell, map);
            }

            return OkHandle(pawn);
        }

        private static string ThingSetDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<ThingSetMakerDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        // Generates the items a ThingSetMakerDef would, without placing them. opts: value_min, value_max, count_min, count_max.
        private static string ThingSet(Dictionary<string, string> a)
        {
            ThingSetMakerDef def = DefDatabase<ThingSetMakerDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing set maker " + Str(a, "def"));
            Opts o = Opts.From(a, "opts");
            var parms = new ThingSetMakerParams();
            if (o.Has("value_min") || o.Has("value_max")) parms.totalMarketValueRange = new FloatRange((float)o.Num("value_min", 0), (float)o.Num("value_max", 1000));
            if (o.Has("count_min") || o.Has("count_max")) parms.countRange = new IntRange(o.Int("count_min", 1), o.Int("count_max", 5));
            List<Thing> things = def.root.Generate(parms);
            return OkHandles(things);
        }

        private static string StuffOptions(Dictionary<string, string> a)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            if (!def.MadeFromStuff) return OkStringList(new List<string>());
            return OkStringList(GenStuff.AllowedStuffsFor(def).Select(s => s.defName).ToList());
        }

        private static string RandomStuff(Dictionary<string, string> a)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            if (!def.MadeFromStuff) return OkJson("null");
            return OkStr(GenStuff.RandomStuffFor(def).defName);
        }

        // generator: Reward, BaseGen, Gift, OnMap, Trader. Default OnMap.
        private static string RollQuality(Dictionary<string, string> a)
        {
            string raw = string.IsNullOrEmpty(Str(a, "generator")) ? "OnMap" : Str(a, "generator");
            if (!Enum.TryParse(raw, true, out QualityGenerator gen)) return Fail("RK1001", "generator must be Reward, BaseGen, Gift, OnMap or Trader");
            return OkStr(QualityUtility.GenerateQuality(gen).ToString());
        }
    }
}
