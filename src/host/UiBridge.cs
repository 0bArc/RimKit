using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimKit
{
    internal static class LuaUiBridge
    {
        public static void OpenWindow(string title, string body, List<(string label, int id)> buttons)
        {
            Find.WindowStack?.Add(new RimLuaDebugWindow(title ?? "RimKit", body ?? "", buttons ?? new List<(string, int)>()));
        }

        public static void OpenFloatMenu(List<(string label, int id)> options)
        {
            if (options == null || options.Count == 0)
            {
                return;
            }

            var items = new List<FloatMenuOption>();
            foreach (var (label, id) in options)
            {
                int captured = id;
                string capturedLabel = label;
                items.Add(new FloatMenuOption(capturedLabel, () =>
                {
                    try
                    {
                        NativeAbi.rimlua_ui_invoke(captured);
                    }
                    catch (Exception e)
                    {
                        Log.Error("[RimKit] ui float callback failed: " + e);
                    }
                }));
            }

            Find.WindowStack?.Add(new FloatMenu(items));
        }

        public static List<(string label, int id)> ParseButtonBlob(string blob)
        {
            var list = new List<(string, int)>();
            if (string.IsNullOrEmpty(blob))
            {
                return list;
            }

            foreach (string line in blob.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int tab = line.LastIndexOf('\t');
                if (tab <= 0)
                {
                    continue;
                }

                string label = line.Substring(0, tab);
                if (!int.TryParse(line.Substring(tab + 1), out int id))
                {
                    continue;
                }

                list.Add((label, id));
            }

            return list;
        }
    }

    internal sealed class RimLuaDebugWindow : Window
    {
        private readonly string title;
        private readonly string body;
        private readonly List<(string label, int id)> buttons;
        private Vector2 scroll;

        public RimLuaDebugWindow(string title, string body, List<(string label, int id)> buttons)
        {
            this.title = title;
            this.body = body;
            this.buttons = buttons;
            doCloseX = true;
            draggable = true;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize => new Vector2(460f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), title);
            Text.Font = GameFont.Small;

            // Live selection (Lua body string is frozen at open).
            string liveSel = "nil";
            Pawn livePawn = null;
            var sel = Find.Selector;
            if (sel?.SelectedPawns != null)
            {
                foreach (Pawn p in sel.SelectedPawns)
                {
                    if (p != null && !p.Destroyed)
                    {
                        livePawn = p;
                        break;
                    }
                }
            }
            if (livePawn == null && sel?.FirstSelectedObject is Pawn fp)
            {
                livePawn = fp;
            }
            if (livePawn != null)
            {
                liveSel = livePawn.LabelShortCap + " @" + livePawn.Position.x + "," + livePawn.Position.z;
            }

            float y = inRect.y + 30f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "LIVE selected: " + liveSel);
            y += 24f;

            float btnH = 30f;
            float gap = 4f;
            int count = buttons?.Count ?? 0;
            float listH = Math.Max(count * (btnH + gap) + 8f, 40f);
            float scrollH = Math.Min(inRect.height * 0.55f, listH + 4f);
            Rect scrollRect = new Rect(inRect.x, y, inRect.width, scrollH);
            Rect view = new Rect(0f, 0f, inRect.width - 20f, listH);
            Widgets.BeginScrollView(scrollRect, ref scroll, view);
            float by = 0f;
            if (count == 0)
            {
                Widgets.Label(new Rect(0f, 0f, view.width, 28f), "(no buttons)");
            }
            else
            {
                foreach (var (label, id) in buttons)
                {
                    if (Widgets.ButtonText(new Rect(0f, by, view.width - 4f, btnH), label))
                    {
                        try
                        {
                            NativeAbi.rimlua_ui_invoke(id);
                        }
                        catch (Exception e)
                        {
                            Log.Error("[RimKit] ui button callback failed: " + e);
                        }
                    }

                    by += btnH + gap;
                }
            }

            Widgets.EndScrollView();

            y = scrollRect.yMax + 8f;
            Rect bodyRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Widgets.Label(bodyRect, body ?? "");
        }
    }
}
