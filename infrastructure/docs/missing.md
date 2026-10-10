# Roadmap and status

What RimKit can do, what is built and what is still open, in build order. The goal: a Lua author can build and publish the kind of mod that sits at the top of the Steam Workshop, not only small tweaks.

- Every row has an ID. `P1-04` is Phase 1, row 4; `W-07` is Workshop readiness, row 7. A mod idea, an issue or a commit can point at it.
- **Built** means it exists and passed the headless tests and the lints. Some of it has not been played in the game yet, and [gaps and next steps](gaps.md) says which.
- **Partly** means it works but a named part is missing. **Open** means not started.
- Related pages: the [kit status board](api/domains.md), [mod ideas](mod-ideas.md) and [what to do next](gaps.md).

## At a glance

| Phase | Goal | Built | Partly | Open |
|-------|------|------:|-------:|-----:|
| [Phase 0](#phase-0-foundations) | Foundations | 18 |  |  |
| [Phase 1](#phase-1-core-simulation-kits) | Core simulation kits | 12 |  |  |
| [Phase 2](#phase-2-systems-kits) | Systems kits | 14 |  |  |
| [Phase 3](#phase-3-presentation) | Presentation | 10 |  |  |
| [Phase 4](#phase-4-extensibility) | Extensibility | 12 |  |  |
| [Phase 5](#phase-5-dlc-kits) | DLC kits | 5 | 1 |  |
| [Phase 6](#phase-6-ecosystem) | Ecosystem | 13 |  |  |
| [Workshop readiness](#workshop-readiness) | Workshop readiness | 14 |  |  |
| **1.0 gate** | Freeze the API | | | 1 |
| **Total** | | **98** | **1** | **1** |

## Still open

Rows that are partly done or not started. The next-step list with checks is in [gaps and next steps](gaps.md).

| ID | Item | State | What is left |
|----|------|-------|--------------|
| P5-05 | Odyssey | Partly | `game.odyssey`: Planet layers, gravship engines with fuel, range, cooldown, substructure and components, space maps, gravship in flight. Launching, travel and orbital sites are not exposed. [dlc](api/dlc.md) |
| G-01 | Freeze the API | Open | Promote ready Experimental kits to Stable, set `api_level = 1`, freeze the names. See [stability](api/stability.md) |

## What works today

| Area | State |
|------|-------|
| Reacting to the game | 116 named events with payloads, with optional filters |
| Changing game behavior | Hooks: prefix, postfix, finalizer, call replacement, overloads, results, arguments, state, priority |
| Reaching any game object | Typed reflection, gated and audited |
| Game state kits | Things, maps, time, factions, pawns (data, health, gear, social, mind), jobs, selection and camera, queries, AI, storyteller, incidents, quests, research, stats, world, combat, construction, power, bills, economy, raids, generation, scenarios, plants, conditions, weather, saving |
| Extending the game | Lua-backed game classes (31 families), stat modifiers, tweak catalog, runtime def changes, Def XML authoring, XML patches, mod queries, hooks into other mods, per-thing saved data |
| DLC | Ideology, Royalty, Biotech, Anomaly (monolith, studies, containment, codex, engagement) and Odyssey (minimal) kits behind one DLC guard |
| Presentation | Widget windows, gizmos, main, inspect and column tabs, designators, areas, graphics, materials, effects, input, audio, alerts, HUD, letters with choices, settings pages |
| Content | Any XML Def, XML patch, texture, sound and translation ships next to the Lua and is loaded by the game as normal |
| Mod plumbing | Settings pages, per game, map and world saved data, runtime key bindings, Lua jobs, movement, JSON, mod interop, capabilities, hot reload, profiler, strict mode (`meta.api_level = 1`) |
| Tooling | CLI (create, sync, ship, check, test, test --in-game, conform, assets, i18n, release-check, publish, diag), editor extension (completions, def names, go to definition, wizard), kit table generator, op lint, release tool, CI workflow, in-game smoke suite, in-game test runner, host unit tests |
| Rules | Naming convention, stability tiers, error codes, the RimKit Standard (draft 2: events, hot reload contract, mod conformance levels, compatibility promise) |

## What kind of mod can be built

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

## Phases

### Phase 0: Foundations

**Goal.** Make growth safe. Nothing here adds a feature, all of it prevents a regression.

All 18 items are built. Checks that run without the game: native tests (`aliastest`, `budget_test`, `kits_test`, `hookbench`), 45 host unit tests (including every catalog event checked against the game assemblies), the reload and in-game runner fixtures, and the lint, docs, version, kit and editor checks. Checks that need the game run in the smoke suite.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P0-01 | C# unit tests | Built | `tests/host`: `tests/host` (xunit, net48): JSON reader, scanner, `Opts`, `Jb`, `JsonLite`, handle table, and the event catalog against the installed game assemblies. 45 tests. Registry and hook bridge need live game types and are covered by the smoke suite. |
| P0-02 | CI | Built | CI workflow: `.github/workflows/ci.yml`: lint, kit table, editor, docs, version and hook-target checks, extension packaging, native build and tests, and a self-hosted job for the host build, unit tests and release check. It runs when pushed. |
| P0-03 | One version source | Built | `src/api/VERSION`: `src/api/VERSION` and `API_LEVEL` feed native, host, extension and docs. |
| P0-04 | Op conformance lint | Built | `check-ops.js`: `check-ops.js`: names, tiers, `since`, duplicates, every kit function resolves to a host op with a description and return type, every registered op has a Lua surface. |
| P0-05 | Typed handles everywhere | Built | Typed handles: Kit functions return wrapped objects (`RimPawn`, `RimThing`, `RimMap`, `RimFaction`) from their declared return type, and everything accepts wrapped objects or handles. Older functions keep integer results until api level 1, see the Standard section 5. |
| P0-06 | Handle lifetime | Built | Handle table: Weak handles for things, maps and factions, pinned handles for the rest, generation counter, `RK2001` on stale use. |
| P0-07 | Cheaper hooks | Built | `game.hooks` fast path: Scalar-only hooks skip JSON: 3.4 us against 7.2 us per call. |
| P0-08 | Hook cost guard | Built | `game.hooks.list`: `game.hooks.list()` reports calls and time, the log warns about hooks over 100 us. |
| P0-09 | One namespace | Built | All code uses the `RimKit` namespace. Persisted types keep the name `RimLuaKit`. |
| P0-10 | Fix key-guessing binders | Built | `bind_op`: `bind_op` takes explicit argument names. |
| P0-11 | One error model | Built | `game.strict_errors`: `game.strict_errors(true)` makes plain handle functions raise `RK####` too. Kit functions always raise. |
| P0-12 | Token-aware scanner | Built | Threat scanner: Token-aware scanner with line numbers. Unit tested, and run in game by the smoke suite. |
| P0-13 | Release process | Built | `release.js`: `release.js` writes and verifies the allowlist and deploys with verification. |
| P0-14 | Game-update watch | Built | `gen-hook-targets.js`: `gen-hook-targets.js` and the smoke suite resolve every shipped hook target and every catalog event against the running game. |
| P0-15 | Crash isolation | Built | Mod error budget: 20 errors in 60 seconds switch a mod off. |
| P0-16 | Safe mode | Built | Safe mode: `-rimkit-safe`, `RIMKIT_SAFE`, `Config/rimkit_safe.txt`. |
| P0-17 | Multi-version support | Built | `game.version`: `game.version.rimworld`, `at_least`, `rimkit`, `api_level` and `game.version.hook` for version-specific targets. Support policy in the Standard section 5. |
| P0-18 | Coverage tracking | Built | `gen-api-reference.js`: The generated reference marks each function that the smoke or native tests call as Tested, with a total. |

### Phase 1: Core simulation kits

**Goal.** An author can read and change the everyday state of the colony without `game.reflect`. The kit shape is in [kits and domains](api/domains.md).

Every kit is built from `src/api/kits/*.kit` (one source for the native binding, the editor stubs, the reference and the editor completions) and has a host op, a native test, stubs, docs and in-game smoke tests.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P1-01 | `game.things` | Built | `game.things`: info, quality, stuff, forbidden, rotation, comps, damage, heal, make, spawn_at, destroy_with, minify, inner, style, owners, assign_owner, storage filters and priority. [things](api/things.md) |
| P1-02 | `game.maps` | Built | `game.maps`: Cells, terrain, roofs, fog, light, temperature, snow, filth, rooms, zones, areas, designations, lords, reachability. [maps](api/maps.md) |
| P1-03 | `game.time` | Built | `game.time`: now, speed, paused, date_text, constants. [time](api/time.md) |
| P1-04 | `game.factions` | Built | `game.factions`: info, goodwill, relations, leader, members, make_temporary, remove, make_peace, declare_war, send_gift. [factions](api/factions.md) |
| P1-05 | `game.pawns` (health) | Built | `game.pawns` health: Hediffs, injuries, body parts, damage, healing, prosthetics, immunity, surgeries. [pawns](api/pawns.md) |
| P1-06 | Job control | Built | `game.jobs`: give, queue, interrupt, reservations, haul, tend, rescue, repair, clean, wait, go_to. [jobs](api/jobs.md) |
| P1-07 | `game.pawns` (gear and policy) | Built | `game.pawns` gear and policy: gear, equip, wear, inventory, outfits, drug and food policies, work priorities, beds, group timetable. [pawns](api/pawns.md) |
| P1-08 | `game.pawns` (social and animals) | Built | `game.pawns` social and animals: interact, opinion reasons, romance, animals, training, taming, prisoners, slaves, recruiting. [pawns](api/pawns.md) |
| P1-09 | `game.pawns` (mind) | Built | `game.pawns` mind: Mental states, break thresholds, inspirations, drugs. [pawns](api/pawns.md) |
| P1-10 | Selection and inspection | Built | `game.selection`, `game.camera`: Selection, inspection, camera jump and position. [selection](api/selection.md) |
| P1-11 | Spawning and generation | Built | Spawning and generation: `game.pawns.generate`, `kinds`, `game.things.thing_set`, stuff and quality rolls. [things](api/things.md), [pawns](api/pawns.md) |
| P1-12 | Typed collections | Built | `game.query`: things, pawns, count_by_def, radius, nearest with filters. [queries](api/query.md) |

Done when: the smart doctor, shift scheduler, zone painter, loadouts and bulk work priorities in [mod ideas](mod-ideas.md) can be built using only kit functions.

### Phase 2: Systems kits

**Goal.** Whole game systems are scriptable.

Every kit is built from `src/api/kits/*.kit`, has host ops, stubs, docs and in-game smoke tests. The in-game smoke suite passes 434 of 434.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P2-01 | `game.ai` | Built | `game.ai`: Work givers and their priority, think trees (list, insert, remove), duties, lords, path costs, danger. [ai](api/ai.md) |
| P2-02 | `game.incidents`, `game.storyteller`, `game.quests` | Built | `game.storyteller`, `game.incidents`, `game.quests`: Storyteller, difficulty, threat points, incidents with options, queued incidents, quest generate, accept, finish, signals. [storyteller](api/storyteller.md) |
| P2-03 | `game.research` | Built | `game.research`: Projects, progress, finish, current, techprints, anomaly knowledge. [research](api/research.md) |
| P2-04 | `game.world` | Built | `game.world`: Tiles, settlements, objects, caravans, travel, world pawns, maps, tile search. [world](api/world.md) |
| P2-05 | `game.combat` | Built | `game.combat`: Verbs, attack, armor, explosions, fire, projectiles, turrets, damage and projectile defs. [combat](api/combat.md) |
| P2-06 | `game.buildings` | Built | `game.build`, `game.power`, `game.bills`, `game.doors`, `game.traps`: Blueprints, frames, instant build, power nets, batteries, bills, recipes, doors, traps. [build](api/build.md) |
| P2-07 | `game.economy` and raids | Built | `game.economy`, `game.raids`: Silver, wealth, prices, traders, ships, stock, call trader, raid strategies and arrival modes, raids. [economy](api/economy.md) |
| P2-08 | `game.generation` | Built | `game.generation`: Terrains, map and world steps, features, rivers, roads, base generation. [generation](api/generation.md) |
| P2-09 | `game.scenarios` | Built | `game.scenarios`: Scenarios, parts, rules, difficulty (in the storyteller kit). [generation](api/generation.md) |
| P2-10 | `game.plants` and agriculture | Built | `game.plants`: Growth, sowing, harvest, fertility, growing zones. [plants](api/plants.md) |
| P2-11 | `game.weather`, `game.conditions` | Built | `game.conditions`, `game.weather`: Conditions, sky, season, weather defs, temperature offsets. [conditions](api/conditions.md) |
| P2-12 | Persistence | Built | `game.save`, `game.json`: Per game, map and world data with JSON values, data versions, autosave, manual saves. [save](api/save.md) |
| P2-13 | More events | Built | Events: 77 in the catalog after the first pass, 116 after the second: work (crafted, mined, construction started, deconstructed, repaired, hauled, dropped, work completed), pawn left colony and escaped, healed, immunity gained, thought lost, social fight, learning saturated, fire started and ended, world generated, caravan arrived, site visited, goods delivered, silver changed, research milestone, room changed, faction relation and leader changed, gizmo clicked, key pressed. [events](api/events.md) |
| P2-14 | `game.stats` reads | Built | `game.stats`: Value and explanation of any stat. [research](api/research.md) |

Done when: a custom storyteller, a trade assistant, a farm planner and a smarter hauling AI can be written in Lua without reflection.

### Phase 3: Presentation

**Goal.** Mods can have their own UI and visuals. The UI rules in the Standard come first.

Built, and the in-game smoke suite (434 of 434) covers registration, state, errors and that views, callbacks and the gizmo bar run. Gizmos, tabs, designators, alerts and the settings page draw through the game's own UI classes, so how they look still needs a human check.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P3-01 | Gizmos | Built | `game.gizmos`: Action buttons, toggles, sliders, hotkeys, per-thing visibility, merged for multiple selection. [gizmos](api/gizmos.md) |
| P3-02 | Windows and widgets | Built | `game.widgets`: Windows from widget trees: labels, buttons, checkboxes, sliders, text, dropdowns, lists with drag reorder, tables, tabs, scroll, progress, images, tooltips, confirm and prompt dialogs. [widgets](api/widgets.md) |
| P3-03 | Tabs | Built | `game.tabs`: Main tabs, inspect tabs, pawn table columns. Architect entries come from designators. [tabs](api/tabs.md) |
| P3-04 | Designators and tools | Built | `game.designators`, `game.areas`: Click and drag tools, ghost highlight, architect categories, allowed areas. [designators](api/designators.md) |
| P3-05 | Graphics | Built | `game.graphics`: Texture info, def graphics, colours, pawn body, hair, head and skin, race render nodes, animations. [graphics](api/graphics.md) |
| P3-06 | Shaders and materials | Built | `game.materials`: Materials from shaders, properties, textures, shaders from asset bundles. [graphics](api/graphics.md) |
| P3-07 | Effects | Built | `game.effects`: Flecks, effecters, floating text, screen shake, highlighted cells, lines, circles, marks. [effects](api/effects.md) |
| P3-08 | Input | Built | `game.input`: Runtime key bindings, key and chord queries, modifiers, mouse. [input](api/input.md) |
| P3-09 | Audio | Built | `game.audio`: Define sounds from clips, play at a cell, songs, volumes, sustainers. [audio](api/audio.md) |
| P3-10 | HUD, alerts and themes | Built | `game.alerts`, `game.hud`, `game.options`: Alerts with culprits, status lines, letters with choices, colour presets, fonts, settings pages. [hud](api/hud.md) |

Done when: the gizmo packs, overlays, custom tabs and apparel packs in [mod ideas](mod-ideas.md) are buildable.

### Phase 4: Extensibility

**Goal.** Mods that add new kinds of things, not only tune existing ones. This is the largest unlock, because most workshop content mods are XML plus a C# class.

Built. The in-game smoke suite covers Lua classes loaded from XML, class replacement, stat modifiers, def changes, authoring and patches, mod queries, tweak targets and per-thing data.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P4-01 | Stats API | Built | `game.stats.modify`, `add_offset`, `add_factor`, `remove_modifier`, `modifiers`: Stat modifiers over the game's own stat calculation. [research](api/research.md) |
| P4-02 | Tweak catalog | Built | `game.tweaks`: 13 named tweak points as data, with generated docs and stubs. [tweaks](api/tweaks.md) |
| P4-03 | Def mutation | Built | `game.defs.set`, `value`, `fields`, `restore`, `changes`: Runtime def changes with a journal. [defs](api/defs.md) |
| P4-04 | Def authoring | Built | `game.defs.to_xml`, `validate_xml`, `write_xml`: Def XML from tables, validated against the game's types. [defs](api/defs.md) |
| P4-05 | XML patch operations | Built | `game.patch`: Build and write patch operations, conditional on mods. [patch](api/patch.md) |
| P4-06 | Lua-backed game classes | Built | `game.classes`: 31 generated proxy families from `src/api/classes.cls`, including quest nodes (with `game.quests.slate_get` and `slate_set`), world gen steps, scenario parts, ritual outcomes, conditional think nodes, and whole-thing building, door and storage classes. [classes](api/classes.md) |
| P4-07 | Custom needs, thoughts, abilities | Built | Needs, thoughts, abilities: Through the `need`, `thought_worker`, `mental_state_worker` and `ability` families. A need needs no XML: `game.needs.define` creates the def when the mod loads. [classes](api/classes.md) |
| P4-08 | Custom saving | Built | `game.save` scope `thing`, Lua comp data: Saved with the object. [save](api/save.md) |
| P4-09 | Def inheritance and DefOf | Built | `game.defs.of`, `of_mod`, `kinds`: Which mod defines a def, def types and fields |
| P4-10 | Custom Def types | Built | `game.defs.data_rows`: Custom data defs as `RimKit.LuaDataDef` |
| P4-11 | Game-class replacement | Built | `game.classes.replace`, `replaceable`: Swap a def's class for a Lua proxy at runtime |
| P4-12 | Harmony for other mods | Built | `game.hooks.in_mod`, `game.mods`: Hook other mods only when present |

Done when: a mod can add a new building with a custom component, a new incident, a new work type with its own work giver and a new need, entirely from Lua plus XML.

#### Game class families to back with Lua (P4-06)

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

### Phase 5: DLC kits

**Goal.** The DLCs get the same treatment as the base game.

Built and checked: registration, the DLC guard (`RK3003` when inactive) and the functions of every DLC active in the test game. Odyssey is excluded from the smoke game because it hangs the quick test, so its functions are only checked for the guard.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P5-01 | Ideology | Built | `game.ideology`: Ideoligions, memes (add and remove through the game's own conflict checks), precepts, roles, certainty, rituals, style categories, renaming. [dlc](api/dlc.md) |
| P5-02 | Royalty | Built | `game.royalty`: Titles, honor, permits, psylink, abilities, the Empire, throne rooms. [dlc](api/dlc.md) |
| P5-03 | Biotech | Built | `game.biotech`: Xenotypes, gene defs, mechanitors, pregnancy, growth, hemogen, gene packs. [dlc](api/dlc.md) |
| P5-04 | Anomaly | Built | `game.anomaly`: Monolith, studies, containment, platforms, codex, creepjoiners, engagement settings. [dlc](api/dlc.md) |
| P5-05 | Odyssey | Partly | `game.odyssey`: Planet layers, gravship engines with fuel, range, cooldown, substructure and components, space maps, gravship in flight. Launching, travel and orbital sites are not exposed. [dlc](api/dlc.md) |
| P5-06 | DLC guard | Built | `game.dlc`: One guard helper for every DLC function. [dlc](api/dlc.md) |

### Phase 6: Ecosystem

**Goal.** A platform other people build on.

Built and checked: the in-game smoke suite (434 of 434 in the game, before the second event pass), native tests (the framework, mocks, interop and a capability check), 45 host unit tests, the reload fixture (13 tests) and the in-game runner fixture (15 tests), the editor and docs checks, and the CLI commands run by hand against a scratch mod. The in-game parts (the dev tools window, the capabilities hub tab, hot reload, the mod failure letter) are covered by the smoke suite except the dev tools window, the hub tab and the mod failure letter, which need a human look.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| P6-01 | Mod interoperability | Built | `game.interop`, `game.mods.order`: Publish functions with semantic versions, ask with `^`, `~`, `>=` requirements, `when` for any load order, safe `call`. [interop](api/interop.md) |
| P6-02 | Hot reload | Built | `game.dev.watch`, `game.dev.reload`: Reload a mod's Lua, per mod: events (and their game patches), hooks, timers, tick and widget callbacks, stat modifiers, windows, debug actions, published APIs and test suites are replaced. Gizmos, alerts, tabs, columns, designators, status lines and settings pages the old version registered are removed first, and the new version adds its own. A reload asked for inside a tick callback waits for the next tick. [hot reload](guide/hot-reload.md) |
| P6-03 | In-game tools | Built | Dev tools window (F11), `game.dev`: Console, hook cost, event recorder, profiler, debug actions, mods and permissions. A Reflect tab browses any game object: pick a root, open members that hold objects, read values live (needs the `developer_reflect` setting). [performance](guide/performance.md) |
| P6-04 | Profiler | Built | `game.profiler`, `perf_budget_us`: Time per mod for ticks, events, hooks, timers, UI and load, a budget with a warning and the `mod.over_budget` event. [performance](guide/performance.md) |
| P6-05 | Capability model | Built | `meta.capabilities`: `reflect`, `hooks`, `files`, `dev`: declared in `meta.lua`, shown in the hub, enforced with `RK4001`, checked by `rimkit mod check`. Mods that do not declare keep full access for now. [capabilities](guide/capabilities.md) |
| P6-06 | Editor extension | Built | Editor extension: Def name completion from the game, go to definition for API names, test, check, release check, assets and diagnostics commands, a new mod wizard with five templates. |
| P6-07 | Docs site | Built | Docs: Search plugin, seven tutorials, a cookbook with one recipe for every mod idea (a check keeps it that way), pages for testing, publishing, capabilities, save safety and performance. [tutorials](guide/tutorials.md), [cookbook](guide/cookbook.md) |
| P6-08 | Test harness for authors | Built | `rimkit mod test`, `game.test`: Headless test runner with a mock host: mocks, recorded calls, events, ticks, captured logs, Lua classes and tweaks, hot reload (`reload`, `subscriptions`). Scenarios in the real game are P6-11. [testing](guide/testing.md) |
| P6-09 | Sample mod pack | Built | Sample mod pack: Ten example mods in `src/examples` and the two video guide mods, each with headless tests that CI runs. The Defs of three of them follow the smoke test mods but have not been played. [examples](../../src/examples/README.md) |
| P6-10 | Telemetry-free diagnostics | Built | `rimkit diag`, `game.dev.bundle`: Zip of the log, mod list and profiler numbers, no network. [performance](guide/performance.md) |
| P6-11 | Tests in the real game | Built | `game.itest`, `rimkit mod test --in-game`: Suites in `Tests/Game` with waits for ticks, conditions and events, spawned things removed afterwards, a JSON report, and a launcher that gives the game its own save data folder and mod list. The runner is tested headless against a fake clock, and the `damage` example's 3 in-game tests passed through the launcher in RimWorld 1.6.4871. [testing](guide/testing.md#testing-in-the-game) |
| P6-12 | Hot reload contract | Built | Reload guarantees in the Standard (section 12), checked by `tests/fixtures/reload`: no doubled handlers, timers, tick callbacks, actions or APIs, no patch count growth, reload inside a callback waits, `events.off` only removes the caller's handlers, repeating timers are safe. [hot reload](guide/hot-reload.md) |
| P6-13 | Standard conformance | Built | `rimkit mod conform`: Levels 0 to 3 (declared, tested, verified in the game) with a line for `Workshop.md`. Standard draft 2. [standard](standard/rks.md) |

### Workshop readiness

**Goal.** What a mod needs around the code so strangers can find, install, trust and keep it.

The CLI commands below were run by hand against a scratch mod. `rimkit publish` writes the item file and builds the SteamCMD command, but has not been run against Steam.

| ID | Item | State | What it does now |
|----|------|-------|------------------|
| W-01 | `rimkit publish` | Built | `rimkit publish`: Release check, staging folder, item file with title, description, change note, tags and preview, SteamCMD run, saves the new id. Untested against Steam, try `--visibility 2` first. [publishing](guide/publishing.md) |
| W-02 | Preview and icon tooling | Built | `rimkit mod assets`: Validates the preview (PNG, 640x360 or more, 16:9, under 1 MB) and the icon, makes placeholders with `--fix`. |
| W-03 | Multi-version folders | Built | `meta.version_folders`: Writes `LoadFolders.xml` and the supported versions, RimKit loads a `Lua/` folder from every listed folder. Not yet tried in game. [meta.lua](guide/meta.md) |
| W-04 | Dependency and conflict metadata | Built | `meta.load_before`, `incompatible_with`: Written to `About.xml` next to `depends` and `load_after`. |
| W-05 | Versioning and changelog | Built | `meta.mod_version`, `game.mods.version`, `game.save.stamp`: Semantic version in the mod list, a changelog check, the version recorded in the save. |
| W-06 | Mod options page | Built | `game.options`: Settings pages with sections, sliders, dropdowns (P3-10). |
| W-07 | Save safety | Built | `game.save.migrate`, `stamp`, `purge`: Ordered migration steps that retry after a failure, version stamp, a clean-up for uninstalling. [save safety](guide/save-safety.md) |
| W-08 | Compatibility guards | Built | `game.hooks.in_mod`, `game.mods`, `game.interop`: Patch only when another mod is present (P4-12). |
| W-09 | Performance budget | Built | Performance budget: Documented budget, profiler and warning. [performance](guide/performance.md) |
| W-10 | Error reporting to players | Built | `mod.disabled` letter: A letter that names the failing mod and offers to turn it off for good. Not yet seen in game. |
| W-11 | Content validation | Built | `rimkit mod check`: Defs and patches well formed, duplicate defNames, mod textures, translation keys, declared capabilities. |
| W-12 | Localization tooling | Built | `rimkit mod i18n`: extract, missing, export and import (csv). |
| W-13 | Release checklist and templates | Built | `rimkit mod release-check`, templates: Checklist plus LICENSE, CREDITS, CHANGELOG, Workshop.md and a test in every new mod. |
| W-14 | Legal and attribution | Built | Legal and attribution: Checks that a licence exists and credits exist for shipped art and sound, and a reminder of Ludeon's rules. It cannot tell who made an asset, that stays with the author. |

### The 1.0 gate

| ID | Item | Done when |
|----|------|-----------|
| G-01 | Freeze the API | Promote ready Experimental kits to Stable, set `api_level = 1`, freeze the names. See [stability](api/stability.md) |

## Reference

### Events still without a catalog row

The catalog has 116 events. These are the ones that cannot be a single catalog row, with the reason and what to use today.

| Wanted | Why there is no event | Use |
|--------|-----------------------|-----|
| Passion changed | The game writes the passion field in place, there is no method to patch | Read `game.pawns` skills when you need them, or poll on `time.hour_changed` |
| Price changed | A price is computed from stats every time it is read, nothing changes at one moment | `trade.completed`, `silver.changed` and reads from `game.economy` |
| Hauled and delivered | The game counts a haul when the pawn picks the thing up | `thing.hauled` for the pickup, `thing.dropped` for the drop |

Every other group from the old list now has an event: pawn left colony and escaped, work completed, crafted, mined, construction started, deconstructed, repaired, hauled and dropped, healed, immunity gained, thought lost, social fight, learning saturated, fire started and ended, world generated, caravan arrived, site visited, goods delivered, silver changed, research milestone, room changed, relation and leader changed, gizmo clicked and key pressed. A new event is one catalog row plus a patch body, and the host test checks it against the game (P2-13).

### Def type coverage

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

### Known debt

| Debt | Effect |
|------|--------|
| The recruited-pawn engagement behavior is hard coded | Cannot tune or disable auto-attack from Lua |
| `Pawn.TickInterval` is a second tick path in 1.6 | Confirmed that `Pawn.Tick` still runs for pawns. Watch it after game updates (P0-14) |
| Entity detection is a heuristic | Look-alike mods can be misdetected |
| The smoke test runner needs the game and a Steam relaunch workaround | Cannot run on a build server (P6-08) |
| `mod/Auth/allowlist.json` is regenerated by hand after a rebuild | A forgotten regeneration blocks Lua (P0-13) |
| Hook context is built from JSON for every call | Hot hooks cost more than needed (P0-07) |
| Hooks run on whichever thread runs the method | Heavy hooks on worker threads serialize behind one lock |
| Events on methods that call engine internals (`time.*`, `letter.received`, `trade.completed`, `key.pressed`, `silver.changed`, `world.generated`) cannot be patched by the host unit test | Their target is checked by name, and Harmony accepting the patch is checked in the game by the smoke suite |

### Out of scope

- Letting Lua load native code, open files freely, run processes or use the network without a named, reviewed API. See [security](guide/security.md).
- A full mirror of every game type as hand-written kits. Kits cover what mods use. Everything else stays reachable through reflection and hooks.
- Multiplayer synchronization. The API targets single-player.
- Replacing RimWorld's engine: pathfinding rewrites, a new renderer, tick-rate overhauls that need engine-level changes. Those stay C# mods.
- Total conversion mods that replace most of the game. They are possible in pieces, but not a goal.
- Performance mods that need to run on every thing every tick. Lua is for logic, not for the hot loop.

### How we choose the next item

1. Phase order, unless something blocks a release.
2. Within a phase, the item that unlocks the most rows in the [Workshop categories](#what-kind-of-mod-can-be-built) table and in [mod ideas](mod-ideas.md).
3. Anything that makes a regression possible (Phase 0) jumps the queue.
4. A request that many mods need becomes a kit function. See [request a feature](guide/request-a-feature.md).
5. Each finished row updates this page, the [kit status board](api/domains.md) and the generated [reference](api/reference.md).
