# Tutorials

Seven short tutorials, one for each step up in what a mod can do. Do them in order the first time. Each one ends with a mod you can run, and each builds on the [quickstart](quickstart.md).

## 1. React to the game

Goal: a message when a colonist dies, saved between sessions.

1. `rimkit mod create Elegy` and open the folder.
2. In `Lua/main.luau`:

```lua
local PKG = "stratware.elegy"
game.events.on("pawn.died", function(e)
  local n = (tonumber(game.data.get(PKG, "deaths")) or 0) + 1
  game.data.set(PKG, "deaths", tostring(n))
  game.ui.message(e.pawn.name .. " is gone. Losses so far: " .. n)
end)
```

3. `rimkit mod ship`, enable the mod and RimKit, kill a colonist with the dev tools.

You used: [events](../api/events.md), `game.data`, `game.ui`. Next: read the colony yourself instead of waiting for an event.

## 2. Read and change the colony

Goal: a key that lists every colonist's mood and drafts the unhappy ones.

Use `game.maps.colonists(map.handle)` for the colonists, `game.pawns.mood(h)` to read, and `game.pawns.set_drafted(h, true)` to act. Bind the key with `game.input.register_key` and poll it in `game.events.on_tick`, as in the [cookbook](cookbook.md). See [pawns](../api/pawns.md), [maps](../api/maps.md) and [queries](../api/query.md). Prefer one `game.query` call to a loop over every thing.

## 3. Whole systems

Goal: your own storyteller rule, a raid that grows with the colony's wealth.

Read `game.storyteller.threat_points()` and `game.economy.wealth()`, decide in a `time.day_changed` handler and call `game.raids.fire`. Then look at [AI](../api/ai.md) to change who hauls first and [research](../api/research.md) to unlock projects. Every system kit follows the same shape, see [kits and domains](../api/domains.md).

## 4. Your own interface

Goal: a window with a table of skills and a button.

Return a tree of tables from a view function and pass it to `game.widgets.open`; handle clicks in the event callback. See [widgets](../api/widgets.md). For buttons on selected things use [gizmos](../api/gizmos.md), for a settings page `game.options.page` on the [HUD page](../api/hud.md).

## 5. Add something new to the game

Goal: a building and an incident whose behaviour is Lua.

1. Write the `ThingDef` in `Defs/` with a `RimKit.CompProperties_Lua` component naming a class.
2. `game.classes.define("comp", "my_class", { tick_rare = function(thing) ... end })`.
3. Do the same for an incident with `RimKit.IncidentWorker_Lua`.

To change numbers instead of adding things, use `game.tweaks.on` and `game.defs.set`. See [Lua classes](../api/classes.md), [defs](../api/defs.md) and [stats](../api/research.md).

## 6. Use an expansion

Goal: a mod that only does something when Ideology or Royalty is installed.

Check `game.dlc.active("Ideology")` first, or call the function and catch `RK3003`. Then use the DLC kit, for example `game.ideology.list()` or `game.royalty.titles()`. See [DLC kits](../api/dlc.md). Remember the Anomaly functions need the Anomaly DLC and Odyssey support is minimal.

## 7. Ship it

Goal: your mod on the Steam Workshop.

Write tests in `Tests/`, run `rimkit mod check`, `rimkit mod test` and `rimkit mod release-check`, then `rimkit publish`. Declare what the mod needs in `meta.capabilities`. Keep a data version for saves. See [testing](testing.md), [publishing](publishing.md), [capabilities](capabilities.md) and [save safety](save-safety.md). When it is live, [performance](performance.md) shows how to find out whether it is slow.
