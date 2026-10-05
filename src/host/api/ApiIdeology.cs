using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;
using static RimKit.Dlc;

namespace RimKit
{
    // game.ideology (Ideology DLC): ideoligions, memes, precepts, roles, rituals, certainty. Ops are ideology.*.
    internal static class ApiIdeology
    {
        private const string Who = "Ideology";

        public static void Register()
        {
            D(Who, "ideology.list", List);
            D(Who, "ideology.info", Info);
            D(Who, "ideology.of_pawn", OfPawn);
            D(Who, "ideology.set_pawn_ideo", SetPawnIdeo);
            D(Who, "ideology.certainty", Certainty);
            D(Who, "ideology.set_certainty", SetCertainty);
            D(Who, "ideology.add_precept", AddPrecept);
            D(Who, "ideology.remove_precept", RemovePrecept);
            D(Who, "ideology.assign_role", AssignRole);
            D(Who, "ideology.unassign_role", UnassignRole);
            D(Who, "ideology.memes", Memes);
            D(Who, "ideology.precept_defs", PreceptDefs);
            D(Who, "ideology.rituals", Rituals);
            D(Who, "ideology.add_meme", AddMeme);
            D(Who, "ideology.remove_meme", RemoveMeme);
            D(Who, "ideology.style_defs", StyleDefs);
            D(Who, "ideology.styles", Styles);
            D(Who, "ideology.set_style", SetStyle);
            D(Who, "ideology.remove_style", RemoveStyle);
            D(Who, "ideology.rename", Rename);
        }

        private static Ideo Find_(Dictionary<string, string> a)
        {
            int id = Int(a, "id");
            return Verse.Find.IdeoManager?.IdeosListForReading.FirstOrDefault(i => i.id == id);
        }

        private static string BadIdeo() => Fail("RK3001", "no ideoligion with that id");

        private static Jb Brief(Ideo i)
        {
            var memes = Jb.Arr();
            foreach (MemeDef m in i.memes) memes.AddS(m.defName);
            int believers = Verse.Find.Maps.SelectMany(m => m.mapPawns.AllPawns).Count(p => p.Ideo == i);
            return Jb.Obj().I("id", i.id).S("name", i.name).S("culture", i.culture?.defName).Raw("memes", memes.ToString()).I("pawns", believers).I("precepts", i.PreceptsListForReading.Count);
        }

        private static string List(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (Ideo i in Verse.Find.IdeoManager.IdeosListForReading) arr.Add(Brief(i));
            return arr.Ok();
        }

        private static string Info(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            var precepts = Jb.Arr();
            var roles = Jb.Arr();
            foreach (Precept p in i.PreceptsListForReading)
            {
                precepts.Add(Jb.Obj().S("def", p.def.defName).S("label", p.LabelCap).S("issue", p.def.issue?.defName).S("class", p.GetType().Name));
                if (p is Precept_Role role)
                {
                    var r = Jb.Obj().S("def", p.def.defName).S("label", p.LabelCap).B("assigned", role.ChosenPawnSingle() != null);
                    if (role.ChosenPawnSingle() != null) r.H("pawn", role.ChosenPawnSingle());
                    roles.Add(r);
                }
            }

            return Brief(i).Raw("precept_list", precepts.ToString()).Raw("roles", roles.ToString()).Ok();
        }

        private static string OfPawn(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Fail("RK2001", "pawn handle is stale or null");
            return p.Ideo == null ? OkJson("null") : Brief(p.Ideo).F("certainty", p.ideo?.Certainty ?? 0).Ok();
        }

        private static string SetPawnIdeo(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ideo == null) return Fail("RK3003", "this pawn has no ideoligion");
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            p.ideo.SetIdeo(i);
            return OkBool(p.Ideo == i);
        }

