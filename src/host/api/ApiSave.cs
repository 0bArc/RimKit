using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimLuaKit;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.save: per-save, per-map and per-world data for Lua mods, save versioning, autosave and manual saves. Ops are save.*.
    // game.data (older) keeps working and shares the per-save store.
    internal static class ApiSave
    {
        public static void Register()
        {
            R("save.get", GetValue);
            R("save.set", SetValue);
            R("save.remove", RemoveValue);
            R("save.keys", KeysOf);
            R("save.version", VersionOf);
            R("save.set_version", SetVersion);
            R("save.game_version", GameVersion);
            R("save.autosave_interval", AutosaveInterval);
            R("save.set_autosave_interval", SetAutosaveInterval);
            R("save.now", SaveNow);
            R("save.files", SaveFiles);
            ApiRegistry.Register("save.load", a => LoadSave(a), "advanced", "0.11.0");
            R("save.forget_thing", ForgetThing);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.7.0");

        private static Dictionary<string, string> Store(Dictionary<string, string> a, out string err)
        {
            err = null;
            string scope = string.IsNullOrEmpty(Str(a, "scope")) ? "game" : Str(a, "scope").ToLowerInvariant();
            switch (scope)
            {
                case "game":
                {
                    var c = RimLuaModDataComponent.GetOrCreate();
                    if (c == null) { err = Fail("RK3001", "no game is loaded"); return null; }
                    return c.Values;
                }
                case "map":
                {
                    Map m = string.IsNullOrEmpty(Str(a, "h")) ? Find.CurrentMap : MapOf(a);
                    if (m == null) { err = Fail("RK2001", "map handle is stale or no map is loaded"); return null; }
                    var c = m.GetComponent<MapComponent_LuaData>();
                    if (c == null) { c = new MapComponent_LuaData(m); m.components.Add(c); }
                    return c.Values;
                }
                case "thing":
                {
                    // Per-object data lives in the per-save store under a key that names the thing.
                    var c = RimLuaModDataComponent.GetOrCreate();
                    if (c == null) { err = Fail("RK3001", "no game is loaded"); return null; }
                    return c.Values;
                }
                case "world":
                {
                    if (Find.World == null) { err = Fail("RK3001", "no world is loaded"); return null; }
                    var c = Find.World.GetComponent<WorldComponent_LuaData>();
                    if (c == null) { c = new WorldComponent_LuaData(Find.World); Find.World.components.Add(c); }
                    return c.Values;
                }
                default:
                    err = Fail("RK1001", "scope must be game, map, world or thing");
                    return null;
            }
        }

        private static string Key(Dictionary<string, string> a, out string err)
        {
            err = null;
            if (string.IsNullOrEmpty(Str(a, "package_id")) || string.IsNullOrEmpty(Str(a, "key")))
            {
                err = Fail("RK1001", "package_id and key are required");
                return null;
            }

            if (string.Equals(Str(a, "scope"), "thing", StringComparison.OrdinalIgnoreCase))
            {
                Thing t = ThingOf(a);
                if (t == null) { err = Fail("RK2001", "scope thing needs a thing as the last argument"); return null; }
                return Str(a, "package_id") + "::t" + t.thingIDNumber + ":" + Str(a, "key");
            }

            return Str(a, "package_id") + "::" + Str(a, "key");
        }

        private static string Prefix(Dictionary<string, string> a, out string err)
        {
            err = null;
            if (string.Equals(Str(a, "scope"), "thing", StringComparison.OrdinalIgnoreCase))
            {
                Thing t = ThingOf(a);
                if (t == null) { err = Fail("RK2001", "scope thing needs a thing as the last argument"); return null; }
                return Str(a, "package_id") + "::t" + t.thingIDNumber + ":";
            }

            return Str(a, "package_id") + "::";
        }

        // Removes everything a mod stored for one thing. Call it when the thing is gone for good.
        private static string ForgetThing(Dictionary<string, string> a)
        {
            if (string.IsNullOrEmpty(Str(a, "package_id"))) return Fail("RK1001", "package_id is required");
            var c = RimLuaModDataComponent.GetOrCreate();
            if (c == null) return Fail("RK3001", "no game is loaded");
            Thing t = ThingOf(a);
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            string prefix = Str(a, "package_id") + "::t" + t.thingIDNumber + ":";
            int n = 0;
            foreach (string k in c.Values.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList()) { c.Values.Remove(k); n++; }
            return OkInt(n);
        }

        private static string GetValue(Dictionary<string, string> a)
        {
            string k = Key(a, out string err);
            if (err != null) return err;
            var store = Store(a, out err);
            if (err != null) return err;
            return store.TryGetValue(k, out string v) ? OkStr(v) : OkJson("null");
        }

        private static string SetValue(Dictionary<string, string> a)
        {
            string k = Key(a, out string err);
            if (err != null) return err;
            var store = Store(a, out err);
            if (err != null) return err;
            store[k] = Str(a, "value");
            return OkBool(true);
        }

        private static string RemoveValue(Dictionary<string, string> a)
        {
            string k = Key(a, out string err);
            if (err != null) return err;
            var store = Store(a, out err);
            if (err != null) return err;
            return OkBool(store.Remove(k));
        }

        private static string KeysOf(Dictionary<string, string> a)
        {
            if (string.IsNullOrEmpty(Str(a, "package_id"))) return Fail("RK1001", "package_id is required");
            var store = Store(a, out string err);
            if (err != null) return err;
            string prefix = Prefix(a, out err);
            if (err != null) return err;
            return OkStringList(store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k.Substring(prefix.Length)).OrderBy(k => k).ToList());
        }

        // The data version a mod last wrote into this save, 0 when it never set one. Use it to migrate old saves.
        private static string VersionOf(Dictionary<string, string> a)
        {
            if (string.IsNullOrEmpty(Str(a, "package_id"))) return Fail("RK1001", "package_id is required");
            var c = RimLuaModDataComponent.GetOrCreate();
            if (c == null) return Fail("RK3001", "no game is loaded");
            return c.Values.TryGetValue(Str(a, "package_id") + "::__data_version", out string v) && int.TryParse(v, out int n) ? OkInt(n) : OkInt(0);
        }

        private static string SetVersion(Dictionary<string, string> a)
        {
            if (string.IsNullOrEmpty(Str(a, "package_id"))) return Fail("RK1001", "package_id is required");
            var c = RimLuaModDataComponent.GetOrCreate();
            if (c == null) return Fail("RK3001", "no game is loaded");
            c.Values[Str(a, "package_id") + "::__data_version"] = Int(a, "version").ToString();
            return OkInt(Int(a, "version"));
        }

        private static string GameVersion(Dictionary<string, string> a)
        {
            return Jb.Obj().S("running", VersionControl.CurrentVersionString).S("loaded_save", ScribeMetaHeaderUtility.loadedGameVersion).Ok();
        }

        private static string AutosaveInterval(Dictionary<string, string> a) => OkFloat(Prefs.AutosaveIntervalDays);

        private static string SetAutosaveInterval(Dictionary<string, string> a)
        {
            float days = Float(a, "days");
            if (days < 0.125f || days > 14f) return Fail("RK1001", "days must be between 0.125 and 14");
            Prefs.AutosaveIntervalDays = days;
            Prefs.Save();
            return OkFloat(Prefs.AutosaveIntervalDays);
        }

        // Saves the game under a name. The name may only contain letters, digits, dash and underscore.
        private static string SaveNow(Dictionary<string, string> a)
        {
            if (Current.Game == null) return Fail("RK3001", "no game is loaded");
            string name = string.IsNullOrEmpty(Str(a, "name")) ? "RimKit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") : Str(a, "name");
            if (!name.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')) return Fail("RK1001", "name may only contain letters, digits, dash and underscore");
            GameDataSaveLoader.SaveGame(name);
            return OkStr(name);
        }

        // Starts loading a save the way the Load button does. It returns at once, the loading screen runs over the next frames.
        private static string LoadSave(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name) || !name.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == ' '))
                return Fail("RK1001", "name may only contain letters, digits, space, dash and underscore");
            if (!File.Exists(GenFilePaths.FilePathForSavedGame(name))) return Fail("RK3001", "no save named " + name);
            LongEventHandler.QueueLongEvent(delegate
            {
                Verse.Profile.MemoryUtility.ClearAllMapsAndWorld();
                Current.Game = new Game();
                Current.Game.InitData = new GameInitData();
                Current.Game.InitData.gameToLoad = name;
            }, "Play", "LoadingLongEvent", true, null);
            return OkBool(true);
        }

        private static string SaveFiles(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (FileInfo f in GenFilePaths.AllSavedGameFiles) arr.Add(Jb.Obj().S("name", Path.GetFileNameWithoutExtension(f.Name)).S("modified", f.LastWriteTime.ToString("s")).I("bytes", f.Length));
            return arr.Ok();
        }
    }
}
