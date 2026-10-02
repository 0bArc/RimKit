using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace RimLuaKit
{
    public class RimLuaSettings : ModSettings
    {
        public Dictionary<string, bool> Bools = new Dictionary<string, bool>();
        public Dictionary<string, float> Floats = new Dictionary<string, float>();
        public Dictionary<string, string> Strings = new Dictionary<string, string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Bools, "bools", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref Floats, "floats", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref Strings, "strings", LookMode.Value, LookMode.Value);
            Bools ??= new Dictionary<string, bool>();
            Floats ??= new Dictionary<string, float>();
            Strings ??= new Dictionary<string, string>();
        }
    }

    internal sealed class ConfigEntry
    {
        public string Key;
        public string Type; // bool | float | string
        public string Label;
        public string DefaultValue;
    }

    internal static class LuaConfigBridge
    {
        public static RimLuaSettings Settings;
        public static readonly List<ConfigEntry> Entries = new List<ConfigEntry>();

        public static void Register(string key, string type, string label, string defaultValue)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            Entries.RemoveAll(e => e.Key == key);
            Entries.Add(new ConfigEntry
            {
                Key = key,
                Type = string.IsNullOrEmpty(type) ? "bool" : type,
                Label = string.IsNullOrEmpty(label) ? key : label,
                DefaultValue = defaultValue ?? ""
            });

            EnsureDefault(key, type, defaultValue);
        }

        public static void EnsureDefault(string key, string type, string defaultValue)
        {
            if (Settings == null)
            {
                return;
            }

            type = (type ?? "bool").ToLowerInvariant();
            if (type == "float")
            {
                if (!Settings.Floats.ContainsKey(key))
                {
                    float.TryParse(defaultValue, out float f);
                    Settings.Floats[key] = f;
                }
            }
            else if (type == "string")
            {
                if (!Settings.Strings.ContainsKey(key))
                {
                    Settings.Strings[key] = defaultValue ?? "";
                }
            }
            else
            {
                if (!Settings.Bools.ContainsKey(key))
                {
                    Settings.Bools[key] = defaultValue == "1" ||
                                         (defaultValue ?? "").Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static string Get(string key)
        {
            if (Settings == null || string.IsNullOrEmpty(key))
            {
                return "";
            }

            if (Settings.Bools.TryGetValue(key, out bool b))
            {
                return b ? "true" : "false";
            }

            if (Settings.Floats.TryGetValue(key, out float f))
            {
                return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (Settings.Strings.TryGetValue(key, out string s))
            {
                return s ?? "";
            }

            return "";
        }

        public static void Set(string key, string value)
        {
            if (Settings == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            var entry = Entries.Find(e => e.Key == key);
            string type = entry?.Type?.ToLowerInvariant() ?? "bool";
            if (type == "float")
            {
                float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float f);
                Settings.Floats[key] = f;
            }
            else if (type == "string")
            {
                Settings.Strings[key] = value ?? "";
            }
            else
            {
                Settings.Bools[key] = value == "1" ||
                                     (value ?? "").Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            Settings.Write();
        }

        public static void DrawSettings(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("RimKit config");
            list.GapLine();

            bool reflect = Settings != null &&
                           Settings.Bools.TryGetValue("rimkit.developer_reflect", out bool rv) && rv;
            list.CheckboxLabeled("Developer reflect (rim.reflect / rim.cs), off by default", ref reflect);
            if (Settings != null)
            {
                bool prev = Settings.Bools.TryGetValue("rimkit.developer_reflect", out bool p) && p;
                Settings.Bools["rimkit.developer_reflect"] = reflect;
                if (prev != reflect) Settings.Write();
            }
            list.GapLine();
            list.Label("Lua config.register keys");
            if (Entries.Count == 0)
            {
                list.Label("No config keys registered yet. Load a Lua mod that calls config.register.");
            }

            foreach (ConfigEntry e in Entries)
            {
                if (e.Type == "float")
                {
                    float v = Settings != null && Settings.Floats.TryGetValue(e.Key, out float f) ? f : 0f;
                    list.Label(e.Label + ": " + v.ToString("0.##"));
                    v = list.Slider(v, 0f, 100f);
                    if (Settings != null)
                    {
                        Settings.Floats[e.Key] = v;
                    }
                }
                else if (e.Type == "string")
                {
                    string v = Settings != null && Settings.Strings.TryGetValue(e.Key, out string s) ? s : "";
                    string next = list.TextEntryLabeled(e.Label, v);
                    if (Settings != null && next != v)
                    {
                        Settings.Strings[e.Key] = next;
                    }
                }
                else
                {
                    bool v = Settings != null && Settings.Bools.TryGetValue(e.Key, out bool b) && b;
                    list.CheckboxLabeled(e.Label, ref v);
                    if (Settings != null)
                    {
                        Settings.Bools[e.Key] = v;
                    }
                }
            }

            list.End();
        }
    }
}
