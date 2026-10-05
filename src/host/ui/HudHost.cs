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
    internal sealed class AlertSpec
    {
        public string Id;
        public string Label;
        public string Explanation;
        public int Check;
        public AlertPriority Priority = AlertPriority.Medium;
    }

    // An alert (the red and yellow boxes at the right edge) whose state comes from a Lua function. The check function returns
    // { active, label?, explanation?, culprits? }.
    public class Alert_LuaAlert : Alert
    {
        internal static readonly Dictionary<string, AlertSpec> Specs = new Dictionary<string, AlertSpec>();
        private AlertSpec spec;
        private string label = "";
        private string explanation = "";

        // The game creates one instance of every Alert subclass at startup. That instance has no spec and stays quiet.
        public Alert_LuaAlert()
        {
        }

        internal Alert_LuaAlert(AlertSpec spec)
        {
            this.spec = spec;
            defaultPriority = spec.Priority;
            label = spec.Label ?? spec.Id;
            explanation = spec.Explanation ?? "";
        }

        public override string GetLabel() => label;

        public override TaggedString GetExplanation() => explanation;

        public override AlertReport GetReport()
        {
            if (spec == null || Find.CurrentMap == null) return false;
            object r = LuaCallbacks.Call(spec.Check);
            if (r is bool b) return b;
            if (!(r is Dictionary<string, object> d)) return false;
            if (!Json.GetBool(d, "active")) return false;
            label = Json.GetString(d, "label", spec.Label ?? spec.Id);
            explanation = Json.GetString(d, "explanation", spec.Explanation ?? "");
            var culprits = new List<Thing>();
            if (d.TryGetValue("culprits", out object cv) && cv is List<object> list)
            {
                foreach (object item in list)
                {
                    Thing t = Opts.HandleOf<Thing>(item);
                    if (t != null && t.Spawned) culprits.Add(t);
                }
            }

            return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : AlertReport.Active;
        }
    }

    // A letter with answer buttons. Choosing one raises the "letter.choice" event with the tag and index, so the answer survives
    // saving and loading (no callback is stored in the save).
    public class ChoiceLetter_LuaChoices : ChoiceLetter
    {
        public List<string> choiceLabels = new List<string>();
        public string tag = "";

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                for (int i = 0; i < choiceLabels.Count; i++)
                {
                    int index = i + 1;
                    string text = choiceLabels[i];
                    string t = tag;
                    yield return new DiaOption(text)
                    {
                        action = () => LuaEventQueue.EnqueuePayload("letter.choice", "{\"tag\":" + JsonLite.Quote(t) + ",\"index\":" + index + ",\"label\":" + JsonLite.Quote(text) + "}"),
                        resolveTree = true,
                    };
                }

                yield return Option_Close;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref choiceLabels, "rimkit_choices", LookMode.Value);
            Scribe_Values.Look(ref tag, "rimkit_tag", "");
            choiceLabels ??= new List<string>();
        }
    }

    [StaticConstructorOnStartup]
    internal static class HudStartup
    {
        static HudStartup()
        {
            try
            {
                EnsureLetterDef();
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] could not create the choice letter def: " + e.Message);
            }
        }

        public static LetterDef EnsureLetterDef()
        {
            LetterDef existing = DefDatabase<LetterDef>.GetNamedSilentFail("RimKit_ChoiceLetter");
            if (existing != null) return existing;
            LetterDef template = LetterDefOf.NeutralEvent;
            var def = new LetterDef
            {
                defName = "RimKit_ChoiceLetter",
                letterClass = typeof(ChoiceLetter_LuaChoices),
                color = template.color,
                flashColor = template.flashColor,
                flashInterval = template.flashInterval,
                bounce = template.bounce,
                arriveSound = template.arriveSound,
            };
            def.shortHash = (ushort)(Math.Abs(def.defName.GetHashCode()) % 60000 + 1);
            DefDatabase<LetterDef>.Add(def);
            return def;
        }
    }

    internal static class HudHost
    {
        public sealed class StatusLine
        {
            public string Id;
            public string Text;
            public string Color;
            public string Corner = "top_left";
        }

        public static readonly List<StatusLine> Lines = new List<StatusLine>();
        private static readonly Dictionary<string, Alert_LuaAlert> Instances = new Dictionary<string, Alert_LuaAlert>();
        private static bool patched;

        public static void EnsurePatched()
        {
            if (patched) return;
            patched = true;
            try
            {
                var harmony = new Harmony("rimkit.hud");
                harmony.Patch(AccessTools.Method(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI)), postfix: new HarmonyMethod(typeof(HudHost), nameof(DrawStatusLines)));
            }
            catch (Exception e)
            {
                patched = false;
                Log.Error("[RimKit] could not patch UIRoot_Play.UIRootOnGUI: " + e.Message);
            }
        }

        public static void DrawStatusLines()
        {
            if (Lines.Count == 0 || Event.current.type != EventType.Repaint) return;
            GameFont old = Text.Font;
            Text.Font = GameFont.Small;
            foreach (string corner in new[] { "top_left", "top_right", "bottom_left" })
            {
                float y = corner == "bottom_left" ? UI.screenHeight - 120f : 70f;
                foreach (StatusLine line in Lines.Where(l => l.Corner == corner))
                {
                    float w = Text.CalcSize(line.Text).x + 12f;
                    float x = corner == "top_right" ? UI.screenWidth - w - 10f : 10f;
                    Rect r = new Rect(x, y, w, 22f);
                    Widgets.DrawBoxSolid(r, new Color(0f, 0f, 0f, 0.45f));
                    GUI.color = WidgetRenderer.ColorOf(line.Color, Color.white);
                    Widgets.Label(new Rect(r.x + 6f, r.y + 1f, r.width, r.height), line.Text);
                    GUI.color = Color.white;
                    y += corner == "bottom_left" ? -24f : 24f;
                }
            }

            Text.Font = old;
        }

        public static string RegisterAlert(AlertSpec spec)
        {
            var readout = (Find.UIRoot as UIRoot_Play)?.alerts;
            if (readout == null) return "the game UI is not ready yet, register alerts after game.ready";
            // The list of every alert is a static field named AllAlerts in 1.6 and was an instance field before.
            FieldInfo listField = AccessTools.Field(typeof(AlertsReadout), "AllAlerts") ?? AccessTools.Field(typeof(AlertsReadout), "allAlerts");
            var all = listField?.GetValue(listField.IsStatic ? null : readout) as List<Alert>;
            if (all == null) return "alerts cannot be added on this game version";
            if (Instances.TryGetValue(spec.Id, out Alert_LuaAlert old)) all.Remove(old);
            Alert_LuaAlert.Specs[spec.Id] = spec;
            var alert = new Alert_LuaAlert(spec);
            Instances[spec.Id] = alert;
            all.Add(alert);
            return null;
        }
    }
}
