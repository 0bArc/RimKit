using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.build (construction), game.power, game.bills, game.doors. Ops: build.*, power.*, bill.*, door.*.
    // game.buildings.power_on, set_power and flick are older ops and keep working.
    internal static class ApiBuild
    {
        public static void Register()
        {
            R("build.can_place", CanPlace);
            R("build.blueprint", PlaceBlueprint);
            R("build.blueprints", Blueprints);
            R("build.frames", Frames);
            R("build.cancel", Cancel);
            R("build.instant", Instant);
            R("build.defs", BuildableDefs);
            R("power.net", PowerNet);
            R("power.set_battery", SetBattery);
            R("bill.list", BillList);
            R("bill.add", BillAdd);
            R("bill.remove", BillRemove);
            R("bill.suspend", BillSuspend);
            R("bill.recipes", Recipes);
            R("door.info", DoorInfo);
            R("door.hold_open", DoorHoldOpen);
            R("trap.info", TrapInfo);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static string BadThing() => Fail("RK2001", "thing handle is stale or null");

        private static string Where(Dictionary<string, string> a, out Map map, out IntVec3 cell)
        {
            cell = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            if (!cell.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            return null;
        }

        private static ThingDef BuildDef(string name, out string err)
        {
            err = null;
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (d == null || !d.BuildableByPlayer && d.designationCategory == null && !d.IsBlueprint)
                err = Fail("RK3001", name + " is not a buildable def");
            return d;
        }

        private static Rot4 RotOf(Opts o) => new Rot4(Math.Max(0, Math.Min(3, o.Int("rotation"))));

        private static ThingDef StuffOf(ThingDef def, Opts o, out string err)
        {
            err = null;
            if (!def.MadeFromStuff) return null;
            if (!string.IsNullOrEmpty(o.Str("stuff")))
            {
                ThingDef s = DefDatabase<ThingDef>.GetNamedSilentFail(o.Str("stuff"));
                if (s == null) err = Fail("RK3001", "unknown stuff " + o.Str("stuff"));
                return s;
            }

            return GenStuff.DefaultStuffFor(def);
        }

        // opts: stuff, rotation (0 to 3). Returns { ok, reason }.
        private static string CanPlace(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 c);
            if (err != null) return err;
            ThingDef def = BuildDef(Str(a, "def"), out err);
            if (err != null) return err;
            Opts o = Opts.From(a, "opts");
            ThingDef stuff = StuffOf(def, o, out err);
            if (err != null) return err;
            AcceptanceReport r = GenConstruct.CanPlaceBlueprintAt(def, c, RotOf(o), map, false, null, null, stuff);
            return Jb.Obj().B("ok", r.Accepted).S("reason", r.Accepted ? null : r.Reason).Ok();
        }

        private static string PlaceBlueprint(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 c);
            if (err != null) return err;
            ThingDef def = BuildDef(Str(a, "def"), out err);
            if (err != null) return err;
            Opts o = Opts.From(a, "opts");
            ThingDef stuff = StuffOf(def, o, out err);
            if (err != null) return err;
            Rot4 rot = RotOf(o);
            AcceptanceReport r = GenConstruct.CanPlaceBlueprintAt(def, c, rot, map, false, null, null, stuff);
            if (!r.Accepted) return Fail("RK3003", r.Reason);
            Thing t = GenConstruct.PlaceBlueprintForBuild(def, c, map, rot, o.Handle<Faction>("faction") ?? Faction.OfPlayer, stuff);
            return OkHandle(t);
        }

        private static Jb SiteJson(Thing t)
        {
            var j = Jb.Obj().H("thing", t).S("def", t.def.defName).S("label", t.LabelCap).I("x", t.Position.x).I("z", t.Position.z);
            if (t is Frame f) j.F("work_done", f.WorkToBuild - f.WorkLeft).F("work_total", f.WorkToBuild);
            return j;
        }

        private static string Blueprints(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var arr = Jb.Arr();
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)) arr.Add(SiteJson(t));
            return arr.Ok();
        }

        private static string Frames(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var arr = Jb.Arr();
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)) arr.Add(SiteJson(t));
            return arr.Ok();
        }

        private static string Cancel(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            if (!(t is Blueprint) && !(t is Frame)) return Fail("RK1001", "only blueprints and frames can be cancelled");
            t.Destroy(DestroyMode.Cancel);
            return OkBool(true);
        }

        // Builds a finished building at once, with no work. opts: stuff, rotation, faction.
        private static string Instant(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 c);
            if (err != null) return err;
            ThingDef def = BuildDef(Str(a, "def"), out err);
            if (err != null) return err;
            Opts o = Opts.From(a, "opts");
            ThingDef stuff = StuffOf(def, o, out err);
            if (err != null) return err;
            Thing t = ThingMaker.MakeThing(def, stuff);
            t.SetFactionDirect(o.Handle<Faction>("faction") ?? Faction.OfPlayer);
            GenSpawn.Spawn(t, c, map, RotOf(o));
            return OkHandle(t);
        }

        private static string BuildableDefs(Dictionary<string, string> a)
        {
            string cat = Str(a, "category");
            var arr = Jb.Arr();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x.BuildableByPlayer))
            {
                if (!string.IsNullOrEmpty(cat) && d.designationCategory?.defName != cat) continue;
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("category", d.designationCategory?.defName).B("stuff", d.MadeFromStuff)
                    .I("size_x", d.size.x).I("size_z", d.size.z).F("work", d.GetStatValueAbstract(StatDefOf.WorkToBuild)));
            }

            return arr.Ok();
        }

        // ---- power

        private static string PowerNet(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            var comp = (t as ThingWithComps)?.GetComp<CompPower>();
            if (comp == null) return Fail("RK3003", t.def.defName + " has no power connection");
            var net = comp.PowerNet;
            var j = Jb.Obj().B("connected", net != null);
            if (comp is CompPowerTrader trader) j.B("powered_on", trader.PowerOn).F("consumption", trader.PowerOutput);
            if (net != null) j.F("gain", net.CurrentEnergyGainRate()).F("stored", net.CurrentStoredEnergy()).I("batteries", net.batteryComps.Count).I("devices", net.powerComps.Count);
            return j.Ok();
        }

        private static string SetBattery(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            var comp = (t as ThingWithComps)?.GetComp<CompPowerBattery>();
            if (comp == null) return Fail("RK3003", t.def.defName + " is not a battery");
            comp.SetStoredEnergyPct(Math.Max(0f, Math.Min(1f, Float(a, "pct"))));
            return OkFloat(comp.StoredEnergyPct);
        }

        // ---- bills

        private static IBillGiver Giver(Dictionary<string, string> a, out string err)
        {
            err = null;
            Thing t = ThingOf(a);
            if (t == null) { err = BadThing(); return null; }
            if (!(t is IBillGiver g)) { err = Fail("RK3003", t.def.defName + " cannot hold bills"); return null; }
            return g;
        }

        private static string BillList(Dictionary<string, string> a)
        {
            IBillGiver g = Giver(a, out string err);
            if (err != null) return err;
            var arr = Jb.Arr();
            int i = 0;
            foreach (Bill b in g.BillStack.Bills)
            {
                var j = Jb.Obj().I("index", i++).S("recipe", b.recipe.defName).S("label", b.LabelCap).B("suspended", b.suspended);
                if (b is Bill_Production p)
                    j.S("mode", p.repeatMode.defName).I("repeat_count", p.repeatCount).I("target_count", p.targetCount).B("paused", p.paused);
                arr.Add(j);
            }

            return arr.Ok();
        }

        // opts: mode (Forever, RepeatCount, TargetCount), count, target.
        private static string BillAdd(Dictionary<string, string> a)
        {
            IBillGiver g = Giver(a, out string err);
            if (err != null) return err;
            RecipeDef r = DefDatabase<RecipeDef>.GetNamedSilentFail(Str(a, "recipe"));
            if (r == null) return Fail("RK3001", "unknown recipe " + Str(a, "recipe"));
            if (!(g as Thing).def.AllRecipes.Contains(r)) return Fail("RK3003", "this building cannot make " + r.defName);
            Bill b = r.MakeNewBill();
            if (b is Bill_Production p)
            {
                Opts o = Opts.From(a, "opts");
                switch (o.Str("mode", "RepeatCount"))
                {
                    case "Forever": p.repeatMode = BillRepeatModeDefOf.Forever; break;
                    case "TargetCount": p.repeatMode = BillRepeatModeDefOf.TargetCount; p.targetCount = Math.Max(1, o.Int("target", 1)); break;
                    case "RepeatCount": p.repeatMode = BillRepeatModeDefOf.RepeatCount; p.repeatCount = Math.Max(1, o.Int("count", 1)); break;
                    default: return Fail("RK1001", "mode must be Forever, RepeatCount or TargetCount");
                }
            }

            g.BillStack.AddBill(b);
            return OkInt(g.BillStack.Count - 1);
        }

        private static string BillRemove(Dictionary<string, string> a)
        {
            IBillGiver g = Giver(a, out string err);
            if (err != null) return err;
            int i = Int(a, "index");
            if (i < 0 || i >= g.BillStack.Count) return Fail("RK1001", "bill index out of range");
            g.BillStack.Delete(g.BillStack.Bills[i]);
            return OkBool(true);
        }

        private static string BillSuspend(Dictionary<string, string> a)
        {
            IBillGiver g = Giver(a, out string err);
            if (err != null) return err;
            int i = Int(a, "index");
            if (i < 0 || i >= g.BillStack.Count) return Fail("RK1001", "bill index out of range");
            g.BillStack.Bills[i].suspended = Bool(a, "suspended");
            return OkBool(g.BillStack.Bills[i].suspended);
        }

        private static string Recipes(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return BadThing();
            var arr = Jb.Arr();
            foreach (RecipeDef r in t.def.AllRecipes)
            {
                var products = Jb.Arr();
                foreach (ThingDefCountClass p in r.products) products.Add(Jb.Obj().S("def", p.thingDef.defName).I("count", p.count));
                arr.Add(Jb.Obj().S("def", r.defName).S("label", r.LabelCap).B("available", r.AvailableNow).F("work", r.workAmount).Raw("products", products.ToString()));
            }

            return arr.Ok();
        }

        // ---- doors and traps

        private static string DoorInfo(Dictionary<string, string> a)
        {
            if (!(ThingOf(a) is Building_Door d)) return Fail("RK3003", "the thing is not a door");
            return Jb.Obj().B("open", d.Open).B("hold_open", d.HoldOpen).B("blocked", d.BlockedOpenMomentary).B("forbidden", d.IsForbidden(Faction.OfPlayer)).Ok();
        }

        private static string DoorHoldOpen(Dictionary<string, string> a)
        {
            if (!(ThingOf(a) is Building_Door d)) return Fail("RK3003", "the thing is not a door");
            var field = AccessTools.Field(typeof(Building_Door), "holdOpenInt");
            if (field == null) return Fail("RK3003", "holding a door open is not supported on this game version");
            field.SetValue(d, Bool(a, "hold"));
            return OkBool(d.HoldOpen);
        }

        private static string TrapInfo(Dictionary<string, string> a)
        {
            if (!(ThingOf(a) is Building_Trap t)) return Fail("RK3003", "the thing is not a trap");
            object armed = AccessTools.Property(t.GetType(), "Armed")?.GetValue(t, null) ?? AccessTools.Field(t.GetType(), "armedInt")?.GetValue(t);
            return Jb.Obj().B("armed", armed is bool b && b).S("class", t.GetType().Name).Ok();
        }
    }
}
