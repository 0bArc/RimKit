# RimKit Standard (RKS)

Status: Draft 2. Applies to every public name, operation, event, error and document that RimKit ships.

The key words MUST, MUST NOT, SHOULD, SHOULD NOT and MAY are used as defined in RFC 2119.

## 1. Purpose

RimKit lets Lua control RimWorld through a C# host and Harmony. The API will grow to cover most of the game, so it needs rules that keep it predictable for mod authors and cheap to maintain. RKS is that rule set. Each rule has a reason, stated with it, so a contributor can apply the rule to a case the text does not list.

## 2. Principles

1. **One source of truth per fact.** A name, version, tier or argument shape is defined once and generated everywhere else. Reason: hand copied facts drift (today the version string exists in three places).
2. **Explicit over implicit.** An argument has one documented key and one type. A unit is in the name when the value is not a plain count. Reason: positional guessing and per-op key names are the main source of ambiguity today.
3. **Fail loudly.** Every failure returns a stable error code. Silent nulls and wrong-type no-ops MUST NOT occur. Reason: a mod that silently does nothing is the most expensive bug to diagnose.
4. **Additive by default.** A released name MUST NOT change meaning. It may be deprecated, then removed at a major version. Reason: clients ship mods that outlive RimKit releases.
5. **Least power.** Each op declares a tier and a capability. Reflection and raw Harmony are the highest tier and are audited. Reason: Lua mods run as the player.
6. **Traceable.** Every op can be traced from Lua name to C# handler to game call to doc entry to test. Reason: the game updates, and a gap must be findable.

## 3. Naming (UNC)

The Unified Naming Convention is defined in [naming](../api/naming.md). RKS requires conformance. A name that does not match UNC MUST NOT be registered.

## 4. Stability tiers

| Tier | Meaning | Change policy |
| --- | --- | --- |
| Stable | Covered by tests and docs. | Changes only at a major version, after one minor release of deprecation. |
| Experimental | Works, may change. | May change at any minor version. Changes are listed in the changelog. |
| Advanced | Can reach arbitrary game code (`reflect`, raw Harmony). | No stability promise. Gated by settings. Every call is audit logged. |

Rules:

1. Every op MUST be registered with a tier and a `since` version. An op without them is rejected at registration.
2. A new op starts Experimental. Promotion to Stable needs a changelog entry, docs, a stub, an example and a passing test.
3. `ApiRegistry` MUST enforce the tier. Advanced ops are denied unless the matching setting is on (`rimkit.developer_reflect` for reflection).
4. The Lua module `require("rimkit")` exposes `stable`, `experimental` and `advanced` tables generated from the registry, not written by hand.

## 5. Versioning

1. RimKit uses Semantic Versioning for the package (`MAJOR.MINOR.PATCH`).
2. A single `RimKitVersion` value is the only place the version is written. The build copies it to the C# constant, the native define, the Lua module and the VS Code extension. A mismatch fails the build.
3. `api_level` is an integer that increases when the Stable or Experimental surface gains an op. `assert_api(n)` fails when `n` is greater than the running level.
4. Release dates in changelogs and manifests use ISO 8601 calendar dates (`YYYY-MM-DD`). Durations in docs use ISO 8601 or an explicit unit name.
5. Deprecated names MUST be removed no earlier than the next major version. Until then each logs one warning per session, naming the canonical replacement.
6. Game versions. RimKit supports the current stable RimWorld release and the one before it. A kit function that exists only in newer games MUST be guarded with `game.version.at_least`, and a hook whose target differs between game versions MUST be installed with `game.version.hook`. A function that cannot work on the running game version raises `RK3003`.
7. Persisted type names MUST NOT change. Saves and settings refer to the original `RimLuaKit` namespace for `GameComponent_PawnControl`, `RimLuaModDataComponent` and `RimLuaSettings`. The rest of the host uses the `RimKit` namespace.
8. Typed results. A function declared to return a game object returns the wrapped object (`RimPawn`, `RimThing`, `RimMap`, `RimFaction`), and a list of them returns a table of wrapped objects. Every function accepts the wrapped object or its integer handle where it takes a game object. Functions from before api level 1 may still return plain integer handles, and that is the only difference between the two. They move to wrapped objects at api level 1.

