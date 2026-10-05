using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimKit
{
    internal sealed class ToolSpec
    {
        public string Id;
        public string Label;
        public string Desc;
        public string Icon;
        public string Hotkey;
        public string Target = "cell";   // cell or thing
        public string Drag = "single";   // single or box
        public string Color;
        public int CanDesignate;
        public int Designate;
        public int Finished;
        public Designator Instance;
    }

    // A click or drag tool defined in Lua. can_designate returns true, false, or a string with the reason it is not allowed.
    public class Designator_LuaTool : Designator
    {
        private readonly ToolSpec spec;

        internal Designator_LuaTool(ToolSpec spec)
        {
            this.spec = spec;
            defaultLabel = spec.Label;
            defaultDesc = spec.Desc;
            icon = (string.IsNullOrEmpty(spec.Icon) ? null : ContentFinder<Texture2D>.Get(spec.Icon, false)) ?? BaseContent.BadTex;
            if (!string.IsNullOrEmpty(spec.Hotkey)) hotKey = DefDatabase<KeyBindingDef>.GetNamedSilentFail(spec.Hotkey);
            soundSucceeded = SoundDefOf.Designate_PlaceBuilding;
            useMouseIcon = true;
        }

        // Rectangle, line and other drag styles come from the game for any cell designator that names a style category.
        public override DrawStyleCategoryDef DrawStyleCategory => spec.Target == "cell" && spec.Drag == "box" ? DrawStyleCategoryDefOf.Orders : null;

        private static AcceptanceReport Report(object r)
        {
            if (r is bool b) return b;
            if (r is string s && s.Length > 0) return new AcceptanceReport(s);
            return true;
        }

        private string CellArg(IntVec3 c) => "{\"x\":" + c.x + ",\"z\":" + c.z + ",\"map\":" + HookCodec.Encode(Map) + "}";

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (spec.Target != "cell" || !c.InBounds(Map)) return false;
            if (spec.CanDesignate <= 0) return true;
            return Report(LuaCallbacks.Call(spec.CanDesignate, CellArg(c)));
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (spec.Designate > 0) LuaCallbacks.Fire(spec.Designate, CellArg(c));
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (spec.Target != "thing") return false;
            if (spec.CanDesignate <= 0) return true;
            return Report(LuaCallbacks.Call(spec.CanDesignate, HookCodec.Encode(t)));
        }

        public override void DesignateThing(Thing t)
        {
            if (spec.Designate > 0) LuaCallbacks.Fire(spec.Designate, HookCodec.Encode(t));
        }

        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();
            if (spec.Finished > 0) LuaCallbacks.Fire(spec.Finished);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            if (spec.Target == "cell" && !string.IsNullOrEmpty(spec.Color) && Find.CurrentMap != null)
            {
                IntVec3 c = UI.MouseCell();
                if (c.InBounds(Find.CurrentMap)) GenDraw.DrawFieldEdges(new List<IntVec3> { c }, WidgetRenderer.ColorOf(spec.Color, Color.white));
            }
        }
    }

    internal static class LuaDesignators
    {
        public static readonly Dictionary<string, ToolSpec> Specs = new Dictionary<string, ToolSpec>();

        public static Designator Create(ToolSpec spec)
        {
            if (Specs.TryGetValue(spec.Id, out ToolSpec previous)) DetachFromArchitect(previous);
            spec.Instance = new Designator_LuaTool(spec);
            Specs[spec.Id] = spec;
            return spec.Instance;
        }

        // Takes a tool out of every architect category it was added to.
        private static void DetachFromArchitect(ToolSpec spec)
        {
            if (spec?.Instance == null) return;
            foreach (DesignationCategoryDef cat in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                (AccessTools.Field(typeof(DesignationCategoryDef), "resolvedDesignators")?.GetValue(cat) as List<Designator>)?.Remove(spec.Instance);
            }
        }

        public static bool Remove(string id)
        {
            if (!Specs.TryGetValue(id, out ToolSpec spec)) return false;
            DetachFromArchitect(spec);
            Specs.Remove(id);
            return true;
        }

        public static string AddToArchitect(ToolSpec spec, string category)
        {
            DesignationCategoryDef cat = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(category);
            if (cat == null) return "unknown architect category " + category;
            var list = AccessTools.Field(typeof(DesignationCategoryDef), "resolvedDesignators")?.GetValue(cat) as List<Designator>;
            if (list == null) return "the architect list is not available on this game version";
            if (list.Contains(spec.Instance)) return null;
            list.Add(spec.Instance);
            return null;
        }

        public static bool Activate(ToolSpec spec)
        {
            if (Find.DesignatorManager == null || spec.Instance == null) return false;
            Find.DesignatorManager.Select(spec.Instance);
            return Find.DesignatorManager.SelectedDesignator == spec.Instance;
        }
    }
}
