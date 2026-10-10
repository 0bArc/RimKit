# Testing your mod

`rimkit mod test` runs the Lua files in your mod's `Tests/` folder without the game. RimKit loads your mod's `Lua/` folder against a mock host: every `game.*` call that would reach RimWorld is answered by the test instead. You decide what the game would say, call your code, and check what it did.

```text
# tests of the mod in the current folder
rimkit mod test

# a specific mod path
rimkit mod test src\examples\hello_lua

# also print what the mod logs
rimkit mod test --verbose
```

`rimkit mod create` adds `Tests/main_test.luau`, a test of the new mod's own on_load code. The command exits with 1 when a test fails, so it fits a CI job. It needs `rimlua_core.dll`: it looks next to `rimkit.exe`, in `../mod/Native`, and in the `RIMKIT_CORE` environment variable (or pass `--core path`).

## Writing a test

In files under `Tests/` you get `describe`, `it`, `expect`, `mock` and `effects` without importing anything. `mock.pawn()` makes a pawn the game would describe, `game.emit` sends an event with it, and `expect(effects.text)` checks what the mod asked the game to show.

```lua
describe("damage", function()
  it("shows damage for a hurt colonist", function()
    local pawn = mock.pawn({ colonist = true, position = { x = 12, z = 30 } })

    game.emit.pawn_damaged(pawn, 7.8)

    expect(effects.text).to_have_been_called({ target = pawn, text = "7", color = "#ff4d4d" })
  end)

  it("ignores animals", function()
    local pawn = mock.pawn({ humanlike = false })

    game.emit.pawn_damaged(pawn, 5)

    expect(effects.text).not_to_have_been_called()
  end)
end)
```

| Piece | Does |
|-------|------|
| `mock.pawn({ humanlike, colonist, name, position, map, def, hp })` | A pawn. It answers `is_humanlike`, `is_colonist`, `name`, its cell and its map. `position = false` is a pawn that is not on a map. Defaults: humanlike, not a colonist, at 10, 10 |
| `mock.thing({ def, hp, stack, position, map })` | A thing that answers `def`, `hp`, `stack`, its cell and its map |
| `game.emit.<domain>_<event>(subject, extra)` | Sends the event `domain.event`. `game.emit.pawn_damaged(pawn, 7.8)` sends `pawn.damaged`. A number fills the amount the event is about (`dealt` for `damaged`, `amount` otherwise). A table adds fields: `game.emit.pawn_damaged(pawn, { dealt = 7 })` |
| `effects.text`, `spy.<domain>.<name>` | A spy on the host call `effects.text`. Any call a mod makes to the game can be spied on this way |
| `expect(spy).to_have_been_called(match?)` | The call happened. With a table, one call has these argument values. `target = pawn` matches the cell of a mock pawn or thing |
| `expect(spy).not_to_have_been_called(match?)` | The call never happened, or never with these values |
| `expect(spy).to_have_been_called_times(n)` | The call happened exactly `n` times |
| `game.test.errors()` | How many handler errors the mod raised during this test |
| `game.test.handler_calls("pawn.damaged")` | How often handlers for the event ran after their filter let the event through |

The same API also works with the long names (`local t = game.test`, `t.mock(op, answer)`, `t.emit(name, payload)`), which you use to answer any other call the game would make:

```lua
local t = game.test

t.describe("My mod", function()
  t.it("blocks the food job when there is no food", function()
    t.mock("map.nutrition", 2)            -- the game would answer 2
    t.start()                              -- runs the mod's on_load handlers
    t.emit("pawn.spawned", { pawn = { is_colonist = true, name = "Ana" } })
    t.expect(t.logged("colonist spawned: Ana")).to_be(true)
  end)
end)
```

| Function | What it does |
|----------|--------------|
| `t.describe(name, body)`, `t.it(name, fn)`, `t.before_each(fn)` | Group and define tests |
| `t.expect(value)` | Checks: `to_be`, `to_equal` (deep), `to_be_nil`, `to_be_truthy`, `to_be_falsy`, `to_be_close(x, eps)`, `to_contain`, `to_have_length`, `to_error(part)` |
| `t.mock(op, answer)` | Replaces the host's answer for one op. A function receives the argument table and may raise an `RK` error. Any other value is returned as is |
| `t.calls(op?)` | The calls the mod made, with their arguments, to check what it asked the game to do |
| `t.emit(name, payload)` | Raises a named event so your handlers run |
| `t.tick(n)` | Runs your `on_tick` callbacks `n` times |
| `t.start()` | Runs your `on_load` callbacks |
| `t.class(family, name)` | The functions of a Lua class you defined with `game.classes.define`, to call directly |
| `t.tweak(name)` | The function you gave to `game.tweaks.on` |
| `t.logs()`, `t.logged(text)` | What the mod logged or showed during the test |

