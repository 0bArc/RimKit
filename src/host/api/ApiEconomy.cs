using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.economy (wealth, prices, traders) and game.raids. Ops: economy.*, raid.*.
    internal static class ApiEconomy
    {
        public static void Register()
        {
            R("economy.silver", Silver);
            R("economy.wealth", Wealth);
            R("economy.price", Price);
            R("economy.give_silver", GiveSilver);
            R("economy.trader_kinds", TraderKinds);
            R("economy.ships", Ships);
            R("economy.stock", Stock);
            R("economy.call_trader", CallTrader);
            R("raid.strategies", Strategies);
            R("raid.arrival_modes", ArrivalModes);
            R("raid.can_fire", RaidCanFire);
            R("raid.fire", RaidFire);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static string BadMap() => Fail("RK2001", "map handle is stale or null");

        private static Map MapOrCurrent(Dictionary<string, string> a) => string.IsNullOrEmpty(Str(a, "h")) ? Find.CurrentMap : MapOf(a);

        private static string Silver(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            return OkInt(m.resourceCounter.Silver);
        }

        private static string Wealth(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            var w = m.wealthWatcher;
            return Jb.Obj().F("total", w.WealthTotal).F("items", w.WealthItems).F("buildings", w.WealthBuildings).F("pawns", w.WealthPawns).F("floors", w.WealthFloorsOnly).Ok();
        }

        // Base market value of a thing (thing handle) or a def (def name, with optional stuff and quality), and the price at the
        // colony's usual trade margins. kind: market, sell, buy.
        private static string Price(Dictionary<string, string> a)
        {
            float market;
            float sellFactor;
            if (!string.IsNullOrEmpty(Str(a, "thing")))
            {
                Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
                if (t == null) return Fail("RK2001", "thing handle is stale or null");
                market = t.MarketValue;
                sellFactor = t.def.GetStatValueAbstract(StatDefOf.SellPriceFactor);
            }
            else
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
                if (d == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
                ThingDef stuff = string.IsNullOrEmpty(Str(a, "stuff")) ? (d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null) : DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "stuff"));
                market = StatDefOf.MarketValue.Worker.GetValue(StatRequest.For(d, stuff, QualityCategory.Normal));
                sellFactor = d.GetStatValueAbstract(StatDefOf.SellPriceFactor);
            }

            return Jb.Obj().F("market", market).F("sell", market * sellFactor * 0.5f).F("buy", market * 1.5f).Ok();
        }

        // Drops silver near the colony the way a trade drop pod would.
        private static string GiveSilver(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            int amount = Math.Max(1, Int(a, "amount"));
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = amount;
            TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(m), m, silver);
            return OkInt(amount);
        }

        private static string TraderKinds(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (TraderKindDef d in DefDatabase<TraderKindDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).B("orbital", d.orbital).B("visitor", d.category == "Visitor" || d.commonality <= 0).F("commonality", d.commonality));
            return arr.Ok();
        }

        private static string Ships(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            var arr = Jb.Arr();
            foreach (PassingShip s in m.passingShipManager.passingShips)
            {
                var j = Jb.Obj().S("name", s.name).S("class", s.GetType().Name);
                if (s is TradeShip ts) j.S("kind", ts.def?.defName).I("goods", ts.Goods.Count()).I("ticks_left", ts.ticksUntilDeparture);
                arr.Add(j);
            }

            return arr.Ok();
        }

        // Stock of a settlement (world object handle) or an orbital trader (index into ships).
        private static string Stock(Dictionary<string, string> a)
        {
            IEnumerable<Thing> goods = null;
            if (!string.IsNullOrEmpty(Str(a, "settlement")))
            {
                var s = ObjectHandles.Get<Settlement>(Int(a, "settlement"));
                if (s == null) return Fail("RK2001", "settlement handle is stale or not a settlement");
                if (s.Faction == Faction.OfPlayer) return Fail("RK3003", "the player's own settlement has no trade stock");
                goods = s.Goods;
            }
            else
            {
                Map m = MapOrCurrent(a);
                if (m == null) return BadMap();
                var ships = m.passingShipManager.passingShips.OfType<TradeShip>().ToList();
                int i = Int(a, "ship");
                if (i < 0 || i >= ships.Count) return Fail("RK1001", "ship index out of range");
                goods = ships[i].Goods;
            }

            var arr = Jb.Arr();
            foreach (Thing t in goods) arr.Add(Jb.Obj().S("def", t.def.defName).S("label", t.LabelCap).I("count", t.stackCount).F("value", t.MarketValue * t.stackCount));
            return arr.Ok();
        }

        private static string CallTrader(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, m);
            parms.forced = true;
            return OkBool(IncidentDefOf.OrbitalTraderArrival.Worker.TryExecute(parms));
        }

        // ---- raids

        private static string Strategies(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (RaidStrategyDef d in DefDatabase<RaidStrategyDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).F("selection_weight", d.selectionWeightPerPointsCurve == null ? 0 : d.selectionWeightPerPointsCurve.Evaluate(1000)));
            return arr.Ok();
        }

        private static string ArrivalModes(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (PawnsArrivalModeDef d in DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).B("walks_in", d.walkIn));
            return arr.Ok();
        }

        private static IncidentParms RaidParms(Map m, Opts o, out string err, out IncidentDef def)
        {
            err = null;
            Faction f = o.Handle<Faction>("faction");
            bool friendly = f != null && !f.HostileTo(Faction.OfPlayer);
            def = friendly ? IncidentDefOf.RaidFriendly : IncidentDefOf.RaidEnemy;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, m);
            parms.forced = true;
            if (f != null) parms.faction = f;
            if (o.Has("points")) parms.points = (float)o.Num("points");
            if (o.Has("strategy"))
            {
                parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(o.Str("strategy"));
                if (parms.raidStrategy == null) err = Fail("RK3001", "unknown raid strategy " + o.Str("strategy"));
            }

            if (o.Has("arrival"))
            {
                parms.raidArrivalMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail(o.Str("arrival"));
                if (parms.raidArrivalMode == null) err = Fail("RK3001", "unknown arrival mode " + o.Str("arrival"));
            }

            return parms;
        }

        private static string RaidCanFire(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            IncidentParms parms = RaidParms(m, Opts.From(a, "opts"), out string err, out IncidentDef def);
            return err ?? OkBool(def.Worker.CanFireNow(parms));
        }

        // opts: faction, points, strategy (RaidStrategyDef), arrival (PawnsArrivalModeDef). A faction friendly to the player sends a friendly raid.
        private static string RaidFire(Dictionary<string, string> a)
        {
            Map m = MapOrCurrent(a);
            if (m == null) return BadMap();
            IncidentParms parms = RaidParms(m, Opts.From(a, "opts"), out string err, out IncidentDef def);
            return err ?? OkBool(def.Worker.TryExecute(parms));
        }
    }
}
