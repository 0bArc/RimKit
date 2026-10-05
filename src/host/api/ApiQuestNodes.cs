using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using RimWorld.QuestGen;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.quests slate access for Lua quest nodes (family quest_node). Ops: quest.slate_get, quest.slate_set, quest.slate_set_object.
    // A quest script runs nodes against a slate of named values. A Lua node reads and writes the same values the XML nodes use.
    internal static class ApiQuestNodes
    {
        public static void Register()
        {
            ApiRegistry.Register("quest.slate_get", a => SlateGet(a), "gameplay", "0.10.0");
            ApiRegistry.Register("quest.slate_set", a => SlateSet(a), "gameplay", "0.10.0");
            ApiRegistry.Register("quest.slate_set_object", a => SlateSetObject(a), "gameplay", "0.10.0");
        }

        // QuestGen.slate always exists, so it only counts while a quest is being generated.
        private static Slate Current => LuaClasses.CurrentSlate ?? (QuestGen.Working ? QuestGen.slate : null);

        private static string NoSlate() => Fail("RK3001", "no quest is being generated. Slate values only exist while a quest node runs.");

        private static string SlateGet(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name)) return Fail("RK1001", "name is required");
            Slate slate = Current;
            if (slate == null) return NoSlate();
            if (!slate.TryGet(name, out object value)) return OkJson("null");
            return OkJson(HookCodec.Encode(value));
        }

        private static string SlateSet(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name)) return Fail("RK1001", "name is required");
            Slate slate = Current;
            if (slate == null) return NoSlate();
            string text = Str(a, "value");
            string kind = Str(a, "kind");
            if (text == null) return Fail("RK1001", "value is required");
            object value;
            if (kind == "string") value = text;
            else if (kind == "bool" || (kind == null && (text == "true" || text == "false"))) value = text == "true";
            else if (kind == "int" || (kind == null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                {
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float rounded)) return Fail("RK1001", "value is not an integer");
                    i = (int)rounded;
                }
                value = i;
            }
            else if (kind == "float" || (kind == null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
            {
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return Fail("RK1001", "value is not a number");
                value = f;
            }
            else value = text;
            slate.Set(name, value);
            return OkBool(true);
        }

        private static string SlateSetObject(Dictionary<string, string> a)
        {
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name)) return Fail("RK1001", "name is required");
            Slate slate = Current;
            if (slate == null) return NoSlate();
            object target = ObjectHandles.Get<object>(Int(a, "target"));
            if (target == null) return Fail("RK2001", "target handle is stale or null");
            slate.Set(name, target);
            return OkBool(true);
        }
    }
}
