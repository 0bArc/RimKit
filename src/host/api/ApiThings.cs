using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.things kit: info, quality, stuff, forbidden, rotation, comps, damage, creating and spawning.
    // def, label, pos, hp, stack, faction, destroy and the other older functions stay in GameApi.
    internal static class ApiThings
    {
        public static void Register()
        {
            ApiRegistry.Register("thing.info", Info, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.quality", Quality, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.set_quality", SetQuality, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.stuff", Stuff, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.forbidden", Forbidden, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.set_forbidden", SetForbidden, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.rotation", RotationOf, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.set_rotation", SetRotation, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.comps", Comps, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.has_comp", HasComp, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.damage", Damage, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.heal", Heal, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.make", Make, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.spawn_at", SpawnAt, "gameplay", "0.5.0");
            ApiRegistry.Register("thing.destroy_with", DestroyWith, "gameplay", "0.5.0");
        }

        private static string Bad() => Fail("RK2001", "thing handle is stale or null");

        private static string Info(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            var sb = new StringBuilder("{");
            sb.Append("\"id\":").Append(t.thingIDNumber.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"def\":").Append(JsonLite.Quote(t.def?.defName ?? ""));
            sb.Append(",\"label\":").Append(JsonLite.Quote(t.LabelCap.ToString()));
            sb.Append(",\"hp\":").Append(t.HitPoints.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"max_hp\":").Append(t.MaxHitPoints.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"stack\":").Append(t.stackCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"spawned\":").Append(t.Spawned ? "true" : "false");
            sb.Append(",\"is_pawn\":").Append(t is Pawn ? "true" : "false");
            sb.Append(",\"is_building\":").Append(t is Building ? "true" : "false");
            sb.Append(",\"rotation\":").Append(t.Rotation.AsInt.ToString(CultureInfo.InvariantCulture));
            if (t.Stuff != null) sb.Append(",\"stuff\":").Append(JsonLite.Quote(t.Stuff.defName));
            if (t.TryGetQuality(out QualityCategory q)) sb.Append(",\"quality\":").Append(JsonLite.Quote(q.ToString()));
            if (t.Spawned)
            {
                sb.Append(",\"x\":").Append(t.Position.x.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"z\":").Append(t.Position.z.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"map\":").Append(HookCodec.Encode(t.Map));
                sb.Append(",\"forbidden\":").Append(t.IsForbidden(Faction.OfPlayer) ? "true" : "false");
            }

            if (t.Faction != null) sb.Append(",\"faction\":").Append(HookCodec.Encode(t.Faction));
            sb.Append('}');
            return OkJson(sb.ToString());
        }

        private static string Quality(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            return t.TryGetQuality(out QualityCategory q) ? OkStr(q.ToString()) : OkJson("null");
        }

        // quality: Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary (any case) or 0 to 6.
        private static string SetQuality(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            CompQuality comp = t.TryGetComp<CompQuality>();
            if (comp == null) return Fail("RK3003", t.def.defName + " has no quality");
            string raw = Str(a, "quality");
            QualityCategory q;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                if (n < 0 || n > 6) return Fail("RK1001", "quality must be 0 to 6");
                q = (QualityCategory)n;
            }
            else if (!Enum.TryParse(raw, true, out q) || !Enum.IsDefined(typeof(QualityCategory), q))
            {
                return Fail("RK1001", "quality must be Awful, Poor, Normal, Good, Excellent, Masterwork or Legendary");
            }

            comp.SetQuality(q, ArtGenerationContext.Colony);
            return OkStr(q.ToString());
        }

        private static string Stuff(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            return t.Stuff == null ? OkJson("null") : OkStr(t.Stuff.defName);
        }

        private static string Forbidden(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            return OkBool(t.Spawned && t.IsForbidden(Faction.OfPlayer));
        }

        private static string SetForbidden(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            if (!t.Spawned) return Fail("RK3001", "thing is not spawned");
            t.SetForbidden(Bool(a, "forbidden"), false);
            return OkBool(t.IsForbidden(Faction.OfPlayer));
        }

        private static string RotationOf(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            return OkInt(t.Rotation.AsInt);
        }

        // rotation: 0 north, 1 east, 2 south, 3 west.
        private static string SetRotation(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            int r = Int(a, "rotation");
            if (r < 0 || r > 3) return Fail("RK1001", "rotation must be 0 to 3");
            t.Rotation = new Rot4(r);
            return OkInt(t.Rotation.AsInt);
        }

        private static string Comps(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            var list = (t as ThingWithComps)?.AllComps ?? new List<ThingComp>();
            return OkStringList(list.Select(c => c.GetType().Name).ToList());
        }

        // class: the comp class name, with or without the Comp prefix and namespace, for example "Power" or "CompPowerTrader".
        private static string HasComp(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            string wanted = Str(a, "class") ?? "";
            int dot = wanted.LastIndexOf('.');
            if (dot >= 0) wanted = wanted.Substring(dot + 1);
            var list = (t as ThingWithComps)?.AllComps ?? new List<ThingComp>();
            bool found = list.Any(c => c.GetType().Name.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                                       c.GetType().Name.Equals("Comp" + wanted, StringComparison.OrdinalIgnoreCase));
            return OkBool(found);
        }

        // Deals damage with a DamageDef (default Cut). Returns the hit points left.
        private static string Damage(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            string defName = string.IsNullOrEmpty(Str(a, "damage_def")) ? "Cut" : Str(a, "damage_def");
            DamageDef def = DefDatabase<DamageDef>.GetNamedSilentFail(defName);
            if (def == null) return Fail("RK3001", "unknown damage def " + defName);
            float amount = Float(a, "amount");
            if (amount <= 0) return Fail("RK1001", "amount must be positive");
            t.TakeDamage(new DamageInfo(def, amount));
            return OkInt(t.Destroyed ? 0 : t.HitPoints);
        }

        private static string Heal(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            if (t is Pawn) return Fail("RK1001", "use game.pawns health functions for pawns");
            t.HitPoints = t.MaxHitPoints;
            return OkInt(t.HitPoints);
        }

        private static string Create(Dictionary<string, string> a, out Thing thing)
        {
            thing = null;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            // Options arrive as one "opts" table from the kit binder. Flat keys still work for older callers.
            Opts o = Opts.From(a, "opts");
            string stuffName = o.Str("stuff", Str(a, "stuff"));
            string qualityName = o.Str("quality", Str(a, "quality"));
            ThingDef stuff = null;
            if (!string.IsNullOrEmpty(stuffName))
            {
                stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
                if (stuff == null) return Fail("RK3001", "unknown stuff " + stuffName);
            }
            else if (def.MadeFromStuff)
            {
                stuff = GenStuff.DefaultStuffFor(def);
            }

            Thing t = ThingMaker.MakeThing(def, stuff);
            int count = o.Has("count") ? Math.Max(1, o.Int("count")) : (Str(a, "count") != "" ? Math.Max(1, Int(a, "count")) : 1);
            if (def.stackLimit > 1) t.stackCount = Math.Min(count, def.stackLimit);
            if (!string.IsNullOrEmpty(qualityName))
            {
                CompQuality comp = t.TryGetComp<CompQuality>();
                if (comp != null && Enum.TryParse(qualityName, true, out QualityCategory q))
                {
                    comp.SetQuality(q, ArtGenerationContext.Colony);
                }
            }

            thing = t;
            return null;
        }

        // Makes a thing without placing it. Options: def, stuff, quality, count.
        private static string Make(Dictionary<string, string> a)
        {
            string err = Create(a, out Thing t);
            return err ?? OkHandle(t);
        }

        // Makes a thing and puts it on a map. Options: def, map, x, z, stuff, quality, count.
        private static string SpawnAt(Dictionary<string, string> a)
        {
            Map map = ObjectHandles.Get<Map>(Int(a, "map"));
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var cell = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!cell.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            string err = Create(a, out Thing t);
            if (err != null) return err;
            GenSpawn.Spawn(t, cell, map);
            return OkHandle(t);
        }

        // mode: Vanish, Deconstruct, KillFinalize, Refund, Cancel, FailConstruction (any case). Default Vanish.
        private static string DestroyWith(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad();
            DestroyMode mode = DestroyMode.Vanish;
            string raw = Str(a, "mode");
            if (!string.IsNullOrEmpty(raw) && !Enum.TryParse(raw, true, out mode))
            {
                return Fail("RK1001", "unknown destroy mode " + raw);
            }

            t.Destroy(mode);
            return OkBool(true);
        }
    }
}
