using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.tabs (main tabs, inspect tabs, pawn table columns, architect entries), game.designators (click and drag tools) and
    // game.areas. Ops: tabs.*, designator.*, area.*.
    internal static class ApiTabs
    {
        public static void Register()
        {
            R("tabs.add_main", AddMain);
            R("tabs.add_inspect", AddInspect);
            R("tabs.add_column", AddColumn);
            R("tabs.remove", RemoveTab);
            R("designator.remove", RemoveTool);
            R("tabs.pawn_tables", PawnTables);
            R("tabs.architect_categories", ArchitectCategories);
            R("designator.add", AddTool);
            R("designator.add_to_architect", ToArchitect);
            R("designator.activate", Activate);
            R("designator.list", ToolList);
            R("area.create", AreaCreate);
            R("area.set", AreaSet);
            R("area.cells", AreaCells);
            R("area.remove", AreaRemove);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        private static TabSpec TabOf(Dictionary<string, string> a, out string err)
        {
            err = null;
            Opts o = Opts.From(a, "opts");
            string id = Str(a, "id");
            if (string.IsNullOrEmpty(id) || !id.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) { err = Fail("RK1001", "id may only contain letters, digits and underscore"); return null; }
            if (Int(a, "view") <= 0) { err = Fail("RK1001", "view must be a function"); return null; }
            return new TabSpec
            {
                Id = id, Label = string.IsNullOrEmpty(Str(a, "label")) ? id : Str(a, "label"), View = Int(a, "view"), OnEvent = Int(a, "on_event"),
                Width = (float)o.Num("width", 480), Height = (float)o.Num("height", 480), Order = (float)o.Num("order", 100), Refresh = o.Has("refresh") ? o.Int("refresh") : 15,
                Visible = Int(a, "visible"),
            };
        }

        private static string AddMain(Dictionary<string, string> a)
        {
            if (Find.UIRoot == null) return Fail("RK3001", "the game UI is not available yet");
            TabSpec spec = TabOf(a, out string err);
            if (err != null) return err;
            string problem = LuaTabs.AddMain(spec);
            return problem == null ? OkStr(spec.Id) : Fail("RK3003", problem);
        }

        // opts: defs (thing def names) or target (pawn, building, item, plant), width, height.
        private static string AddInspect(Dictionary<string, string> a)
        {
            TabSpec spec = TabOf(a, out string err);
            if (err != null) return err;
            Opts o = Opts.From(a, "opts");
            string problem = LuaTabs.AddInspect(spec, o.Strings("defs"), o.Str("target"));
            return problem == null ? OkStr(spec.Id) : Fail("RK3003", problem);
        }

        // The cell callback gets the pawn and returns a string or { text, color, tip }.
        private static string AddColumn(Dictionary<string, string> a)
        {
            string id = Str(a, "id");
            if (string.IsNullOrEmpty(id) || !id.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) return Fail("RK1001", "id may only contain letters, digits and underscore");
            if (Int(a, "cell") <= 0) return Fail("RK1001", "cell must be a function");
            Opts o = Opts.From(a, "opts");
            var spec = new ColumnSpec { Id = id, Label = string.IsNullOrEmpty(Str(a, "label")) ? id : Str(a, "label"), Cell = Int(a, "cell"), Sort = Int(a, "sort"), OnClick = Int(a, "on_click"), Width = (float)o.Num("width", 120) };
            string problem = LuaTabs.AddColumn(Str(a, "table"), spec);
            return problem == null ? OkStr(id) : Fail("RK3003", problem);
        }

        private static string RemoveTab(Dictionary<string, string> a) => OkBool(LuaTabs.Remove(Str(a, "id")));

        private static string RemoveTool(Dictionary<string, string> a) => OkBool(LuaDesignators.Remove(Str(a, "id")));

        private static string PawnTables(Dictionary<string, string> a) => OkStringList(DefDatabase<PawnTableDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string ArchitectCategories(Dictionary<string, string> a) => OkStringList(DefDatabase<DesignationCategoryDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        // ---- designators

        // opts: label, desc, icon, hotkey, target (cell or thing), drag (single or box), color (highlight under the mouse).
        private static string AddTool(Dictionary<string, string> a)
        {
            string id = Str(a, "id");
            if (string.IsNullOrEmpty(id)) return Fail("RK1001", "id is required");
            if (Int(a, "designate") <= 0) return Fail("RK1001", "designate must be a function");
            Opts o = Opts.From(a, "opts");
            string target = o.Str("target", "cell");
            if (target != "cell" && target != "thing") return Fail("RK1001", "target must be cell or thing");
            var spec = new ToolSpec
            {
                Id = id, Label = o.Str("label", id), Desc = o.Str("desc", ""), Icon = o.Str("icon"), Hotkey = o.Str("hotkey"), Target = target, Drag = o.Str("drag", "single"),
                Color = o.Str("color"), CanDesignate = Int(a, "can_designate"), Designate = Int(a, "designate"), Finished = Int(a, "on_finished"),
            };
            LuaDesignators.Create(spec);
            if (!string.IsNullOrEmpty(o.Str("category")))
            {
                string problem = LuaDesignators.AddToArchitect(spec, o.Str("category"));
                if (problem != null) return Fail("RK3003", problem);
            }

            return OkStr(id);
        }

        private static string ToArchitect(Dictionary<string, string> a)
        {
            if (!LuaDesignators.Specs.TryGetValue(Str(a, "id"), out ToolSpec spec)) return Fail("RK3001", "no designator with id " + Str(a, "id"));
            string problem = LuaDesignators.AddToArchitect(spec, Str(a, "category"));
            return problem == null ? OkBool(true) : Fail("RK3003", problem);
        }

        private static string Activate(Dictionary<string, string> a)
        {
            if (!LuaDesignators.Specs.TryGetValue(Str(a, "id"), out ToolSpec spec)) return Fail("RK3001", "no designator with id " + Str(a, "id"));
            return OkBool(LuaDesignators.Activate(spec));
        }

        private static string ToolList(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ToolSpec s in LuaDesignators.Specs.Values) arr.Add(Jb.Obj().S("id", s.Id).S("label", s.Label).S("target", s.Target).S("drag", s.Drag));
            return arr.Ok();
        }

        // ---- areas

        private static Area FindArea(Map map, string label) => map.areaManager.AllAreas.FirstOrDefault(x => x.Label == label);

        private static string AreaCreate(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            string label = Str(a, "label");
            if (string.IsNullOrEmpty(label)) return Fail("RK1001", "label is required");
            if (FindArea(map, label) != null) return OkBool(false);
            if (!map.areaManager.TryMakeNewAllowed(out Area_Allowed area)) return Fail("RK3003", "no more allowed areas can be made");
            area.SetLabel(label);
            return OkBool(true);
        }

        private static string AreaSet(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            Area area = FindArea(map, Str(a, "label"));
            if (area == null) return Fail("RK3001", "no area labelled " + Str(a, "label"));
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            area[c] = Bool(a, "value");
            return OkBool(area[c]);
        }

        private static string AreaCells(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            Area area = FindArea(map, Str(a, "label"));
            if (area == null) return Fail("RK3001", "no area labelled " + Str(a, "label"));
            var arr = Jb.Arr();
            foreach (IntVec3 c in area.ActiveCells.Take(2000)) arr.Add(Jb.Obj().I("x", c.x).I("z", c.z));
            return arr.Ok();
        }

        private static string AreaRemove(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            Area area = FindArea(map, Str(a, "label"));
            if (area == null) return OkBool(false);
            area.Delete();
            return OkBool(true);
        }
    }
}
