using System.Collections.Generic;
using System.Linq;
using RimKit;
using Verse;

// Types whose full names are written into saves and settings files.
// They keep the original namespace on purpose: renaming them would make existing saves fail to find
// their components (recruited pawns, per-mod data) and reset the saved RimKit options.
// Everything else lives in the RimKit namespace. See docs/standard/rks.md, "Persisted type names".
namespace RimLuaKit
{
    // Saves which pawns we made draftable (hediff + this component).
    public class GameComponent_PawnControl : GameComponent
    {
        public List<int> ControlledIds = new List<int>();

        public GameComponent_PawnControl(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ControlledIds, "rimlua_controlled_pawns", LookMode.Value);
            ControlledIds ??= new List<int>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PawnControl.RestoreFromSave(ControlledIds);
        }

        public static GameComponent_PawnControl GetOrCreate()
        {
            Game g = Current.Game;
            if (g == null)
            {
                return null;
            }

            var c = g.GetComponent<GameComponent_PawnControl>();
            if (c == null)
            {
                c = new GameComponent_PawnControl(g);
                g.components.Add(c);
            }

            return c;
        }

        public void WriteIds(HashSet<int> ids)
        {
            ControlledIds = ids.ToList();
        }
    }

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

    // RimKit options. The settings file stores this class name.
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
}

// Per-map and per-world data bags for Lua mods (game.save). Like the types above, these full names are written into saves
// and must not change once released.
namespace RimKit
{
    public class MapComponent_LuaData : MapComponent
    {
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public MapComponent_LuaData(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Values, "lua_map_data", LookMode.Value, LookMode.Value);
            Values ??= new Dictionary<string, string>();
        }
    }

    public class WorldComponent_LuaData : RimWorld.Planet.WorldComponent
    {
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public WorldComponent_LuaData(RimWorld.Planet.World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Values, "lua_world_data", LookMode.Value, LookMode.Value);
            Values ??= new Dictionary<string, string>();
        }
    }
}