        private static string Certainty(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ideo == null) return Fail("RK3003", "this pawn has no ideoligion");
            return OkFloat(p.ideo.Certainty);
        }

        private static string SetCertainty(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ideo == null) return Fail("RK3003", "this pawn has no ideoligion");
            float target = Math.Max(0f, Math.Min(1f, Float(a, "value")));
            p.ideo.OffsetCertainty(target - p.ideo.Certainty);
            return OkFloat(p.ideo.Certainty);
        }

        private static string AddPrecept(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            PreceptDef def = DefDatabase<PreceptDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown precept " + Str(a, "def"));
            if (i.PreceptsListForReading.Any(p => p.def == def)) return OkBool(false);
            Precept precept = PreceptMaker.MakePrecept(def);
            i.AddPrecept(precept, true);
            return OkBool(i.PreceptsListForReading.Contains(precept));
        }

        private static string RemovePrecept(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            Precept p = i.PreceptsListForReading.FirstOrDefault(x => x.def.defName == Str(a, "def"));
            if (p == null) return OkBool(false);
            i.RemovePrecept(p);
            return OkBool(true);
        }

        // ---- memes and styles

        private static MemeDef MemeOf(Dictionary<string, string> a) => DefDatabase<MemeDef>.GetNamedSilentFail(Str(a, "def"));

        // Changes the meme list the way the ideoligion editor does: precepts that conflict with the new memes are removed first.
        // Precepts a new meme requires are not added, use ideology.add_precept for those.
        private static void ApplyMemes(Ideo i, List<MemeDef> next)
        {
            List<MemeDef> old = new List<MemeDef>(i.memes);
            if (i.foundation != null)
            {
                foreach (Precept p in i.foundation.GetPreceptsToRemoveFromMemeChanges(old, next).ToList()) i.RemovePrecept(p, true);
            }
            i.memes.Clear();
            i.memes.AddRange(next);
            i.SortMemesInDisplayOrder();
            i.RecachePrecepts();
            i.RegenerateDescription(false);
        }

        private static string AddMeme(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            MemeDef meme = MemeOf(a);
            if (meme == null) return Fail("RK3001", "unknown meme " + Str(a, "def"));
            if (i.memes.Contains(meme)) return OkBool(false);
            foreach (MemeDef have in i.memes)
            {
                if (have.exclusionTags != null && meme.exclusionTags != null && have.exclusionTags.Intersect(meme.exclusionTags).Any())
                    return Fail("RK1001", "meme " + meme.defName + " conflicts with " + have.defName + ". Remove that meme first.");
                if (have.category == MemeCategory.Structure && meme.category == MemeCategory.Structure)
                    return Fail("RK1001", "an ideoligion has one structure meme, " + have.defName + ". Remove it first.");
            }
            ApplyMemes(i, i.memes.Concat(new[] { meme }).ToList());
            return OkBool(i.memes.Contains(meme));
        }

        private static string RemoveMeme(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            MemeDef meme = i.memes.FirstOrDefault(m => m.defName == Str(a, "def"));
            if (meme == null) return OkBool(false);
            ApplyMemes(i, i.memes.Where(m => m != meme).ToList());
            return OkBool(!i.memes.Contains(meme));
        }

        private static string StyleDefs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (StyleCategoryDef s in DefDatabase<StyleCategoryDef>.AllDefsListForReading) arr.Add(Jb.Obj().S("def", s.defName).S("label", s.label));
            return arr.Ok();
        }

        private static string Styles(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            var arr = Jb.Arr();
            if (i.thingStyleCategories != null)
                foreach (ThingStyleCategoryWithPriority s in i.thingStyleCategories) arr.Add(Jb.Obj().S("def", s.category.defName).S("label", s.category.label).F("priority", s.priority));
            return arr.Ok();
        }

        private static string SetStyle(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            StyleCategoryDef def = DefDatabase<StyleCategoryDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown style category " + Str(a, "def"));
            float priority = string.IsNullOrEmpty(Str(a, "priority")) ? 1f : Float(a, "priority");
            if (i.thingStyleCategories == null) i.thingStyleCategories = new List<ThingStyleCategoryWithPriority>();
            ThingStyleCategoryWithPriority existing = i.thingStyleCategories.FirstOrDefault(s => s.category == def);
            if (existing != null) existing.priority = priority;
            else i.thingStyleCategories.Add(new ThingStyleCategoryWithPriority(def, priority));
            return OkBool(true);
        }

        private static string RemoveStyle(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            if (i.thingStyleCategories == null) return OkBool(false);
            int removed = i.thingStyleCategories.RemoveAll(s => s.category.defName == Str(a, "def"));
            return OkBool(removed > 0);
        }

        private static string Rename(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            string name = Str(a, "name");
            if (string.IsNullOrWhiteSpace(name)) return Fail("RK1001", "name is required");
            i.name = name;
            string adjective = Str(a, "adjective");
            if (!string.IsNullOrWhiteSpace(adjective)) i.adjective = adjective;
            return OkBool(true);
        }

        private static Precept_Role RoleOf(Ideo i, string def) => i.PreceptsListForReading.OfType<Precept_Role>().FirstOrDefault(r => r.def.defName == def);

        private static string AssignRole(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Fail("RK2001", "pawn handle is stale or null");
            Ideo i = p.Ideo;
            if (i == null) return Fail("RK3003", "this pawn has no ideoligion");
            Precept_Role role = RoleOf(i, Str(a, "role"));
            if (role == null) return Fail("RK3001", "the pawn's ideoligion has no role " + Str(a, "role"));
            role.Assign(p, true);
            return OkBool(role.ChosenPawnSingle() == p);
        }

        private static string UnassignRole(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.Ideo == null) return Fail("RK3003", "this pawn has no ideoligion");
            Precept_Role role = p.Ideo.GetRole(p);
            if (role == null) return OkBool(false);
            role.Unassign(p, true);
            return OkBool(true);
        }

        private static string Memes(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (MemeDef m in DefDatabase<MemeDef>.AllDefsListForReading) arr.Add(Jb.Obj().S("def", m.defName).S("label", m.label).S("category", m.category.ToString()));
            return arr.Ok();
        }

        private static string PreceptDefs(Dictionary<string, string> a)
        {
            string issue = Str(a, "issue");
            var arr = Jb.Arr();
            foreach (PreceptDef d in DefDatabase<PreceptDef>.AllDefsListForReading)
            {
                if (!string.IsNullOrEmpty(issue) && d.issue?.defName != issue) continue;
                arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("issue", d.issue?.defName));
            }

            return arr.Ok();
        }

        private static string Rituals(Dictionary<string, string> a)
        {
            Ideo i = Find_(a);
            if (i == null) return BadIdeo();
            var arr = Jb.Arr();
            foreach (Precept_Ritual r in i.PreceptsListForReading.OfType<Precept_Ritual>())
                arr.Add(Jb.Obj().S("def", r.def.defName).S("label", r.LabelCap).S("outcome", r.outcomeEffect?.def?.defName));
            return arr.Ok();
        }
    }
}
