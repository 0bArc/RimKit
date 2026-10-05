# Unified Naming Convention (UNC)

UNC gives every RimKit name one shape from C# to Lua. A mod author who knows the convention can guess the name of an op that they have not seen. It is part of the [RimKit Standard](../standard/rks.md).

## 1. The path rule

The Lua path is `game.<domain>.<verb_noun>`. The host op that backs it keeps the singular domain, so the mapping is mechanical and the Lua surface is the contract.

| Layer | Form | Example |
| --- | --- | --- |
| Lua call | `game.<domains>.<verb_noun>(...)` | `game.pawns.set_skill(p, "Shooting", 12)` |
| Host op id | `<domain>.<verb_noun>` (singular domain) | `pawn.set_skill` |
| C# handler | `Api<Domain>` class, method `<VerbNoun>` | `ApiPawn.SetSkill` |
| Stub | generated into `stubs/rimkit_unc.lua` | |

Host op ids are an implementation detail. Mods use the Lua path. A new domain gets one C# class, one Lua table and one doc page, never a suffixed variant such as `Plus`.

## 2. Domains

A domain is a plural noun for the thing it controls: `pawns`, `things`, `maps`, `time`, `research`, `incidents`, `storyteller`, `quests`, `world`, `combat`, `economy`, `buildings`, `plants`, `animals`, `defs`, `saves`, `ui`, `audio`, `weather`, `factions`. DLC domains use the DLC name: `ideology`, `royalty`, `biotech`, `anomaly`, `odyssey`.

One domain, one C# class, one Lua table, one doc page. A domain MUST NOT be split across classes with suffixes such as `Plus`.

## 3. Verbs and nouns

| Intent | Name form | Example |
| --- | --- | --- |
| Read one value | noun | `game.time.ticks()` |
| Read a flag | `is_<adj>` or `has_<noun>` | `pawn.is_downed`, `pawn.has_hediff` |
| Read many | plural noun | `game.maps.pawns(map)` |
| Find by condition | `find_<noun>` | `game.things.find_nearest(...)` |
| Change a value | `set_<noun>` | `game.pawns.set_name(p, "Ana")` |
| Add or remove | `add_<noun>`, `remove_<noun>` | `add_trait`, `remove_hediff` |
| Run an action | verb | `kill`, `spawn`, `destroy`, `start_job` |
| Run a request that can be refused | `try_<verb>` returns `ok, err` | `try_fire` |

Rules:

1. Names are lowercase `snake_case`, ASCII only, at most 3 words.
2. A name is a verb or a noun, never an abbreviation that is not a game term. Game terms keep the game spelling in lowercase (`hediff`, `def`, `lord`).
3. Booleans MUST read as a question (`is_`, `has_`, `can_`).
4. A getter and setter pair share the noun (`hunger` and `set_hunger`).

## 4. Types

OO wrapper types are `Rim<Noun>` in PascalCase: `RimPawn`, `RimThing`, `RimMap`, `RimFaction`, `RimHediff`, `RimDef`, `RimCell`. A wrapper exposes:

- Properties for nouns and flags (`pawn.name`, `pawn.is_downed`).
- Methods for verbs (`pawn:kill()`).
- The same names as the flat op (`game.pawns.kill(p)` equals `p:kill()`).

A wrapper MUST NOT add a name that has no flat op.

## 5. Events

`<domain_singular>.<past_tense_verb>`, for example `pawn.died`, `thing.spawned`, `hediff.added`, `job.ended`, `research.finished`, `game.loaded`. Subscribe with `events.on("pawn.died", fn)`. The handler receives one payload table.

Sugar globals follow `on_<domain>_<verb>` only for legacy names and are deprecated.

## 6. Arguments

Argument keys are fixed by role, so the host never guesses:

| Role | Key |
| --- | --- |
| Subject object | `h` |
| Second object | `target` or `other` |
| Definition | `def` (a `defName`) |
| Map | `map` |
| Position | `pos` (a `RimCell` or `{x, y}`) |
| Scalar | `value`, `amount`, `count` |
| Text | `text`, `label`, `key` |
| Options | `opts` (table) |

Lua calls use positional arguments. The binder maps position to key from a declared signature in the manifest, not by type guessing.

## 7. Units

| Quantity | Rule |
| --- | --- |
| Fractions | Float 0 to 1. Suffix `_ratio` when the name is not obviously a fraction. No `_pct`. |
| Time | `_ticks`, `_seconds`, `_days`. Calendar dates use ISO 8601 strings. |
| Distance | `_cells` |
| Mass, temperature | Game units, stated in the doc (`kg`, `celsius`) |

## 8. Constants

`UPPER_SNAKE` in a domain table: `game.time.SPEED_PAUSED`. Def names stay as the game spells them (`"Shooting"`, `"Anomaly"`).

## 9. Deprecated aliases

The single source of truth is `src/api/aliases.json` in the repository. The native runtime, `rimkit migrate` and the VS Code extension all read that file, so they cannot disagree.

Behaviour:

1. The canonical `game.<domain>.<name>` function exists and never warns.
2. The old name keeps working. The first call in a session logs one line, for example `deprecated: rim.pawn.name is now game.pawns.name (the old name works until 1.0.0)`.
3. Old names are removed no earlier than 1.0.0.
4. Core shorthands `events.on`, `log.info` and `timer.after`, and the `game.tick`, `game.current_map`, `game.player_faction` helpers, stay valid. They also exist under `game.events`, `game.log`, `game.timer`, `game.time`, `game.maps` and `game.factions`.

Examples:

| Old | Canonical |
| --- | --- |
| `rim.pawn.name(h)` | `game.pawns.name(h)` |
| `rim.pawn.hunger_pct(h)` | `game.pawns.hunger(h)` |
| `rim.pawn.health_pct(h)` | `game.pawns.health_ratio(h)` |
| `health.tend(h)` | `game.pawns.tend(h)` |
| `surgery.queue_operation(h, r)` | `game.pawns.queue_surgery(h, r)` |
| `rim.find.tick()` | `game.time.ticks()` |
| `rim.find.current_map()` | `game.maps.current()` |
| `rim.map.total_human_edible_nutrition(m)` | `game.maps.nutrition(m)` |
| `rim.job.start(p, j)` | `game.jobs.start_job(p, j)` |
| `world_api.set_weather(d)` | `game.weather.set(d)` |
| `incident.try_fire(d)` | `game.incidents.try_fire(d)` |
| `path.walk(p, x, z)` | `game.paths.walk(p, x, z)` |
| `building.flick(h, v)` | `game.buildings.flick(h, v)` |
| `rim.hooks.prefix(t, m, fn)` | `game.hooks.prefix(t, m, fn)` |
| `ui.message(t)`, `rim.message(t)` | `game.ui.message(t)` |
| `rim.on_load(fn)` | `game.events.on_load(fn)` |
| `pawn:seek_medical_help()` | `pawn:seek_medical()` |
| `map:spawn_thing(...)` | `map:spawn(...)` |
| `events.on("pawn_died", f)` | `events.on("pawn.died", function(e) ... e.pawn ... end)` |

`rim.reflect`, `rim.cs` and `rim.harmony` are not rewritten, because their argument and result shapes differ or they are the same table as another name. See [reflect](reflect.md).

Rewrite a mod with `rimkit migrate`. See [migration](migration.md).

## 10. Checklist for a new op

1. Pick the domain. If none fits, propose a domain first.
2. Write the name from section 3. Check it is not a synonym of an existing op.
3. Fix the argument keys from section 6.
4. Register it with tier and `since`.
5. Add doc, stub, example and test.
