# Things kit

`game.things` reads and changes items, buildings and pawns as things. The thing (a `RimThing`, `RimPawn` or its handle) is the first argument. The kit is Experimental in the [stability tiers](stability.md). Older functions (`def`, `label`, `pos`, `hp`, `max_hp`, `set_hp`, `stack`, `set_stack`, `faction`, `set_faction`, `map`, `spawned`, `destroy`, `despawn`) are unchanged.

| Function | Returns | Notes |
|----------|---------|-------|
| `game.things.info(thing)` | table | `id`, `def`, `label`, `hp`, `max_hp`, `stack`, `spawned`, `is_pawn`, `is_building`, `rotation`, and when they apply `stuff`, `quality`, `x`, `z`, `map`, `forbidden`, `faction`. |
| `game.things.quality(thing)` | string? | `Awful` to `Legendary`, nil when the thing has no quality. |
| `game.things.set_quality(thing, quality)` | string | Name (any case) or 0 to 6. Raises `RK3003` when the thing has no quality. |
| `game.things.stuff(thing)` | string? | Material def name. |
| `game.things.forbidden(thing)` | boolean | For the player faction. |
| `game.things.set_forbidden(thing, forbidden)` | boolean | The thing must be spawned. |
| `game.things.rotation(thing)` | integer | 0 north, 1 east, 2 south, 3 west. |
| `game.things.set_rotation(thing, rotation)` | integer | |
| `game.things.comps(thing)` | string[] | Comp class names, for example `CompQuality`. |
| `game.things.has_comp(thing, class)` | boolean | With or without the `Comp` prefix. |
| `game.things.damage(thing, amount[, damage_def])` | integer | Damage def defaults to `Cut`. Returns hit points left. |
| `game.things.heal(thing)` | integer | Back to maximum hit points. Not for pawns. |
| `game.things.destroy_with(thing[, mode])` | | `Vanish`, `Deconstruct`, `KillFinalize`, `Refund`, `Cancel`, `FailConstruction`. |
| `game.things.make(def[, opts])` | RimThing | Creates a thing without placing it. |
| `game.things.spawn_at(def, map, x, z[, opts])` | RimThing | Creates and places a thing. |

`opts` for `make` and `spawn_at`: `stuff` (def name, defaults to the def's default material), `quality`, `count` (clamped to the stack limit).

Errors: a stale handle raises `RK2001`, an unknown def or damage def raises `RK3001`, a bad argument raises `RK1001`.

```lua
-- Drop a masterwork revolver next to a colonist
local pos = game.things.info(colonist)
local gun = game.things.spawn_at("Gun_Revolver", colonist:map(), pos.x + 1, pos.z, { quality = "Masterwork" })
```

Not yet in the kit: thing filters, minified items, styles and ownership. They are tracked as P1-01 in the [roadmap](../missing.md).
