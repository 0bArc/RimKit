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
    // Lua-defined gizmos: action buttons, toggles and sliders on pawns, buildings, items and plants. The specs live here, one Harmony
    // postfix on Thing.GetGizmos appends the ones that apply to the selected thing.
    internal sealed class GizmoSpec
    {
        public string Id;
        public string Kind;          // action, toggle, slider
        public string Label;
        public string Desc;
        public string Icon;
        public string Hotkey;
        public string Target = "any";   // pawn, colonist, building, item, plant, thing, any
        public HashSet<string> Defs = new HashSet<string>();
        public float Order = 100f;
        public int Visible;
        public int OnClick;
        public int State;
        public int Value;
        public int OnChange;
        public float Min;
        public float Max = 1f;
        public float Step;
        public string Package;
    }

    internal static class LuaGizmos
    {
        private static readonly Dictionary<string, GizmoSpec> Specs = new Dictionary<string, GizmoSpec>();
        private static bool patched;

        public static IEnumerable<GizmoSpec> All => Specs.Values.OrderBy(s => s.Order).ThenBy(s => s.Id).ToList();

        public static void Add(GizmoSpec spec)
        {
            Specs[spec.Id] = spec;
            EnsurePatched();
        }

        public static bool Remove(string id) => Specs.Remove(id);

        private static void EnsurePatched()
        {
            if (patched) return;
            patched = true;
            try
            {
                var harmony = new Harmony("rimkit.gizmos");
                MethodBase target = AccessTools.Method(typeof(Thing), nameof(Thing.GetGizmos));
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(LuaGizmos), nameof(Postfix)));
            }
            catch (Exception e)
            {
                patched = false;
                Log.Error("[RimKit] could not patch Thing.GetGizmos: " + e.Message);
            }
        }

        private static bool Applies(GizmoSpec s, Thing t)
        {
            if (s.Defs.Count > 0 && !s.Defs.Contains(t.def.defName)) return false;
            switch (s.Target)
            {
                case "pawn": return t is Pawn;
                case "colonist": return t is Pawn p && p.IsColonistPlayerControlled;
                case "building": return t is Building;
                case "item": return t.def.category == ThingCategory.Item;
                case "plant": return t is Plant;
                case "thing":
                case "any": return true;
                default: return false;
            }
        }

        public static void Postfix(Thing __instance, ref IEnumerable<Gizmo> __result)
        {
            if (Specs.Count == 0 || __instance == null) return;
            __result = Extend(__result, __instance);
        }

        private static IEnumerable<Gizmo> Extend(IEnumerable<Gizmo> original, Thing t)
        {
            if (original != null) foreach (Gizmo g in original) yield return g;
            foreach (GizmoSpec s in All)
            {
                if (!Applies(s, t)) continue;
                if (s.Visible > 0 && !LuaCallbacks.CallBool(s.Visible, HookCodec.Encode(t), true)) continue;
                Gizmo gizmo = Build(s, t);
                if (gizmo != null) yield return gizmo;
            }
        }

        private static Texture2D IconOf(GizmoSpec s) => (string.IsNullOrEmpty(s.Icon) ? null : ContentFinder<Texture2D>.Get(s.Icon, false)) ?? BaseContent.BadTex;

        private static Gizmo Build(GizmoSpec s, Thing t)
        {
            string arg = HookCodec.Encode(t);
            KeyBindingDef hotkey = string.IsNullOrEmpty(s.Hotkey) ? null : DefDatabase<KeyBindingDef>.GetNamedSilentFail(s.Hotkey);
            switch (s.Kind)
            {
                case "toggle":
                    return new Command_Toggle
                    {
                        defaultLabel = s.Label, defaultDesc = s.Desc, icon = IconOf(s), hotKey = hotkey, Order = s.Order, groupKey = s.Id.GetHashCode(),
                        isActive = () => LuaCallbacks.CallBool(s.State, arg, false),
                        toggleAction = () => LuaCallbacks.Fire(s.OnClick, arg),
                    };
                case "slider":
                    return new LuaSliderGizmo(s, arg);
                default:
                    return new Command_Action
                    {
                        defaultLabel = s.Label, defaultDesc = s.Desc, icon = IconOf(s), hotKey = hotkey, Order = s.Order, groupKey = s.Id.GetHashCode(),
                        action = () => LuaCallbacks.Fire(s.OnClick, arg),
                    };
            }
        }
    }

    // A slider in the gizmo grid. The value is read from Lua on every draw and written back when the player drags.
    internal sealed class LuaSliderGizmo : Gizmo
    {
        private readonly GizmoSpec spec;
        private readonly string arg;

        public LuaSliderGizmo(GizmoSpec spec, string arg)
        {
            this.spec = spec;
            this.arg = arg;
            Order = spec.Order;
        }

        public override float GetWidth(float maxWidth) => Mathf.Min(150f, maxWidth);

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            double value = LuaCallbacks.CallNumber(spec.Value, arg, spec.Min);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 18f), spec.Label + ": " + (spec.Step >= 1f ? Mathf.RoundToInt((float)value).ToString() : ((float)value).ToString("0.##")));
            float next = Widgets.HorizontalSlider(new Rect(inner.x, inner.y + 24f, inner.width, 22f), (float)value, spec.Min, spec.Max);
            if (spec.Step > 0f) next = Mathf.Round(next / spec.Step) * spec.Step;
            if (Mathf.Abs(next - (float)value) > 1e-5f) LuaCallbacks.Fire(spec.OnChange, JsonOut.Of(new Dictionary<string, object> { ["value"] = (double)next }).Replace("}", ",\"thing\":" + arg + "}"));
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(rect) && !string.IsNullOrEmpty(spec.Desc)) TooltipHandler.TipRegion(rect, spec.Desc);
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
