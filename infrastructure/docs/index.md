# RimKit

Write RimWorld mods in Lua. RimKit loads your scripts, gives them a safe, documented API to the game, and lets them hook any game method when the API does not cover something yet.

`stratware.rimkit` · [Stratware.win](https://stratware.win/)

```lua
-- Colonists never lose accuracy with distance.
game.hooks.postfix("Verse.ShotReport", "HitFactorFromShooter", function(ctx)
  ctx:set_result(1)
end)

game.events.on("pawn.died", function(e)
  game.ui.message(e.pawn.name .. " died")
end)
```

## How it fits together

```text
Your Lua mod  ->  RimKit C++ core (Lua 5.4, sandbox)  ->  RimKit C# host  ->  Harmony  ->  RimWorld
```

You write Lua. RimKit handles loading, security checks, the API, and patching the game. Details: [architecture](concepts/architecture.md).

## Start here

- First mod in about ten minutes: [Quickstart](guide/quickstart.md)
- What the API offers: [API overview](api/overview.md) and the full [reference](api/reference.md)
- React to game events: [Events](api/events.md)
- Change how the game behaves: [Hooks](api/hooks.md)
- Edit pawn skills, traits, needs, thoughts: [Pawns kit](api/pawns.md)
- Call into something the API does not cover yet: [Typed reflection](api/reflect.md)
- Update an older mod: [Migration](api/migration.md)
- Fix a problem: [Troubleshooting](guide/troubleshooting.md)
- See what is planned: [What is missing](missing.md)
- Get ideas: [Mod ideas](mod-ideas.md)

## What you can and cannot build

You can build most mods that are game logic: reacting to events, changing numbers and behavior, pawn data, right-click menus, windows and panels, hotkeys, settings, saved data, plus any XML Defs, patches and translations shipped next to the Lua.

You cannot yet define new C# classes (custom buildings, comps, job drivers with real toils), replace the game renderer, or use every planned kit (zones, trade, and similar still grow over time). The full gap list, in phases, is in [what is missing](missing.md).

## Status

RimKit is pre-1.0. Every function has a stability tier ([stability](api/stability.md)). Stable names will not break before 1.0. Experimental names can change in a minor version. Targets RimWorld 1.6 with Harmony.

## Documentation map

- **API**: [overview](api/overview.md), [reference](api/reference.md), [events](api/events.md), [hooks](api/hooks.md), [pawns](api/pawns.md), [anomaly](api/anomaly.md), [reflect](api/reflect.md), [kits and domains](api/domains.md), [naming](api/naming.md), [stability](api/stability.md), [migration](api/migration.md), [older names](api/legacy.md)
- **Concepts**: [architecture](concepts/architecture.md), [game inventory](concepts/game-inventory.md), [the RimKit Standard](standard/rks.md)
- **Project**: [what is missing](missing.md), [mod ideas](mod-ideas.md)
