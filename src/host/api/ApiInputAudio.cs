using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.input (runtime key bindings, chords, mouse) and game.audio (defining sounds, music, sustainers, volumes). Ops: input.*, audio.*.
    // input.binding_just_pressed and audio.play are older ops and keep working.
    internal static class ApiInputAudio
    {
        private static readonly Dictionary<int, Sustainer> Sustainers = new Dictionary<int, Sustainer>();
        private static int nextSustainer = 1;

        public static void Register()
        {
            R("input.register_key", RegisterKey);
            R("input.key_down", KeyDown);
            R("input.key_pressed", KeyPressed);
            R("input.chord_pressed", ChordPressed);
            R("input.modifiers", Modifiers);
            R("input.mouse", Mouse);
            R("input.key_names", KeyNames);
            R("input.bindings", Bindings);
            R("audio.define", DefineSound);
            R("audio.play_at", PlayAt);
            R("audio.sound_defs", SoundDefs);
            R("audio.songs", Songs);
            R("audio.play_song", PlaySong);
            R("audio.silence_music", SilenceMusic);
            R("audio.volume", VolumeOf);
            R("audio.set_volume", SetVolume);
            R("audio.sustainer_start", SustainerStart);
            R("audio.sustainer_end", SustainerEnd);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        // ---- input

        private static bool Key(string name, out KeyCode code) => Enum.TryParse(name, true, out code) && Enum.IsDefined(typeof(KeyCode), code);

        // Makes a key binding the player can rebind in the options. default is a Unity key name such as K or F9.
        private static string RegisterKey(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name) || !name.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) return Fail("RK1001", "name may only contain letters, digits and underscore");
            if (DefDatabase<KeyBindingDef>.GetNamedSilentFail(name) != null) return OkBool(false);
            if (!Key(string.IsNullOrEmpty(Str(a, "default")) ? "None" : Str(a, "default"), out KeyCode code)) return Fail("RK1001", "unknown key " + Str(a, "default"));
            KeyBindingCategoryDef cat = DefDatabase<KeyBindingCategoryDef>.GetNamedSilentFail(string.IsNullOrEmpty(Str(a, "category")) ? "Misc" : Str(a, "category")) ?? DefDatabase<KeyBindingCategoryDef>.AllDefsListForReading.FirstOrDefault();
            var def = new KeyBindingDef { defName = name, label = string.IsNullOrEmpty(Str(a, "label")) ? name : Str(a, "label"), category = cat, defaultKeyCodeA = code, defaultKeyCodeB = KeyCode.None };
            def.shortHash = (ushort)(Math.Abs(name.GetHashCode()) % 60000 + 1);
            DefDatabase<KeyBindingDef>.Add(def);
            try
            {
                var prefs = AccessTools.Field(typeof(KeyPrefsData), "keyPrefs")?.GetValue(KeyPrefs.KeyPrefsData) as Dictionary<KeyBindingDef, KeyBindingData>;
                if (prefs != null && !prefs.ContainsKey(def)) prefs[def] = new KeyBindingData(def.defaultKeyCodeA, def.defaultKeyCodeB);
            }
            catch (Exception e)
            {
                Log.Warning("[RimKit] key binding " + name + " registered, but its default could not be stored: " + e.Message);
            }

            return OkBool(true);
        }

        private static string KeyDown(Dictionary<string, string> a)
        {
            if (!Key(Str(a, "key"), out KeyCode code)) return Fail("RK1001", "unknown key " + Str(a, "key"));
            return OkBool(Input.GetKey(code));
        }

        private static string KeyPressed(Dictionary<string, string> a)
        {
            if (!Key(Str(a, "key"), out KeyCode code)) return Fail("RK1001", "unknown key " + Str(a, "key"));
            return OkBool(Input.GetKeyDown(code));
        }

        // chord: modifiers and a key joined with plus, for example "ctrl+shift+K". True on the frame the key goes down with the modifiers held.
        private static string ChordPressed(Dictionary<string, string> a)
        {
            string[] parts = (Str(a, "chord") ?? "").Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (parts.Length == 0) return Fail("RK1001", "chord is empty");
            bool ctrl = false, shift = false, alt = false;
            KeyCode main = KeyCode.None;
            foreach (string p in parts)
            {
                switch (p.ToLowerInvariant())
                {
                    case "ctrl": case "control": ctrl = true; break;
                    case "shift": shift = true; break;
                    case "alt": alt = true; break;
                    default:
                        if (!Key(p, out main)) return Fail("RK1001", "unknown key " + p);
                        break;
                }
            }

            if (main == KeyCode.None) return Fail("RK1001", "the chord needs a key");
            bool held = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) == ctrl
                        && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) == shift
                        && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) == alt;
            return OkBool(held && Input.GetKeyDown(main));
        }

        private static string Modifiers(Dictionary<string, string> a)
        {
            return Jb.Obj().B("ctrl", Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)).B("shift", Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                .B("alt", Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)).Ok();
        }

        private static string Mouse(Dictionary<string, string> a)
        {
            Vector2 p = UI.MousePositionOnUIInverted;
            var j = Jb.Obj().F("x", p.x).F("y", p.y).B("left", Input.GetMouseButton(0)).B("right", Input.GetMouseButton(1)).B("middle", Input.GetMouseButton(2))
                .B("left_down", Input.GetMouseButtonDown(0)).B("right_down", Input.GetMouseButtonDown(1)).F("scroll", Input.mouseScrollDelta.y);
            if (Find.CurrentMap != null)
            {
                IntVec3 c = UI.MouseCell();
                j.I("cell_x", c.x).I("cell_z", c.z).B("on_map", c.InBounds(Find.CurrentMap));
            }

            return j.Ok();
        }

        private static string KeyNames(Dictionary<string, string> a) => OkStringList(Enum.GetNames(typeof(KeyCode)).ToList());

        private static string Bindings(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (KeyBindingDef d in DefDatabase<KeyBindingDef>.AllDefsListForReading) arr.Add(Jb.Obj().S("def", d.defName).S("label", d.label).S("category", d.category?.defName).S("key", d.MainKey.ToString()));
            return arr.Ok();
        }

        // ---- audio

        // Defines a sound from clips in a mod's Sounds folder (paths without the extension). opts: volume (0 to 1), pitch (0.5 to 2), context (Any, MapOnly, WorldOnly), max_voices.
        private static string DefineSound(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name) || !name.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) return Fail("RK1001", "name may only contain letters, digits and underscore");
            if (DefDatabase<SoundDef>.GetNamedSilentFail(name) != null) return OkBool(false);
            var clips = new List<string>();
            if (a.TryGetValue("clips", out string json) && Json.TryParse(json, out object parsed) && parsed is List<object> list)
                foreach (object item in list) if (item is string s && s.Length > 0) clips.Add(s);
            if (clips.Count == 0) return Fail("RK1001", "clips must list at least one clip path");
            foreach (string clip in clips)
                if (ContentFinder<AudioClip>.Get(clip, false) == null) return Fail("RK3001", "audio clip not found: " + clip);
            Opts o = Opts.From(a, "opts");
            var def = new SoundDef { defName = name, sustain = false, maxVoices = Math.Max(1, o.Has("max_voices") ? o.Int("max_voices") : 4) };
            def.context = Enum.TryParse(o.Str("context", "Any"), true, out SoundContext ctx) ? ctx : SoundContext.Any;
            float volume = (float)Math.Max(0.0, Math.Min(1.0, o.Num("volume", 1)));
            float pitch = (float)Math.Max(0.5, Math.Min(2.0, o.Num("pitch", 1)));
            var sub = new SubSoundDef
            {
                name = name, volumeRange = new FloatRange(volume * 100f, volume * 100f), pitchRange = new FloatRange(pitch, pitch), muteWhenPaused = false,
                grains = clips.Select(c => (AudioGrain)new AudioGrain_Clip { clipPath = c }).ToList(),
            };
            def.subSounds = new List<SubSoundDef> { sub };
            def.shortHash = (ushort)(Math.Abs(name.GetHashCode()) % 60000 + 1);
            def.ResolveReferences();
            DefDatabase<SoundDef>.Add(def);
            return OkBool(true);
        }

        private static string PlayAt(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var cell = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!cell.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown sound " + Str(a, "def"));
            def.PlayOneShot(new TargetInfo(cell, map));
            return OkBool(true);
        }

        private static string SoundDefs(Dictionary<string, string> a) => OkStringList(DefDatabase<SoundDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string Songs(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (SongDef s in DefDatabase<SongDef>.AllDefsListForReading) arr.Add(Jb.Obj().S("def", s.defName).S("clip", s.clipPath).F("volume", s.volume));
            return arr.Ok();
        }

        private static string PlaySong(Dictionary<string, string> a)
        {
            if (Find.MusicManagerPlay == null) return Fail("RK3001", "the music manager is not available yet");
            SongDef def = DefDatabase<SongDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown song " + Str(a, "def"));
            Find.MusicManagerPlay.ForcePlaySong(def, false);
            return OkBool(true);
        }

        private static string SilenceMusic(Dictionary<string, string> a)
        {
            if (Find.MusicManagerPlay == null) return Fail("RK3001", "the music manager is not available yet");
            Find.MusicManagerPlay.ForceSilenceFor(Math.Max(1f, Float(a, "seconds")));
            return OkBool(true);
        }

        private static string VolumeOf(Dictionary<string, string> a)
        {
            switch ((Str(a, "kind") ?? "").ToLowerInvariant())
            {
                case "game": return OkFloat(Prefs.VolumeGame);
                case "music": return OkFloat(Prefs.VolumeMusic);
                case "ambient": return OkFloat(Prefs.VolumeAmbient);
                default: return Fail("RK1001", "kind must be game, music or ambient");
            }
        }

        private static string SetVolume(Dictionary<string, string> a)
        {
            float v = Math.Max(0f, Math.Min(1f, Float(a, "value")));
            switch ((Str(a, "kind") ?? "").ToLowerInvariant())
            {
                case "game": Prefs.VolumeGame = v; break;
                case "music": Prefs.VolumeMusic = v; break;
                case "ambient": Prefs.VolumeAmbient = v; break;
                default: return Fail("RK1001", "kind must be game, music or ambient");
            }

            Prefs.Save();
            return OkFloat(v);
        }

        private static string SustainerStart(Dictionary<string, string> a)
        {
            Map map = MapOf(a);
            if (map == null) return Fail("RK2001", "map handle is stale or null");
            var cell = new IntVec3(Int(a, "x"), 0, Int(a, "z"));
            if (!cell.InBounds(map)) return Fail("RK1001", "cell is outside the map");
            SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown sound " + Str(a, "def"));
            if (!def.sustain) return Fail("RK3003", Str(a, "def") + " is not a sustained sound");
            Sustainer s = def.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(cell, map), MaintenanceType.PerTick));
            if (s == null) return Fail("RK3003", "the sound did not start");
            int id = nextSustainer++;
            Sustainers[id] = s;
            return OkInt(id);
        }

        private static string SustainerEnd(Dictionary<string, string> a)
        {
            if (!Sustainers.TryGetValue(Int(a, "id"), out Sustainer s)) return OkBool(false);
            Sustainers.Remove(Int(a, "id"));
            s.End();
            return OkBool(true);
        }
    }
}
