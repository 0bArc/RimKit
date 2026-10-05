using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimKit
{
    // Strings for runtime-created UI. Tabs translate their label key, so the label is added as a keyed string.
    internal static class LuaStrings
    {
        public static void Define(string key, string text)
        {
            try
            {
                var lang = LanguageDatabase.activeLanguage;
                var field = AccessTools.Field(typeof(LoadedLanguage), "keyedReplacements");
                var dict = field?.GetValue(lang) as IDictionary<string, LoadedLanguage.KeyedReplacement>;
                if (dict == null) return;
                dict[key] = new LoadedLanguage.KeyedReplacement { key = key, value = text, fileSource = "RimKit", fileSourceLine = 0, fileSourceFullPath = "RimKit", isPlaceholder = false };
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] could not define the string " + key + ": " + e.Message);
            }
        }
    }

    internal sealed class TabSpec
    {
        public string Id;
        public string Label;
        public int View;
        public int OnEvent;
        public float Width = 480f;
        public float Height = 480f;
        public float Order = 100f;
        public int Visible;
        public readonly WidgetContext Context = new WidgetContext();
        public Dictionary<string, object> Tree;
        public int LastBuild = -1000;
        public int Refresh = 15;

        public void Draw(Rect rect, string arg)
        {
            Context.Emit = (id, kind, value) =>
            {
                Context.Dirty = true;
                if (OnEvent > 0) LuaCallbacks.Fire(OnEvent, JsonOut.Of(new Dictionary<string, object> { ["id"] = id, ["kind"] = kind, ["value"] = value, ["state"] = Context.State }));
            };
            if (Tree == null || Context.Dirty || Time.frameCount - LastBuild >= Refresh)
            {
                LastBuild = Time.frameCount;
                Context.Dirty = false;
                object r = LuaCallbacks.Call(View, "{\"state\":" + JsonOut.Of(Context.State) + (arg != null ? ",\"subject\":" + arg : "") + "}");
                Tree = r as Dictionary<string, object> ?? Tree;
            }

            if (Tree == null) return;
            float h = WidgetRenderer.Measure(Tree, rect.width, Context);
            WidgetRenderer.Draw(Tree, new Rect(rect.x, rect.y, rect.width, Mathf.Min(h, rect.height)), Context);
        }
    }

    // A main tab (the bar at the bottom of the screen) showing a widget view.
    public class MainTabWindow_LuaWidgets : MainTabWindow
    {
        internal static readonly Dictionary<string, TabSpec> Specs = new Dictionary<string, TabSpec>();

        private TabSpec Spec => def != null && Specs.TryGetValue(def.defName, out TabSpec s) ? s : null;

        public override Vector2 RequestedTabSize => Spec != null ? new Vector2(Spec.Width, Spec.Height) : new Vector2(480f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            Spec?.Draw(inRect, null);
        }
    }

    // An inspect tab (next to Health, Needs, ...) showing a widget view for the selected thing.
    internal sealed class ITab_LuaWidgets : ITab
    {
        private readonly TabSpec spec;

        public string SpecId => spec.Id;

        public ITab_LuaWidgets(TabSpec spec)
        {
            this.spec = spec;
            size = new Vector2(spec.Width, spec.Height);
            labelKey = "RimKit_Tab_" + spec.Id;
        }

        public override bool IsVisible => SelThing != null && (spec.Visible <= 0 || LuaCallbacks.CallBool(spec.Visible, HookCodec.Encode(SelThing), true));

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            spec.Draw(rect, HookCodec.Encode(SelThing));
        }
    }

    internal sealed class ColumnSpec
    {
        public string Id;
        public string Label;
        public int Cell;
        public int Sort;
        public int OnClick;
        public float Width = 120f;
    }

    // A column in a pawn table (work tab, animals tab, ...). The cell callback returns a string or { text, color, tip }.
    public class PawnColumnWorker_Lua : PawnColumnWorker
    {
        internal static readonly Dictionary<string, ColumnSpec> Specs = new Dictionary<string, ColumnSpec>();

        private ColumnSpec Spec => def != null && Specs.TryGetValue(def.defName, out ColumnSpec s) ? s : null;

        public override int GetMinWidth(PawnTable table) => Mathf.RoundToInt(Spec?.Width ?? 100f);

        public override int GetOptimalWidth(PawnTable table) => GetMinWidth(table);

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            ColumnSpec s = Spec;
            if (s == null) return;
            object r = LuaCallbacks.Call(s.Cell, HookCodec.Encode(pawn));
            string text = r as string;
            string tip = null;
            Color color = Color.white;
            if (r is Dictionary<string, object> d)
            {
                text = Json.GetString(d, "text", "");
                tip = Json.GetString(d, "tip");
                color = WidgetRenderer.ColorOf(Json.GetString(d, "color"), Color.white);
            }

            if (text == null) return;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = color;
            Widgets.Label(new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (!string.IsNullOrEmpty(tip)) TooltipHandler.TipRegion(rect, tip);
            if (s.OnClick > 0 && Widgets.ButtonInvisible(rect)) LuaCallbacks.Fire(s.OnClick, HookCodec.Encode(pawn));
        }

        public override int Compare(Pawn a, Pawn b)
        {
            ColumnSpec s = Spec;
            if (s == null || s.Sort <= 0) return 0;
            return LuaCallbacks.CallNumber(s.Sort, HookCodec.Encode(a), 0).CompareTo(LuaCallbacks.CallNumber(s.Sort, HookCodec.Encode(b), 0));
        }
    }

    internal static class LuaTabs
    {
        public static string AddMain(TabSpec spec)
        {
            MainButtonDef existing = DefDatabase<MainButtonDef>.GetNamedSilentFail("RimKit_" + spec.Id);
            if (existing != null)
            {
                // Registering the same id again (a hot reload) replaces the callbacks and shows the button again.
                MainTabWindow_LuaWidgets.Specs[existing.defName] = spec;
                existing.label = spec.Label;
                existing.description = spec.Label;
                existing.order = (int)spec.Order;
                existing.buttonVisible = true;
                return null;
            }
            var def = new MainButtonDef
            {
                defName = "RimKit_" + spec.Id,
                label = spec.Label,
                description = spec.Label,
                tabWindowClass = typeof(MainTabWindow_LuaWidgets),
                order = (int)spec.Order,
                buttonVisible = true,
                validWithoutMap = false,
                minimized = false,
                canBeTutorDenied = false,
                iconPath = null,
            };
            def.shortHash = (ushort)(Math.Abs(def.defName.GetHashCode()) % 60000 + 1);
            MainTabWindow_LuaWidgets.Specs[def.defName] = spec;
            DefDatabase<MainButtonDef>.Add(def);
            try
            {
                var root = (Find.UIRoot as UIRoot_Play)?.mainButtonsRoot;
                var list = AccessTools.Field(typeof(MainButtonsRoot), "allButtonsInOrder")?.GetValue(root) as List<MainButtonDef>;
                if (list != null)
                {
                    list.Add(def);
                    list.Sort((a, b) => a.order.CompareTo(b.order));
                }
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] the main tab is registered but the bar could not be refreshed: " + e.Message);
            }

            return null;
        }

        public static string AddInspect(TabSpec spec, IEnumerable<string> defNames, string target)
        {
            LuaStrings.Define("RimKit_Tab_" + spec.Id, spec.Label);
            var defs = new List<ThingDef>();
            foreach (string n in defNames)
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d == null) return "unknown thing def " + n;
                defs.Add(d);
            }

            if (defs.Count == 0)
            {
                switch (target)
                {
                    case "pawn": defs.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Pawn)); break;
                    case "building": defs.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Building)); break;
                    case "item": defs.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Item)); break;
                    case "plant": defs.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Plant)); break;
                    default: return "give defs or a target of pawn, building, item or plant";
                }
            }

            foreach (ThingDef d in defs)
            {
                if (d.inspectorTabsResolved == null) d.inspectorTabsResolved = new List<InspectTabBase>();
                d.inspectorTabsResolved.RemoveAll(x => x is ITab_LuaWidgets old && old.SpecId == spec.Id);
                d.inspectorTabsResolved.Add(new ITab_LuaWidgets(spec));
            }

            return null;
        }

        /// <summary>Removes a main tab, inspect tab or pawn column by id. Main tabs and columns are defs, so they are hidden and detached instead of deleted.</summary>
        public static bool Remove(string id)
        {
            bool removed = false;
            MainButtonDef main = DefDatabase<MainButtonDef>.GetNamedSilentFail("RimKit_" + id);
            if (main != null && MainTabWindow_LuaWidgets.Specs.Remove(main.defName))
            {
                main.buttonVisible = false;
                removed = true;
            }

            PawnColumnDef col = DefDatabase<PawnColumnDef>.GetNamedSilentFail("RimKit_Col_" + id);
            if (col != null && PawnColumnWorker_Lua.Specs.Remove(col.defName))
            {
                foreach (PawnTableDef table in DefDatabase<PawnTableDef>.AllDefsListForReading) table.columns.Remove(col);
                removed = true;
            }

            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d.inspectorTabsResolved != null && d.inspectorTabsResolved.RemoveAll(x => x is ITab_LuaWidgets old && old.SpecId == id) > 0) removed = true;
            }

            return removed;
        }

        public static string AddColumn(string table, ColumnSpec spec)
        {
            PawnTableDef tableDef = DefDatabase<PawnTableDef>.GetNamedSilentFail(table);
            if (tableDef == null) return "unknown pawn table " + table;
            PawnColumnDef existing = DefDatabase<PawnColumnDef>.GetNamedSilentFail("RimKit_Col_" + spec.Id);
            if (existing != null)
            {
                // Registering the same id again (a hot reload) replaces the callbacks.
                PawnColumnWorker_Lua.Specs[existing.defName] = spec;
                existing.label = spec.Label;
                if (!tableDef.columns.Contains(existing)) tableDef.columns.Add(existing);
                return null;
            }
            var def = new PawnColumnDef { defName = "RimKit_Col_" + spec.Id, label = spec.Label, workerClass = typeof(PawnColumnWorker_Lua), sortable = spec.Sort > 0 };
            def.shortHash = (ushort)(Math.Abs(def.defName.GetHashCode()) % 60000 + 1);
            PawnColumnWorker_Lua.Specs[def.defName] = spec;
            DefDatabase<PawnColumnDef>.Add(def);
            tableDef.columns.Add(def);
            return null;
        }
    }
}
