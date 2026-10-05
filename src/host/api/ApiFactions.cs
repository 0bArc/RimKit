using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.factions kit: goodwill, relations, leaders, members, definitions. Ops use the singular domain faction.*.
    // faction.player, of_def, name, list, is_hostile and set_relation are older ops in GameApi and keep working.
    internal static class ApiFactions
    {
        public static void Register()
        {
            ApiRegistry.Register("faction.info", Info, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.goodwill", Goodwill, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.set_goodwill", SetGoodwill, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.adjust_goodwill", AdjustGoodwill, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.relation", Relation, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.leader", Leader, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.members", Members, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.hostiles", Hostiles, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.allies", Allies, "gameplay", "0.5.0");
            ApiRegistry.Register("faction.defs", Defs, "gameplay", "0.5.0");
        }

        private static Faction Of(Dictionary<string, string> a) => ObjectHandles.Get<Faction>(Int(a, "h"));

        // "other" is optional and defaults to the player faction.
        private static Faction Other(Dictionary<string, string> a)
        {
            if (!a.ContainsKey("other") || string.IsNullOrEmpty(a["other"]))
            {
                return Faction.OfPlayer;
            }

            return ObjectHandles.Get<Faction>(Int(a, "other"));
        }

        private static string Bad(string what) => Fail("RK2001", what + " handle is stale or null");

        private static string Info(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            if (f == null) return Bad("faction");
            Faction player = Faction.OfPlayer;
            var sb = new StringBuilder("{");
            sb.Append("\"name\":").Append(JsonLite.Quote(f.Name ?? ""));
            sb.Append(",\"def\":").Append(JsonLite.Quote(f.def?.defName ?? ""));
            sb.Append(",\"label\":").Append(JsonLite.Quote(f.def?.LabelCap.ToString() ?? ""));
            sb.Append(",\"is_player\":").Append(f.IsPlayer ? "true" : "false");
            sb.Append(",\"hidden\":").Append(f.Hidden ? "true" : "false");
            sb.Append(",\"defeated\":").Append(f.defeated ? "true" : "false");
            sb.Append(",\"temporary\":").Append(f.temporary ? "true" : "false");
            sb.Append(",\"permanent_enemy\":").Append(f.def != null && f.def.permanentEnemy ? "true" : "false");
            sb.Append(",\"tech_level\":").Append(JsonLite.Quote(f.def?.techLevel.ToString() ?? ""));
            if (!f.IsPlayer)
            {
                sb.Append(",\"goodwill\":").Append(f.GoodwillWith(player).ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"relation\":").Append(JsonLite.Quote(f.RelationKindWith(player).ToString()));
            }

            if (f.leader != null)
            {
                sb.Append(",\"leader\":").Append(HookCodec.Encode(f.leader));
            }

            sb.Append('}');
            return OkJson(sb.ToString());
        }

        private static string Goodwill(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            Faction o = Other(a);
            if (f == null || o == null) return Bad("faction");
            if (f == o) return Fail("RK1001", "a faction has no goodwill with itself");
            return OkInt(f.GoodwillWith(o));
        }

        private static string SetGoodwill(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            Faction o = Other(a);
            if (f == null || o == null) return Bad("faction");
            if (f == o) return Fail("RK1001", "pick two different factions");
            int target = Math.Max(-100, Math.Min(100, Int(a, "value")));
            // Written directly on both sides so the value is exact. TryAffectGoodwillWith would scale it by the difficulty setting.
            f.RelationWith(o).baseGoodwill = target;
            o.RelationWith(f).baseGoodwill = target;
            return OkInt(f.GoodwillWith(o));
        }

        private static string AdjustGoodwill(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            Faction o = Other(a);
            if (f == null || o == null) return Bad("faction");
            if (f == o) return Fail("RK1001", "pick two different factions");
            f.TryAffectGoodwillWith(o, Int(a, "delta"), false, false, null, null);
            return OkInt(f.GoodwillWith(o));
        }

        private static string Relation(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            Faction o = Other(a);
            if (f == null || o == null) return Bad("faction");
            if (f == o) return OkStr("Ally");
            return OkStr(f.RelationKindWith(o).ToString());
        }

        private static string Leader(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            if (f == null) return Bad("faction");
            return OkHandle(f.leader);
        }

        private static string Members(Dictionary<string, string> a)
        {
            Faction f = Of(a);
            if (f == null) return Bad("faction");
            var pawns = new List<Pawn>();
            foreach (Map map in Find.Maps)
            {
                pawns.AddRange(map.mapPawns.AllPawns.Where(p => p.Faction == f));
            }

            if (Find.WorldPawns != null)
            {
                pawns.AddRange(Find.WorldPawns.AllPawnsAlive.Where(p => p.Faction == f && !pawns.Contains(p)));
            }

            return OkHandles(pawns);
        }

        private static string FactionsWhere(Dictionary<string, string> a, Func<Faction, Faction, bool> keep)
        {
            Faction f = Of(a);
            if (f == null) return Bad("faction");
            var all = Find.FactionManager?.AllFactionsListForReading ?? new List<Faction>();
            return OkHandles(all.Where(o => o != f && keep(f, o)).ToList());
        }

        private static string Hostiles(Dictionary<string, string> a) => FactionsWhere(a, (f, o) => f.HostileTo(o));

        private static string Allies(Dictionary<string, string> a) => FactionsWhere(a, (f, o) => f.RelationKindWith(o) == FactionRelationKind.Ally);

        // Every faction definition in the loaded game, so a mod can pick one by name.
        private static string Defs(Dictionary<string, string> a)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (FactionDef d in DefDatabase<FactionDef>.AllDefsListForReading)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"def\":").Append(JsonLite.Quote(d.defName));
                sb.Append(",\"label\":").Append(JsonLite.Quote(d.label ?? d.defName));
                sb.Append(",\"hidden\":").Append(d.hidden ? "true" : "false");
                sb.Append(",\"permanent_enemy\":").Append(d.permanentEnemy ? "true" : "false");
                sb.Append(",\"tech_level\":").Append(JsonLite.Quote(d.techLevel.ToString())).Append('}');
            }

            sb.Append(']');
            return OkJson(sb.ToString());
        }
    }
}
