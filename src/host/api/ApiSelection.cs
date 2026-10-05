using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.selection and game.camera: what the player has selected or inspects, and camera control. Ops are selection.* and camera.*.
    // The older game.selection.first and game.selection.things stay in GameApi.
    internal static class ApiSelection
    {
        public static void Register()
        {
            R("selection.pawns", SelectedPawns);
            R("selection.zones", SelectedZones);
            R("selection.count", Count);
            R("selection.select", Select);
            R("selection.add", Add);
            R("selection.clear", Clear);
            R("selection.inspected", Inspected);
            R("camera.jump_cell", JumpCell);
            R("camera.jump_thing", JumpThing);
            R("camera.position", Position);
            R("camera.mouse_cell", MouseCell);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string NoGame() => Fail("RK3001", "no game is loaded");

        private static string SelectedPawns(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            return OkHandles(Find.Selector.SelectedPawns.ToList());
        }

        private static string SelectedZones(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            var arr = Jb.Arr();
            foreach (object o in Find.Selector.SelectedObjects.Where(x => x is Zone))
            {
                var z = (Zone)o;
                arr.Add(Jb.Obj().I("id", z.ID).S("label", z.label).S("type", z.GetType().Name).I("cells", z.cells.Count));
            }

            return arr.Ok();
        }

        private static string Count(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            return OkInt(Find.Selector.NumSelected);
        }

        private static string Select(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null || !t.Spawned) return Fail("RK2001", "thing handle is stale or not spawned");
            Find.Selector.ClearSelection();
            Find.Selector.Select(t, false, true);
            return OkBool(Find.Selector.IsSelected(t));
        }

        private static string Add(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null || !t.Spawned) return Fail("RK2001", "thing handle is stale or not spawned");
            Find.Selector.Select(t, false, true);
            return OkBool(Find.Selector.IsSelected(t));
        }

        private static string Clear(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            Find.Selector.ClearSelection();
            return OkBool(true);
        }

        // The single selected object: a thing (handle) or a zone, nil when nothing or several are selected.
        private static string Inspected(Dictionary<string, string> a)
        {
            if (Find.Selector == null) return NoGame();
            object o = Find.Selector.SingleSelectedObject;
            if (o == null) return OkJson("null");
            if (o is Thing t) return Jb.Obj().S("kind", "thing").H("thing", t).Ok();
            if (o is Zone z) return Jb.Obj().S("kind", "zone").I("id", z.ID).S("label", z.label).Ok();
            return Jb.Obj().S("kind", o.GetType().Name).Ok();
        }

        private static string JumpCell(Dictionary<string, string> a)
        {
            Map map = ObjectHandles.Get<Map>(Int(a, "map"));
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var c = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!c.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            CameraJumper.TryJump(c, map);
            return OkBool(true);
        }

        private static string JumpThing(Dictionary<string, string> a)
        {
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            CameraJumper.TryJump(t);
            return OkBool(true);
        }

        private static string Position(Dictionary<string, string> a)
        {
            if (Find.CameraDriver == null || Find.CurrentMap == null) return NoGame();
            IntVec3 c = Find.CameraDriver.MapPosition;
            return Jb.Obj().I("x", c.x).I("z", c.z).H("map", Find.CurrentMap).S("zoom", Find.CameraDriver.CurrentZoom.ToString()).Ok();
        }

        private static string MouseCell(Dictionary<string, string> a)
        {
            if (Find.CurrentMap == null) return NoGame();
            IntVec3 c = UI.MouseCell();
            return Jb.Obj().I("x", c.x).I("z", c.z).B("in_bounds", c.InBounds(Find.CurrentMap)).Ok();
        }
    }
}