Every test starts with no mocks, no recorded calls and no logs. Ops you did not mock answer `nil`, so a test never reaches a real game by accident.

The mock host is strict where the real game would be, so a passing test cannot hide a bad call:

- `t.mock("pawn.set_draftd", ...)` raises `RK1001`: the host has no op with that name.
- A kit function called with the wrong type (a string where it wants a number), a missing required argument or too many arguments raises `RK1001`.
- A pawn, thing, map or faction argument takes the wrapped object or its integer handle. Anything else raises `RK2001`.

Op names are the kit names: `game.pawns.skills` is the op `pawn.skills`, and the argument keys are the parameter names, with the subject object under `h`. The reference page of each kit lists the functions, and `t.calls()` shows the exact op and arguments when you are unsure.

## Generate tests

You do not have to write the first tests by hand. `rimkit mod gen-tests` loads the mod against the mock host, sees which events it registered and with which filters, and writes `Tests/generated_test.luau`:

```text
rimkit mod gen-tests
rimkit mod test
```

For the damage example it writes tests like these:

```lua
it("pawn.damaged: handles a matching event", function()
  local subject = mock.pawn({ humanlike = true })
  game.emit.pawn_damaged(subject, { dealt = 2 })
  expect(game.test.errors()).to_be(0)
  expect(game.test.handler_calls("pawn.damaged") >= 1).to_be(true)
end)

it("pawn.damaged: ignores a pawn that is not humanlike", function()
  local subject = mock.pawn({ humanlike = false })
  game.emit.pawn_damaged(subject, { dealt = 2 })
  expect(game.test.handler_calls("pawn.damaged")).to_be(0)
end)
```

- Every event handler gets a test that sends a matching event and checks that nothing raised an error and the handler ran.
- Every filter key gets a test that breaks it (`humanlike`, `colonist`, `def`, `min_dealt`, or a payload field) and checks that the handler did not run. Those are skipped for an event that has more than one handler, because the count cannot tell them apart.
- There is one test that the mod loads without errors.
- The generator reads what the mod registers, not what it does. It cannot know that the number should be red, so add checks like `expect(effects.text).to_have_been_called(...)` by hand, in your own `Tests/main_test.luau`.
- Run it again after you change a filter. It overwrites `generated_test.luau` only while the file still starts with its "Generated by" line. Remove that line to keep a hand-edited copy safe, or pass `--force` to replace it anyway.

## Hot reload

`game.test.reload()` runs the mod's Lua again, as `game.dev.reload` does in the game, and `game.test.subscriptions(event)` tells how many handlers hold the game patch behind an event. Together they check that your mod survives a reload:

```lua
describe("my mod", function()
  it("survives a hot reload", function()
    game.test.start()
    local before = game.test.subscriptions("pawn.damaged")
    for _ = 1, 3 do expect(game.test.reload()).to_be(true) end
    expect(game.test.subscriptions("pawn.damaged")).to_be(before)
  end)
end)
```

`rimkit mod gen-tests` writes this test for you. See [hot reload](hot-reload.md) for what a reload does and does not undo.

## Tips

- Keep logic in small functions and publish them with `game.interop.publish`, or test them through the Lua class or tweak that uses them.
- Test the edges: a missing colonist, a setting outside its range, a pawn without skills.
- A test cannot check how something looks or how the game reacts. For that, run the mod in game. The in-game smoke suite in `tests/smoke` shows how to run scripted scenarios against a real game.
- `rimkit mod check` runs on the files, `rimkit mod test` runs the logic, the game runs the rest. Use all three before you publish.

## Testing in the game

The mock host answers what you tell it to. Some things only the real game can answer: that a patch applies, that a pawn takes damage, that your gizmo shows up, that a reload leaves one handler and not two. For those, write tests that run inside RimWorld.

In-game tests live in `Tests/Game/`, next to your mock tests. `rimkit mod test` skips that folder, and the game loads it only when a test run asks for it. A test is a function that gets a context `t`. It can wait for game ticks, wait for a condition or an event, and the things it spawns are removed when it ends.

