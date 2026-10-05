using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimKit
{
    internal sealed class OptionField
    {
        public string Key;
        public string Type = "bool";   // bool, float, string, choice
        public string Label;
        public string Default = "";
        public float Min;
        public float Max = 100f;
        public float Step;
        public string Tip;
        public List<string> Choices = new List<string>();
    }

    internal sealed class OptionsPage
    {
        public string PackageId;
        public string Title;
        public List<OptionField> Fields = new List<OptionField>();
        public int OnChange;
    }

    // Settings pages that Lua mods define with game.options.page. They show up as tabs in RimKit's entry of the mod settings window,
    // and the values are stored with the rest of the RimKit settings, so config.get reads them.
    internal static class OptionsPages
    {
        public static readonly List<OptionsPage> Pages = new List<OptionsPage>();
        private static readonly WidgetContext Context = new WidgetContext();
        private static int selected = -1;       // -1 is the general page
        private static OptionsPage current;

        public static int RemovePackage(string packageId) => Pages.RemoveAll(p => p.PackageId == packageId);

        public static void Register(OptionsPage page)
        {
            Pages.RemoveAll(p => p.PackageId == page.PackageId && p.Title == page.Title);
            Pages.Add(page);
            foreach (OptionField f in page.Fields) LuaConfigBridge.Register(f.Key, f.Type == "choice" ? "string" : f.Type, f.Label, f.Default, true);
        }

        public static string ValueText(OptionField f)
        {
            string v = LuaConfigBridge.Get(f.Key);
            return string.IsNullOrEmpty(v) ? f.Default : v;
        }

        private static Dictionary<string, object> Tree(OptionsPage page)
        {
            var kids = new List<object>();
            foreach (OptionField f in page.Fields)
            {
                string v = ValueText(f);
                switch (f.Type)
                {
                    case "float":
                    {
                        float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
                        kids.Add(new Dictionary<string, object> { ["type"] = "slider", ["id"] = f.Key, ["label"] = f.Label, ["min"] = (double)f.Min, ["max"] = (double)f.Max, ["step"] = (double)f.Step, ["value"] = (double)x, ["tip"] = f.Tip });
                        ((Dictionary<string, object>)kids[kids.Count - 1])["h"] = 40.0;
                        Context.State[f.Key] = (double)x;
                        break;
                    }
                    case "string":
                        kids.Add(new Dictionary<string, object> { ["type"] = "label", ["text"] = f.Label, ["tip"] = f.Tip });
                        kids.Add(new Dictionary<string, object> { ["type"] = "text", ["id"] = f.Key, ["value"] = v, ["tip"] = f.Tip });
                        Context.State[f.Key] = v;
                        break;
                    case "choice":
                        kids.Add(new Dictionary<string, object>
                        {
                            ["type"] = "row", ["h"] = 28.0,
                            ["children"] = new List<object>
                            {
                                new Dictionary<string, object> { ["type"] = "label", ["text"] = f.Label, ["tip"] = f.Tip },
                                new Dictionary<string, object> { ["type"] = "dropdown", ["id"] = f.Key, ["options"] = f.Choices.Cast<object>().ToList(), ["value"] = v },
                            },
                        });
                        Context.State[f.Key] = v;
                        break;
                    default:
                    {
                        bool b = v == "true" || v == "1";
                        kids.Add(new Dictionary<string, object> { ["type"] = "checkbox", ["id"] = f.Key, ["text"] = f.Label, ["value"] = b, ["tip"] = f.Tip });
                        Context.State[f.Key] = b;
                        break;
                    }
                }
            }

            return new Dictionary<string, object> { ["type"] = "column", ["gap"] = 6.0, ["children"] = kids };
        }

        private static void Changed(string key, object value)
        {
            OptionField field = current?.Fields.FirstOrDefault(f => f.Key == key);
            if (field == null) return;
            string text = value is bool b ? (b ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
            LuaConfigBridge.Set(key, text);
            string payload = "{\"package_id\":" + JsonLite.Quote(current.PackageId) + ",\"key\":" + JsonLite.Quote(key) + ",\"value\":" + JsonOut.Of(value) + "}";
            if (current.OnChange > 0) LuaCallbacks.Fire(current.OnChange, payload);
            LuaEventQueue.EnqueuePayload("options.changed", payload);
        }

        // Draws the tab strip and the selected page. Returns false when the general page is selected, so the caller draws it.
        public static bool Draw(Rect rect, out Rect rest)
        {
            rest = rect;
            if (Pages.Count == 0) return false;
            float x = rect.x;
            var labels = new List<string> { "General" };
            labels.AddRange(Pages.Select(p => p.Title));
            for (int i = -1; i < Pages.Count; i++)
            {
                string label = i < 0 ? "General" : Pages[i].Title;
                float w = Text.CalcSize(label).x + 24f;
                Rect tr = new Rect(x, rect.y, w, 28f);
                if (i == selected) Widgets.DrawHighlightSelected(tr);
                if (Widgets.ButtonText(tr, label, i != selected)) selected = i;
                x += w + 2f;
            }

            rest = new Rect(rect.x, rect.y + 34f, rect.width, rect.height - 34f);
            if (selected < 0 || selected >= Pages.Count) return false;
            current = Pages[selected];
            Context.Emit = (id, kind, value) => Changed(id, value);
            var tree = Tree(current);
            float h = WidgetRenderer.Measure(tree, rest.width - 20f, Context);
            Rect view = new Rect(0f, 0f, rest.width - 20f, h);
            Vector2 scroll = Context.Scrolls.TryGetValue("options", out Vector2 sv) ? sv : Vector2.zero;
            Widgets.BeginScrollView(rest, ref scroll, view);
            WidgetRenderer.Draw(tree, view, Context);
            Widgets.EndScrollView();
            Context.Scrolls["options"] = scroll;
            return true;
        }
    }
}
