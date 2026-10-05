# What is missing

The complete list of what RimKit cannot do yet, in the order we should build it, aimed at one goal: a Lua author can build and publish the kind of mod that sits at the top of the Steam Workshop, not only small tweaks.

How to read this page:

- **Now** is what works today, so the gaps have context.
- **Workshop categories** says, for each kind of popular mod, whether it can be built today and which gaps block it.
- Every gap has an ID (`P1-04` is Phase 1, row 4; `W-07` is Workshop readiness, row 7), so a mod idea, an issue or a commit can point at it.
- Sizes are rough: S is days, M is a couple of weeks, L is a month or more of focused work.
- The [kit status board](api/domains.md) shows per-domain state. [Mod ideas](mod-ideas.md) shows what each phase unlocks.

## Now

| Area | State |
|------|-------|
| Reacting to the game | 87 named events with payloads |
| Changing game behavior | Hooks: prefix, postfix, finalizer, call replacement, overloads, results, arguments, state, priority |
| Reaching any game object | Typed reflection, gated and audited |
| Game state kits | Things, maps, time, factions, pawns (data, health, gear, social, mind), jobs, selection and camera, queries, AI, storyteller, incidents, quests, research, stats, world, combat, construction, power, bills, economy, raids, generation, scenarios, plants, conditions, weather, saving |
| Extending the game | Lua-backed game classes (31 families), stat modifiers, tweak catalog, runtime def changes, Def XML authoring, XML patches, mod queries, hooks into other mods, per-thing saved data |
| DLC | Ideology, Royalty, Biotech, Anomaly (monolith, studies, containment, codex, engagement) and Odyssey (minimal) kits behind one DLC guard |
| Presentation | Widget windows, gizmos, main, inspect and column tabs, designators, areas, graphics, materials, effects, input, audio, alerts, HUD, letters with choices, settings pages |
| Content | Any XML Def, XML patch, texture, sound and translation ships next to the Lua and is loaded by the game as normal |
| Mod plumbing | Settings pages, per game, map and world saved data, runtime key bindings, Lua jobs, movement, JSON, mod interop, capabilities, hot reload, profiler, strict mode (`meta.api_level = 1`) |
| Tooling | CLI (create, sync, ship, check, test, assets, i18n, release-check, publish, diag), editor extension (completions, def names, go to definition, wizard), kit table generator, op lint, release tool, CI workflow, in-game smoke suite, host unit tests |
| Rules | Naming convention, stability tiers, error codes, the RimKit Standard |

## Workshop categories

What it takes to build the mods people actually subscribe to. "Now" means the whole mod can be built today. "Partly" means the core can, with reflection or gaps. "Blocked" means a missing piece stops it. IDs point at the gaps below.

| Category | Typical examples | It needs | State | Blocked by |
|----------|------------------|----------|-------|------------|
| Quality of life tweaks | Auto-draft, smarter notifications, bulk work priorities | Events, hooks, pawn kit, UI | Now | |
| Numbers and info panels | Colony stats, mood explainers, skill tables | Reading pawns and maps, windows, tables | Now | |
| Pawn behavior and AI | Smarter hauling, better doctors, auto-tend, work priorities | Work givers, job givers, think trees, job control | Now | |
| Stat and balance tweaks | Accuracy, speed, work speed, comfort, beauty | Stat hooks, def mutation | Now | |
| New items and recipes | Weapons, apparel, foods, drugs, furniture, materials | XML Defs, recipes, research, optional comps | Now | |
| New buildings with behavior | Generators, turrets, workstations, doors | XML plus building classes, comps, place workers | Now | |
| New plants, animals, races | Crops, creatures, new playable races | XML plus renderers, graphics, animal AI | Partly | P3-05 |
| New factions and cultures | Factions, raiders, traders, pawn kinds | XML plus faction generation, raid strategies | Now | |
| Storytellers and incidents | Custom storyteller, new raids, events with choices | Storyteller state, incident workers, quests, letters with choices | Now | |
| Quests | Quest chains, contracts | Quest scripting and parts | Now | |
| Combat overhauls | Ballistics, armor, ammo | Verbs, projectiles, damage workers, stat parts | Now | |
| Medical overhauls | Surgery, prosthetics, diseases | Hediffs, body parts, recipes, workers | Now | |
| Needs, moods, thoughts | New needs, social systems, mental breaks | Need and thought classes, workers | Now | |
| Traits, skills, backstories | New traits, passions, backgrounds | XML plus workers | Now | |
| Genetics and xenotypes | Custom genes, xenotypes | Biotech kit, gene effects | Now | |
| Ideology and rituals | Memes, precepts, rituals | Ideology kit, ritual behaviors | Now | |
| Royalty and psycasts | Titles, permits, abilities | Royalty kit, ability comps | Now | |
| Abilities and powers | Psycasts, gene abilities, active effects | Ability comps and verbs | Now | |
| World and map generation | Biomes, terrain, map types, world features | Gen steps, biome and terrain defs, world gen | Now | |
| Scenarios and start options | Custom scenarios, challenges | Scenario parts, setup | Now | |
| Mechanoids and robots | Mechs, drones, automation | Biotech mech kit, AI, pawn kinds | Now | |
| Vehicles, ships, travel | Gravships, transport | Odyssey kit, world travel, map transitions | Partly | Launching a gravship, travel and orbital sites (D-02) |
| New UI and tabs | Main tabs, inspect tabs, gizmos, designators | Gizmos, tabs, widgets, tools | Now | |
| Graphics and visuals | Apparel layers, hair, body types, effects, overlays | Render nodes, graphics, shaders, textures, fleck effects | Partly | Custom graphic classes |
| Audio | Music, ambient, effect sounds | SoundDefs (XML works), runtime play and music | Now | |
| Compatibility patches | Make mod X work with mod Y | Detect mods, conditional hooks, other mods' types | Now | |
| Library mods | Shared code other mods depend on | Cross-mod API, versioning | Now | |
| Developer tools | Consoles, inspectors, debug actions | Reflection browser, debug action registry | Now | |
| Translations | Language packs | Keyed strings (works), tooling | Now | W-12 |