## 6. Errors

1. An op failure returns an error object with `code`, `op` and `message`. Lua raises it as an error whose message starts with the code.
2. Codes are `RK` plus four digits. The first digit is the class:

| Class | Range | Meaning |
| --- | --- | --- |
| 1 | RK1000 to RK1999 | Argument errors (missing, wrong type, out of range) |
| 2 | RK2000 to RK2999 | Handle errors (stale, wrong kind, null) |
| 3 | RK3000 to RK3999 | Game state errors (no map, not spawned, DLC inactive) |
| 4 | RK4000 to RK4999 | Permission errors (tier denied, sandbox, capability) |
| 5 | RK5000 to RK5999 | Host errors (unexpected exception, version mismatch) |

3. Codes are never reused. A retired code stays reserved. `RK1002` means a name that was removed at api level 1 was used by a mod that opted in with `meta.api_level = 1`.
4. The `message` is for people and MAY change. The `code` is for programs and MUST NOT.

## 7. Arguments and returns

1. The subject of an op is the key `h`. A second game object is `target` or `other`. A def is `def` (a `defName` string). A scalar is `value` or `amount`. No other per-op names for these roles.
2. Ratios are floats from 0 to 1 and the name ends in `_ratio` when ambiguous. Percent values MUST NOT be used.
3. Time values end in `_ticks`, `_seconds` or `_days`. Distances end in `_cells`. Rotation values state `degrees` or use `Rot4` names.
4. Floats follow IEEE 754 binary32 as the game does. Hosts MUST NOT round values before returning them.
5. Identifiers are `defName` strings. Display text is `label`. A name MUST NOT mix the two roles.
6. A returned game object is a typed handle (kind plus id plus generation). Collections return Lua arrays of handles. Absent values return `nil`, never an empty string.
7. Handles from a removed map or a previous game MUST fail with an RK2xxx error, never resolve to another object.

## 8. Events

1. Event names are `<domain>.<past_tense_verb>` (see UNC).
2. An event delivers a payload table with named fields. A single bare handle is not a payload.
3. A hot event (called many times per tick) is opt in. Its Harmony patch installs on first subscription and is removed on last unsubscribe.
4. Handler errors are caught, logged with mod id and event name, and do not stop other handlers or the game.
5. The queue has a documented per tick budget. Dropped events increment a counter that `game.diagnostics` exposes.
6. Every catalog event has a name, a one sentence description that ends with `Payload: field, field.` (or no payload), a hot flag, a patch body, a stub and a row in the generated events page. Field names are lowercase with underscores. A field that holds a game object is a handle, a def is its `defName`, a count or flag is a plain number or boolean.
7. A patch body reads its inputs from `__instance`, `__args` and `__result`. It may name an `out` or `ref` parameter, because `__args` does not carry the final value of one.
8. An event whose target is inherited (declared on a base class) patches the declared method and filters by type in the body. Harmony refuses to patch an inherited method through a subclass.
9. Every catalog event is checked against the game's own assemblies on every host test run: the target resolves, the patch body exists, and Harmony accepts the patch. A game update that breaks an event fails that run, not a player.

## 9. Harmony and reflection use

1. Hooks MUST name the target by type, method and, when overloaded, parameter types.
2. A hook declares priority when order matters. Default priority is the Harmony default.
3. A hook that can change gameplay (prefix that skips, result writes) is Advanced.
4. Reflection stays gated by `rimkit.developer_reflect`, limited to the allowed game namespaces, and every call is written to the audit log with mod id, target and outcome.
5. The host MUST NOT expose file, process, network or assembly loading through any tier.

## 10. Documentation

1. Every domain has a page listing each op with name, tier, `since`, arguments, returns, errors and the Harmony targets it relies on.
2. Docs use short formal sentences and contain no em dash or en dash, per the repository markup rule.
3. Every Experimental or Stable op has a stub and at least one example.

## 11. Conformance and tests

An automated lint checks, on every build:

1. Every registered op id matches UNC and has a tier and `since`.
2. Every op has a doc entry and a stub.
3. Every event meets section 8, including the host test against the game assemblies.
4. Argument keys follow section 7.
5. The version is identical in all generated locations.
6. The headless suites pass: native tests, host unit tests, the reload fixture and the in-game runner fixture, and the tests of every example mod.

