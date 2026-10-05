using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.alerts, game.hud (status lines, letters with choices, colour presets, fonts) and game.options (settings pages).
    // Ops: alert.*, hud.*, options.*.
    internal static class ApiHud
    {
        public static void Register()
        {
            R("alert.add", AlertAdd);
            R("alert.remove", AlertRemove);
            R("alert.list", AlertList);
            R("hud.status", Status);
            R("hud.clear_status", ClearStatus);
            R("hud.letter", Letter);
            R("hud.colors", Colors);
            R("hud.set_color", SetColor);
            R("hud.set_font", SetFont);
            R("options.page", OptionsPage);
            R("options.get", OptionsGet);
            R("options.set", OptionsSet);
            R("options.pages", OptionsList);
            R("options.remove_page", OptionsRemovePage);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        // ---- alerts

        // opts: label, explanation, priority (Medium, High, Critical). The check function returns true, false or { active, label, explanation, culprits }.
        private static string AlertAdd(Dictionary<string, string> a)
        {
            string id = Str(a, "id");
            if (string.IsNullOrEmpty(id)) return Fail("RK1001", "id is required");
            if (Int(a, "check") <= 0) return Fail("RK1001", "check must be a function");
            Opts o = Opts.From(a, "opts");
            if (!Enum.TryParse(o.Str("priority", "Medium"), true, out AlertPriority priority)) return Fail("RK1001", "priority must be Medium, High or Critical");
            string problem = HudHost.RegisterAlert(new AlertSpec { Id = id, Label = o.Str("label", id), Explanation = o.Str("explanation", ""), Check = Int(a, "check"), Priority = priority });
            return problem == null ? OkStr(id) : Fail("RK3003", problem);
        }

        private static string AlertRemove(Dictionary<string, string> a)
        {
            bool had = Alert_LuaAlert.Specs.Remove(Str(a, "id"));
            return OkBool(had);
        }

        private static string AlertList(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (AlertSpec s in Alert_LuaAlert.Specs.Values) arr.Add(Jb.Obj().S("id", s.Id).S("label", s.Label).S("priority", s.Priority.ToString()));
            return arr.Ok();
        }

        // ---- hud

        // corner: top_left (default), top_right, bottom_left. color: a theme color or #rrggbb. Calling again with the same id updates the line.
        private static string Status(Dictionary<string, string> a)
        {
            string id = Str(a, "id");
            if (string.IsNullOrEmpty(id)) return Fail("RK1001", "id is required");
            string corner = string.IsNullOrEmpty(Str(a, "corner")) ? "top_left" : Str(a, "corner");
            if (corner != "top_left" && corner != "top_right" && corner != "bottom_left") return Fail("RK1001", "corner must be top_left, top_right or bottom_left");
            HudHost.StatusLine line = HudHost.Lines.FirstOrDefault(l => l.Id == id);
            if (line == null) { line = new HudHost.StatusLine { Id = id }; HudHost.Lines.Add(line); }
            line.Text = Str(a, "text");
            line.Color = Str(a, "color");
            line.Corner = corner;
            HudHost.EnsurePatched();
            return OkBool(true);
        }

        private static string ClearStatus(Dictionary<string, string> a) => OkBool(HudHost.Lines.RemoveAll(l => l.Id == Str(a, "id")) > 0);

        // Sends a letter with answer buttons. Answers raise the letter.choice event with the tag. type: NeutralEvent, PositiveEvent, NegativeEvent, ThreatSmall, ThreatBig.
        private static string Letter(Dictionary<string, string> a)
        {
            if (Find.LetterStack == null) return Fail("RK3001", "no game is loaded");
            var choices = new List<string>();
            if (a.TryGetValue("choices", out string json) && Json.TryParse(json, out object parsed) && parsed is List<object> list)
                foreach (object item in list) if (item is string s && s.Length > 0) choices.Add(s);
            if (choices.Count == 0) return Fail("RK1001", "choices must list at least one answer");
            LetterDef def = HudStartup.EnsureLetterDef();
            Letter letter = LetterMaker.MakeLetter(Str(a, "label"), Str(a, "text"), def);
            var choice = letter as ChoiceLetter_LuaChoices;
            if (choice == null) return Fail("RK3003", "the choice letter could not be created");
            choice.choiceLabels = choices;
            choice.tag = Str(a, "tag") ?? "";
            choice.lookTargets = ObjectHandles.Get<Thing>(Int(a, "target")) is Thing t ? new LookTargets(t) : null;
            Find.LetterStack.ReceiveLetter(choice);
            return OkBool(true);
        }

        private static string Colors(Dictionary<string, string> a)
        {
            var j = Jb.Obj();
            foreach (var kv in Theme.Colors) j.S(kv.Key, "#" + ColorUtility.ToHtmlStringRGB(kv.Value));
            return j.Ok();
        }

        private static string SetColor(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name) || !name.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) return Fail("RK1001", "name may only contain letters, digits and underscore");
            if (!ColorUtility.TryParseHtmlString(Str(a, "color"), out Color c)) return Fail("RK1001", "color must look like #rrggbb");
            Theme.Colors[name] = c;
            return OkStr("#" + ColorUtility.ToHtmlStringRGB(c));
        }

        private static string SetFont(Dictionary<string, string> a)
        {
            string f = Str(a, "font");
            if (f != "tiny" && f != "small" && f != "medium") return Fail("RK1001", "font must be tiny, small or medium");
            Theme.DefaultFont = f;
            return OkStr(f);
        }

        // ---- options

        // fields: list of { key, type (bool, float, string, choice), label, default, min, max, step, tip, choices }.
        private static string OptionsPage(Dictionary<string, string> a)
        {
            string pkg = Str(a, "package_id");
            if (string.IsNullOrEmpty(pkg)) return Fail("RK1001", "package_id is required");
            var page = new OptionsPage { PackageId = pkg, Title = string.IsNullOrEmpty(Str(a, "title")) ? pkg : Str(a, "title"), OnChange = Int(a, "on_change") };
            if (!a.TryGetValue("fields", out string json) || !Json.TryParse(json, out object parsed) || !(parsed is List<object> list) || list.Count == 0)
                return Fail("RK1001", "fields must list at least one field");
            foreach (object item in list)
            {
                var d = Json.AsObject(item);
                if (d == null) continue;
                string key = Json.GetString(d, "key");
                if (string.IsNullOrEmpty(key)) return Fail("RK1001", "every field needs a key");
                string type = Json.GetString(d, "type", "bool");
                if (type != "bool" && type != "float" && type != "string" && type != "choice") return Fail("RK1001", "field " + key + ": type must be bool, float, string or choice");
                var f = new OptionField { Key = key, Type = type, Label = Json.GetString(d, "label", key), Tip = Json.GetString(d, "tip"), Choices = Json.GetStringList(d, "choices") };
                object def = d.TryGetValue("default", out object dv) ? dv : null;
                f.Default = def is bool b ? (b ? "true" : "false") : def is double dd ? dd.ToString(CultureInfo.InvariantCulture) : def is long ll ? ll.ToString(CultureInfo.InvariantCulture) : (def as string ?? "");
                f.Min = (float)(d.TryGetValue("min", out object mn) && mn is double md ? md : mn is long ml ? ml : 0);
                f.Max = (float)(d.TryGetValue("max", out object mx) && mx is double xd ? xd : mx is long xl ? xl : 100);
                f.Step = (float)(d.TryGetValue("step", out object st) && st is double sd ? sd : st is long sl ? sl : 0);
                if (type == "choice" && f.Choices.Count == 0) return Fail("RK1001", "field " + key + ": a choice needs choices");
                page.Fields.Add(f);
            }

            OptionsPages.Register(page);
            return OkInt(page.Fields.Count);
        }

        private static string OptionsGet(Dictionary<string, string> a)
        {
            foreach (OptionsPage p in OptionsPages.Pages)
            {
                OptionField f = p.Fields.FirstOrDefault(x => x.Key == Str(a, "key"));
                if (f == null) continue;
                string v = OptionsPages.ValueText(f);
                switch (f.Type)
                {
                    case "bool": return OkBool(v == "true" || v == "1");
                    case "float": return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ? OkFloat(x) : OkFloat(0);
                    default: return OkStr(v);
                }
            }

            return Fail("RK3001", "no option with key " + Str(a, "key"));
        }

        private static string OptionsSet(Dictionary<string, string> a)
        {
            if (!OptionsPages.Pages.Any(p => p.Fields.Any(f => f.Key == Str(a, "key")))) return Fail("RK3001", "no option with key " + Str(a, "key"));
            LuaConfigBridge.Set(Str(a, "key"), Str(a, "value"));
            return OkBool(true);
        }

        private static string OptionsRemovePage(Dictionary<string, string> a) => OkBool(OptionsPages.RemovePackage(Str(a, "package_id")) > 0);

        private static string OptionsList(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (OptionsPage p in OptionsPages.Pages) arr.Add(Jb.Obj().S("package_id", p.PackageId).S("title", p.Title).I("fields", p.Fields.Count));
            return arr.Ok();
        }
    }
}
