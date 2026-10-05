using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.widgets: Lua-defined windows built from a widget tree, confirm and text prompts. Ops are widgets.*.
    internal static class ApiWidgets
    {
        public static void Register()
        {
            R("widgets.open", Open);
            R("widgets.close", Close);
            R("widgets.set_state", SetState);
            R("widgets.state", State);
            R("widgets.invalidate", Invalidate);
            R("widgets.windows", Windows);
            R("widgets.confirm", Confirm);
            R("widgets.prompt", Prompt);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        private static string NoUi() => Fail("RK3001", "the game UI is not available yet");

        // opts: width (default 480), height (default 520), refresh (frames between view calls, default 15), pause, resizable.
        private static string Open(Dictionary<string, string> a)
        {
            if (Find.WindowStack == null) return NoUi();
            int view = Int(a, "view");
            if (view <= 0) return Fail("RK1001", "view must be a function");
            Opts o = Opts.From(a, "opts");
            int id = WidgetWindows.Add(string.IsNullOrEmpty(Str(a, "title")) ? "RimKit" : Str(a, "title"), view, Int(a, "on_event"),
                (float)o.Num("width", 480), (float)o.Num("height", 520), o.Has("refresh") ? o.Int("refresh") : 15, o.Bool("pause"), o.Bool("resizable", true));
            return OkInt(id);
        }

        private static string Close(Dictionary<string, string> a)
        {
            LuaWidgetWindow w = WidgetWindows.Get(Int(a, "id"));
            if (w == null) return OkBool(false);
            w.Close();
            return OkBool(true);
        }

        private static string SetState(Dictionary<string, string> a)
        {
            LuaWidgetWindow w = WidgetWindows.Get(Int(a, "id"));
            if (w == null) return Fail("RK3001", "no window with id " + Str(a, "id"));
            string key = Str(a, "key");
            if (string.IsNullOrEmpty(key)) return Fail("RK1001", "key is required");
            object value = Str(a, "value");
            string raw = Str(a, "value");
            if (raw == "true" || raw == "false") value = raw == "true";
            else if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)) value = d;
            w.Context.Set(key, value);
            w.Context.Dirty = true;
            return OkBool(true);
        }

        private static string State(Dictionary<string, string> a)
        {
            LuaWidgetWindow w = WidgetWindows.Get(Int(a, "id"));
            if (w == null) return Fail("RK3001", "no window with id " + Str(a, "id"));
            return OkJson(JsonOut.Of(w.Context.State));
        }

        private static string Invalidate(Dictionary<string, string> a)
        {
            LuaWidgetWindow w = WidgetWindows.Get(Int(a, "id"));
            if (w == null) return OkBool(false);
            w.Context.Dirty = true;
            return OkBool(true);
        }

        private static string Windows(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (LuaWidgetWindow w in WidgetWindows.All) arr.Add(Jb.Obj().I("id", w.WindowId).S("title", w.Title));
            return arr.Ok();
        }

        // opts: yes_label, no_label.
        private static string Confirm(Dictionary<string, string> a)
        {
            if (Find.WindowStack == null) return NoUi();
            Opts o = Opts.From(a, "opts");
            Find.WindowStack.Add(new Dialog_LuaConfirm(Str(a, "text"), Int(a, "on_yes"), Int(a, "on_no"), o.Str("yes_label"), o.Str("no_label")));
            return OkBool(true);
        }

        private static string Prompt(Dictionary<string, string> a)
        {
            if (Find.WindowStack == null) return NoUi();
            if (Int(a, "on_ok") <= 0) return Fail("RK1001", "on_ok must be a function");
            Find.WindowStack.Add(new Dialog_LuaPrompt(Str(a, "title"), Str(a, "text"), Str(a, "default"), Int(a, "on_ok"), Int(a, "on_cancel")));
            return OkBool(true);
        }
    }
}
