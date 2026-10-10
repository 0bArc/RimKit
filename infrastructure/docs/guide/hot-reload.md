# Hot reload

Hot reload runs a mod's Lua again while the game is running, so you change a file, save, and see the result without restarting. This page says what a reload does, what it guarantees, what it cannot do, and how to test that your mod survives it.

```lua
-- Reload this mod whenever one of its Lua files changes (checked once a second)
game.dev.watch(true)

-- Or reload on demand, for example from a debug action
game.dev.reload("yourname.mymod")
```

`game.dev.watch(true)` and `game.dev.reload` need Development mode and the `dev` capability. The watcher reloads a mod only when one of its own Lua files changed, and `reload` only the mod you name.

## What a reload does

In this order:

1. Takes away what the old version put on the screen: gizmos, alerts, tabs, columns, designators, status lines, settings pages and open windows.
2. Takes away the old version's stat modifiers (`game.stats.modify`, `add_offset`, `add_factor`). Without this a factor of 1.5 would apply twice after the first reload.
3. Removes its `on_load` and `on_tick` callbacks, timers, event handlers, hooks and tweaks. Each removed event handler also gives back its claim on the game patch behind the event, so a hot event is unpatched again when the last handler goes.
4. Forgets its debug actions, the APIs it published with `game.interop.publish` and the in-game test suites it registered, so a name the new version no longer uses does not stay behind.
5. Forgets the cached modules of the mod, so `require` reads the new files.
6. Reads `About/RimKit.json` again (capabilities, budget), resets the error budget, and runs every Lua file of the mod.
7. Runs the new `on_load` callbacks.

The log says `reloaded <package id>` or `reloaded <package id> with errors`. A syntax error in a file leaves the rest of the mod loaded and does not make the watcher reload in a loop: fix the file, save again.

## When a reload happens

A reload never runs while the engine walks its callback lists, because removing a callback from the list being walked would corrupt it. So:

| Where `game.dev.reload` is called | When it runs |
|-----------------------------------|--------------|
| A debug action, the console, an event handler, a hook, a UI callback | Right away if nothing is walking a list, otherwise at the start of the next tick |
| An `on_tick`, `on_load` or timer callback | At the start of the next tick |
| The file watcher | At the start of a tick |

The call returns `true` when the reload was done or queued, and raises `RK3001` when the mod has no Lua loaded.

## What it cannot do

- **State outside your callbacks is kept.** Global variables and anything saved with `game.save` survive the reload. Top-level code runs again on top of them, so write `x = x or {}` and not `x = {}` for state you want to keep, and do not append to a global list at the top level.
- **Game classes and Defs.** Lua classes (`game.classes.define`) are defined again by name. XML Defs, patches and textures are loaded once at startup and need a restart.
- **Native registrations.** Key bindings made with `game.input.register_key` stay registered. They are keyed by name, so registering them again is harmless.
- **Things you did to the game.** Pawns you spawned, jobs you gave and defs you changed with `game.defs.set` stay as they are. Undo them in your own `on_load` if the new version should start clean.

## Test that your mod survives a reload

A mod is safe to reload when running it a second time leaves exactly the handlers it had after the first. Test that with the mock host, no game needed:

```lua
describe("my mod", function()
  it("survives a hot reload", function()
    game.test.start()
    local before = game.test.subscriptions("pawn.damaged")
    for _ = 1, 3 do expect(game.test.reload()).to_be(true) end
    expect(game.test.errors()).to_be(0)
    expect(game.test.subscriptions("pawn.damaged")).to_be(before)
    expect(game.events.count("pawn.damaged", "yourname.mymod")).to_be(1)
  end)
end)
```

`rimkit mod gen-tests` writes this test for every event your mod listens to, so you start with one. The pieces:

| Function | What it does |
|----------|--------------|
| `game.test.reload(package_id?)` | Reloads the mod under test, as `game.dev.reload` does in the game. Returns true when it loaded without errors |
| `game.test.subscriptions(event)` | How many handlers hold the game patch behind the event. A reload must not make it grow |
| `game.events.count(event, package_id?)` | How many handlers listen to the event, or how many of one mod's |

The [RimKit Standard](../standard/rks.md) asks for this test at level 2.

## Test it in the real game

The mock host cannot see the real patches. For that, write an in-game test, see [testing in the game](testing.md#testing-in-the-game). The `damage` example ships one: it reloads the mod inside a running game, checks that one damage handler is left, and hurts a pawn to check that the mod still reacts.

## How reload is checked

| Check | Where it runs |
|-------|---------------|
| No doubled handlers, timers, tick callbacks, debug actions or published APIs after several reloads | `tests/fixtures/reload`, headless, in CI |
| A reload asked for by a tick handler waits for the next tick, one asked for by an event handler finishes that handler first | Same fixture |
| A timer that starts the next timer is not corrupted | Same fixture |
| `events.off` only removes the handlers of the mod that calls it | Same fixture |
| Stat modifiers and windows are released | Host code, covered by the in-game `damage` test only for handlers. Not yet run in a game, see [gaps](../gaps.md) |
