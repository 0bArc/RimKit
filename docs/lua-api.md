# Lua API

Prefer the high-level OO surface and bound `rim.*` tables. Handles still exist under `rim.pawn` / `rim.map` / … as the normal handle API.

`rim.invoke` is an **escape interface** for explicitly designated operations (`api.list`, `reflect.*`) and is **not** a general mechanism for calling GameApi operations.

## OO (Phase 1)

```lua
function on_pawn_spawned(pawn)
  if pawn.is_colonist then
    pawn:give_item("Component", 1)
    log.info(pawn.name .. " spawned")
  end
end

events.on("pawn_died", function(pawn)
  log.info(pawn.name .. " died")
end)

local map = game.current_map()
timer.after(60, function()
  player.send_message("one second later")
end)
```

| Namespace | Role |
|-----------|------|
| `log` / `rim.log` | logging |
| `events` | `on` / `off` (`pawn_spawned`, `pawn_died`) |
| `game` | `current_map()`, `tick()`, `player_faction()` |
| `player` | `send_message(text)` |
| `timer` | `after(ticks, fn)` |
| `jobs` | `register` / `start` (Phase 2) |
| `RimPawn` | props + methods |
| `RimMap` / `RimThing` / `RimFaction` | same pattern |
| `selected_pawn` | updated each tick |

**RimPawn:** `name`, `health`, `hunger`, `is_colonist`, `is_humanlike`, `map`, `faction`, `give_item`, `set_name`, `add_trait`, `remove_trait`, `give_hediff`, `draft`, `kill`, `seek_medical_help`, `start_job`, `has_skill`

**Sugar:** file-level `on_pawn_spawned` / `on_pawn_died` auto-wired at `on_load`.

**Wrap:** `rim.wrap(h)`, `rim.wrap_map(h)`, `rim.wrap_thing(h)`, `rim.wrap_faction(h)`.

## Jobs / Faction / Map (Phase 2)

```lua
jobs.register("idle_wave", {
  can_do = function(pawn) return pawn.is_colonist end,
  execute = function(pawn)
    -- return true when finished, false to keep ticking
    log.info(pawn.name .. " waving")
    return true
  end
})

-- later:
pawn:start_job("idle_wave")
-- or jobs.start(pawn, "idle_wave")

local cells = game.current_map():find_cells({ terrain = "Soil", limit = 20 })
for _, c in ipairs(cells) do
  log.info("cell " .. c.x .. "," .. c.z)
end

local pf = game.player_faction()
if pawn.faction and pf and pawn.faction:is_hostile(pf) then
  pawn.faction:set_relation(pf, "Neutral")
end
```

Kit ships `Defs/JobDefs/RimLua_Jobs.xml` (`RimLua_Scripted` + `JobDriver_RimLua`).

## Path / NPC movement

Uses RimWorld `JobDefOf.Goto` + `PathFinder.FindPathNow` (1.6). No official multiplayer; singleplayer path jobs are fine.

```lua
local p = selected_pawn
local pos = p.position  -- { x, z }
if p:can_reach(40, 40) then
  local nodes = path.compute(p, 40, 40)
  log.info("nodes=" .. #nodes)
  p:walk_to(40, 40)           -- optional 3rd arg: sprint=true
end
p:wander(14)
p:stop()
-- aliases: path.walk / path.wander / path.compute / path.stop
```

## UI / Config / Defs (Phase 3)

```lua
ui.window({
  title = "Debug",
  body = "status text",
  buttons = {
    { label = "Do thing", on_click = function() log.info("click") end },
  },
})

ui.float_menu({
  { label = "Ping", action = function() ui.message("hi") end },
})

config.register("verbose", { type = "bool", default = true, label = "Verbose" })
if config.get_bool("verbose") then log.info("on") end

-- rimkit mod sync reads defs.lua -> Defs/ThingDefs/RimLua_FromDefsLua.xml
-- runtime write also works; restart required to load Def
defs.register_thing({
  defName = "MyItem",
  label = "my item",
  package_id = "my.mod",
})
```

See [Jobs Test](example-jobs.md).

## Hooks (Harmony escape)

Callbacks receive `RimPawn` userdata (not raw ints).

```lua
rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(pawn)
  return true  -- false = skip original
end

rim.postfix["Some.Type"].SomeMethod = function(pawn)
end

rim.events.prefix["RimWorld.JobGiver_GetFood.TryGiveJob"] = function(pawn)
  return true
end
```

