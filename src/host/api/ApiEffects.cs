using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.effects: flecks, motes, effecters, floating text, screen shake and map overlays (highlighted cells, lines, circles). Ops: effects.*.
    internal static class ApiEffects
    {
        private sealed class Overlay
        {
            public int Id;
            public string Kind;       // cells, line, circle, target
            public Map Map;
            public List<IntVec3> Cells;
            public Vector3 A, B;
            public float Radius;
            public Color Color;
            public int ExpireTick;    // game tick, -1 = until cleared
            public Thing Target;
        }

        private static readonly List<Overlay> Overlays = new List<Overlay>();
        private static int nextId = 1;
        private static bool patched;

        public static void Register()
        {
            R("effects.fleck", Fleck);
            R("effects.text", FloatText);
            R("effects.effecter", EffecterRun);
            R("effects.screen_shake", ScreenShake);
            R("effects.highlight_cells", HighlightCells);
            R("effects.line", Line);
            R("effects.circle", Circle);
            R("effects.mark", Mark);
            R("effects.clear", Clear);
            R("effects.fleck_defs", FleckDefs);
            R("effects.effecter_defs", EffecterDefs);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        private static Color ColorOf(string spec, Color fallback) => WidgetRenderer.ColorOf(spec, fallback);

        private static string Where(Dictionary<string, string> a, out Map map, out IntVec3 cell)
        {
            cell = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            if (!cell.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            return null;
        }

        // opts: scale, rotation, speed, angle (velocity direction in degrees).
        private static string Fleck(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 cell);
            if (err != null) return err;
            FleckDef def = DefDatabase<FleckDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown fleck " + Str(a, "def"));
            Opts o = Opts.From(a, "opts");
            FleckCreationData data = FleckMaker.GetDataStatic(cell.ToVector3Shifted(), map, def, (float)o.Num("scale", 1));
            data.rotation = (float)o.Num("rotation", 0);
            if (o.Has("speed")) { data.velocitySpeed = (float)o.Num("speed"); data.velocityAngle = (float)o.Num("angle", 0); }
            map.flecks.CreateFleck(data);
            return OkBool(true);
        }

        private static string FloatText(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 cell);
            if (err != null) return err;
            MoteMaker.ThrowText(cell.ToVector3Shifted(), map, Str(a, "text"), ColorOf(Str(a, "color"), Color.white));
            return OkBool(true);
        }

        // Plays an effecter once at a cell.
        private static string EffecterRun(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 cell);
            if (err != null) return err;
            EffecterDef def = DefDatabase<EffecterDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown effecter " + Str(a, "def"));
            Effecter e = def.Spawn();
            e.Trigger(new TargetInfo(cell, map), TargetInfo.Invalid);
            e.Cleanup();
            return OkBool(true);
        }

        private static string ScreenShake(Dictionary<string, string> a)
        {
            if (Find.CameraDriver == null) return Fail("RK3001", "no camera is available");
            float magnitude = Math.Max(0f, Math.Min(2f, Float(a, "magnitude")));
            Find.CameraDriver.shaker.DoShake(magnitude);
            return OkBool(true);
        }

        private static void EnsurePatched()
        {
            if (patched) return;
            patched = true;
            try
            {
                var harmony = new Harmony("rimkit.effects");
                harmony.Patch(AccessTools.Method(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate)), postfix: new HarmonyMethod(typeof(ApiEffects), nameof(DrawOverlays)));
            }
            catch (Exception e)
            {
                patched = false;
                Log.Error("[RimKit] could not patch MapInterface.MapInterfaceUpdate: " + e.Message);
            }
        }

        private static SimpleColor Simple(Color c)
        {
            if (c.r > 0.8f && c.g > 0.8f && c.b > 0.8f) return SimpleColor.White;
            if (c.r > 0.7f && c.g > 0.7f) return SimpleColor.Yellow;
            if (c.r > 0.7f && c.g > 0.35f) return SimpleColor.Orange;
            if (c.r > 0.6f && c.b > 0.6f) return SimpleColor.Magenta;
            if (c.g > 0.6f && c.b > 0.6f) return SimpleColor.Cyan;
            if (c.r > c.g && c.r > c.b) return SimpleColor.Red;
            if (c.g > c.b) return SimpleColor.Green;
            return SimpleColor.Blue;
        }

        public static void DrawOverlays()
        {
            if (Overlays.Count == 0 || Find.CurrentMap == null) return;
            int now = (Current.Game?.tickManager?.TicksGame ?? 0);
            Overlays.RemoveAll(o => o.ExpireTick >= 0 && now > o.ExpireTick || o.Map == null || o.Map.Disposed);
            foreach (Overlay o in Overlays)
            {
                if (o.Map != Find.CurrentMap) continue;
                switch (o.Kind)
                {
                    case "cells": GenDraw.DrawFieldEdges(o.Cells, o.Color); break;
                    case "line": GenDraw.DrawLineBetween(o.A, o.B, Simple(o.Color)); break;
                    case "circle": GenDraw.DrawCircleOutline(o.A, o.Radius, Simple(o.Color)); break;
                    case "target": if (o.Target != null && o.Target.Spawned) GenDraw.DrawTargetHighlight(o.Target); break;
                }
            }
        }

        private static string Add(Overlay o, int ticks)
        {
            o.Id = nextId++;
            o.ExpireTick = ticks > 0 ? ((Current.Game?.tickManager?.TicksGame ?? 0)) + ticks : -1;
            Overlays.Add(o);
            EnsurePatched();
            return OkInt(o.Id);
        }

        // cells: a list of { x, z }. opts: color, ticks (how long, default until cleared).
        private static string HighlightCells(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var cells = new List<IntVec3>();
            if (a.TryGetValue("cells", out string json) && Json.TryParse(json, out object parsed) && parsed is List<object> list)
            {
                foreach (object item in list)
                {
                    var d = Json.AsObject(item);
                    if (d == null) continue;
                    var c = new IntVec3((int)Json.GetLong(d, "x"), 0, (int)Json.GetLong(d, "z"));
                    if (c.InBounds(map)) cells.Add(c);
                }
            }

            if (cells.Count == 0) return Fail("RK1001", "cells must list at least one { x, z } inside the map");
            Opts o = Opts.From(a, "opts");
            return Add(new Overlay { Kind = "cells", Map = map, Cells = cells, Color = ColorOf(o.Str("color"), Color.yellow) }, o.Int("ticks"));
        }

        private static string Line(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 from);
            if (err != null) return err;
            var to = new IntVec3(Int(a, "x2"), 0, Int(a, "z2"));
            Opts o = Opts.From(a, "opts");
            return Add(new Overlay { Kind = "line", Map = map, A = from.ToVector3Shifted(), B = to.ToVector3Shifted(), Color = ColorOf(o.Str("color"), Color.white) }, o.Int("ticks"));
        }

        private static string Circle(Dictionary<string, string> a)
        {
            string err = Where(a, out Map map, out IntVec3 c);
            if (err != null) return err;
            Opts o = Opts.From(a, "opts");
            return Add(new Overlay { Kind = "circle", Map = map, A = c.ToVector3Shifted(), Radius = Math.Max(0.5f, Float(a, "radius")), Color = ColorOf(o.Str("color"), Color.white) }, o.Int("ticks"));
        }

        private static string Mark(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null || t.Map == null) return Fail("RK2001", "thing handle is stale or not on a map");
            return Add(new Overlay { Kind = "target", Map = t.Map, Target = t, Color = Color.white }, Int(a, "ticks"));
        }

        private static string Clear(Dictionary<string, string> a)
        {
            if (string.IsNullOrEmpty(Str(a, "id"))) { int n = Overlays.Count; Overlays.Clear(); return OkInt(n); }
            return OkInt(Overlays.RemoveAll(o => o.Id == Int(a, "id")));
        }

        private static string FleckDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<FleckDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string EffecterDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<EffecterDef>.AllDefsListForReading.Select(d => d.defName).ToList());
    }
}
