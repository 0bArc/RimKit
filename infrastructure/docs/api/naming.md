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

Every event also has its own function, `game.events.on_<domain>_<verb>(fn, filter?)`, for example `on_pawn_died`. Its handler gets the pawn (or thing) first and the payload second, and the editor types both from that name.

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

## 9. Checklist for a new op

1. Pick the domain. If none fits, propose a domain first.
2. Write the name from section 3. Check it is not a synonym of an existing op.
3. Fix the argument keys from section 6.
4. Register it with tier and `since`.
5. Add doc, stub, example and test.