Reading the table: roughly half the categories need either a kit (Phases 1 and 2) or Lua-backed game classes (Phase 4). Phase 4 is the largest single unlock, because most "new content with behavior" mods are XML plus a C# class.

## Phase summary

| Phase | Goal | Unlocks | Rows |
|-------|------|---------|------|
| 0. Foundations | Safe to grow: tests, CI, one version, cheaper hooks, typed handles, crash isolation | Confidence to build fast | 18 |
| 1. Core simulation kits | Things, maps, time, factions, the rest of pawns, jobs | Most tweak and automate mods without reflection | 12 |
| 2. Systems kits | AI, research, storyteller and quests, economy, world, combat, buildings, plants, generation | Whole-system mods | 14 |
| 3. Presentation | Gizmos, tabs, designators, overlays, graphics, audio | Mods with their own UI and visuals | 10 |
| 4. Extensibility | Stats, tweaks, runtime Defs, Lua-backed game classes | New kinds of things, not just tuning | 12 (done) |
| 5. DLC kits | Ideology, Royalty, Biotech, Odyssey, Anomaly depth | DLC mods | 6 (done) |
| 6. Ecosystem | Interop, hot reload, profiler, capabilities, tools | A platform others build on | 10 (built, in-game scenario saves not) |
| W. Workshop readiness | Publish, version, package, support | A mod that is ready for strangers | 14 (built) |
| 1.0 gate | Freeze the API | A promise to authors | 1 |

## Phase 0: Foundations

Goal: make growth safe. Nothing here adds a feature, all of it prevents a regression.

| ID | Item | Why | Size | Done when |
|----|------|-----|------|-----------|
| P0-01 | C# unit tests | The host (registry, codec, hook bridge, policy) is only tested through the game | M | A test project builds in CI and covers `HookCodec`, `Json`, `ReflectPolicy`, alias and op registration |
| P0-02 | CI | Builds and tests are manual today | S | A workflow builds native and host, runs the native tests, lints names against `src/api/aliases.json`, runs the docs check |
| P0-03 | One version source | The version is written in the native module, the extension and the docs | S | A single value feeds all of them and a mismatch fails the build (RKS section 5) |
| P0-04 | Op conformance lint | Rules in the Standard are not enforced | M | A check fails the build for an op without tier, `since`, doc entry or stub |
| P0-05 | Typed handles everywhere | Older functions return plain integers | M | Every function that returns an object returns a wrapped value, one shared wrapper |
| P0-06 | Handle lifetime | Handles keep objects alive until a game loads | M | Weak references with a generation counter, stale handles fail with `RK2001` even within a game |
| P0-07 | Cheaper hooks | Every hook call builds and parses JSON | M | A fast path for scalar-only hooks, measured: a hook on a hot getter costs microseconds |
| P0-08 | Hook cost guard | A hot hook can slow the game and nothing warns | S | `game.hooks.list()` reports call counts and time, and the log warns about an expensive hook |
| P0-09 | Remove the old namespace | The C# namespace is still `RimLuaKit` and some logs say RimLuaKit | S | One brand everywhere, with a settings migration so saved options survive |
| P0-10 | Fix key-guessing binders | The old `bind_op` helper maps arguments by guessing keys | M | Every legacy function is bound with explicit argument names |
| P0-11 | One error model | Older functions return `nil` and log, newer ones raise | S | One policy in the Standard and every function follows it |
| P0-12 | Token-aware scanner | Words like `load(` in a function name quarantine a mod | S | The scanner understands Lua tokens, not raw text, and reports the exact line |
| P0-13 | Release process | `rimkit build` and allowlist regeneration are manual | S | One command builds, hashes, packs and verifies a release |
| P0-14 | Game-update watch | A game update can silently break a hook target | M | A CI job resolves every catalog event and every shipped hook target against the current `Assembly-CSharp` and fails on a missing or changed method |
| P0-15 | Crash isolation | One broken mod should not break the others or the game | M | A Lua error budget per mod: after N errors in a window the mod's hooks are disabled with a clear message |
| P0-16 | Safe mode | A player needs a way to boot without Lua | S | A setting or command-line flag skips all Lua and says so in the log |
| P0-17 | Multi-version support | RimWorld 1.5 and 1.6 differ, and 1.7 will | L | Version-aware hook targets and kits, per-version tests, a documented support policy |
| P0-18 | Coverage tracking | No one knows which functions are tested | S | The generated reference shows a tested mark per function, from the smoke suite |

### Phase 0 status

All 18 items are built. Checks that run without the game: native tests (`aliastest`, `budget_test`, `kits_test`, `hookbench`), 37 host unit tests, and the lint, docs, version, kit and editor checks. Checks that need the game run in the smoke suite.

| ID | Kit or tool | What was added |
|----|-------------|----------------|
| P0-01 | `tests/host` | `tests/host` (xunit, net48): JSON reader, scanner, `Opts`, `Jb`, `JsonLite`, handle table. 34 tests. Registry and hook bridge need live game types and are covered by the smoke suite. |
| P0-02 | CI workflow | `.github/workflows/ci.yml`: lint, kit table, editor, docs, version and hook-target checks, extension packaging, native build and tests, and a self-hosted job for the host build, unit tests and release check. It runs when pushed. |
| P0-03 | `src/api/VERSION` | `src/api/VERSION` and `API_LEVEL` feed native, host, extension and docs. |
| P0-04 | `check-ops.js` | `check-ops.js`: names, tiers, `since`, duplicates, every kit function resolves to a host op with a description and return type, every registered op has a Lua surface. |
| P0-05 | Typed handles | Kit functions return wrapped objects (`RimPawn`, `RimThing`, `RimMap`, `RimFaction`) from their declared return type, and everything accepts wrapped objects or handles. Older functions keep integer results until api level 1, see the Standard section 5. |
| P0-06 | Handle table | Weak handles for things, maps and factions, pinned handles for the rest, generation counter, `RK2001` on stale use. |
| P0-07 | `game.hooks` fast path | Scalar-only hooks skip JSON: 3.4 us against 7.2 us per call. |
| P0-08 | `game.hooks.list` | `game.hooks.list()` reports calls and time, the log warns about hooks over 100 us. |
| P0-09 | Namespace `RimKit` | Namespace `RimKit`. Persisted types keep `RimLuaKit`. |
| P0-10 | `bind_op` | `bind_op` takes explicit argument names. |
| P0-11 | `game.strict_errors` | `game.strict_errors(true)` raises `RK####` from older functions. Kit functions always raise. |
| P0-12 | Threat scanner | Token-aware scanner with line numbers. Unit tested, and run in game by the smoke suite. |
| P0-13 | `release.js` | `release.js` writes and verifies the allowlist and deploys with verification. |
| P0-14 | `gen-hook-targets.js` | `gen-hook-targets.js` and the smoke suite resolve every shipped hook target and every catalog event against the running game. |
| P0-15 | Mod error budget | 20 errors in 60 seconds switch a mod off. |
| P0-16 | Safe mode | `-rimkit-safe`, `RIMKIT_SAFE`, `Config/rimkit_safe.txt`. |
| P0-17 | `game.version` | `game.version.rimworld`, `at_least`, `rimkit`, `api_level` and `game.version.hook` for version-specific targets. Support policy in the Standard section 5. |
| P0-18 | `gen-api-reference.js` | The generated reference marks each function that the smoke or native tests call as Tested, with a total. |

