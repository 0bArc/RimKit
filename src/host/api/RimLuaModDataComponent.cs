using System.Collections.Generic;
using Verse;

namespace RimLuaKit
{
    // Per-save string bag for Lua mods.
    public class RimLuaModDataComponent : GameComponent
    {
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public RimLuaModDataComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Values, "rimlua_mod_data", LookMode.Value, LookMode.Value);
            Values ??= new Dictionary<string, string>();
        }

        public static RimLuaModDataComponent GetOrCreate()
        {
            Game g = Verse.Current.Game;
            if (g == null) return null;
            var c = g.GetComponent<RimLuaModDataComponent>();
            if (c == null)
            {
                c = new RimLuaModDataComponent(g);
                g.components.Add(c);
            }
            return c;
        }
    }
}
