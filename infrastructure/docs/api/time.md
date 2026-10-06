# Time and version kit

`game.time` reads the calendar and controls game speed. `game.version` tells a mod which RimWorld and which RimKit it runs on. Both are Experimental in the [stability tiers](stability.md).

## game.time

| Function | Returns | Notes |
|----------|---------|-------|
| `game.time.ticks()` | integer | Ticks since this game started. |
| `game.time.now()` | table | Calendar fields for the current map, see below. Raises `RK3001` when no game is loaded. |
| `game.time.speed()` | string | `Paused`, `Normal`, `Fast`, `Superfast` or `Ultrafast`. |
| `game.time.set_speed(speed)` | string | Accepts a name (any case) or 0 to 4. Returns the speed that was set. A bad value raises `RK1001`. |
| `game.time.paused()` | boolean | |
| `game.time.set_paused(paused)` | boolean | Returns the state after the call. |
| `game.time.date_text([ticks])` | string | A readable date such as `5th of Aprimay, 5500`. Without an argument it uses the current time. |

Constants: `game.time.ticks_per_hour` (2500), `ticks_per_day` (60000), `ticks_per_quadrum` (900000), `ticks_per_year` (3600000).

`game.time.now()` returns `ticks`, `abs_ticks`, `hour`, `hour_float`, `day_of_year`, `day_of_season`, `day_of_quadrum`, `year`, `season`, `quadrum`, `day_percent`, `is_night`, `ticks_per_hour` and `ticks_per_day`. Calendar fields use the longitude of the current map, so two colonies on different tiles can have different hours.

```lua
game.events.on_tick(function()
  local now = game.time.now()
  if now.hour == 6 and now.day_of_quadrum == 1 then
    game.ui.message("A new quadrum begins: " .. now.quadrum)
  end
end)

-- Drop to normal speed when an incident fires
game.events.on("incident.fired", function() game.time.set_speed("Normal") end)
```

## game.version

| Function | Returns | Notes |
|----------|---------|-------|
| `game.version.rimworld()` | table | `{ major, minor, build, text }` of the running game. |
| `game.version.at_least("1.6")` | boolean | True when the game is 1.6 or newer. |
| `game.version.rimkit()` | string | The RimKit version. |
| `game.version.api_level()` | integer | The API level from the [Standard](../standard/rks.md). It only changes when something breaks. |

Use `at_least` to guard a call that exists only in newer games instead of catching the error.

### Hooks that differ between game versions

`game.version.hook(kind, targets, fn[, opts])` installs a hook for the game version that is running. `targets` maps a minimum game version to a target, and the entry with the highest minimum that the game satisfies is used. When no entry fits, nothing is installed and the call returns nil.

```lua
game.version.hook("postfix", {
  ["1.5"] = { type = "Verse.Pawn", method = "Tick" },
  ["1.6"] = { type = "Verse.Pawn", method = "TickInterval", sig = { "System.Int32" } },
}, function(ctx)
  -- runs once per pawn tick on either game version
end)
```

RimKit supports the current stable RimWorld release and the one before it, see the [Standard](../standard/rks.md). The game-update watch in the smoke suite resolves every shipped hook target against the running game, so a target that a game update renames shows up on the next run.

## Game-update watch

`game.hooks.check_target(type, method, sig)` returns `{ found, problem }` and says whether a Harmony target still resolves in the running game. `game.hooks.check_events()` does the same for every catalog event. The smoke suite runs both against every hook target the shipped mods use (generated into `tests/smoke/Lua/targets.lua` by `infrastructure/tools/gen-hook-targets.js`), so a game update that renames a method fails the suite with the name of the target.