## Phase 1: Core simulation kits

Goal: an author can read and change the everyday state of the colony without `game.reflect`. The kit shape is in [kits and domains](api/domains.md).

| ID | Kit | Missing | Size |
|----|-----|---------|------|
| P1-01 | `game.things` | Comps (read and set), quality, stuff, minified items, styles, hit points and damage, forbidden, ownership, rotation, `ThingMaker` and spawn with options, destroy modes, thing filters | M |
| P1-02 | `game.maps` | Cells (terrain get and set, roofs, fog, temperature, light, filth, snow), rooms and room roles, regions and reachability, zones and areas, designations, lords, map components, edges and drop spots | L |
| P1-03 | `game.time` | Date, season, quadrum, hour of day, speed get and set, pause, ticks per second, `GenDate` helpers, day and night | S |
| P1-04 | `game.factions` | Goodwill, relations, leaders, faction definitions, temporary factions, diplomacy actions, hostile and ally lists | M |
| P1-05 | `game.pawns` (health) | Injuries and body parts as tables (`def`, `severity`, `bleeding`, `part`, `tendable`), applying damage, healing, immunity, prosthetics, surgery details, hediff comps | M |
| P1-06 | Job control | Start any vanilla job with targets (tend, rescue, haul, repair, clean), job queue, interrupt, forced and drafted jobs, reservations | M |
| P1-07 | `game.pawns` (gear and policy) | Equipment, apparel and inventory with full control, outfits, drug and food policies, work settings, bed and room ownership, timetable edits for groups | M [pawns](api/pawns.md) |
| P1-08 | `game.pawns` (social and animals) | Social interactions, opinions with reasons, marriage and romance, animals (training, master, bonds, wildness), prisoners and slaves (guest status, recruitment, interaction modes) | L [pawns](api/pawns.md) |
| P1-09 | `game.pawns` (mind) | Mental states with parameters, inspirations, break thresholds, drug effects, needs by group | M [pawns](api/pawns.md) |
| P1-10 | Selection and inspection | Selected things and zones, inspected objects, camera control and jump | S |
| P1-11 | Spawning and generation | Pawn generation requests, pawn kinds, thing sets, stuff and quality rolls | M [things](api/things.md), [pawns](api/pawns.md) |
| P1-12 | Typed collections | Efficient queries over many objects (all pawns of a faction, things of a def in an area) with filters, so mods do not loop in Lua | M |

Done when: the smart doctor, shift scheduler, zone painter, loadouts and bulk work priorities in [mod ideas](mod-ideas.md) can be built using only kit functions.

### Phase 1 status

Every kit is built from `src/api/kits/*.kit` (one source for the native binding, the editor stubs, the reference and the editor completions) and has a host op, a native test, stubs, docs and in-game smoke tests.

| ID | Kit | Functions |
|----|-----|-----------|
| P1-01 | `game.things` | info, quality, stuff, forbidden, rotation, comps, damage, heal, make, spawn_at, destroy_with, minify, inner, style, owners, assign_owner, storage filters and priority. [things](api/things.md) |
| P1-02 | `game.maps` | Cells, terrain, roofs, fog, light, temperature, snow, filth, rooms, zones, areas, designations, lords, reachability. [maps](api/maps.md) |
| P1-03 | `game.time` | now, speed, paused, date_text, constants. [time](api/time.md) |
| P1-04 | `game.factions` | info, goodwill, relations, leader, members, make_temporary, remove, make_peace, declare_war, send_gift. [factions](api/factions.md) |
| P1-05 | `game.pawns` health | Hediffs, injuries, body parts, damage, healing, prosthetics, immunity, surgeries. [pawns](api/pawns.md) |
| P1-06 | `game.jobs` | give, queue, interrupt, reservations, haul, tend, rescue, repair, clean, wait, go_to. [jobs](api/jobs.md) |
| P1-07 | `game.pawns` gear and policy | gear, equip, wear, inventory, outfits, drug and food policies, work priorities, beds, group timetable. [pawns](api/pawns.md) |
| P1-08 | `game.pawns` social and animals | interact, opinion reasons, romance, animals, training, taming, prisoners, slaves, recruiting. [pawns](api/pawns.md) |
| P1-09 | `game.pawns` mind | Mental states, break thresholds, inspirations, drugs. [pawns](api/pawns.md) |
| P1-10 | `game.selection`, `game.camera` | Selection, inspection, camera jump and position. [selection](api/selection.md) |
| P1-11 | Spawning and generation | `game.pawns.generate`, `kinds`, `game.things.thing_set`, stuff and quality rolls. [things](api/things.md), [pawns](api/pawns.md) |
| P1-12 | `game.query` | things, pawns, count_by_def, radius, nearest with filters. [queries](api/query.md) |

## Phase 2: Systems kits

Goal: whole game systems are scriptable.

