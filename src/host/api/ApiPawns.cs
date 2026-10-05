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
    // game.pawns kit: skills, needs, traits, thoughts, relations, backstory, capacities, timetable, genes.
    // Op ids use the singular domain (pawn.*). Every op returns a stable RK code on failure.
    internal static class ApiPawns
    {
        public static void Register()
        {
            // skills
            ApiRegistry.Register("pawn.skills", Skills);
            ApiRegistry.Register("pawn.skill_info", SkillInfo);
            ApiRegistry.Register("pawn.set_passion", SetPassion);
            ApiRegistry.Register("pawn.add_skill_xp", AddSkillXp);
            // needs
            ApiRegistry.Register("pawn.needs", Needs);
            ApiRegistry.Register("pawn.need", NeedLevel);
            ApiRegistry.Register("pawn.set_need", SetNeed);
            // traits (add_trait replaces the degree 0 legacy op: the registry is consulted first)
            ApiRegistry.Register("pawn.traits", Traits);
            ApiRegistry.Register("pawn.has_trait", HasTrait);
            ApiRegistry.Register("pawn.add_trait", AddTrait);
            ApiRegistry.Register("pawn.remove_trait", RemoveTrait);
            // thoughts
            ApiRegistry.Register("pawn.thoughts", Thoughts);
            ApiRegistry.Register("pawn.memories", Memories);
            ApiRegistry.Register("pawn.add_thought", AddThought);
            ApiRegistry.Register("pawn.remove_thought", RemoveThought);
            ApiRegistry.Register("pawn.opinion_of", OpinionOf);
            // relations
            ApiRegistry.Register("pawn.relations", Relations);
            ApiRegistry.Register("pawn.has_relation", HasRelation);
            ApiRegistry.Register("pawn.add_relation", AddRelation);
            ApiRegistry.Register("pawn.remove_relation", RemoveRelation);
            // backstory
            ApiRegistry.Register("pawn.backstory", Backstory);
            ApiRegistry.Register("pawn.set_backstory", SetBackstory);
            // capacities
            ApiRegistry.Register("pawn.capacities", Capacities);
            ApiRegistry.Register("pawn.capacity", Capacity);
            // timetable
            ApiRegistry.Register("pawn.timetable", Timetable);
            ApiRegistry.Register("pawn.set_assignment", SetAssignment);
            // genes (Biotech)
            ApiRegistry.Register("pawn.genes", Genes);
            ApiRegistry.Register("pawn.has_gene", HasGene);
            ApiRegistry.Register("pawn.add_gene", AddGene);
            ApiRegistry.Register("pawn.remove_gene", RemoveGene);
            ApiRegistry.Register("pawn.xenotype", Xenotype);
            // translation for mod strings (Languages/<lang>/Keyed)
            ApiRegistry.Register("ui.translate", Translate);
        }

        // ---------------------------------------------------------------- helpers

        private static Pawn RequirePawn(Dictionary<string, string> args, out string error)
        {
            error = null;
            Pawn p = PawnOf(args);
            if (p == null)
            {
                error = Fail("RK2001", "pawn handle " + Int(args, "h") + " is stale or null");
            }

            return p;
        }

        private static string Missing(string what, string name) => Fail("RK3001", what + " not found: " + name);

        private static T FindDef<T>(string name) where T : Def =>
            string.IsNullOrEmpty(name) ? null : DefDatabase<T>.GetNamedSilentFail(name);

        // Writes {"k":v,...} with values encoded by HookCodec (defs as names, pawns as handles, floats safely).
        private static string Row(params object[] keyValues)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i + 1 < keyValues.Length; i += 2)
            {
                if (i > 0) sb.Append(',');
                Json.WriteString(sb, (string)keyValues[i]);
                sb.Append(':');
                HookCodec.Encode(sb, keyValues[i + 1], 0);
            }

            return sb.Append('}').ToString();
        }

        private static string List(IEnumerable<string> rows) => OkJson("[" + string.Join(",", rows) + "]");

        // ---------------------------------------------------------------- skills

        private static string SkillRow(SkillRecord s) => Row(
            "def", s.def.defName,
            "label", s.def.skillLabel.ToString(),
            "level", s.Level,
            "passion", s.passion.ToString(),
            "xp", s.xpSinceLastLevel,
            "xp_required", s.XpRequiredForLevelUp,
            "disabled", s.TotallyDisabled);

        private static string Skills(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.skills == null) return Fail("RK3001", "pawn has no skills (not humanlike)");
            return List(p.skills.skills.Select(SkillRow));
        }

        private static SkillRecord FindSkill(Dictionary<string, string> args, out Pawn pawn, out string error)
        {
            pawn = RequirePawn(args, out error);
            if (pawn == null) return null;
            if (pawn.skills == null)
            {
                error = Fail("RK3001", "pawn has no skills (not humanlike)");
                return null;
            }

            string name = Str(args, "skill");
            SkillDef def = FindDef<SkillDef>(name);
            SkillRecord record = def == null ? null : pawn.skills.GetSkill(def);
            if (record == null) error = Missing("skill", name);
            return record;
        }

        private static string SkillInfo(Dictionary<string, string> args)
        {
            SkillRecord s = FindSkill(args, out _, out string error);
            return s == null ? error : OkJson(SkillRow(s));
        }

        private static string SetPassion(Dictionary<string, string> args)
        {
            SkillRecord s = FindSkill(args, out _, out string error);
            if (s == null) return error;
            string raw = Str(args, "passion");
            if (!Enum.TryParse(raw, true, out Passion passion) || !Enum.IsDefined(typeof(Passion), passion))
            {
                return Fail("RK1001", "passion must be None, Minor or Major, got " + raw);
            }

            s.passion = passion;
            return OkBool(true);
        }

        private static string AddSkillXp(Dictionary<string, string> args)
        {
            SkillRecord s = FindSkill(args, out _, out string error);
            if (s == null) return error;
            s.Learn(Float(args, "amount"), Bool(args, "direct"));
            return OkInt(s.Level);
        }

        // ---------------------------------------------------------------- needs

        private static string Needs(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.needs == null) return Fail("RK3001", "pawn has no needs");
            return List(p.needs.AllNeeds.Select(n => Row(
                "def", n.def.defName,
                "label", n.LabelCap.ToString(),
                "level", n.CurLevelPercentage)));
        }

        private static Need FindNeed(Dictionary<string, string> args, out string error)
        {
            Pawn p = RequirePawn(args, out error);
            if (p == null) return null;
            if (p.needs == null)
            {
                error = Fail("RK3001", "pawn has no needs");
                return null;
            }

            string name = Str(args, "def");
            NeedDef def = FindDef<NeedDef>(name);
            Need need = def == null ? null : p.needs.TryGetNeed(def);
            if (need == null) error = Missing("need", name);
            return need;
        }

        private static string NeedLevel(Dictionary<string, string> args)
        {
            Need n = FindNeed(args, out string error);
            return n == null ? error : OkFloat(n.CurLevelPercentage);
        }

        private static string SetNeed(Dictionary<string, string> args)
        {
            Need n = FindNeed(args, out string error);
            if (n == null) return error;
            n.CurLevelPercentage = Math.Max(0f, Math.Min(1f, Float(args, "level")));
            return OkBool(true);
        }

        // ---------------------------------------------------------------- traits

        private static string Traits(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.story?.traits == null) return Fail("RK3001", "pawn has no traits");
            return List(p.story.traits.allTraits.Select(t => Row(
                "def", t.def.defName,
                "degree", t.Degree,
                "label", t.LabelCap.ToString())));
        }

        private static string HasTrait(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.story?.traits == null) return OkBool(false);
            TraitDef def = FindDef<TraitDef>(Str(args, "def"));
            if (def == null) return Missing("trait", Str(args, "def"));
            return OkBool(args.ContainsKey("degree") ? p.story.traits.HasTrait(def, Int(args, "degree")) : p.story.traits.HasTrait(def));
        }

        private static string AddTrait(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.story?.traits == null) return Fail("RK3001", "pawn has no traits");
            TraitDef def = FindDef<TraitDef>(Str(args, "def"));
            if (def == null) return Missing("trait", Str(args, "def"));
            int degree = Int(args, "degree");
            if (def.degreeDatas != null && def.degreeDatas.Count > 0 && !def.degreeDatas.Any(d => d.degree == degree))
            {
                string valid = string.Join(", ", def.degreeDatas.Select(d => d.degree.ToString(CultureInfo.InvariantCulture)));
                return Fail("RK1001", def.defName + " has no degree " + degree + ". Valid: " + valid);
            }

            if (p.story.traits.HasTrait(def))
            {
                return OkBool(false);
            }

            p.story.traits.GainTrait(new Trait(def, degree, false));
            return OkBool(true);
        }

        private static string RemoveTrait(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.story?.traits == null) return OkBool(false);
            TraitDef def = FindDef<TraitDef>(Str(args, "def"));
            if (def == null) return Missing("trait", Str(args, "def"));
            Trait trait = p.story.traits.GetTrait(def);
            if (trait == null) return OkBool(false);
            p.story.traits.RemoveTrait(trait);
            return OkBool(true);
        }

        // ---------------------------------------------------------------- thoughts

        private static string Thoughts(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            ThoughtHandler handler = p.needs?.mood?.thoughts;
            if (handler == null) return Fail("RK3001", "pawn has no mood");
            var all = new List<Thought>();
            handler.GetAllMoodThoughts(all);
            return List(all.Select(t => Row(
                "def", t.def.defName,
                "label", t.LabelCap.ToString(),
                "mood_offset", t.MoodOffset())));
        }

        private static string Memories(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            MemoryThoughtHandler memories = p.needs?.mood?.thoughts?.memories;
            if (memories == null) return Fail("RK3001", "pawn has no mood");
            return List(memories.Memories.Select(m => Row(
                "def", m.def.defName,
                "label", m.LabelCap.ToString(),
                "age", m.age,
                "mood_offset", m.MoodOffset(),
                "other", m.otherPawn)));
        }

        private static string AddThought(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            MemoryThoughtHandler memories = p.needs?.mood?.thoughts?.memories;
            if (memories == null) return Fail("RK3001", "pawn has no mood");
            ThoughtDef def = FindDef<ThoughtDef>(Str(args, "def"));
            if (def == null) return Missing("thought", Str(args, "def"));
            Pawn other = args.ContainsKey("other") ? ObjectHandles.Get<Pawn>(Int(args, "other")) : null;
            memories.TryGainMemory(def, other);
            return OkBool(true);
        }

        private static string RemoveThought(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            MemoryThoughtHandler memories = p.needs?.mood?.thoughts?.memories;
            if (memories == null) return Fail("RK3001", "pawn has no mood");
            ThoughtDef def = FindDef<ThoughtDef>(Str(args, "def"));
            if (def == null) return Missing("thought", Str(args, "def"));
            memories.RemoveMemoriesOfDef(def);
            return OkBool(true);
        }

        private static string OpinionOf(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            Pawn other = ObjectHandles.Get<Pawn>(Int(args, "other"));
            if (other == null) return Fail("RK2001", "other pawn handle is stale or null");
            if (p.relations == null) return Fail("RK3001", "pawn has no relations");
            return OkInt(p.relations.OpinionOf(other));
        }

        // ---------------------------------------------------------------- relations

        private static string Relations(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.relations == null) return Fail("RK3001", "pawn has no relations");
            return List(p.relations.DirectRelations.Select(r => Row("def", r.def.defName, "other", r.otherPawn)));
        }

        private static bool RelationArgs(Dictionary<string, string> args, out Pawn p, out Pawn other, out PawnRelationDef def, out string error)
        {
            other = null;
            def = null;
            p = RequirePawn(args, out error);
            if (p == null) return false;
            if (p.relations == null)
            {
                error = Fail("RK3001", "pawn has no relations");
                return false;
            }

            other = ObjectHandles.Get<Pawn>(Int(args, "other"));
            if (other == null)
            {
                error = Fail("RK2001", "other pawn handle is stale or null");
                return false;
            }

            def = FindDef<PawnRelationDef>(Str(args, "def"));
            if (def == null)
            {
                error = Missing("relation", Str(args, "def"));
                return false;
            }

            return true;
        }

        private static string HasRelation(Dictionary<string, string> args)
        {
            return RelationArgs(args, out Pawn p, out Pawn other, out PawnRelationDef def, out string error)
                ? OkBool(p.relations.DirectRelationExists(def, other))
                : error;
        }

        private static string AddRelation(Dictionary<string, string> args)
        {
            if (!RelationArgs(args, out Pawn p, out Pawn other, out PawnRelationDef def, out string error)) return error;
            if (p.relations.DirectRelationExists(def, other)) return OkBool(false);
            p.relations.AddDirectRelation(def, other);
            return OkBool(true);
        }

        private static string RemoveRelation(Dictionary<string, string> args)
        {
            if (!RelationArgs(args, out Pawn p, out Pawn other, out PawnRelationDef def, out string error)) return error;
            return OkBool(p.relations.TryRemoveDirectRelation(def, other));
        }

        // ---------------------------------------------------------------- backstory

        private static string Backstory(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.story == null) return Fail("RK3001", "pawn has no story");
            return OkJson(Row(
                "childhood", p.story.Childhood?.defName,
                "childhood_title", p.story.Childhood?.title,
                "adulthood", p.story.Adulthood?.defName,
                "adulthood_title", p.story.Adulthood?.title));
        }

        private static string SetBackstory(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.story == null) return Fail("RK3001", "pawn has no story");
            BackstoryDef def = FindDef<BackstoryDef>(Str(args, "def"));
            if (def == null) return Missing("backstory", Str(args, "def"));
            string slot = Str(args, "slot").ToLowerInvariant();
            if (slot == "childhood") p.story.Childhood = def;
            else if (slot == "adulthood") p.story.Adulthood = def;
            else return Fail("RK1001", "slot must be childhood or adulthood, got " + slot);
            return OkBool(true);
        }

        // ---------------------------------------------------------------- capacities

        private static string Capacities(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.health?.capacities == null) return Fail("RK3001", "pawn has no health");
            return List(DefDatabase<PawnCapacityDef>.AllDefsListForReading.Select(d => Row(
                "def", d.defName,
                "level", p.health.capacities.GetLevel(d),
                "capable", p.health.capacities.CapableOf(d))));
        }

        private static string Capacity(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.health?.capacities == null) return Fail("RK3001", "pawn has no health");
            PawnCapacityDef def = FindDef<PawnCapacityDef>(Str(args, "def"));
            if (def == null) return Missing("capacity", Str(args, "def"));
            return OkFloat(p.health.capacities.GetLevel(def));
        }

        // ---------------------------------------------------------------- timetable

        private static string Timetable(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.timetable == null) return Fail("RK3001", "pawn has no timetable (not a colonist)");
            var names = new List<string>();
            for (int hour = 0; hour < 24; hour++)
            {
                names.Add(JsonLite.Quote(p.timetable.GetAssignment(hour).defName));
            }

            return OkJson("[" + string.Join(",", names) + "]");
        }

        private static string SetAssignment(Dictionary<string, string> args)
        {
            Pawn p = RequirePawn(args, out string error);
            if (p == null) return error;
            if (p.timetable == null) return Fail("RK3001", "pawn has no timetable (not a colonist)");
            int hour = Int(args, "hour");
            if (hour < 0 || hour > 23) return Fail("RK1001", "hour must be 0 to 23, got " + hour);
            TimeAssignmentDef def = FindDef<TimeAssignmentDef>(Str(args, "def"));
            if (def == null) return Missing("time assignment", Str(args, "def"));
            p.timetable.SetAssignment(hour, def);
            return OkBool(true);
        }

        // ---------------------------------------------------------------- genes (Biotech)

        private static Pawn_GeneTracker RequireGenes(Dictionary<string, string> args, out Pawn pawn, out string error)
        {
            error = null;
            pawn = RequirePawn(args, out error);
            if (pawn == null) return null;
            if (!ModsConfig.BiotechActive)
            {
                error = Fail("RK3003", "Biotech is not active");
                return null;
            }

            if (pawn.genes == null) error = Fail("RK3001", "pawn has no genes");
            return pawn.genes;
        }

        private static string Genes(Dictionary<string, string> args)
        {
            Pawn_GeneTracker genes = RequireGenes(args, out _, out string error);
            if (genes == null) return error;
            return List(genes.GenesListForReading.Select(g => Row(
                "def", g.def.defName,
                "label", g.LabelCap.ToString(),
                "active", g.Active,
                "xenogene", genes.Xenogenes.Contains(g))));
        }

        private static string HasGene(Dictionary<string, string> args)
        {
            Pawn_GeneTracker genes = RequireGenes(args, out _, out string error);
            if (genes == null) return error;
            GeneDef def = FindDef<GeneDef>(Str(args, "def"));
            if (def == null) return Missing("gene", Str(args, "def"));
            return OkBool(genes.HasGene(def));
        }

        private static string AddGene(Dictionary<string, string> args)
        {
            Pawn_GeneTracker genes = RequireGenes(args, out _, out string error);
            if (genes == null) return error;
            GeneDef def = FindDef<GeneDef>(Str(args, "def"));
            if (def == null) return Missing("gene", Str(args, "def"));
            if (genes.HasGene(def)) return OkBool(false);
            genes.AddGene(def, Bool(args, "xenogene"));
            return OkBool(true);
        }

        private static string RemoveGene(Dictionary<string, string> args)
        {
            Pawn_GeneTracker genes = RequireGenes(args, out _, out string error);
            if (genes == null) return error;
            GeneDef def = FindDef<GeneDef>(Str(args, "def"));
            if (def == null) return Missing("gene", Str(args, "def"));
            Gene gene = genes.GetGene(def);
            if (gene == null) return OkBool(false);
            genes.RemoveGene(gene);
            return OkBool(true);
        }

        private static string Xenotype(Dictionary<string, string> args)
        {
            Pawn_GeneTracker genes = RequireGenes(args, out _, out string error);
            if (genes == null) return error;
            return OkJson(Row("def", genes.Xenotype?.defName, "label", genes.XenotypeLabel));
        }

        // ---------------------------------------------------------------- translate

        // ui.translate: key plus optional a0..a3 for {0}..{3}. An unknown key returns the fallback or the key itself.
        private static string Translate(Dictionary<string, string> args)
        {
            string key = Str(args, "key");
            string fallback = args.ContainsKey("fallback") ? Str(args, "fallback") : key;
            string text;
            if (!string.IsNullOrEmpty(key) && key.TryTranslate(out TaggedString translated))
            {
                text = translated.RawText;
            }
            else
            {
                text = fallback;
            }

            for (int i = 0; i < 4; i++)
            {
                string name = "a" + i.ToString(CultureInfo.InvariantCulture);
                if (args.ContainsKey(name))
                {
                    text = text.Replace("{" + i.ToString(CultureInfo.InvariantCulture) + "}", Str(args, name));
                }
            }

            return OkStr(text);
        }
    }
}
