# Performance and diagnostics

A mod shares one game with dozens of others, and the game has about 16 milliseconds per frame at normal speed. RimKit measures what each mod's Lua costs so a slow mod is found by data, not by guessing.

## The budget

Every callback that runs Lua is timed and charged to the mod that registered it: tick callbacks, events, hooks, timers, UI callbacks and the load itself. Every 600 ticks RimKit works out the average time a mod used per tick. If that is above the mod's budget it logs one warning naming the mod and raises the `mod.over_budget` event.

The default budget is 300 microseconds per tick, about 2 percent of a frame at normal speed. Change it in `meta.lua`:

```lua
meta.perf_budget_us = 150
```

A mod that stays inside its budget costs a player nothing they will notice. Typical culprits when it does not:

- Scanning every pawn or thing in an `on_tick` callback. Use `game.timer`, `game.events` or a rare interval, and `game.query` (one native call) instead of looping in Lua.
- A hook on a method the game calls thousands of times per tick, such as `Thing.get_MarketValue`. `game.hooks.list()` shows calls and time per hook.
- Building a widget tree in `view` that calls many kit functions per frame. Cache the values and refresh them every few ticks.

## Reading the numbers

```lua
for _, row in ipairs(game.profiler.report()) do
  print(row.mod, row.avg_tick_us, row.hook_us / 1000, row.over_budget)
end
game.profiler.reset()
```

Each row has `tick_us`, `event_us`, `hook_us`, `timer_us`, `ui_us` and `load_us` (totals in microseconds) with matching `_calls` counts, plus `avg_tick_us`, `budget_us` and `over_budget`. The `(unattributed)` row is RimKit's own work.

## The dev tools window

Turn on Development mode in the game options, then press F11 (rebind it in the key options). The window has seven tabs:

| Tab | What it shows |
|-----|---------------|
| Console | Run a Lua expression or statement. Needs Development mode |
| Hooks | Every hook with calls, total and average time, slowest first |
| Events | Record named events with their tick and payload to see what the game raises and in what order |
| Profiler | The per mod table above |
| Actions | Debug actions: any mod can add one with `game.dev.action(name, fn)` |
| Mods | Each Lua mod's version and declared capabilities |
| Reflect | Browse any game object: pick a root (the static Verse.Find class, the selected thing, the map, the player faction, or a type name), open members that hold objects, and read values live. Needs the RimKit setting developer_reflect |

The RimKit actions export every def name for the editor's completion and write a diagnostics bundle.

## Hot reload

```lua
game.dev.watch(true)            -- reload a mod when any of its Lua files changes
game.dev.reload("my.mod")       -- or reload now (needs the "dev" capability)
```

Reload removes the mod's events, hooks, timers, tick callbacks, widget callbacks and what it added to the screen, then runs its files again and its `on_load` handlers. `require`d modules are read again. It is meant for tuning logic and layouts while the game runs. What it cannot undo:

- Gizmos, alerts, tabs, pawn columns, designators, status lines and settings pages the mod registered are removed before the new version runs, and the new version registers its own. Key bindings stay, because the game keeps them. Tools already placed in an architect menu are taken out of it, and the new version adds them again.
- Changes the mod made to the game (defs, stats, spawned things) stay.
- Defs and XML are read at start, so changing XML needs a restart.

## Bug reports

`rimkit diag` zips the game log, `ModsConfig.xml` and, if you used the dev tools action, the mod list in load order with versions and the profiler numbers. It never uses the network. Open the zip before you attach it: the log can contain your Windows user name in file paths.

```text
rimkit diag            newest bundle from the game, or a fresh one from the log
rimkit diag --fresh    always build a new one from the log
```

In game, the Actions tab has "RimKit: write a diagnostics bundle". If a mod keeps failing, RimKit switches its Lua off for the session after 20 errors in 60 seconds and shows a letter that names the mod and offers to turn it off for good. Safe mode (`-rimkit-safe`, the `RIMKIT_SAFE` environment variable, or a `Config/rimkit_safe.txt` file) starts the game with no Lua at all.