| ID | Kit | Missing | Size |
|----|-----|---------|------|
| P2-01 | `game.ai` | Work givers (enable, disable, priority, custom scan), job givers, think trees (insert, remove, replace nodes), duties, lords and lord jobs, toils, pathing costs, danger levels | L |
| P2-02 | `game.incidents`, `game.storyteller`, `game.quests` | Incident parameters, firing with control, storyteller state (threat points, cycles, difficulty, wealth), quest generation, accept, objectives, rewards, signals, letters with choices | L |
| P2-03 | `game.research` | Projects, progress, finishing, prerequisites, tabs, techprint and anomaly knowledge | S |
| P2-04 | `game.world` | World map, tiles, biomes, settlements, world objects, caravans, travel, quest sites, world pawns, world layers, map transitions | L |
| P2-05 | `game.combat` | Verbs and projectiles, damage definitions, melee, ranged, turrets, armor, stat reads, combat events, attack control, explosions, fire | M |
| P2-06 | `game.buildings` | Construction (blueprints, frames), power nets, batteries, bills and recipes, storage settings, doors, traps, production, rooms, repair | L |
| P2-07 | `game.economy` and raids | Trade (prices, stock, shops), caravans, silver, market value, orbital traders, gifts, raid strategies, arrival modes, faction generation | L |
| P2-08 | `game.generation` | Map gen steps, biome and terrain data, world gen steps, features, rivers, roads, ruins and sites, base gen | L |
| P2-09 | `game.scenarios` | Scenario parts, start conditions, setup screens, game rules and difficulty | M |
| P2-10 | `game.plants` and agriculture | Growth, sowing, harvest, plant defs, zones, soil, fertility | M |
| P2-11 | `game.weather`, `game.conditions` | Game conditions, sky, wind, seasons beyond get and set, temperature offsets | M |
| P2-12 | Persistence | Save custom objects from Lua (a `Scribe` bridge), per-map and per-world data, save and load events carrying data, autosave control, save versioning | M |
| P2-13 | More events | See the event list below. Ongoing | M, ongoing |
| P2-14 | `game.stats` reads | Read any `StatDef` value for a thing with the explanation breakdown | S |

Done when: a custom storyteller, a trade assistant, a farm planner and a smarter hauling AI can be written in Lua without reflection.

### Phase 2 status

Every kit is built from `src/api/kits/*.kit`, has host ops, stubs, docs and in-game smoke tests. The in-game suite passes 363 of 363.

| ID | Kit | Functions |
|----|-----|-----------|
| P2-01 | `game.ai` | Work givers and their priority, think trees (list, insert, remove), duties, lords, path costs, danger. [ai](api/ai.md) |
| P2-02 | `game.storyteller`, `game.incidents`, `game.quests` | Storyteller, difficulty, threat points, incidents with options, queued incidents, quest generate, accept, finish, signals. [storyteller](api/storyteller.md) |
| P2-03 | `game.research` | Projects, progress, finish, current, techprints, anomaly knowledge. [research](api/research.md) |
| P2-04 | `game.world` | Tiles, settlements, objects, caravans, travel, world pawns, maps, tile search. [world](api/world.md) |
| P2-05 | `game.combat` | Verbs, attack, armor, explosions, fire, projectiles, turrets, damage and projectile defs. [combat](api/combat.md) |
| P2-06 | `game.build`, `game.power`, `game.bills`, `game.doors`, `game.traps` | Blueprints, frames, instant build, power nets, batteries, bills, recipes, doors, traps. [build](api/build.md) |
| P2-07 | `game.economy`, `game.raids` | Silver, wealth, prices, traders, ships, stock, call trader, raid strategies and arrival modes, raids. [economy](api/economy.md) |
| P2-08 | `game.generation` | Terrains, map and world steps, features, rivers, roads, base generation. [generation](api/generation.md) |
| P2-09 | `game.scenarios` | Scenarios, parts, rules, difficulty (in the storyteller kit). [generation](api/generation.md) |
| P2-10 | `game.plants` | Growth, sowing, harvest, fertility, growing zones. [plants](api/plants.md) |
| P2-11 | `game.conditions`, `game.weather` | Conditions, sky, season, weather defs, temperature offsets. [conditions](api/conditions.md) |
| P2-12 | `game.save`, `game.json` | Per game, map and world data with JSON values, data versions, autosave, manual saves. [save](api/save.md) |
| P2-13 | Events | 49 new events, 77 in the catalog. [events](api/events.md) |
| P2-14 | `game.stats` | Value and explanation of any stat. [research](api/research.md) |

## Phase 3: Presentation

Goal: mods can have their own UI and visuals. The UI rules in the Standard come first.

| ID | Area | Missing | Size |
|----|------|---------|------|
| P3-01 | Gizmos | Buttons on pawns, things and the selection, toggles, sliders, command groups, hotkeys on gizmos | M |
| P3-02 | Windows and widgets | A real widget set: lists, tables, text fields, sliders, tabs, scroll, tooltips, confirm dialogs, drag and drop, layout helpers, beyond the simple window | L |
| P3-03 | Tabs | Main tabs, inspect tabs, pawn table columns, the architect menu entries, bill and recipe UI | L |
| P3-04 | Designators and tools | Click-and-drag tools, designations, ghost previews, area painting | M |
| P3-05 | Graphics | Texture loading from the mod folder, graphics for things and pawns, colour and mask variants, apparel and hair layers, body types, render nodes (the 1.6 pawn render tree), animation | L |
| P3-06 | Shaders and materials | Materials from Lua, shader parameters, custom shaders as asset bundles | L |
| P3-07 | Effects | `Fleck`s, `Mote`s, effecters, particles, screen shake, overlays, map highlighting, cell and line drawing | L |
| P3-08 | Input | Create key bindings at runtime, chorded keys, mouse input, text input | S |
| P3-09 | Audio | Play and define sounds, music, sustainers, volume, sound environments | S |
| P3-10 | HUD, alerts and themes | Status lines, alerts, letters with custom stacks, fonts, colour presets, mod options pages | M |

Done when: the gizmo packs, overlays, custom tabs and apparel packs in [mod ideas](mod-ideas.md) are buildable.

