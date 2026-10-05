using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;
using static RimKit.Dlc;

namespace RimKit
{
    // Anomaly depth (monolith, studies, containment, codex, creepjoiners, engagement) and Odyssey (gravships, planet layers). Ops: anomaly.*, odyssey.*.
    // The older anomaly.recruit, capture, knock_out and friends keep working.
    internal static class ApiAnomalyOdyssey
    {
        public static void Register()
        {
            const string An = "Anomaly";
            D(An, "anomaly.monolith", Monolith);
            D(An, "anomaly.set_monolith_level", SetMonolithLevel);
            D(An, "anomaly.study", Study);
            D(An, "anomaly.set_study", SetStudy);
            D(An, "anomaly.containment", Containment);
            D(An, "anomaly.platforms", Platforms);
            D(An, "anomaly.codex", Codex);
            D(An, "anomaly.creepjoiners", Creepjoiners);
            D(An, "anomaly.engagement", Engagement);
            D(An, "anomaly.set_engagement", SetEngagement);
            D(An, "anomaly.set_pawn_engagement", SetPawnEngagement);

            const string Od = "Odyssey";
            D(Od, "odyssey.layers", Layers);
            D(Od, "odyssey.engines", Engines);
            D(Od, "odyssey.gravship", Gravship);
        }

        // ---- Anomaly

        private static string Monolith(Dictionary<string, string> a)
        {
            GameComponent_Anomaly an = Verse.Find.Anomaly;
            if (an == null) return Fail("RK3001", "no game is loaded");
            return Jb.Obj().I("level", an.Level).I("highest_level", an.HighestLevelReached).S("level_def", an.LevelDef?.defName).S("next_level_def", an.NextLevelDef?.defName)
                .B("spawned", an.MonolithSpawned).B("study_completed", an.MonolithStudyCompleted).B("questline_ended", an.QuestlineEnded).Ok();
        }

        private static string SetMonolithLevel(Dictionary<string, string> a)
        {
            GameComponent_Anomaly an = Verse.Find.Anomaly;
            if (an == null) return Fail("RK3001", "no game is loaded");
            int level = Int(a, "level");
            if (level < 0 || level > 6) return Fail("RK1001", "level must be 0 to 6");
            MonolithLevelDef def = DefDatabase<MonolithLevelDef>.AllDefsListForReading.FirstOrDefault(d => d.level == level);
            if (def == null) return Fail("RK3001", "no monolith level " + level);
            an.SetLevel(def, false);
            return OkInt(an.Level);
        }

        private static CompStudiable Studiable(Dictionary<string, string> a, out string err)
        {
            err = null;
            Thing t = ThingOf(a);
            if (t == null) { err = Fail("RK2001", "thing handle is stale or null"); return null; }
            var c = (t as ThingWithComps)?.GetComp<CompStudiable>();
            if (c == null) err = Fail("RK3003", t.def.defName + " cannot be studied");
            return c;
        }

        private static string Study(Dictionary<string, string> a)
        {
            CompStudiable c = Studiable(a, out string err);
            if (err != null) return err;
            return Jb.Obj().B("enabled", c.studyEnabled).F("progress", c.ProgressPercent).B("completed", c.Completed).S("category", c.KnowledgeCategory?.defName)
                .B("needs_platform", c.RequiresHoldingPlatform).B("needs_prisoner", c.RequiresImprisonment).F("knowledge_gained", c.anomalyKnowledgeGained).Ok();
        }

        // opts: enabled, progress (0 to 1).
        private static string SetStudy(Dictionary<string, string> a)
        {
            CompStudiable c = Studiable(a, out string err);
            if (err != null) return err;
            Opts o = Opts.From(a, "opts");
            if (o.Has("enabled")) c.SetStudyEnabled(o.Bool("enabled"));
            if (o.Has("progress"))
            {
                // The study amount differs per entity, so measure it: one point gives a known fraction.
                var points = AccessTools.Field(typeof(CompStudiable), "studyPoints");
                if (points != null)
                {
                    points.SetValue(c, 1f);
                    float fraction = c.ProgressPercent;
                    float total = fraction > 0f ? 1f / fraction : 0f;
                    points.SetValue(c, total > 0f ? (float)Math.Max(0, Math.Min(1, o.Num("progress"))) * total : 0f);
                }
            }

            return OkFloat(c.ProgressPercent);
        }

        private static string Containment(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            var c = (t as ThingWithComps)?.GetComp<CompHoldingPlatformTarget>();
            if (c == null) return Fail("RK3003", t.def.defName + " is not an entity that can be held");
            var j = Jb.Obj().S("mode", c.containmentMode.ToString()).B("held", c.CurrentlyHeldOnPlatform).B("can_be_captured", c.CanBeCaptured).B("can_study", c.CanStudy).B("extract_bioferrite", c.extractBioferrite);
            if (c.HeldPlatform != null) j.H("platform", c.HeldPlatform);
            return j.Ok();
        }

