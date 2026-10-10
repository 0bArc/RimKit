# Changes

What changed in RimKit, release by release, and how each change was checked. This page is the source of the `CHANGES.md` file in the repository root.

How to read it:

- **Checked** means a test or lint that runs without the game passed. The test is named.
- **Not run in the game** means the code is written and compiles, but nobody has played it yet. [Gaps and next steps](gaps.md) lists the in-game checks still to do, with an ID for each.
- Nothing here is marked as working unless something ran. If a change was not checked, it says so.
- The version is in `src/api/VERSION`. The Workshop copy is older than this page: 0.11.0 has not been published, see R-01 in [gaps](gaps.md).

## 0.11.0 (2026-10-08, not yet on the Workshop)

### Added

- **Helm.** A standalone tool in `helm/` that starts a game, keeps it running and operates it from outside: call operations, run Lua, step time, watch events, run scripts. RimKit ships the adapter and a RimWorld profile. It runs the real game with `-batchmode -nographics`. Windows only. [Operate the game with Helm](guide/helm.md)
  - Checked: 81 checks against a fake game over the real pipe. Run in the game: a headless RimWorld 1.6.4871 launched by Helm answered `info`, stepped 600 ticks, ran Lua, took a call, streamed a `pawn.damaged` event, passed an 8 step script and closed on `shutdown`. `rimkit mod test --in-game --headless` passed the `damage` tests 3 of 3.
  - Not run in the game: other games, and anything that is not Windows.
- **`time.step(ticks)`** runs game ticks now, with events and mods' tick callbacks after each tick. **`rimlua_eval`** lets the host run Lua text. **`dev.helm_publish`** and **`dev.helm_active`** connect events to Helm. The Helm library is hashed into the allowlist under `helm`.
- **28 new events, 116 in total.** Pawn left colony and escaped, work completed, item crafted, mining completed, construction started, building deconstructed, thing repaired, hauled and dropped, hediff healed, immunity gained, thought lost, social fight started, skill learning saturated, fire started and ended, world generated, caravan arrived, site visited, goods delivered, silver changed, research milestone, room changed, faction relation and leader changed, gizmo clicked, key pressed. [Events](api/events.md)
  - Checked: `tests/host/EventCatalogTests.cs` resolves every event target against the installed game and has Harmony accept each patch.
  - Not run in the game: the patch bodies themselves (V-12). Six events patch methods that call engine internals and cannot be patched by that test.