### Phase 3 status

Built, and the in-game smoke suite (363 of 363) covers registration, state, errors and that views, callbacks and the gizmo bar run. Gizmos, tabs, designators, alerts and the settings page draw through the game's own UI classes, so how they look still needs a human check.

| ID | Kit | Functions |
|----|-----|-----------|
| P3-01 | `game.gizmos` | Action buttons, toggles, sliders, hotkeys, per-thing visibility, merged for multiple selection. [gizmos](api/gizmos.md) |
| P3-02 | `game.widgets` | Windows from widget trees: labels, buttons, checkboxes, sliders, text, dropdowns, lists with drag reorder, tables, tabs, scroll, progress, images, tooltips, confirm and prompt dialogs. [widgets](api/widgets.md) |
| P3-03 | `game.tabs` | Main tabs, inspect tabs, pawn table columns. Architect entries come from designators. [tabs](api/tabs.md) |
| P3-04 | `game.designators`, `game.areas` | Click and drag tools, ghost highlight, architect categories, allowed areas. [designators](api/designators.md) |
| P3-05 | `game.graphics` | Texture info, def graphics, colours, pawn body, hair, head and skin, race render nodes, animations. [graphics](api/graphics.md) |
| P3-06 | `game.materials` | Materials from shaders, properties, textures, shaders from asset bundles. [graphics](api/graphics.md) |
| P3-07 | `game.effects` | Flecks, effecters, floating text, screen shake, highlighted cells, lines, circles, marks. [effects](api/effects.md) |
| P3-08 | `game.input` | Runtime key bindings, key and chord queries, modifiers, mouse. [input](api/input.md) |
| P3-09 | `game.audio` | Define sounds from clips, play at a cell, songs, volumes, sustainers. [audio](api/audio.md) |
| P3-10 | `game.alerts`, `game.hud`, `game.options` | Alerts with culprits, status lines, letters with choices, colour presets, fonts, settings pages. [hud](api/hud.md) |

## Phase 4: Extensibility

Goal: mods that add new kinds of things, not only tune existing ones. This is the largest unlock, because most workshop content mods are XML plus a C# class.

| ID | Item | What it is | Size |
|----|------|------------|------|
| P4-01 | Stats API | `game.stats.modify("ShootingAccuracyPawn", fn)` over the game's stat calculation, covering hundreds of tweaks with one hook, plus stat parts and offsets | M |
| P4-02 | Tweak catalog | Named tweak points declared as data (like `src/api/aliases.json`) with generated docs and stubs. Super Accuracy becomes `game.tweaks.on("shot.distance_accuracy", fn)` | M |
| P4-03 | Def mutation | `game.defs.set("ThingDef", "Steel", "stackLimit", 1000)` safely at runtime, with a restore and a record of what changed | M |
| P4-04 | Def authoring | Generate valid Def XML (things, recipes, research, hediffs, thoughts, abilities) from Lua tables, validated against the game's loader | M |
| P4-05 | XML patch operations | Apply and generate patch operations from Lua, conditional on other mods | S |
| P4-06 | Lua-backed game classes | C# proxy classes that call into Lua. See the class table below | L |
| P4-07 | Custom needs, thoughts, abilities | Need classes, thought workers, mental state workers, abilities with verbs and comps, defined in Lua | M |
| P4-08 | Custom saving | Per-object and per-component data saved with the object, with schema versions | M |
| P4-09 | Def inheritance and DefOf | `ParentName`, abstract defs, `DefOf` access from Lua, resolving defs from other mods | S |
| P4-10 | Custom Def types | Define a new Def class from XML plus Lua so mods can add their own data tables | M |
| P4-11 | Game-class replacement | Swap a Def's worker or class for a Lua-backed one at load time | M |
| P4-12 | Harmony for other mods | Hook methods in other mods' assemblies by name, with a "mod present" guard | M |

Done when: a mod can add a new building with a custom component, a new incident, a new work type with its own work giver and a new need, entirely from Lua plus XML.

### Game class families to back with Lua (P4-06)

Each family is a base class a mod normally subclasses in C#. RimKit supplies one generic proxy per family whose methods call named Lua functions. Order is by how many workshop mods need it.

| Family | Typical use | Priority |
|--------|-------------|----------|
| `ThingComp` and `CompProperties` | Behavior on buildings, items, pawns | 1 |
| `JobDriver` with toils, `WorkGiver_Scanner`, `JobGiver` | New work, new AI | 1 |
| `HediffComp`, `Hediff` | Diseases, implants, effects | 1 |
| `IncidentWorker`, `GameCondition`, `StorytellerComp` | Events, conditions, storytellers | 1 |
| `StatPart`, `StatWorker` | Stat rules | 1 |
| `Designator`, `ITab`, `Gizmo`, `Command` | UI actions | 2 |
| `PlaceWorker`, `RecipeWorker` | Placement rules, recipe effects | 2 |
| `Verb`, `Projectile`, `DamageWorker` | Weapons and damage | 2 |
| `ThoughtWorker`, `MentalStateWorker`, `Need` | Mood and mind | 2 |
| `Alert`, `MainButtonWorker`, `Dialog` | HUD | 2 |
| `Ability` comps and `CompAbilityEffect` | Powers | 2 |
| `QuestPart`, `QuestNode`, `LordJob`, `LordToil` | Quests and groups | 3 |
| `GenStep`, `WorldGenStep`, `ScenPart`, `ThingSetMaker` | Generation and scenarios | 3 |
| `RitualOutcomeEffectWorker` and ritual behaviors | Ideology rituals | 3 |
| `GeneDef` effect hooks | Genes | 3 |
| `ThinkNode` | AI trees | 3 |

Design rules: the proxy must be generated, not hand written per class, so adding a family is a table row; every callback is guarded by the Lua error budget (P0-15); and saved fields go through the custom saving system (P4-08).

### Phase 4 status

Built. The in-game smoke suite passed 406 of 406 for the first pass, and covers Lua classes loaded from XML, class replacement, stat modifiers, def changes, authoring and patches, mod queries, tweak targets and per-thing data.