A change that breaks the lint MUST NOT be merged.

## 12. Hot reload contract

Hot reload (`game.dev.reload`, `game.dev.watch`) is part of the Standard, because a mod author who cannot reload loses the speed the whole kit exists to give. The host MUST guarantee the following. See [hot reload](../guide/hot-reload.md) for the full description.

1. After any number of reloads a mod has exactly the handlers, hooks, timers, tick callbacks, stat modifiers, debug actions, published APIs, gizmos, alerts, tabs, designators, status lines, settings pages and windows that one load gives it. Nothing is doubled and nothing from the old version stays behind.
2. A removed event handler releases its claim on the game patch behind the event. Reloading must not make a patch count grow.
3. A reload never changes a callback list while the engine walks it. A reload asked for from a tick, load or timer callback runs at the start of the next tick.
4. A failed reload (a syntax error, an error in `on_load`) leaves the host in a state where the next reload works, and never makes the watcher reload in a loop.
5. `events.off` removes only the handlers of the mod that calls it.
6. A repeating timer (a timer callback that starts the next timer) runs on every period, whatever the size of the timer list.
7. Every guarantee has a check. Items 2 to 6 and the handler, timer, tick, action and API parts of item 1 are checked headless by the reload fixture on every build. The parts that live in the game process (gizmos, alerts, tabs, designators, status lines, settings pages, windows and stat modifiers) are checked by in-game tests, and stay listed in [gaps](../gaps.md) until those tests have run in a game. A guarantee with no check MUST NOT be added.

A mod that wants to conform keeps its persistent state in `game.save`, or guards top level state with `x = x or {}`.

## 13. Mod conformance

A mod conforms to the Standard at one of four levels. `rimkit mod conform` computes the level and prints a line to put in `Workshop.md`.

| Level | Name | The mod |
|-------|------|---------|
| 0 | Not conforming | Misses a level 1 requirement |
| 1 | Declared | Sets `meta.api_level = 1`, declares `meta.capabilities`, has a semantic `mod_version` with a changelog entry, a licence file, and passes `rimkit mod check` |
| 2 | Tested | Level 1, and has tests that pass under `rimkit mod test`, at least one of them calls `reload()` (hot reload), and the Lua uses none of the pre-1.0 `rim.*` names |
| 3 | Verified in the game | Level 2, and has in-game tests in `Tests/Game` whose report from `rimkit mod test --in-game` shows no failure |

Rules:

1. A level is a claim about the mod's files, not about its quality. A level 3 mod can still be a bad mod.
2. The tool is the definition. A requirement that the tool does not check MUST NOT be in the table.
3. A mod MAY say "Built to the RimKit Standard, level N" in its description. It MUST NOT claim a level it does not meet.
4. The levels do not expire with a RimKit release. A new Draft adds a requirement at the next major version of the Standard, never inside a level.

## 14. Compatibility promise

1. Within one `api_level`, a name that was Stable keeps its meaning and its argument keys. A bug fix that changes a result in a way a correct mod could notice needs a deprecation first.
2. A name is deprecated for at least one minor release before removal, and removal happens only when `api_level` increases. The warning names the replacement.
3. `rimkit migrate` rewrites a mod from the previous `api_level` mechanically. If a change cannot be rewritten mechanically, it is not allowed in a Stable name.
4. RimKit supports the current stable RimWorld release and the one before it (section 5). A game update that breaks a catalog event or hook target is a RimKit bug, and the next patch release fixes it.
5. A mod written to level 2 of this Standard keeps passing its own tests across a RimKit patch or minor release. If it does not, that is a RimKit bug and is reported as one.

## 15. Revision

RKS changes through a pull request that states the problem and the reason for the new rule. The draft number increases with each accepted change.

| Draft | Change |
|-------|--------|
| 1 | First version: purpose, naming, tiers, versioning, errors, arguments, events, Harmony use, documentation, conformance lint |
| 2 | Event rules checked against the game assemblies, the hot reload contract, mod conformance levels (`rimkit mod conform`), the compatibility promise |