- **Tests in the real game.** `game.itest` runs suites from `Tests/Game/` inside RimWorld, with waits for ticks, conditions and events, cleanup of what a test spawned, and a JSON report. [Testing in the game](guide/testing.md#testing-in-the-game)
  - Checked: the runner against a fake clock, 15 tests in `tests/fixtures/itest`.
  - Run in the game (RimWorld 1.6.4871, 2026-10-08): the `damage` example's three in-game tests passed through the launcher, including the one that hot reloads the mod and checks one handler is left.
- **`rimkit mod test --in-game`.** Starts the game on its own throwaway save data folder and mod list, so your own `ModsConfig.xml`, settings and saves are not used, then reads the report. The tests are read from your source folder, so the shipped copy does not carry them.
  - Run in the game: it launched RimWorld, ran the tests, wrote the report and closed the game, three times in a row. The game log shows `RKTEST` lines and the host closing with the right exit code.
- **`rimkit mod conform`.** Checks a mod against the [RimKit Standard](standard/rks.md) and prints its level, 0 to 3, with a line for the Workshop page. The `damage` example reaches level 3 with its in-game report, and CI checks level 2.
- **`rimkit update`.** One command for the people who work on RimKit: it fixes the version in every file, regenerates the kit table, docs and editor stubs, runs the lints, and with `--release` rewrites the release allowlist and verifies it. `--check` only checks. [CLI](guide/cli.md)
- **RimKit Standard draft 2.** Event rules, the hot reload contract, mod conformance levels and a compatibility promise. [Standard](standard/rks.md)
- **Hot reload guide.** What a reload does, when it runs, what it cannot do, and how to test it. [Hot reload](guide/hot-reload.md)
- **Small API additions.** `game.log.warn`, `game.events.count(name, package_id?)`, `game.test.reload()` and `game.test.subscriptions(event)`. `rimkit mod gen-tests` now writes a hot reload test.
- **This page.**

### Fixed

- **`pawn.damaged` could never install.** It patched `Pawn.TakeDamage`, which the game does not declare, and Harmony refuses that. It now patches `Thing.TakeDamage` and keeps only pawns. Found by the new event test. It had been in the catalog before this release.
- **Hot reload left the game patch behind every event installed**, and the count grew with each reload. A hot event stayed patched for the rest of the session.
- **Hot reload doubled stat modifiers.** A factor added with `game.stats.add_factor` applied once more after every reload. Stat modifiers and windows are now released with the mod.
- **A reload from inside a tick, load or timer callback could corrupt the callback list.** It now waits for the next tick.
- **A timer that starts the next timer could corrupt the timer list.** This is the usual way to repeat something. Due timers are now taken out of the list before they run.
- **`events.off` removed the handlers of every mod.** It now removes only the handlers of the mod that calls it, as the documentation always said.
- **An `on_load` or `on_tick` callback that registered another callback could use freed memory.** The lists are now walked by index on a copy.
- **The in-game runner lost events.** Its first real run in the game showed `pawn.damaged` never reaching a test. The runner's dispatcher used a generic `ipairs` loop that failed with "attempt to call a table value" when an event arrived while a test was running. It now uses plain indexing. This was invisible to the fake-clock tests and found only by running in the game. Ecosystem Lua chunks also have names now (`rimkit/itest:67`) so an error says where it came from.
- **Tame Anomalies defaulted to F6 and F7**, which clash with vanilla Research and Quests. It now uses `[` and `]`.
- **`ReflectProbe` called `game.log.warn`, which did not exist.** It does now.
- **Editor package version disagreed with `src/api/VERSION`**, which failed the version lint. `rimkit update` fixes this class of error.
- **CI**: the mod tests step was glued to the previous line and never ran, and the native test compiles lacked `/std:c++17`. Both fixed. CI now also runs the reload and runner fixtures.
  - Checked: `tests/fixtures/reload` (13 tests) covers the hot reload fixes, run headless.
  - Run in the game: one handler left after a reload, and the mod still reacts. Not run in the game: stat modifier, window and gizmo release (V-04, V-13).

### Changed

- `game.dev.reload` returns as soon as the reload is done or queued, and raises `RK3001` when the mod has no Lua loaded.
- Version 0.11.0 (was 0.10.0), `api_level` stays 0.

### How this release was checked

| Check | Result |
|-------|--------|
| Host unit tests (`tests/host`, includes every event against the game assemblies) | 45 passed |
| Native tests: `budget_test`, `kits_test`, `aliastest` | passed |
| Mod tests of every example, guide mod and fixture | passed |
| Generators and lints: kit table, kit pages, events, ops, editor, docs, hook targets, Luau definitions | up to date |
| In-game tests of the `damage` example, through `rimkit mod test --in-game` | 3 of 3 passed |
| In-game smoke suite | not run since the second event pass (it was 434 of 434 before) |

## 0.10.0

The state of the code on the Workshop. In short: a Luau runtime on a C++ core with a C# Harmony host, 105 kits and 568 functions, 88 events, hooks, typed reflection, Lua-backed game classes (31 families), tweaks, settings, Def authoring, DLC kits (Ideology, Royalty, Biotech, Anomaly, and a minimal Odyssey), widgets, gizmos, tabs, designators, a dev tools window, a profiler, a capability model, a mock-host test runner, the CLI and a VS Code extension. The in-game smoke suite passed 434 of 434.

## Earlier versions

Versions before 0.11.0 were not tagged as releases in this repository. The history is in `git log`: nine commits since April 2026. The `since` field of each operation in the [API reference](api/reference.md) says in which version it appeared, back to 0.6.0.

## Where things stand

[Roadmap and status](missing.md) lists what is built, partly built and open. [Gaps and next steps](gaps.md) lists what still needs a person to try it in the game, which is the main reason there is no 1.0 yet. We would rather say that here than have you find it out.