| ID | Kit | Functions |
|----|-----|-----------|
| P4-01 | `game.stats.modify`, `add_offset`, `add_factor`, `remove_modifier`, `modifiers` | Stat modifiers over the game's own stat calculation. [research](api/research.md) |
| P4-02 | `game.tweaks` | 13 named tweak points as data, with generated docs and stubs. [tweaks](api/tweaks.md) |
| P4-03 | `game.defs.set`, `value`, `fields`, `restore`, `changes` | Runtime def changes with a journal. [defs](api/defs.md) |
| P4-04 | `game.defs.to_xml`, `validate_xml`, `write_xml` | Def XML from tables, validated against the game's types. [defs](api/defs.md) |
| P4-05 | `game.patch` | Build and write patch operations, conditional on mods. [patch](api/patch.md) |
| P4-06 | `game.classes` | 31 generated proxy families from `src/api/classes.cls`, including quest nodes (with `game.quests.slate_get` and `slate_set`), world gen steps, scenario parts, ritual outcomes, conditional think nodes, and whole-thing building, door and storage classes. [classes](api/classes.md) |
| P4-07 | Needs, thoughts, abilities | Through the `need`, `thought_worker`, `mental_state_worker` and `ability` families. A need needs no XML: `game.needs.define` creates the def when the mod loads. [classes](api/classes.md) |
| P4-08 | `game.save` scope `thing`, Lua comp data | Saved with the object. [save](api/save.md) |
| P4-09 | `game.defs.of`, `of_mod`, `kinds` | Which mod defines a def, def types and fields |
| P4-10 | `game.defs.data_rows` | Custom data defs as `RimKit.LuaDataDef` |
| P4-11 | `game.classes.replace`, `replaceable` | Swap a def's class for a Lua proxy at runtime |
| P4-12 | `game.hooks.in_mod`, `game.mods` | Hook other mods only when present |

Gaps left: none that is known. Needs can now be defined from Lua with `game.needs.define`, and buildings, doors and storage buildings have their own Lua classes. The quest node, world gen step, scenario part, ritual outcome and conditional think node families were added after the first Phase 4 pass and are covered by smoke tests that have not been run in the game yet.

## Phase 5: DLC kits

Goal: the DLCs get the same treatment as the base game.

| ID | DLC | Missing | Size |
|----|-----|---------|------|
| P5-01 | Ideology | Memes, precepts, rituals, roles, styles, ideoligion editing, ideo events | L |
| P5-02 | Royalty | Titles, permits, psycasts, honor, favor, the empire, throne rooms | M |
| P5-03 | Biotech | Beyond genes: xenotypes, mechanitors and mechs, pregnancy and children, the growth system, sanguophages, gene packs | L |
| P5-04 | Anomaly | Depth beyond entities: containment, the monolith and void, studies, rituals, creepjoiners, engagement behavior exposed to Lua | M |
| P5-05 | Odyssey | Gravships, orbital sites, travel, new mission types, space maps | M |
| P5-06 | DLC guard | Every DLC function checks the DLC is active and returns `RK3003` otherwise, with one shared helper and a test | S |

### Phase 5 status

Built and checked: registration, the DLC guard (`RK3003` when inactive) and the functions of every DLC active in the test game. Odyssey is excluded from the smoke game because it hangs the quick test, so its functions are only checked for the guard.

