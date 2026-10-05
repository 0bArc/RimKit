# Older names

The first version of the API used `rim.*` tables and flat globals such as `ui`, `anomaly`, `data`. Every one of those functions still works. Each has a canonical `game.<domain>` name and prints one deprecation line the first time it is used. They are removed no earlier than 1.0.0.

Rewrite a mod automatically: [migration](migration.md). Look up a name: the "Old" column in the [reference](reference.md).

## What still has no canonical home

These are not deprecated:

| Name | Notes |
|------|-------|
| `events.on`, `events.off`, `log.info`, `log.error`, `timer.after` | Core shorthands. Also `game.events`, `game.log`, `game.timer` |
| `game.tick`, `game.current_map`, `game.player_faction` | Helpers. Also `game.time.ticks`, `game.maps.current`, `game.factions.player` |
| `require("rimkit")` | The API contract: `version`, `api_level`, `assert_api`, tier tables |
| `rim.wrap`, `rim.wrap_map`, `rim.wrap_thing`, `rim.wrap_faction`, `rim.wrap_entity` | Turn a handle into a wrapped object |
| `rim.invoke` | Escape for `api.list` and the host `reflect.*` operations only |
| `rim.prefix[...]`, `rim.postfix[...]`, `rim.events.prefix[...]` | Assignment sugar for hooks, see [hooks](hooks.md) |
| `selected_pawn` | Global, updated each tick with the first selected pawn |

## Objects

`RimPawn`, `RimMap`, `RimThing`, `RimFaction`, `RimEntity`, `RimObject` are the wrapped types. Their members are in the Lua stubs and the editor shows them.

Two members were renamed: `pawn:seek_medical_help()` is now `pawn:seek_medical()`, and `map:spawn_thing(...)` is now `map:spawn(...)`.

## Lua jobs

```lua
game.jobs.register("idle_wave", {
  can_do = function(pawn) return pawn.is_colonist end,
  execute = function(pawn)
    -- return true when finished, false to keep ticking
    return true
  end,
})
pawn:start_job("idle_wave")   -- or game.jobs.start(pawn, "idle_wave")
```

The kit ships `Defs/JobDefs/RimLua_Jobs.xml` (`RimLua_Scripted` and a driver that calls your Lua).

## Movement

Uses the game's `Goto` job and path finder.

```lua
if pawn:can_reach(40, 40) then
  local nodes = game.paths.compute(pawn, 40, 40)
  pawn:walk_to(40, 40)        -- optional third argument: sprint
end
pawn:wander(14)
pawn:stop()
```

## Windows, settings and Defs

```lua
game.ui.window({ title = "Debug", body = "status", buttons = {
  { label = "Do thing", on_click = function() end },
} })
game.ui.float_menu({ { label = "Ping", action = function() game.ui.message("hi") end } })

game.config.register("verbose", { type = "bool", default = true, label = "Verbose" })
if game.config.get_bool("verbose") then end

game.defs.register_thing({ defName = "MyItem", label = "my item", package_id = "my.mod" })
```

`rimkit mod sync` also reads a `defs.lua` file and writes `Defs/ThingDefs/RimLua_FromDefsLua.xml`. A new Def needs a game restart.

## `rim.reflect` and `rim.cs`

The first reflection API. Strings in, integer handles out, and they are not rewritten by `rimkit migrate` because the new [typed reflection](reflect.md) has different arguments and results. Both need the `developer_reflect` setting.

## Handle functions

The `rim.find`, `rim.map`, `rim.thing`, `rim.pawn`, `rim.faction`, `rim.defs`, `rim.job` and `rim.ui` tables, and the flat tables `anomaly`, `data`, `health`, `surgery`, `building`, `work`, `world_api`, `incident`, `audio`, `control`, `path`, `jobs`, `util`, `config`, `input`, `defs`, `ui`, take integer handles and return integer handles. The canonical versions also accept wrapped objects.
