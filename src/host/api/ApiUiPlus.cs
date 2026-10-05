using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    internal static class ApiUiPlus
    {
        public static void Register()
        {
            ApiRegistry.Register("ui.panel", OpenPanel, "gameplay");
            ApiRegistry.Register("ui.close_panel", ClosePanel, "gameplay");
        }

        private static string OpenPanel(Dictionary<string, string> args)
        {
            string title = Str(args, "title");
            if (string.IsNullOrEmpty(title)) title = "RimKit";
            string body = Str(args, "body");
            var checks = ParseLines(Str(args, "checks"));
            var listItems = ParseLines(Str(args, "list"));
            Find.WindowStack?.Add(new RimLuaPanelWindow(title, body, checks, listItems, Str(args, "buttons")));
            return OkBool(true);
        }

        private static string ClosePanel(Dictionary<string, string> args)
        {
            if (Find.WindowStack == null) return OkBool(false);
            var win = Find.WindowStack.Windows.FirstOrDefault(w => w is RimLuaPanelWindow);
            if (win != null)
            {
                win.Close();
                return OkBool(true);
            }
            return OkBool(false);
        }

        private static List<string> ParseLines(string blob)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(blob)) return list;
            foreach (string line in blob.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list;
        }
    }

    internal sealed class RimLuaPanelWindow : Window
    {
        private readonly string title;
        private string body;
        private readonly List<(string label, bool on)> checks;
        private readonly List<string> listItems;
        private readonly List<(string label, int id)> buttons;
        private Vector2 scroll;

        public RimLuaPanelWindow(string title, string body, List<string> checkLabels, List<string> listItems, string buttonBlob)
        {
            this.title = title ?? "RimKit";
            this.body = body ?? "";
            checks = new List<(string, bool)>();
            if (checkLabels != null)
            {
                foreach (string c in checkLabels) checks.Add((c, false));
            }
            this.listItems = listItems ?? new List<string>();
            buttons = LuaUiBridge.ParseButtonBlob(buttonBlob);
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(480f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), title);
            Text.Font = GameFont.Small;
            float y = 40f;
            Widgets.Label(new Rect(0f, y, inRect.width, 60f), body);
            y += 68f;
            for (int i = 0; i < checks.Count; i++)
            {
                bool v = checks[i].on;
                Widgets.CheckboxLabeled(new Rect(0f, y, inRect.width, 24f), checks[i].label, ref v);
                checks[i] = (checks[i].label, v);
                y += 28f;
            }
            Rect listRect = new Rect(0f, y, inRect.width, Mathf.Min(160f, inRect.height - y - 80f));
            Rect view = new Rect(0f, 0f, listRect.width - 16f, listItems.Count * 22f + 4f);
            Widgets.BeginScrollView(listRect, ref scroll, view);
            for (int i = 0; i < listItems.Count; i++)
            {
                Widgets.Label(new Rect(0f, i * 22f, view.width, 22f), listItems[i]);
            }
            Widgets.EndScrollView();
            y = listRect.yMax + 12f;
            float bx = 0f;
            foreach (var (label, id) in buttons)
            {
                Rect br = new Rect(bx, y, 120f, 32f);
                if (Widgets.ButtonText(br, label))
                {
                    try { NativeAbi.rimlua_ui_invoke(id); }
                    catch (System.Exception e) { Log.Error("[RimKit] panel button: " + e.Message); }
                }
                bx += 128f;
            }
        }
    }
}