| ID | Kit | Functions |
|----|-----|-----------|
| P5-01 | `game.ideology` | Ideoligions, memes (add and remove through the game's own conflict checks), precepts, roles, certainty, rituals, style categories, renaming. [dlc](api/dlc.md) |
| P5-02 | `game.royalty` | Titles, honor, permits, psylink, abilities, the Empire, throne rooms. [dlc](api/dlc.md) |
| P5-03 | `game.biotech` | Xenotypes, gene defs, mechanitors, pregnancy, growth, hemogen, gene packs. [dlc](api/dlc.md) |
| P5-04 | `game.anomaly` | Monolith, studies, containment, platforms, codex, creepjoiners, engagement settings. [dlc](api/dlc.md) |
| P5-05 | `game.odyssey` | Planet layers, gravship engines with fuel, range, cooldown, substructure and components, space maps, gravship in flight. Launching, travel and orbital sites are not exposed. [dlc](api/dlc.md) |
| P5-06 | `game.dlc` | One guard helper for every DLC function. [dlc](api/dlc.md) |

## Phase 6: Ecosystem

Goal: a platform other people build on.

| ID | Item | Why | Size |
|----|------|-----|------|
| P6-01 | Mod interoperability | See other mods, their Defs and Lua APIs; publish functions to other mods with versions; detect load order | M |
| P6-02 | Hot reload | Edit Lua and see it without a restart | M |
| P6-03 | In-game tools | A console, a live reflection browser, a hook inspector, an event recorder, a debug action registry | M |
| P6-04 | Profiler | Per-mod time in hooks, events and ticks | M |
| P6-05 | Capability model | Per-mod declared permissions (reflection, hooks, file export) shown to the player and enforced | M |
| P6-06 | Editor extension | Hover docs, def-name completion from the game, go to definition for the API, a test runner, project templates | M |
| P6-07 | Docs site | Search-first site, tutorials per phase, a cookbook of recipes (one per row in the mod ideas list) | M |
| P6-08 | Test harness for authors | Run a mod's scripted scenarios headless against a mock host, and in game against a scenario save | L |
| P6-09 | Sample mod pack | Ten maintained example mods covering each category in the table above | M |
| P6-10 | Telemetry-free diagnostics | A one-click bundle (log, mod list, versions, errors) for bug reports, with no network use | S |

### Phase 6 status

Built and checked: the in-game smoke suite (421 of 421 at its last run in the game; the checks added since for quest nodes, Ideology styles and the newer class families have not been run in the game yet), native tests (the framework, mocks, interop and a capability check), 37 host unit tests, the editor and docs checks, and the CLI commands run by hand against a scratch mod. The in-game parts (the dev tools window, the capabilities hub tab, hot reload, the mod failure letter) are covered by the smoke suite except the dev tools window, the hub tab and the mod failure letter, which need a human look.

| ID | Kit or tool | What was added |
|----|-------------|----------------|
| P6-01 | `game.interop`, `game.mods.order` | Publish functions with semantic versions, ask with `^`, `~`, `>=` requirements, `when` for any load order, safe `call`. [interop](api/interop.md) |
| P6-02 | `game.dev.watch`, `game.dev.reload` | Reload a mod's Lua, per mod: events, hooks, timers, tick and widget callbacks are replaced. Gizmos, alerts, tabs, columns, designators, status lines and settings pages the old version registered are removed first, and the new version adds its own. [dev](api/dev.md) |
| P6-03 | Dev tools window (F11), `game.dev` | Console, hook cost, event recorder, profiler, debug actions, mods and permissions. A Reflect tab browses any game object: pick a root, open members that hold objects, read values live (needs the `developer_reflect` setting). [performance](guide/performance.md) |
| P6-04 | `game.profiler`, `perf_budget_us` | Time per mod for ticks, events, hooks, timers, UI and load, a budget with a warning and the `mod.over_budget` event. [performance](guide/performance.md) |
| P6-05 | `meta.capabilities` | `reflect`, `hooks`, `files`, `dev`: declared in `meta.lua`, shown in the hub, enforced with `RK4001`, checked by `rimkit mod check`. Mods that do not declare keep full access for now. [capabilities](guide/capabilities.md) |
| P6-06 | Editor extension | Def name completion from the game, go to definition for API names, test, check, release check, assets and diagnostics commands, a new mod wizard with five templates. |
| P6-07 | Docs | Search plugin, seven tutorials, a cookbook with one recipe for every mod idea (a check keeps it that way), pages for testing, publishing, capabilities, save safety and performance. [tutorials](guide/tutorials.md), [cookbook](guide/cookbook.md) |
| P6-08 | `rimkit mod test`, `game.test` | Headless test runner with a mock host: mocks, recorded calls, events, ticks, captured logs, Lua classes and tweaks. Not built: scripted scenarios against a scenario save. [testing](guide/testing.md) |
| P6-09 | Sample mod pack | Ten example mods in `src/examples` and the two video guide mods, each with headless tests that CI runs. The Defs of three of them follow the smoke test mods but have not been played. [examples](../../src/examples/README.md) |
| P6-10 | `rimkit diag`, `game.dev.bundle` | Zip of the log, mod list and profiler numbers, no network. [performance](guide/performance.md) |

## Workshop readiness

What a mod needs around the code so strangers can find, install, trust and keep it.

| ID | Item | Why | Size |
|----|------|-----|------|
| W-01 | `rimkit publish` | Upload to the Steam Workshop from the CLI: package, preview image, title, tags, description, change note | M |
| W-02 | Preview and icon tooling | The workshop needs a preview image and the game a mod icon. Validate sizes and generate placeholders | S |
| W-03 | Multi-version folders | `LoadFolders.xml` and `About` `supportedVersions` for several game versions, with per-version Lua and Defs | M |
| W-04 | Dependency and conflict metadata | `modDependencies`, `loadBefore`, `loadAfter`, `incompatibleWith`, checked by `rimkit mod check` | S |
| W-05 | Versioning and changelog | Semantic mod versions, a changelog file, a version shown in the mod list and in save data | S |
| W-06 | Mod options page | A proper settings page (sections, sliders, dropdowns, reset) instead of single entries in a list | M |
| W-07 | Save safety | Uninstall and update safety: what happens to a save when the mod is removed, a def is renamed, or a field changes. A migration helper and a test for each | L |
| W-08 | Compatibility guards | Helpers to patch only when another mod is present, resolve its types, and survive its absence | M |
| W-09 | Performance budget | A documented budget (microseconds per tick per mod), a profiler view and a warning when exceeded | M |
| W-10 | Error reporting to players | A friendly in-game notice when a mod's Lua fails, naming the mod and offering to disable it, not only a log line | S |
| W-11 | Content validation | `rimkit mod check` validates Defs, patches, textures, keys and strings: missing translations, unused keys, bad references | M |
| W-12 | Localization tooling | Extract Keyed strings, find missing keys per language, import and export translation files | S |
| W-13 | Release checklist and templates | A workshop description template, licence and credit file, a pre-publish checklist in the CLI | S |
| W-14 | Legal and attribution | Guidance and checks for assets, licences and Ludeon's modding terms | S |

### Workshop readiness status

The CLI commands below were run by hand against a scratch mod. `rimkit publish` writes the item file and builds the SteamCMD command, but has not been run against Steam.

| ID | Kit or tool | What was added |
|----|-------------|----------------|
| W-01 | `rimkit publish` | Release check, staging folder, item file with title, description, change note, tags and preview, SteamCMD run, saves the new id. Untested against Steam, try `--visibility 2` first. [publishing](guide/publishing.md) |
| W-02 | `rimkit mod assets` | Validates the preview (PNG, 640x360 or more, 16:9, under 1 MB) and the icon, makes placeholders with `--fix`. |
| W-03 | `meta.version_folders` | Writes `LoadFolders.xml` and the supported versions, RimKit loads a `Lua/` folder from every listed folder. Not yet tried in game. [meta.lua](guide/meta.md) |
| W-04 | `meta.load_before`, `incompatible_with` | Written to `About.xml` next to `depends` and `load_after`. |
| W-05 | `meta.mod_version`, `game.mods.version`, `game.save.stamp` | Semantic version in the mod list, a changelog check, the version recorded in the save. |
| W-06 | `game.options` | Settings pages with sections, sliders, dropdowns (P3-10). |
| W-07 | `game.save.migrate`, `stamp`, `purge` | Ordered migration steps that retry after a failure, version stamp, a clean-up for uninstalling. [save safety](guide/save-safety.md) |
| W-08 | `game.hooks.in_mod`, `game.mods`, `game.interop` | Patch only when another mod is present (P4-12). |
| W-09 | Performance budget | Documented budget, profiler and warning. [performance](guide/performance.md) |
| W-10 | `mod.disabled` letter | A letter that names the failing mod and offers to turn it off for good. Not yet seen in game. |
| W-11 | `rimkit mod check` | Defs and patches well formed, duplicate defNames, mod textures, translation keys, declared capabilities. |
| W-12 | `rimkit mod i18n` | extract, missing, export and import (csv). |
| W-13 | `rimkit mod release-check`, templates | Checklist plus LICENSE, CREDITS, CHANGELOG, Workshop.md and a test in every new mod. |
| W-14 | Legal and attribution | Checks that a licence exists and credits exist for shipped art and sound, and a reminder of Ludeon's rules. It cannot tell who made an asset, that stays with the author. |

## The 1.0 gate

| ID | Item | Done when |
|----|------|-----------|
| G-01 | Freeze the API | Promote ready Experimental kits to Stable, set `api_level = 1`, freeze the names, remove deprecated aliases, publish the migration guide. See [stability](api/stability.md) |

## Events still missing

The catalog has 87 events. Most of the groups below are covered by an event that already exists (for example `thing.damaged` for injuries, `job.ended` for interrupted jobs, `quest.ended` for failed and expired quests). These are the ones still without one. Each is one catalog row plus a patch (P2-13).

| Group | Events |
|-------|--------|
| Pawn lifecycle | Left colony, escaped |
| Work and jobs | Work completed, item crafted, mining, construction started, deconstructed, repaired, hauled, dropped |
| Health | Healed, immunity gained |
| Mind and social | Thought lost, social fight |
| Skills | Passion changed, learning saturated |
| Combat | Fire started and out |
| World and map | World generated, caravan arrived, site visited |
| Economy | Goods delivered, silver changed, price changed |
| Research | Progress milestones |
| Buildings | Room changed |
| Factions | Relation changed, leader changed |
| UI | Gizmo clicked, key pressed |

## Def type coverage

Every Def type can already be shipped as XML, because RimKit does not touch XML loading. What differs is behavior: where a Def needs a C# class, a Lua mod needs the Lua-backed class (P4-06) or a kit.

| Def type | XML now | Lua behavior needs |
|----------|---------|--------------------|
| `ThingDef` (items, buildings, plants, animals) | Yes | Comps, building and place classes (P4-06) |
| `RecipeDef`, `ResearchProjectDef`, `ResearchTabDef` | Yes | Recipe workers (P4-06) |
| `HediffDef`, `ThoughtDef`, `TraitDef`, `NeedDef`, `SkillDef` | Yes | Comps and workers (P4-06, P4-07) |
| `StatDef`, `StatCategoryDef` | Yes | Stat workers and parts (P4-01, P4-06) |
| `JobDef`, `WorkGiverDef`, `WorkTypeDef`, `ThinkTreeDef`, `DutyDef` | Yes | Drivers, givers, nodes (P2-01, P4-06) |
| `IncidentDef`, `QuestScriptDef`, `StorytellerDef`, `GameConditionDef` | Yes | Workers, comps, quest parts (P2-02, P4-06) |
| `FactionDef`, `PawnKindDef`, `RaidStrategyDef` | Yes | Raid and faction logic (P2-07) |
| `BiomeDef`, `TerrainDef`, `GenStepDef`, `WorldGenStepDef` | Yes | Gen steps (P2-08, P4-06) |
| `ScenarioDef`, `ScenPartDef` | Yes | Scenario parts (P2-09, P4-06) |
| `AbilityDef`, `GeneDef`, `XenotypeDef` | Yes | Ability comps, gene effects (P4-06, P5-03) |
| `MemeDef`, `PreceptDef`, `RitualBehaviorDef` | Yes | Ritual workers (P5-01, P4-06) |
| `SoundDef`, `MusicDef` | Yes | Runtime play (P3-09) |
| `KeyBindingDef`, `MainButtonDef`, `DesignationCategoryDef` | Yes | Button workers and tabs (P3-03) |
| `ThingCategoryDef`, `BodyDef`, `BodyPartDef`, `DamageDef` | Yes | Damage workers (P4-06) |
| Translations (`Keyed`, `DefInjected`) | Yes | Tooling (W-12) |

## Known debt (small, fix soon)

| Debt | Effect |
|------|--------|
| The recruited-pawn engagement behavior is hard coded | Cannot tune or disable auto-attack from Lua |
| `Pawn.TickInterval` is a second tick path in 1.6 | Confirmed that `Pawn.Tick` still runs for pawns. Watch it after game updates (P0-14) |
| Entity detection is a heuristic | Look-alike mods can be misdetected |
| The smoke test runner needs the game and a Steam relaunch workaround | Cannot run on a build server (P6-08) |
| `mod/Auth/allowlist.json` is regenerated by hand after a rebuild | A forgotten regeneration blocks Lua (P0-13) |
| F6 and F7 clash with vanilla keys | Tame Anomalies defaults overlap Research and Quests |
| `rim.reflect` and `game.reflect` coexist with different shapes | Two ways to do one thing until 1.0 |
| Hook context is built from JSON for every call | Hot hooks cost more than needed (P0-07) |
| Hooks run on whichever thread runs the method | Heavy hooks on worker threads serialize behind one lock |

## Out of scope

- Letting Lua load native code, open files freely, run processes or use the network without a named, reviewed API. See [security](guide/security.md).
- A full mirror of every game type as hand-written kits. Kits cover what mods use. Everything else stays reachable through reflection and hooks.
- Multiplayer synchronization. The API targets single-player.
- Replacing RimWorld's engine: pathfinding rewrites, a new renderer, tick-rate overhauls that need engine-level changes. Those stay C# mods.
- Total conversion mods that replace most of the game. They are possible in pieces, but not a goal.
- Performance mods that need to run on every thing every tick. Lua is for logic, not for the hot loop.

## How we choose the next item

1. Phase order, unless something blocks a release.
2. Within a phase, the item that unlocks the most rows in the [Workshop categories](#workshop-categories) table and in [mod ideas](mod-ideas.md).
3. Anything that makes a regression possible (Phase 0) jumps the queue.
4. A request that many mods need becomes a kit function. See [request a feature](guide/request-a-feature.md).
5. Each finished row updates this page, the [kit status board](api/domains.md) and the generated [reference](api/reference.md).