Aliases: `rim.prefixes` = `rim.prefix`, `rim.postfixes` = `rim.postfix`.

Legacy: `rim.hooks.prefix(type, method, fn)`.

## Core

`rim.log`, `rim.message`, `rim.on_load`, `rim.on_tick`

### `rim.invoke` (escape only)

```lua
rim.invoke("api.list")
rim.invoke("reflect.get", { h = h, member = "def" })
```

Allowed ops: exact `api.list`, or case-sensitive `reflect.<suffix>` with no whitespace / empty / `..` segments. Gameplay ops (`pawn.*`, `anomaly.*`, …) must use bound tables:

```lua
rim.pawn.is_humanlike(h)
rim.pawn.faction_is_player(h)
```

## Find / Map / Pawn / Reflect (handle-based)

`rim.find.*`, `rim.map.*`, `rim.pawn.*` are the normal handle API. Prefer `rim.reflect.*` bindings; `rim.invoke("reflect…")` is the same escape path. Reflect needs `developer_reflect` where gated.

## Find

`rim.find.tick()`, `current_map()`, `world()`, `selected()`, `selected_things()`, `maps()`, `any_player_pawn()`

## Defs

```lua
rim.defs.get("ThingDef", "MealSimple")
rim.defs.exists("HediffDef", "Flu")
rim.defs.list("SkillDef")
rim.defs.label(handle)
```

## Map

`nutrition` / `total_human_edible_nutrition`, `width`, `height`, `pawns`, `colonists`, `prisoners`, `things`, `things_of_def(map, def)`, `spawn(map, def, x, z[, stack])`, `find_cells`

## Thing

`def`, `label`, `label_short`, `destroy`, `despawn`, `pos`, `set_pos`, `hp`, `max_hp`, `set_hp`, `stack`, `set_stack`, `faction`, `set_faction`, `map`, `spawned`

## Pawn

Identity: `is_humanlike`, `is_colonist`, `is_prisoner`, `is_slave`, `is_downed`, `is_dead`, `faction_is_player`, `name`, `gender`, `age`, `kind`, `map`, `faction`

Needs: `hunger` / `hunger_pct`, `rest`, `recreation`, `mood`, `set_hunger`, `set_rest`, `health_pct`

Gear/health: `equipment`, `apparel`, `inventory`, `carry`, `give_hediff`, `remove_hediff`, `hediffs`, `give_thing` / `give_item`, `set_name`, `add_trait`, `remove_trait`, `seek_medical`, `strip`

Skills/jobs: `skill`, `set_skill`, `job_def`, `end_job`, `drafted`, `set_drafted`, `kill`, `job.start_lua`

## Faction / Job / UI

`rim.faction.player()`, `list()`, `name(h)`, `of_def(def)`, `is_hostile`, `set_relation`

`rim.job.make(def[, target])`, `rim.job.start(pawn, job)`, `job.start_lua`

`rim.ui.message(text)`, `rim.ui.letter(label, text)`

## Reflect (`rim.reflect` / `rim.cs`)

**Off by default.** Enable RimKit setting `developer_reflect` (hub Settings or Mod options) for authors.

When enabled: gameplay types only (`Verse` / `RimWorld` / `UnityEngine` / `HarmonyLib`).
Blocked: `System.Diagnostics`, `System.IO`, `System.Net`, `Assembly`/`Type` meta, process start, raw file IO.

```lua
rim.reflect.type(h)
rim.reflect.members(h)
rim.reflect.get(h, "Label")
rim.reflect.set(h, "HitPoints", 10)
rim.reflect.call(h, "Kill", "")
```

## Security model

Builtin pAuth in RimKit. Load order: **Harmony then RimKit**.

1. **AuthGate**: allowlist hashes (Harmony + host + native). Must match or no Lua loads.
2. **Lua quarantine**: each enabled `Lua/**/*.lua` pack is scanned; hostile packs are skipped; clean packs still load.
3. **Sandboxed VM**: no `io` / `os` / `debug` / `dofile` / `load` / `loadlib`; `require` only under that mod's `Lua/`
4. **GameApi allowlist**: unknown ops fail; domain modules via `ApiRegistry`; `rim.invoke` only `api.list` / `reflect.*`; def/export writes path-jailed

**What Lua mods cannot be:** keyloggers, free Process/cmdline, arbitrary filesystem, silent unrestricted net, runtime `load()`. Least-privilege utils (`util.open_folder`, `util.write_export`) are named and jailed. See [api](api/index.md).

`rimkit build` regenerates `Auth/allowlist.json`.
