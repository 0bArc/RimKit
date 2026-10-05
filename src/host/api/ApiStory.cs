using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.storyteller, game.incidents (more), game.quests, game.research, game.stats. Ops: storyteller.*, incident.*, quest.*, research.*, stat.*.
    internal static class ApiStory
    {
        public static void Register()
        {
            R("storyteller.info", StorytellerInfo);
            R("storyteller.defs", StorytellerDefs);
            R("storyteller.set", SetStoryteller);
            R("storyteller.threat_points", ThreatPoints);
            R("storyteller.difficulty", DifficultyOf);
            R("storyteller.set_difficulty", SetDifficulty);
            R("incident.defs", IncidentDefs);
            R("incident.can_fire", CanFire);
            R("incident.fire", Fire);
            R("incident.queue", QueueIncident);
            R("quest.list", QuestList);
            R("quest.defs", QuestDefs);
            R("quest.generate", QuestGenerate);
            R("quest.accept", QuestAccept);
            R("quest.finish", QuestFinish);
            R("quest.signal", QuestSignal);
            R("research.list", ResearchList);
            R("research.info", ResearchInfo);
            R("research.current", ResearchCurrent);
            R("research.set_current", ResearchSetCurrent);
            R("research.finish", ResearchFinish);
            R("research.set_progress", ResearchSetProgress);
            R("research.knowledge", Knowledge);
            R("stat.value", StatValue);
            R("stat.explain", StatExplain);
            R("stat.defs", StatDefs);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static string NoGame() => Fail("RK3001", "no game is loaded");

        private static IIncidentTarget TargetFor(Dictionary<string, string> a)
        {
            Map m = MapOf(a);
            if (m != null) return m;
            return Find.CurrentMap ?? (IIncidentTarget)Find.World;
        }

        // ---- storyteller

        private static string StorytellerInfo(Dictionary<string, string> a)
        {
            Storyteller s = Find.Storyteller;
            if (s == null) return NoGame();
            var j = Jb.Obj().S("def", s.def.defName).S("label", s.def.label).S("difficulty", Find.Storyteller.difficultyDef?.defName);
            Map map = Find.CurrentMap;
            if (map != null)
            {
                j.F("threat_points", StorytellerUtility.DefaultThreatPointsNow(map)).F("wealth", map.wealthWatcher.WealthTotal)
                    .I("colonists", map.mapPawns.FreeColonistsCount);
            }

            return j.Ok();
        }

        private static string StorytellerDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (StorytellerDef d in DefDatabase<StorytellerDef>.AllDefsListForReading.Where(x => !x.listVisible || true))
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).B("visible", d.listVisible));
            return arr.Ok();
        }

        private static string SetStoryteller(Dictionary<string, string> a)
        {
            if (Find.Storyteller == null) return NoGame();
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown storyteller " + Str(a, "def"));
            Find.Storyteller.def = def;
            Find.Storyteller.Notify_DefChanged();
            return OkStr(def.defName);
        }

        private static string ThreatPoints(Dictionary<string, string> a)
        {
            if (Find.Storyteller == null) return NoGame();
            return OkFloat(StorytellerUtility.DefaultThreatPointsNow(TargetFor(a)));
        }

        private static IEnumerable<MemberInfo> DifficultyMembers()
        {
            Type t = typeof(Difficulty);
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool)) yield return f;
        }

        private static string DifficultyOf(Dictionary<string, string> a)
        {
            if (Find.Storyteller?.difficulty == null) return NoGame();
            var j = Jb.Obj();
            foreach (FieldInfo f in DifficultyMembers())
            {
                object v = f.GetValue(Find.Storyteller.difficulty);
                if (v is bool b) j.B(f.Name, b);
                else if (v is int i) j.I(f.Name, i);
                else if (v is float x) j.F(f.Name, x);
            }

            return j.Ok();
        }

        private static string SetDifficulty(Dictionary<string, string> a)
        {
            if (Find.Storyteller?.difficulty == null) return NoGame();
            FieldInfo f = DifficultyMembers().OfType<FieldInfo>().FirstOrDefault(x => x.Name == Str(a, "name"));
            if (f == null) return Fail("RK3001", "unknown difficulty setting " + Str(a, "name"));
            string raw = Str(a, "value");
            if (f.FieldType == typeof(bool)) f.SetValue(Find.Storyteller.difficulty, raw == "true" || raw == "1");
            else if (f.FieldType == typeof(int)) f.SetValue(Find.Storyteller.difficulty, (int)Math.Round(double.Parse(raw, CultureInfo.InvariantCulture)));
            else f.SetValue(Find.Storyteller.difficulty, float.Parse(raw, CultureInfo.InvariantCulture));
            return OkStr(f.GetValue(Find.Storyteller.difficulty).ToString());
        }

        // ---- incidents

        private static string IncidentDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (IncidentDef d in DefDatabase<IncidentDef>.AllDefsListForReading)
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("category", d.category?.defName).F("base_chance", d.baseChance)
                    .F("min_threat_points", d.minThreatPoints).B("pointless", d.pointsScaleable == false));
            return arr.Ok();
        }

        private static IncidentParms Parms(IncidentDef def, Dictionary<string, string> a)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, TargetFor(a));
            Opts o = Opts.From(a, "opts");
            if (o.Has("points")) parms.points = (float)o.Num("points");
            if (o.Has("faction")) parms.faction = o.Handle<Faction>("faction");
            if (o.Bool("forced")) parms.forced = true;
            return parms;
        }

        private static string CanFire(Dictionary<string, string> a)
        {
            if (Find.Storyteller == null) return NoGame();
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown incident " + Str(a, "def"));
            return OkBool(def.Worker.CanFireNow(Parms(def, a)));
        }

        // opts: points, faction, forced (fire even when the game says it cannot).
        private static string Fire(Dictionary<string, string> a)
        {
            if (Find.Storyteller == null) return NoGame();
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown incident " + Str(a, "def"));
            IncidentParms parms = Parms(def, a);
            return OkBool(def.Worker.TryExecute(parms));
        }

        // Queues an incident to fire after a number of ticks.
        private static string QueueIncident(Dictionary<string, string> a)
        {
            if (Find.Storyteller == null) return NoGame();
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown incident " + Str(a, "def"));
            IncidentParms parms = Parms(def, a);
            int ticks = Math.Max(1, Int(a, "ticks"));
            var qi = new QueuedIncident(new FiringIncident(def, null, parms), Find.TickManager.TicksGame + ticks);
            return OkBool(Find.Storyteller.incidentQueue.Add(qi));
        }

        // ---- quests

        private static Jb QuestJson(Quest q)
        {
            return Jb.Obj().I("id", q.id).S("name", q.name).S("state", q.State.ToString()).S("root", q.root?.defName)
                .F("challenge", q.challengeRating).I("age_ticks", q.TicksSinceAppeared).B("accepted", q.State == QuestState.Ongoing)
                .B("hidden", q.hidden).I("parts", q.PartsListForReading.Count);
        }

        private static string QuestList(Dictionary<string, string> a)
        {
            if (Find.QuestManager == null) return NoGame();
            var arr = Jb.Arr();
            string state = Str(a, "state");
            foreach (Quest q in Find.QuestManager.QuestsListForReading)
            {
                if (!string.IsNullOrEmpty(state) && !q.State.ToString().Equals(state, StringComparison.OrdinalIgnoreCase)) continue;
                arr.Add(QuestJson(q));
            }

            return arr.Ok();
        }

        private static string QuestDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<QuestScriptDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string QuestGenerate(Dictionary<string, string> a)
        {
            if (Find.QuestManager == null) return NoGame();
            QuestScriptDef root = DefDatabase<QuestScriptDef>.GetNamedSilentFail(Str(a, "root"));
            if (root == null) return Fail("RK3001", "unknown quest script " + Str(a, "root"));
            float points = string.IsNullOrEmpty(Str(a, "points")) ? StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap ?? (IIncidentTarget)Find.World) : Float(a, "points");
            Quest q = QuestUtility.GenerateQuestAndMakeAvailable(root, points);
            return q == null ? Fail("RK3001", "the quest could not be generated") : QuestJson(q).Ok();
        }

        private static Quest FindQuest(Dictionary<string, string> a) => Find.QuestManager?.QuestsListForReading.FirstOrDefault(q => q.id == Int(a, "id"));

        private static string QuestAccept(Dictionary<string, string> a)
        {
            Quest q = FindQuest(a);
            if (q == null) return Fail("RK3001", "no quest with id " + Str(a, "id"));
            if (q.State != QuestState.NotYetAccepted) return OkBool(false);
            Pawn who = ObjectHandles.Get<Pawn>(Int(a, "pawn")) ?? Find.AnyPlayerHomeMap?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            q.Accept(who);
            return OkBool(q.State == QuestState.Ongoing);
        }

        // outcome: Success, Fail, InvalidPreAcceptance, Unknown.
        private static string QuestFinish(Dictionary<string, string> a)
        {
            Quest q = FindQuest(a);
            if (q == null) return Fail("RK3001", "no quest with id " + Str(a, "id"));
            if (!Enum.TryParse(string.IsNullOrEmpty(Str(a, "outcome")) ? "Success" : Str(a, "outcome"), true, out QuestEndOutcome outcome))
                return Fail("RK1001", "outcome must be Success, Fail, InvalidPreAcceptance or Unknown");
            q.End(outcome, true);
            return OkStr(q.State.ToString());
        }

        private static string QuestSignal(Dictionary<string, string> a)
        {
            if (Find.SignalManager == null) return NoGame();
            if (string.IsNullOrEmpty(Str(a, "tag"))) return Fail("RK1001", "tag is required");
            Find.SignalManager.SendSignal(new Signal(Str(a, "tag")));
            return OkBool(true);
        }

        // ---- research

        private static Jb ResearchJson(ResearchProjectDef r)
        {
            var pre = Jb.Arr();
            if (r.prerequisites != null) foreach (var p in r.prerequisites) pre.AddS(p.defName);
            return Jb.Obj().S("def", r.defName).S("label", r.label).F("cost", r.CostApparent).F("progress", r.ProgressReal).F("progress_pct", r.ProgressPercent)
                .B("finished", r.IsFinished).B("can_start", r.CanStartNow).S("tab", r.tab?.defName).S("tech_level", r.techLevel.ToString())
                .Raw("prerequisites", pre.ToString()).I("techprints", r.TechprintCount).I("techprints_applied", r.TechprintsApplied);
        }

        private static string ResearchList(Dictionary<string, string> a)
        {
            string filter = Str(a, "filter");
            var arr = Jb.Arr();
            foreach (ResearchProjectDef r in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (filter == "finished" && !r.IsFinished) continue;
                if (filter == "available" && (r.IsFinished || !r.CanStartNow)) continue;
                if (filter == "locked" && (r.IsFinished || r.CanStartNow)) continue;
                arr.Add(ResearchJson(r));
            }

            return arr.Ok();
        }

        private static string ResearchInfo(Dictionary<string, string> a)
        {
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(Str(a, "def"));
            return r == null ? Fail("RK3001", "unknown research " + Str(a, "def")) : ResearchJson(r).Ok();
        }

        private static string ResearchCurrent(Dictionary<string, string> a)
        {
            if (Find.ResearchManager == null) return NoGame();
            ResearchProjectDef r = Find.ResearchManager.GetProject();
            return r == null ? OkJson("null") : ResearchJson(r).Ok();
        }

        private static string ResearchSetCurrent(Dictionary<string, string> a)
        {
            if (Find.ResearchManager == null) return NoGame();
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(Str(a, "def"));
            if (r == null) return Fail("RK3001", "unknown research " + Str(a, "def"));
            Find.ResearchManager.SetCurrentProject(r);
            return OkBool(Find.ResearchManager.GetProject() == r);
        }

        private static string ResearchFinish(Dictionary<string, string> a)
        {
            if (Find.ResearchManager == null) return NoGame();
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(Str(a, "def"));
            if (r == null) return Fail("RK3001", "unknown research " + Str(a, "def"));
            if (r.IsFinished) return OkBool(false);
            Find.ResearchManager.FinishProject(r, false, null, true);
            return OkBool(r.IsFinished);
        }

        private static string ResearchSetProgress(Dictionary<string, string> a)
        {
            if (Find.ResearchManager == null) return NoGame();
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(Str(a, "def"));
            if (r == null) return Fail("RK3001", "unknown research " + Str(a, "def"));
            var progress = AccessTools.Field(typeof(ResearchManager), "progress")?.GetValue(Find.ResearchManager) as Dictionary<ResearchProjectDef, float>;
            if (progress == null) return Fail("RK3001", "research progress is not available in this game version");
            progress[r] = Math.Max(0f, Math.Min(r.CostApparent, Float(a, "progress")));
            return OkFloat(r.ProgressReal);
        }

        // Anomaly knowledge: applies "add" points to a category (Basic or Advanced) when given, and returns the project they go to.
        private static string Knowledge(Dictionary<string, string> a)
        {
            if (!ModsConfig.AnomalyActive) return Fail("RK3003", "the Anomaly DLC is not active");
            KnowledgeCategoryDef cat = DefDatabase<KnowledgeCategoryDef>.GetNamedSilentFail(Str(a, "category"));
            if (cat == null) return Fail("RK3001", "unknown knowledge category " + Str(a, "category"));
            if (!string.IsNullOrEmpty(Str(a, "add"))) Find.ResearchManager.ApplyKnowledge(cat, Float(a, "add"));
            ResearchProjectDef current = Find.ResearchManager.GetProject(cat);
            return Jb.Obj().S("project", current?.defName).F("progress", current?.ProgressReal ?? 0).Ok();
        }

        // ---- stats

        private static StatDef Stat(Dictionary<string, string> a, out string err)
        {
            err = null;
            StatDef s = DefDatabase<StatDef>.GetNamedSilentFail(Str(a, "stat"));
            if (s == null) err = Fail("RK3001", "unknown stat " + Str(a, "stat"));
            return s;
        }

        private static string StatValue(Dictionary<string, string> a)
        {
            StatDef s = Stat(a, out string err);
            if (err != null) return err;
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            if (s.Worker.IsDisabledFor(t)) return OkJson("null");
            return OkFloat(t.GetStatValue(s, true));
        }

        private static string StatExplain(Dictionary<string, string> a)
        {
            StatDef s = Stat(a, out string err);
            if (err != null) return err;
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            StatRequest req = StatRequest.For(t);
            return Jb.Obj().F("value", s.Worker.IsDisabledFor(t) ? 0 : s.Worker.GetValue(req)).S("text", s.Worker.GetExplanationUnfinalized(req, s.toStringNumberSense))
                .S("final", s.Worker.GetStatDrawEntryLabel(s, s.Worker.GetValue(req), s.toStringNumberSense, req)).Ok();
        }

        private static string StatDefs(Dictionary<string, string> a)
        {
            string cat = Str(a, "category");
            var arr = Jb.Arr();
            foreach (StatDef s in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (!string.IsNullOrEmpty(cat) && s.category?.defName != cat) continue;
                arr.Add(Jb.Obj().S("def", s.defName).S("label", s.label).S("category", s.category?.defName).B("show", s.showOnPawns || s.showOnNonWorkTables));
            }

            return arr.Ok();
        }
    }
}
