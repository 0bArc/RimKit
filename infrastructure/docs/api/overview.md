# API overview

Everything is under one table, `game`, organized by domain. Names are lowercase `snake_case`: `game.pawns.set_passion`, `game.time.ticks`, `game.ui.message`. The complete list is generated in the [reference](reference.md).

## Layers

Use the highest layer that does the job. Prefer kits. Fall back in this order.

=== "Kits"

    **Stable or Experimental.** Normal API. Documented, typed names under `game.*`.

    ```lua
    game.pawns.set_need(pawn, "Food", 0.8)
    game.anomaly.recruit(entity)
    ```

=== "Events"

    **Experimental.** React when something happens in the game.

    ```lua
    game.events.on("pawn.died", function(e)
      game.ui.message(e.pawn.name .. " died")
    end)
    ```

=== "Hooks"

    **Advanced.** Change how a Verse or RimWorld method behaves (Harmony).

    ```lua
    game.hooks.postfix("Verse.ShotReport", "HitFactorFromShooter", function(ctx)
      ctx:set_result(1)
    end)
    ```

=== "Reflection"

    **Advanced, gated.** Read or call into Verse, RimWorld, and UnityEngine when no kit fits. Off by default.

    ```lua
    local name = game.reflect.get(pawn, "Name")
    ```


## Domains

| Domain | What it controls | Page |
|--------|------------------|------|
| `game.pawns` | Skills, needs, traits, thoughts, relations, backstory, genes, equipment, jobs, movement | [pawns](pawns.md) |
| `game.things` | Any thing: hit points, stack, position, faction, destroy | [reference](reference.md#things) |
| `game.maps` | Pawns, things and nutrition on a map, spawning | [reference](reference.md#maps) |
| `game.factions` | Player faction, lists, hostility | [reference](reference.md#factions) |
| `game.time`, `game.weather`, `game.world`, `game.incidents` | Ticks, weather, world, firing incidents | [reference](reference.md) |
| `game.jobs`, `game.paths`, `game.control` | Lua-authored jobs, movement, possession | [reference](reference.md#jobs) |
| `game.work`, `game.buildings` | Work priorities, power and switches | [reference](reference.md#work) |
| `game.defs` | Reading Defs, registering new things | [reference](reference.md#defs) |
| `game.ui` | Messages, letters, windows, panels, float menus, translated strings | [reference](reference.md#ui) |
| `game.config`, `game.data`, `game.input` | Settings, saved data, key bindings | [reference](reference.md) |
| `game.events`, `game.log`, `game.timer` | Events, logging, timers | [events](events.md) |
| `game.anomaly`, `game.anomalies` | Anomaly DLC entities | [anomaly](anomaly.md) |
| `game.hooks` | Harmony hooks | [hooks](hooks.md) |
| `game.reflect` | Typed reflection | [reflect](reflect.md) |

## Wrapped objects

Functions that return game objects return wrapped values with methods and properties:

```lua
local colonist = rim.wrap(game.maps.colonists(game.current_map().handle)[1])
print(colonist.name, colonist.is_colonist, colonist.skills[1].level)
colonist:add_trait("Beauty", 2)
```

Types: `RimPawn`, `RimThing`, `RimMap`, `RimFaction`, `RimEntity` (an anomaly entity), and `RimObject` (anything else, with `handle`, `type_name` and `def`). See [handles](../concepts/architecture.md#handles).

## Calling conventions

- A function that acts on an object takes it first: `game.pawns.set_need(pawn, "Food", 0.5)`.
- Defs are strings: `"Shooting"`, `"Steel"`, `"Catharsis"`.
- Ratios are 0 to 1. Times are ticks unless a name says otherwise.
- Newer kits raise a Lua error starting with an `RK` code. Older functions return `nil` and log. See [troubleshooting](../guide/troubleshooting.md).

## Names and compatibility

- [naming](naming.md): how names are formed.
- [stability](stability.md): what may change.
- [migration](migration.md): moving from the old names (`rim.pawn.name`, `anomaly.recruit`, ...) with one command.
- [older names](legacy.md): the previous surface, kept working until 1.0.

## Security

Lua runs in a sandbox with an allowlisted API. See [security](../guide/security.md). Need something that is blocked? [Request a feature](../guide/request-a-feature.md).