```lua
-- Tests/Game/damage_in_game.luau
local suite = game.itest.suite("damage")

suite.test("a hurt colonist raises pawn.damaged with the damage dealt", function(t)
  local pawn = t.spawn_pawn()
  local hits = t.watch("pawn.damaged")

  pawn:damage(10, "Cut")

  t.wait_until(function() return #hits > 0 end, 60, "pawn.damaged never fired")
  t.expect(hits[1].dealt > 0).to_be(true)
end)
```

### Run them

```text
# starts RimWorld on its own throwaway save data folder, runs the tests, prints the result and closes the game
rimkit mod test --in-game

# options
rimkit mod test --in-game --game "D:GamesRimWorld" --timeout 600 --filter damage --with some.dependency --keep-open
```

The command gives the game its own mod list (Harmony, RimKit, your mod and the `depends` of your `meta.lua`, plus any `--with` ids) through `-savedatafolder`. It does not read or change your own `ModsConfig.xml`, settings or saves, and it copies nothing into the game folder. The tests are read from your source folder, so the shipped copy does not need to carry `Tests/Game`. Your mod must already be somewhere the game finds mods: the `Mods` folder (a junction to your source folder works), or the Workshop. It exits with 0 when every test passed, 1 when one failed, and 2 when the game could not run the tests. The report is also saved as `rimkit_ingame_report.json`.

Already in a running game with Development mode on? Use the console in the dev tools window (F11):

```lua
game.itest.load("yourname.mymod")
game.itest.run({ mods = { "yourname.mymod" }, filter = "damage" })
```

Progress goes to the log as `RKTEST PASS`, `RKTEST FAIL` and `RKTEST SKIP` lines, then `RKTEST DONE pass=N fail=M skip=K`.

### The test context

| Function | What it does |
|----------|--------------|
| `t.expect(value)` | The same checks as the mock tests: `to_be`, `to_equal`, `to_contain`, `to_error` and so on |
| `t.wait_ticks(n)` | Waits for `n` game ticks |
| `t.wait_until(fn, timeout_ticks?, message?)` | Waits until `fn()` is true, and fails with the message after `timeout_ticks` (600 by default) |
| `t.watch(event, filter?)` | A list that collects the payloads of an event while the test runs |
| `t.wait_event(event, filter?, timeout_ticks?)` | Waits for one event and returns its payload |
| `t.map()`, `t.center()` | The current map (the test is skipped when there is none) and its middle cell |
| `t.spawn(def, x, z, opts?)`, `t.spawn_pawn(opts?)` | Spawns a thing or a colonist, removed when the test ends |
| `t.cleanup(fn)` | Runs `fn` when the test ends, pass or fail |
| `t.set_speed(name)` | Changes the game speed for this test |
| `t.skip(reason)`, `t.fail(message)`, `t.log(text)`, `t.ticks()` | Skip, fail, note something in the report, read the tick |

`suite.before_each(fn)` and `suite.after_each(fn)` get the same context. `suite.skip(name, reason)` lists a test as skipped.

### Rules

- A test that raises an error or fails a check is a failure and the run goes on. A wait that times out fails with its message. A test that takes more than 18000 frames fails whatever it waits for (`max_frames`).
- Tests run one after the other, in the order they were declared, on the main thread, between frames. Do not block: use `t.wait_ticks` and `t.wait_until`.
- If the game is paused the runner unpauses it, because waits count game ticks.
- Leave the colony as you found it. Things made with `t.spawn` and `t.spawn_pawn` are removed for you. Anything else needs `t.cleanup`.
- Tests change a real game. Run them on a throwaway game, which `rimkit mod test --in-game` does for you.

### What is checked, and what is not

The runner itself is tested without a game against a fake clock: waiting, timeouts, events, skips, cleanup, the report and quitting. The launcher and the host side (`dev.test_env`, `dev.write_report`, `dev.quit`) have run against a real game: the `damage` example's three in-game tests passed on RimWorld 1.6.4871. That is one mod on one machine, so treat the launcher as new, see [gaps](../gaps.md).

The report is JSON: `{ schema, rimkit, game, pass, fail, skip, tests = [{ suite, name, mod, status, ticks, message, log }] }`. `rimkit mod conform --report rimkit_ingame_report.json` reads it to award level 3 of the [RimKit Standard](../standard/rks.md).
