using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimKit
{
    // A window whose content is a Lua widget tree. The view function is called when something changed or every refresh interval.
    internal sealed class LuaWidgetWindow : Window
    {
        public readonly int WindowId;
        private readonly string title;
        private readonly int viewId;
        private readonly int eventId;
        private readonly Vector2 size;
        private readonly int refreshFrames;
        private readonly WidgetContext ctx = new WidgetContext();
        private Dictionary<string, object> tree;
        private int lastBuild = -1000;
        private Vector2 rootScroll;

        public WidgetContext Context => ctx;

        public LuaWidgetWindow(int windowId, string title, int viewId, int eventId, float width, float height, int refreshFrames, bool pause, bool resizable)
        {
            WindowId = windowId;
            this.title = title;
            this.viewId = viewId;
            this.eventId = eventId;
            size = new Vector2(width, height);
            this.refreshFrames = Math.Max(1, refreshFrames);
            doCloseX = true;
            forcePause = pause;
            draggable = true;
            resizeable = resizable;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            ctx.Emit = Emit;
        }

        public override Vector2 InitialSize => size;

        private void Emit(string id, string kind, object value)
        {
            ctx.Dirty = true;
            if (eventId <= 0) return;
            var arg = new Dictionary<string, object> { ["window"] = (long)WindowId, ["id"] = id, ["kind"] = kind, ["value"] = value, ["state"] = ctx.State };
            LuaCallbacks.Fire(eventId, JsonOut.Of(arg));
        }

        public void Rebuild()
        {
            lastBuild = Time.frameCount;
            ctx.Dirty = false;
            string stateJson = JsonOut.Of(ctx.State);
            object result = LuaCallbacks.Call(viewId, "{\"state\":" + stateJson + ",\"window\":" + WindowId + "}");
            if (result is Dictionary<string, object> d) tree = d;
            else if (tree == null) tree = new Dictionary<string, object> { ["type"] = "label", ["text"] = "(the view returned nothing)" };
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (tree == null || ctx.Dirty || Time.frameCount - lastBuild >= refreshFrames) Rebuild();
            Rect area = new Rect(0f, 0f, inRect.width, inRect.height);
            float contentHeight = WidgetRenderer.Measure(tree, area.width - 16f, ctx);
            if (contentHeight <= area.height)
            {
                WidgetRenderer.Draw(tree, new Rect(0f, 0f, area.width, contentHeight), ctx);
                return;
            }

            Rect view = new Rect(0f, 0f, area.width - 16f, contentHeight);
            Widgets.BeginScrollView(area, ref rootScroll, view);
            WidgetRenderer.Draw(tree, view, ctx);
            Widgets.EndScrollView();
        }

        public override void PostClose()
        {
            base.PostClose();
            WidgetWindows.Forget(WindowId);
            if (eventId > 0) LuaCallbacks.Fire(eventId, JsonOut.Of(new Dictionary<string, object> { ["window"] = (long)WindowId, ["id"] = null, ["kind"] = "close", ["state"] = ctx.State }));
        }

        public string Title => title;
    }

    internal static class WidgetWindows
    {
        private static readonly Dictionary<int, LuaWidgetWindow> Open = new Dictionary<int, LuaWidgetWindow>();
        private static int next = 1;

        public static int Add(string title, int viewId, int eventId, float width, float height, int refreshFrames, bool pause, bool resizable)
        {
            int id = next++;
            var w = new LuaWidgetWindow(id, title, viewId, eventId, width, height, refreshFrames, pause, resizable);
            Open[id] = w;
            Find.WindowStack.Add(w);
            return id;
        }

        public static LuaWidgetWindow Get(int id) => Open.TryGetValue(id, out LuaWidgetWindow w) ? w : null;

        public static void Forget(int id) => Open.Remove(id);

        public static IEnumerable<LuaWidgetWindow> All => Open.Values.ToList();
    }

    // Yes/no confirmation. Both answers call back into Lua.
    internal sealed class Dialog_LuaConfirm : Window
    {
        private readonly string text;
        private readonly int yesId;
        private readonly int noId;
        private readonly string yesLabel;
        private readonly string noLabel;
        private bool answered;

        public Dialog_LuaConfirm(string text, int yesId, int noId, string yesLabel, string noLabel)
        {
            this.text = text;
            this.yesId = yesId;
            this.noId = noId;
            this.yesLabel = string.IsNullOrEmpty(yesLabel) ? "Yes" : yesLabel;
            this.noLabel = string.IsNullOrEmpty(noLabel) ? "No" : noLabel;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = false;
            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize => new Vector2(420f, 190f);

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, inRect.height - 44f), text);
            float w = (inRect.width - 12f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 36f, w, 32f), yesLabel)) Answer(yesId);
            if (Widgets.ButtonText(new Rect(w + 12f, inRect.height - 36f, w, 32f), noLabel)) Answer(noId);
        }

        private void Answer(int id)
        {
            answered = true;
            Close();
            if (id > 0) LuaCallbacks.Fire(id);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!answered && noId > 0) LuaCallbacks.Fire(noId);
        }
    }

    // Text input. Calls back with the entered text.
    internal sealed class Dialog_LuaPrompt : Window
    {
        private readonly string title;
        private readonly string text;
        private string value;
        private readonly int okId;
        private readonly int cancelId;
        private bool done;

        public Dialog_LuaPrompt(string title, string text, string value, int okId, int cancelId)
        {
            this.title = title;
            this.text = text;
            this.value = value ?? "";
            this.okId = okId;
            this.cancelId = cancelId;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(420f, 230f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), title);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 60f), text);
            value = Widgets.TextField(new Rect(0f, 100f, inRect.width, 30f), value);
            float w = (inRect.width - 12f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 36f, w, 32f), "OK")) Finish(true);
            if (Widgets.ButtonText(new Rect(w + 12f, inRect.height - 36f, w, 32f), "Cancel")) Finish(false);
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                Finish(true);
                Event.current.Use();
            }
        }

        private void Finish(bool ok)
        {
            done = true;
            Close();
            if (ok && okId > 0) LuaCallbacks.Fire(okId, JsonOut.Of(value));
            else if (!ok && cancelId > 0) LuaCallbacks.Fire(cancelId);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!done && cancelId > 0) LuaCallbacks.Fire(cancelId);
        }
    }
}
