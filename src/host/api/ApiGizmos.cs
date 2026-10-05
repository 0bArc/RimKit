using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.gizmos: buttons, toggles and sliders on pawns, things and the selection. Ops are gizmo.*.
    internal static class ApiGizmos
    {
        public static void Register()
        {
            R("gizmo.add", AddAction);
            R("gizmo.add_toggle", AddToggle);
            R("gizmo.add_slider", AddSlider);
            R("gizmo.remove", Remove);
            R("gizmo.list", List);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        private static GizmoSpec Spec(Dictionary<string, string> a, string kind, out string err)
        {
            err = null;
            string id = Str(a, "id");
            if (string.IsNullOrEmpty(id)) { err = Fail("RK1001", "id is required"); return null; }
            Opts o = Opts.From(a, "opts");
            string target = o.Str("target", "any");
            if (!new[] { "pawn", "colonist", "building", "item", "plant", "thing", "any" }.Contains(target)) { err = Fail("RK1001", "target must be pawn, colonist, building, item, plant, thing or any"); return null; }
            return new GizmoSpec
            {
                Id = id, Kind = kind, Label = o.Str("label", id), Desc = o.Str("desc", ""), Icon = o.Str("icon"), Hotkey = o.Str("hotkey"), Target = target,
                Defs = new HashSet<string>(o.Strings("defs")), Order = (float)o.Num("order", 100), Min = (float)o.Num("min", 0), Max = (float)o.Num("max", 1), Step = (float)o.Num("step", 0),
                Visible = Int(a, "visible"),
            };
        }

        private static string AddAction(Dictionary<string, string> a)
        {
            GizmoSpec s = Spec(a, "action", out string err);
            if (err != null) return err;
            s.OnClick = Int(a, "on_click");
            if (s.OnClick <= 0) return Fail("RK1001", "on_click must be a function");
            LuaGizmos.Add(s);
            return OkStr(s.Id);
        }

        private static string AddToggle(Dictionary<string, string> a)
        {
            GizmoSpec s = Spec(a, "toggle", out string err);
            if (err != null) return err;
            s.OnClick = Int(a, "on_toggle");
            s.State = Int(a, "state");
            if (s.OnClick <= 0 || s.State <= 0) return Fail("RK1001", "state and on_toggle must be functions");
            LuaGizmos.Add(s);
            return OkStr(s.Id);
        }

        private static string AddSlider(Dictionary<string, string> a)
        {
            GizmoSpec s = Spec(a, "slider", out string err);
            if (err != null) return err;
            s.Value = Int(a, "value");
            s.OnChange = Int(a, "on_change");
            if (s.Value <= 0 || s.OnChange <= 0) return Fail("RK1001", "value and on_change must be functions");
            LuaGizmos.Add(s);
            return OkStr(s.Id);
        }

        private static string Remove(Dictionary<string, string> a) => OkBool(LuaGizmos.Remove(Str(a, "id")));

        private static string List(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (GizmoSpec s in LuaGizmos.All) arr.Add(Jb.Obj().S("id", s.Id).S("kind", s.Kind).S("label", s.Label).S("target", s.Target).F("order", s.Order));
            return arr.Ok();
        }
    }
}
