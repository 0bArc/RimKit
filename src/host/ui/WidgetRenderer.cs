using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace RimKit
{
    // Immediate-mode renderer for the Lua widget tree (game.widgets). A Lua view function returns a tree of tables, this draws it
    // every frame. Values the player edits (checkboxes, sliders, text, dropdowns, selections, tabs, scroll) live in a state table keyed
    // by widget id, and every interaction is reported to the Lua event handler as (id, kind, value).
    internal sealed class WidgetContext
    {
        public readonly Dictionary<string, object> State = new Dictionary<string, object>();
        public readonly Dictionary<string, Vector2> Scrolls = new Dictionary<string, Vector2>();
        public Action<string, string, object> Emit;
        public bool Dirty = true;

        // Drag and drop of list items.
        public string DragList;
        public int DragFrom = -1;

        public T Get<T>(string id, T fallback)
        {
            if (id == null || !State.TryGetValue(id, out object v) || v == null) return fallback;
            try
            {
                if (v is T t) return t;
                return (T)Convert.ChangeType(v, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        public void Set(string id, object value) => State[id] = value;
    }

    internal static class WidgetRenderer
    {
        private const float Gap = 4f;
        private const float RowHeight = 28f;

        // ---- node access

        private static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;

        private static string S(Dictionary<string, object> n, string key, string fallback = null) => Json.GetString(n, key, fallback);

        private static float F(Dictionary<string, object> n, string key, float fallback = 0f)
        {
            if (n != null && n.TryGetValue(key, out object v))
            {
                if (v is double d) return (float)d;
                if (v is long l) return l;
            }

            return fallback;
        }

        private static bool B(Dictionary<string, object> n, string key, bool fallback = false) => Json.GetBool(n, key, fallback);

        private static bool Has(Dictionary<string, object> n, string key) => n != null && n.TryGetValue(key, out object v) && v != null;

        private static List<Dictionary<string, object>> Kids(Dictionary<string, object> n, string key = "children")
        {
            var list = new List<Dictionary<string, object>>();
            if (n != null && n.TryGetValue(key, out object v) && v is List<object> items)
            {
                foreach (object item in items)
                {
                    var o = Obj(item);
                    if (o != null) list.Add(o);
                    else if (item is string s) list.Add(new Dictionary<string, object> { ["type"] = "label", ["text"] = s });
                }
            }

            return list;
        }

        // ---- theme

        public static Color ColorOf(string spec, Color fallback)
        {
            if (string.IsNullOrEmpty(spec)) return fallback;
            if (Theme.Colors.TryGetValue(spec, out Color named)) return named;
            if (spec[0] == '#' && ColorUtility.TryParseHtmlString(spec, out Color c)) return c;
            return fallback;
        }

        private static GameFont FontOf(Dictionary<string, object> n)
        {
            switch (S(n, "font", Theme.DefaultFont))
            {
                case "tiny": return GameFont.Tiny;
                case "medium": return GameFont.Medium;
                default: return GameFont.Small;
            }
        }

        // ---- measuring

        public static float Measure(Dictionary<string, object> n, float width, WidgetContext ctx)
        {
            if (n == null) return 0f;
            if (Has(n, "h")) return F(n, "h");
            switch (S(n, "type", "label"))
            {
                case "column":
                {
                    var kids = Kids(n);
                    float gap = F(n, "gap", Gap), h = 0f;
                    for (int i = 0; i < kids.Count; i++) h += Measure(kids[i], width, ctx) + (i > 0 ? gap : 0f);
                    return h;
                }
                case "row":
                {
                    var kids = Kids(n);
                    float h = 0f;
                    foreach (float w in RowWidths(kids, width, F(n, "gap", Gap)).Select((w, i) => Measure(kids[i], w, ctx))) h = Mathf.Max(h, w);
                    return h;
                }
                case "label":
                {
                    GameFont old = Text.Font;
                    Text.Font = FontOf(n);
                    float h = B(n, "wrap", true) ? Text.CalcHeight(S(n, "text", ""), width) : Text.LineHeight;
                    Text.Font = old;
                    return h;
                }
                case "button": return RowHeight;
                case "checkbox": return 24f;
                case "slider": return Has(n, "label") ? 40f : 24f;
                case "text": return B(n, "multiline") ? F(n, "lines", 4) * 24f : RowHeight - 4f;
                case "dropdown": return RowHeight;
                case "list": return F(n, "height", 150f);
                case "table":
                {
                    int rows = Kids(n, "rows_unused").Count;
                    return (n.TryGetValue("rows", out object r) && r is List<object> rl ? rl.Count : rows) * 24f + 28f;
                }
                case "tabs":
                {
                    var tabs = Kids(n, "tabs");
                    string active = ActiveTab(n, tabs, ctx);
                    var content = tabs.FirstOrDefault(t => S(t, "id") == active);
                    return 30f + (content != null && Has(content, "content") ? Measure(Obj(content["content"]), width, ctx) : 0f);
                }
                case "scroll": return F(n, "height", 200f);
                case "progress": return 22f;
                case "image": return F(n, "height", F(n, "size", 48f));
                case "space": return F(n, "size", 8f);
                case "separator": return 8f;
                default: return RowHeight;
            }
        }

        private static List<float> RowWidths(List<Dictionary<string, object>> kids, float width, float gap)
        {
            float fixedTotal = 0f;
            int flexible = 0;
            foreach (var k in kids)
            {
                if (Has(k, "w")) fixedTotal += F(k, "w"); else flexible++;
            }

            float free = Mathf.Max(0f, width - fixedTotal - gap * Mathf.Max(0, kids.Count - 1));
            float each = flexible > 0 ? free / flexible : 0f;
            return kids.Select(k => Has(k, "w") ? F(k, "w") : each).ToList();
        }

        private static string ActiveTab(Dictionary<string, object> n, List<Dictionary<string, object>> tabs, WidgetContext ctx)
        {
            string id = S(n, "id", "tabs");
            string active = ctx.Get<string>(id, null);
            if (active == null || !tabs.Any(t => S(t, "id") == active)) active = S(n, "active", tabs.Count > 0 ? S(tabs[0], "id") : null);
            return active;
        }

        // ---- drawing

        public static void Draw(Dictionary<string, object> n, Rect rect, WidgetContext ctx)
        {
            if (n == null) return;
            string type = S(n, "type", "label");
            string id = S(n, "id");
            bool enabled = B(n, "enabled", true);
            Color oldColor = GUI.color;
            if (!enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            try
            {
                switch (type)
                {
                    case "column":
                    {
                        float y = rect.y, gap = F(n, "gap", Gap);
                        foreach (var k in Kids(n))
                        {
                            float h = Measure(k, rect.width, ctx);
                            Draw(k, new Rect(rect.x, y, rect.width, h), ctx);
                            y += h + gap;
                        }

                        break;
                    }
                    case "row":
                    {
                        var kids = Kids(n);
                        var widths = RowWidths(kids, rect.width, F(n, "gap", Gap));
                        float x = rect.x;
                        for (int i = 0; i < kids.Count; i++)
                        {
                            Draw(kids[i], new Rect(x, rect.y, widths[i], rect.height), ctx);
                            x += widths[i] + F(n, "gap", Gap);
                        }

                        break;
                    }
                    case "label": DrawLabel(n, rect); break;
                    case "button":
                        if (Widgets.ButtonText(rect, S(n, "text", ""), true, true, enabled) && enabled) ctx.Emit?.Invoke(id, "click", null);
                        break;
                    case "checkbox":
                    {
                        bool v = ctx.Get(id, B(n, "value"));
                        bool before = v;
                        Widgets.CheckboxLabeled(rect, S(n, "text", ""), ref v, !enabled);
                        if (v != before) { ctx.Set(id, v); ctx.Emit?.Invoke(id, "change", v); }
                        break;
                    }
                    case "slider": DrawSlider(n, rect, ctx, id); break;
                    case "text": DrawText(n, rect, ctx, id); break;
                    case "dropdown": DrawDropdown(n, rect, ctx, id); break;
                    case "list": DrawList(n, rect, ctx, id); break;
                    case "table": DrawTable(n, rect, ctx); break;
                    case "tabs": DrawTabs(n, rect, ctx, id); break;
                    case "scroll": DrawScroll(n, rect, ctx, id); break;
                    case "progress":
                    {
                        float v = Mathf.Clamp01(F(n, "value"));
                        Widgets.FillableBar(rect, v, SolidColorMaterials.NewSolidColorTexture(ColorOf(S(n, "color"), Theme.Colors["accent"])), BaseContent.GreyTex, true);
                        string text = S(n, "text");
                        if (!string.IsNullOrEmpty(text)) { Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(rect, text); Text.Anchor = TextAnchor.UpperLeft; }
                        break;
                    }
                    case "image":
                    {
                        Texture2D tex = ContentFinder<Texture2D>.Get(S(n, "texture", ""), false) ?? BaseContent.BadTex;
                        float size = Mathf.Min(rect.height, F(n, "size", rect.height));
                        GUI.DrawTexture(new Rect(rect.x, rect.y, Has(n, "width") ? F(n, "width") : size, size), tex);
                        break;
                    }
                    case "separator": Widgets.DrawLineHorizontal(rect.x, rect.center.y, rect.width); break;
                    case "space": break;
                }

                string tip = S(n, "tip");
                if (!string.IsNullOrEmpty(tip) && Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, tip);
            }
            finally
            {
                GUI.color = oldColor;
            }
        }

        private static void DrawLabel(Dictionary<string, object> n, Rect rect)
        {
            GameFont old = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Text.Font = FontOf(n);
            Text.WordWrap = B(n, "wrap", true);
            switch (S(n, "align", "left"))
            {
                case "center": Text.Anchor = TextAnchor.UpperCenter; break;
                case "right": Text.Anchor = TextAnchor.UpperRight; break;
                default: Text.Anchor = TextAnchor.UpperLeft; break;
            }

            Color c = GUI.color;
            GUI.color = ColorOf(S(n, "color"), Color.white) * c;
            Widgets.Label(rect, S(n, "text", ""));
            GUI.color = c;
            Text.Font = old;
            Text.Anchor = oldAnchor;
            Text.WordWrap = oldWrap;
        }

        private static void DrawSlider(Dictionary<string, object> n, Rect rect, WidgetContext ctx, string id)
        {
            float min = F(n, "min", 0f), max = F(n, "max", 1f), step = F(n, "step", 0f);
            float v = ctx.Get(id, F(n, "value", min));
            Rect bar = rect;
            if (Has(n, "label"))
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), S(n, "label") + ": " + (step >= 1f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##")));
                bar = new Rect(rect.x, rect.y + 18f, rect.width, rect.height - 18f);
            }

            float next = Widgets.HorizontalSlider(bar, v, min, max);
            if (step > 0f) next = Mathf.Round(next / step) * step;
            if (Mathf.Abs(next - v) > 1e-5f) { ctx.Set(id, (double)next); ctx.Emit?.Invoke(id, "change", (double)next); }
        }

        private static void DrawText(Dictionary<string, object> n, Rect rect, WidgetContext ctx, string id)
        {
            string v = ctx.Get(id, S(n, "value", ""));
            string next = B(n, "multiline") ? Widgets.TextArea(rect, v) : Widgets.TextField(rect, v);
            if (next != v)
            {
                if (Has(n, "max_length") && next.Length > (int)F(n, "max_length")) next = next.Substring(0, (int)F(n, "max_length"));
                ctx.Set(id, next);
                ctx.Emit?.Invoke(id, "change", next);
            }
        }

        private static void DrawDropdown(Dictionary<string, object> n, Rect rect, WidgetContext ctx, string id)
        {
            var options = new List<KeyValuePair<string, string>>();
            if (n.TryGetValue("options", out object ov) && ov is List<object> list)
            {
                foreach (object o in list)
                {
                    if (o is string s) options.Add(new KeyValuePair<string, string>(s, s));
                    else if (Obj(o) is Dictionary<string, object> d) options.Add(new KeyValuePair<string, string>(S(d, "value", S(d, "label", "")), S(d, "label", S(d, "value", ""))));
                }
            }

            string current = ctx.Get(id, S(n, "value", options.Count > 0 ? options[0].Key : ""));
            string label = options.FirstOrDefault(o => o.Key == current).Value ?? current;
            if (Widgets.ButtonText(rect, label))
            {
                var items = options.Select(o =>
                {
                    string key = o.Key;
                    return new FloatMenuOption(o.Value, () => { ctx.Set(id, key); ctx.Emit?.Invoke(id, "change", key); });
                }).ToList();
                if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
            }
        }

        private static void DrawList(Dictionary<string, object> n, Rect rect, WidgetContext ctx, string id)
        {
            var items = new List<(string id, string label, string tip)>();
            if (n.TryGetValue("items", out object iv) && iv is List<object> list)
            {
                foreach (object o in list)
                {
                    if (o is string s) items.Add((s, s, null));
                    else if (Obj(o) is Dictionary<string, object> d) items.Add((S(d, "id", S(d, "label", "")), S(d, "label", S(d, "id", "")), S(d, "tip")));
                }
            }

            string selected = ctx.Get(id, S(n, "selected"));
            bool reorder = B(n, "reorder");
            Widgets.DrawMenuSection(rect);
            Rect view = new Rect(0f, 0f, rect.width - 20f, items.Count * 24f);
            Vector2 scroll = ctx.Scrolls.TryGetValue(id ?? "list", out Vector2 sv) ? sv : Vector2.zero;
            Widgets.BeginScrollView(rect.ContractedBy(2f), ref scroll, view);
            for (int i = 0; i < items.Count; i++)
            {
                Rect row = new Rect(0f, i * 24f, view.width, 24f);
                if (items[i].id == selected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Widgets.Label(new Rect(row.x + 6f, row.y + 2f, row.width - 12f, 22f), items[i].label);
                if (!string.IsNullOrEmpty(items[i].tip)) TooltipHandler.TipRegion(row, items[i].tip);
                if (reorder && Mouse.IsOver(row) && Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
                {
                    ctx.DragList = id;
                    ctx.DragFrom = i;
                    Event.current.Use();
                }
                else if (Widgets.ButtonInvisible(row))
                {
                    ctx.Set(id, items[i].id);
                    ctx.Emit?.Invoke(id, "select", items[i].id);
                }

                if (ctx.DragList == id && ctx.DragFrom >= 0 && Event.current.type == EventType.MouseUp && Mouse.IsOver(row))
                {
                    if (i != ctx.DragFrom) ctx.Emit?.Invoke(id, "reorder", new Dictionary<string, object> { ["from"] = (long)(ctx.DragFrom + 1), ["to"] = (long)(i + 1) });
                    ctx.DragList = null;
                    ctx.DragFrom = -1;
                }
            }

            Widgets.EndScrollView();
            ctx.Scrolls[id ?? "list"] = scroll;
            if (Event.current.type == EventType.MouseUp && ctx.DragList == id) { ctx.DragList = null; ctx.DragFrom = -1; }
            if (ctx.DragList == id && ctx.DragFrom >= 0 && ctx.DragFrom < items.Count)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                Widgets.Label(new Rect(Event.current.mousePosition.x + 8f, Event.current.mousePosition.y, 200f, 22f), items[ctx.DragFrom].label);
                GUI.color = Color.white;
            }
        }

        private static void DrawTable(Dictionary<string, object> n, Rect rect, WidgetContext ctx)
        {
            var cols = Kids(n, "columns");
            if (cols.Count == 0) return;
            var widths = RowWidths(cols, rect.width, 0f);
            float x = rect.x;
            GameFont old = Text.Font;
            Text.Font = GameFont.Small;
            for (int i = 0; i < cols.Count; i++)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x + 4f, rect.y, widths[i] - 4f, 24f), "<b>" + S(cols[i], "label", "") + "</b>");
                x += widths[i];
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(rect.x, rect.y + 26f, rect.width);
            if (n.TryGetValue("rows", out object rv) && rv is List<object> rows)
            {
                float y = rect.y + 28f;
                int index = 0;
                foreach (object row in rows)
                {
                    var cells = row as List<object>;
                    if (cells == null) continue;
                    Rect rr = new Rect(rect.x, y, rect.width, 24f);
                    if (index % 2 == 1) Widgets.DrawLightHighlight(rr);
                    x = rect.x;
                    for (int i = 0; i < cols.Count && i < cells.Count; i++)
                    {
                        Rect cr = new Rect(x + 4f, y, widths[i] - 4f, 24f);
                        if (cells[i] is Dictionary<string, object> cn) Draw(cn, cr, ctx);
                        else { Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(cr, Convert.ToString(cells[i], CultureInfo.InvariantCulture)); Text.Anchor = TextAnchor.UpperLeft; }
                        x += widths[i];
                    }

                    y += 24f;
                    index++;
                }
            }

            Text.Font = old;
        }

        private static void DrawTabs(Dictionary<string, object> n, Rect rect, WidgetContext ctx, string id)
        {
            var tabs = Kids(n, "tabs");
            string active = ActiveTab(n, tabs, ctx);
            float x = rect.x;
            foreach (var t in tabs)
            {
                string label = S(t, "label", S(t, "id", ""));
                float w = Text.CalcSize(label).x + 24f;
                Rect tr = new Rect(x, rect.y, w, 28f);
                bool on = S(t, "id") == active;
                if (on) Widgets.DrawHighlightSelected(tr);
                if (Widgets.ButtonText(tr, label, !on) && !on)
                {
                    ctx.Set(id ?? "tabs", S(t, "id"));
                    ctx.Emit?.Invoke(id, "tab", S(t, "id"));
                }

                x += w + 2f;
            }

            Widgets.DrawLineHorizontal(rect.x, rect.y + 29f, rect.width);
            var content = tabs.FirstOrDefault(t => S(t, "id") == active);
            if (content != null && Has(content, "content"))
            {
                Dictionary<string, object> body = Obj(content["content"]);
                Draw(body, new Rect(rect.x, rect.y + 34f, rect.width, Measure(body, rect.width, ctx)), ctx);
            }
        }

        private static void DrawScroll(Dictionary<string, object> n, Rect rect, WidgetContext ctx, string id)
        {
            Dictionary<string, object> child = Obj(n.ContainsKey("child") ? n["child"] : null);
            if (child == null) return;
            float contentHeight = Measure(child, rect.width - 20f, ctx);
            Rect view = new Rect(0f, 0f, rect.width - 20f, Mathf.Max(contentHeight, rect.height));
            string key = id ?? "scroll";
            Vector2 scroll = ctx.Scrolls.TryGetValue(key, out Vector2 sv) ? sv : Vector2.zero;
            Widgets.BeginScrollView(rect, ref scroll, view);
            Draw(child, new Rect(0f, 0f, view.width, contentHeight), ctx);
            Widgets.EndScrollView();
            ctx.Scrolls[key] = scroll;
        }
    }

    // Colour presets and the default font, shared by every window and options page. Mods change them with game.hud.
    internal static class Theme
    {
        public static string DefaultFont = "small";

        public static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            ["accent"] = new Color(0.3f, 0.7f, 0.9f),
            ["good"] = new Color(0.4f, 0.85f, 0.4f),
            ["warning"] = new Color(0.95f, 0.8f, 0.3f),
            ["bad"] = new Color(0.9f, 0.35f, 0.3f),
            ["muted"] = new Color(0.65f, 0.65f, 0.65f),
            ["text"] = Color.white,
        };
    }
}
