# Kits and domains

A kit is a curated, documented `game.<domain>` API for one part of the game. This page is the status board: what is shipped, what is partial, what is planned. The roadmap with phases is in [what is missing](../missing.md).

## Status

| Domain | State | Notes |
|--------|-------|-------|
| `game.pawns` | Shipped (Experimental) | Skills, needs, traits, thoughts, relations, backstory, capacities, timetable, genes. Gear and policies are still older functions. [pawns](pawns.md) |
| `game.anomaly`, `game.anomalies` | Shipped (Experimental) | Entities, recruit, capture, monolith, studies, containment, codex, engagement. [anomaly](anomaly.md), [dlc](dlc.md) |
| `game.things` | Shipped (Experimental) | Info, quality, stuff, forbidden, rotation, comps, damage, make and spawn. [things](things.md) |
| `game.maps` | Shipped (Experimental) | Cells, terrain, roofs, fog, rooms, zones, designations, lords, reachability. [maps](maps.md) |
| `game.factions` | Shipped (Experimental) | Goodwill, relations, leaders, members, definitions. [factions](factions.md) |
| `game.time`, `game.version` | Shipped (Experimental) | Calendar, speed, pause, date text, RimWorld and RimKit version checks. [time](time.md) |
| `game.world` | Shipped (Experimental) | Tiles, settlements, world objects, caravans, world pawns, maps. [world](world.md) |
| `game.weather`, `game.conditions` | Shipped (Experimental) | Weather, sky, seasons, game conditions. [conditions](conditions.md) |
| `game.storyteller`, `game.incidents`, `game.quests` | Shipped (Experimental) | Storyteller, difficulty, incidents with options, quests, signals. [storyteller](storyteller.md) |
| `game.research`, `game.stats` | Shipped (Experimental) | Projects, progress, knowledge, every stat with its explanation. [research](research.md) |
| `game.ai` | Shipped (Experimental) | Work givers, think trees, duties, lords, path costs. [ai](ai.md) |
| `game.combat` | Shipped (Experimental) | Verbs, armor, explosions, fire, projectiles, turrets. [combat](combat.md) |
| `game.economy`, `game.raids` | Shipped (Experimental) | Silver, wealth, prices, traders, raids. [economy](economy.md) |
| `game.generation`, `game.scenarios` | Shipped (Experimental) | Terrain, generation steps, base generation, scenario and rules. [generation](generation.md) |
| `game.plants` | Shipped (Experimental) | Growth, sowing, harvest, fertility, growing zones. [plants](plants.md) |
| `game.save` | Shipped (Experimental) | Per game, map and world data, versions, autosave, saves. [save](save.md) |
| `game.jobs` | Shipped (Experimental) | Any vanilla job with targets, queue, reservations, helpers, plus Lua jobs. [jobs](jobs.md) |
| `game.paths`, `game.control` | Shipped (Experimental) | Movement, possession |
| `game.query` | Shipped (Experimental) | Filtered queries over things and pawns. [queries](query.md) |
| `game.selection`, `game.camera` | Shipped (Experimental) | Selection, inspection, camera control. [selection](selection.md) |
| `game.work` | Minimal | Work priorities |
| `game.build`, `game.power`, `game.bills`, `game.doors`, `game.traps`, `game.buildings` | Shipped (Experimental) | Blueprints, frames, power nets, bills, doors, traps. [build](build.md) |
| `game.ui` | Shipped | Messages, letters, windows, panels, float menus, translate |
| `game.widgets` | Shipped (Experimental) | Lua windows from widget trees, confirm and prompt dialogs. [widgets](widgets.md) |
| `game.gizmos` | Shipped (Experimental) | Buttons, toggles and sliders on the gizmo bar. [gizmos](gizmos.md) |
| `game.tabs` | Shipped (Experimental) | Main tabs, inspect tabs, pawn table columns. [tabs](tabs.md) |
| `game.designators`, `game.areas` | Shipped (Experimental) | Click and drag tools, architect entries, allowed areas. [designators](designators.md) |
| `game.graphics`, `game.materials` | Shipped (Experimental) | Textures, def graphics, pawn looks, render nodes, materials, bundles. [graphics](graphics.md) |
| `game.effects` | Shipped (Experimental) | Flecks, effecters, text, shake, overlays. [effects](effects.md) |
| `game.audio` | Shipped (Experimental) | Define and play sounds, music, volumes, sustainers. [audio](audio.md) |
| `game.alerts`, `game.hud`, `game.options` | Shipped (Experimental) | Alerts, status lines, letters with choices, themes, settings pages. [hud](hud.md) |
| `game.defs` | Shipped (Advanced) | Runtime changes with restore, def lookup across mods, custom data defs, Def XML authoring. [defs](defs.md) |
| `game.classes` | Shipped (Advanced) | Lua-backed game classes in 31 families, data on Lua comps, class replacement. [classes](classes.md) |
| `game.patch`, `game.mods` | Shipped (Advanced) | XML patch building and writing, installed mod queries. [patch](patch.md) |
| `game.interop` | Shipped (Experimental) | Versioned functions mods offer each other. [interop](interop.md) |
| `game.dev`, `game.profiler` | Shipped (Advanced) | Hot reload, debug actions, event recorder, eval, def export, diagnostics, per mod time. [dev](dev.md) |
| `game.test` | Shipped (Experimental) | Mock host tests run by `rimkit mod test`. [testing](../guide/testing.md) |
| `game.tweaks` | Shipped (Experimental) | Named tweak points over game methods. [tweaks](tweaks.md) |
| `game.dlc`, `game.ideology`, `game.royalty`, `game.biotech`, `game.odyssey` | Shipped (Experimental) | DLC guard and one kit per expansion. [dlc](dlc.md) |
| `game.input` | Shipped (Experimental) | Runtime key bindings, chords, mouse. [input](input.md) |
| `game.config`, `game.data`, `game.events`, `game.log`, `game.timer`, `game.json` | Shipped | Settings, saved data, events, logging, timers, JSON |
| `game.hooks`, `game.reflect` | Shipped (Advanced) | [hooks](hooks.md), [reflect](reflect.md) |

Every function in the shipped domains is in the [reference](reference.md).

## Kit convention

Every kit follows the same shape, so a new domain is predictable:

| Piece | Pattern |
|-------|---------|
| Namespace | `game.<plural domain>`, functions `verb_noun` ([naming](naming.md)) |
| Subject | The object comes first: `game.pawns.add_trait(pawn, ...)` |
| Objects | A wrapped type (`RimPawn`) with the same names as properties and methods |
| Reads | Return tables of plain data (`{ def, label, level }`), defs as `defName` strings |
| Failures | A Lua error starting with an `RK` code |
| Host | One C# class per domain in `src/host/api/` registering `domain.verb_noun` operations |
| Binding | A native function in `src/native/core/` |
| Docs | A page in `infrastructure/docs/api/`, stubs in `src/editor/stubs/rimkit.lua`, regenerate the [reference](reference.md) |
| Test | A check in `tests/smoke/Lua/main.lua` |

The rules behind this are in [the RimKit Standard](../standard/rks.md).

## Tiers

Every function is Stable, Experimental or Advanced. See [stability](stability.md). New kits start Experimental and are promoted after a full minor cycle with docs, an example, stubs and a test.

## Escapes

When no kit function exists:

1. A named [event](events.md) if you only need to react.
2. A [hook](hooks.md) to change behavior.
3. [`game.reflect`](reflect.md) to read or call anything.

If you find yourself using an escape often, that is a request for a kit function. See [request a feature](../guide/request-a-feature.md).