        private static string Platforms(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var arr = Jb.Arr();
            foreach (Building_HoldingPlatform p in map.listerBuildings.allBuildingsColonist.OfType<Building_HoldingPlatform>())
            {
                var j = Jb.Obj().H("platform", p).B("occupied", p.Occupied);
                if (p.HeldPawn != null) j.H("held", p.HeldPawn);
                arr.Add(j);
            }

            return arr.Ok();
        }

        private static string Codex(Dictionary<string, string> a)
        {
            if (Verse.Find.EntityCodex == null) return Fail("RK3001", "no game is loaded");
            var arr = Jb.Arr();
            foreach (EntityCodexEntryDef d in DefDatabase<EntityCodexEntryDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("category", d.category?.defName).B("discovered", Verse.Find.EntityCodex.Discovered(d)));
            return arr.Ok();
        }

        private static string Creepjoiners(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            FieldInfo field = AccessTools.Field(typeof(Pawn), "creepjoiner");
            if (field == null) return Fail("RK3003", "creepjoiners are not available on this game version");
            return OkHandles(map.mapPawns.AllPawns.Where(p => field.GetValue(p) != null).ToList());
        }

        // How recruited and controlled pawns pick fights. This is the setting the Tame Anomalies mod relied on, now tunable.
        private static string Engagement(Dictionary<string, string> a)
        {
            return Jb.Obj().B("enabled", Engage.Enabled).F("range", Engage.Range).I("interval", Engage.Interval).B("auto_draft", Engage.AutoDraft).F("melee_range", Engage.MeleeRange)
                .I("disabled_pawns", Engage.Disabled.Count).Ok();
        }

        // opts: enabled, range (cells to look for hostiles), interval (ticks between checks, 5 to 600), auto_draft, melee_range.
        private static string SetEngagement(Dictionary<string, string> a)
        {
            Opts o = Opts.From(a, "opts");
            if (o.Has("enabled")) Engage.Enabled = o.Bool("enabled");
            if (o.Has("range")) Engage.Range = (float)Math.Max(5, Math.Min(150, o.Num("range")));
            if (o.Has("interval")) Engage.Interval = Math.Max(5, Math.Min(600, o.Int("interval")));
            if (o.Has("auto_draft")) Engage.AutoDraft = o.Bool("auto_draft");
            if (o.Has("melee_range")) Engage.MeleeRange = (float)Math.Max(1, Math.Min(60, o.Num("melee_range")));
            return Engagement(a);
        }

        private static string SetPawnEngagement(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Fail("RK2001", "pawn handle is stale or null");
            if (Bool(a, "enabled")) Engage.Disabled.Remove(p.thingIDNumber); else Engage.Disabled.Add(p.thingIDNumber);
            return OkBool(!Engage.Disabled.Contains(p.thingIDNumber));
        }

        // ---- Odyssey

        private static string Layers(Dictionary<string, string> a)
        {
            Type t = GenTypes.GetTypeInAnyAssembly("RimWorld.Planet.PlanetLayerDef") ?? GenTypes.GetTypeInAnyAssembly("PlanetLayerDef");
            if (t == null) return Fail("RK3003", "planet layers are not available on this game version");
            var db = typeof(DefDatabase<>).MakeGenericType(t);
            var list = AccessTools.Property(db, "AllDefsListForReading")?.GetValue(null, null) as System.Collections.IEnumerable;
            var arr = Jb.Arr();
            foreach (Def d in list.Cast<Def>()) arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label));
            return arr.Ok();
        }

        private static string Engines(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            Type engine = GenTypes.GetTypeInAnyAssembly("RimWorld.Building_GravEngine") ?? GenTypes.GetTypeInAnyAssembly("Building_GravEngine");
            if (engine == null) return Fail("RK3003", "gravships are not available on this game version");
            var arr = Jb.Arr();
            foreach (Building b in map.listerBuildings.allBuildingsColonist.Where(x => engine.IsInstanceOfType(x))) arr.Add(Jb.Obj().H("engine", b).S("def", b.def.defName).I("x", b.Position.x).I("z", b.Position.z));
            return arr.Ok();
        }

        private static string Gravship(Dictionary<string, string> a)
        {
            object ship = AccessTools.Property(typeof(Verse.Find), "CurrentGravship")?.GetValue(null, null);
            return ship == null ? OkJson("null") : Jb.Obj().S("class", ship.GetType().Name).S("name", (AccessTools.Field(ship.GetType(), "name") ?? (MemberInfo)null)?.ToString()).Ok();
        }
    }
}
