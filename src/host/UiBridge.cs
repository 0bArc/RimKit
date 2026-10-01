using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimLuaKit
{
    internal static class LuaUiBridge
    {
        public static void OpenWindow(string title, string body, List<(string label, int id)> buttons)
        {
            Find.WindowStack?.Add(new RimLuaDebugWindow(title ?? "RimLua", body ?? "", buttons ?? new List<(string, int)>()));
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
                        Log.Error("[RimLuaKit] ui float callback failed: " + e);
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

        public override Vector2 InitialSize => new Vector2(420f, 360f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), title);
            Text.Font = GameFont.Small;

            float y = inRect.y + 40f;
            float btnH = 32f;
            float gap = 6f;
            float listH = buttons.Count * (btnH + gap) + 8f;
            Rect view = new Rect(0f, 0f, inRect.width - 16f, listH);
            Rect scrollRect = new Rect(inRect.x, y, inRect.width, Math.Min(160f, listH + 4f));
            Widgets.BeginScrollView(scrollRect, ref scroll, view);
            float by = 0f;
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
                        Log.Error("[RimLuaKit] ui button callback failed: " + e);
                    }
                }

                by += btnH + gap;
            }

            Widgets.EndScrollView();

            y = scrollRect.yMax + 10f;
            Rect bodyRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Widgets.Label(bodyRect, body);
        }
    }
}
