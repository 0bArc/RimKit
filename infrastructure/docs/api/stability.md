# API stability

Every function has a tier, so you know what you can rely on. The names below use the `game.<domain>` form.

```lua
local rk = require("rimkit")
-- rk.version, rk.api_level, rk.stable, rk.experimental
rk.assert_api(0)   -- errors if the running RimKit is older than level 0
```

`require("rimkit")` is the contract entry: the version and the tiers. The `game` table and the globals are always available.

## Tiers

| Tier | Promise |
|------|---------|
| **Stable** | No removal and no change of meaning until the next major version. Additions are allowed |
| **Experimental** | The shape may change in a minor version. Every change is in the changelog with a migration note |
| **Advanced** | No compatibility promise. Gated by settings, audited, and the rules may tighten |

## What is in each tier

### Stable

- Every kit page listed in [kits and domains](domains.md) (the `game.*` kits added after the base API) is Experimental until it has been used by a released mod.
- Pawns, things, maps and factions: the handle functions behind `game.pawns` (identity, needs, health, gear, jobs), `RimThing` properties and methods, `game.things.make`, `game.maps`, `game.factions`
- `game.events.on` and `off` (and the `events` shorthand), `game.log`, `game.timer`
- `game.data`
- `game.ui.message`, `letter`, `panel`, `window`, `float_menu`
- `game.current_map`, `game.tick`, `game.player_faction`
- Wrapped objects: `RimPawn`, `RimMap`, `RimThing`, `RimFaction`

### Experimental

- The typed pawn kit: skills, needs, traits, thoughts, relations, backstory, capacities, timetable, genes ([pawns](pawns.md)), and `game.ui.translate`
- The anomaly kit: `game.anomaly`, `game.anomalies` ([anomaly](anomaly.md))
- The DLC kits: `game.dlc`, `game.ideology`, `game.royalty`, `game.biotech`, `game.odyssey`, and the anomaly depth functions ([dlc](dlc.md))
- `game.tweaks` ([tweaks](tweaks.md))
- `rimkit.signal` and `rimkit.promise` ([signal and promise](signal-promise.md))
- `game.interop` ([interop](interop.md)), `game.test` ([testing](../guide/testing.md)), `game.save.migrate`, `stamp` and `purge`
- Named events with payloads and `events.list` ([events](events.md))
- `game.jobs`, `game.paths`, `game.control`, `game.config`, `game.defs.register_thing`, `game.ui.on_map_float_menu`
- `game.buildings`, `game.work`, `game.weather`, `game.incidents`, `game.audio`
- Simple hook sugar: `game.hooks.before[...]` and `game.hooks.after[...]`

### Advanced

- `game.dev` and `game.profiler` ([dev](dev.md)), and `meta.capabilities` ([capabilities](../guide/capabilities.md))
- `game.classes` ([classes](classes.md)), `game.defs` runtime changes and authoring ([defs](defs.md)), `game.patch`, `game.mods` ([patch](patch.md))
- `game.stats.modify` and its helpers ([research](research.md))
- `game.hooks` (all of it), and in particular hooks that skip the original, write results or arguments, catch exceptions, or replace a call ([hooks](hooks.md))
- `game.reflect` ([reflect](reflect.md))
- `rim.invoke`

## Rules

1. A new function is Experimental for at least one full minor version.
2. Promotion to Stable needs docs, an example, a stub, a test, and a changelog entry.
3. Breaking a Stable function means a new major version (`api_level` increases). Breaking an Experimental function means a minor version and a migration note.
4. A rename after 1.0 keeps the old name working for one minor version, prints one deprecation line per session, and is announced in the changelog.
5. Advanced functions may change at any time, but a change is still announced.

The full policy, including deprecation and versioning, is section 4 and 5 of [the RimKit Standard](../standard/rks.md).

## Version fields

| Field | Meaning |
|-------|---------|
| `version` | The RimKit package version, `MAJOR.MINOR.PATCH` |
| `api_level` | An integer that rises when the API gains functions or breaks. `0` before 1.0, `1` at 1.0 |

## Opting in to api level 1 now

The whole API stays at level 0 until the 1.0 gate. A mod can adopt the level 1 rules today by setting `meta.api_level = 1` in `meta.lua`. For that mod:

| Rule | Level 0 (default) | `meta.api_level = 1` |
|------|-------------------|----------------------|
| A failing handle function | Logs and returns `nil` | Raises the `RK` error, like every kit function |
| Capabilities | Undeclared means full access | Undeclared means none, declared means exactly those |

Every function accepts a wrapped object or an integer handle for pawns, things, maps and factions, at both levels.

Functions that return plain integer handles keep doing so until the whole API moves to level 1.

## The 1.0 gate

RimKit 1.0 happens when:

- The Stable set is frozen for at least one minor version.
- Every Stable function has a stub, an example, docs and a test.
- Experimental kits that are ready have been promoted. Others may keep changing.
- `api_level` is 1 and the changelog states the compatibility promise.

The work that gets us there is in [what is missing](../missing.md).

## Asking for a promotion or a new function

See [request a feature](../guide/request-a-feature.md). A named kit function is preferred over Advanced reflection.
