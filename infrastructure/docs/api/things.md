# Things

A thing is an object. Read it with properties, change it with assignment, act on it with methods. `game.things` only holds the functions that make things. The kit is Experimental in the [stability tiers](stability.md).

```lua
game.events.on("thing.spawned", function(e)
  local thing = e.thing
  if thing.is_building and thing.hp < thing.max_hp then
    thing.hp = thing.max_hp
  end
end)
```

A `RimPawn` is a thing too, so every property and method below works on a pawn as well.

## Properties

| Property | Type | Notes |
|----------|------|-------|
| `thing.id` | integer | Unique id of the thing. |
| `thing.def` | string | Def name. |
| `thing.label`, `thing.label_short` | string | Name shown to the player. |
| `thing.hp` | integer | Hit points. Assign to change them. |
| `thing.max_hp` | integer | Maximum hit points. |
| `thing.stack` | integer | Stack size. Assign to change it. |
| `thing.spawned` | boolean | Whether it is on a map. |
| `thing.is_pawn`, `thing.is_building` | boolean | What kind of thing it is. |
| `thing.position` | cell or nil | `{ x, z }`. nil when it is not on a map. Assign a cell to move it. |
| `thing.map` | RimMap or nil | The map it is on. |
| `thing.faction` | RimFaction or nil | The owner. Assign a faction to change it. |

Each property is one read of the game, so reading `thing.hp` does not build a table. `thing:info()` still returns everything at once when you need most of it.

## Methods

| Method | Returns | Notes |
|--------|---------|-------|
| `thing:info()` | table | `id`, `def`, `label`, `hp`, `max_hp`, `stack`, `spawned`, `is_pawn`, `is_building`, `rotation`, and when they apply `stuff`, `quality`, `x`, `z`, `map`, `forbidden`, `faction`. |
| `thing:quality()` | string? | `Awful` to `Legendary`, nil when the thing has no quality. |
| `thing:set_quality(quality)` | string | Name (any case) or 0 to 6. Raises `RK3003` when the thing has no quality. |
| `thing:stuff()` | string? | Material def name. |
| `thing:forbidden()`, `thing:set_forbidden(on)` | boolean | For the player faction. The thing must be spawned to change it. |
| `thing:rotation()`, `thing:set_rotation(r)` | integer | 0 north, 1 east, 2 south, 3 west. |
| `thing:comps()` | string[] | Comp class names, for example `CompQuality`. |
| `thing:has_comp(class)` | boolean | With or without the `Comp` prefix. |
| `thing:damage(amount[, damage_def])` | integer | Damage def defaults to `Cut`. Returns hit points left. |
| `thing:heal()` | integer | Back to maximum hit points. Not for pawns. |
| `thing:destroy()`, `thing:despawn()` | | Remove the thing. |
| `thing:destroy_with(mode)` | | `Vanish`, `Deconstruct`, `KillFinalize`, `Refund`, `Cancel`, `FailConstruction`. |
| `thing:minify()`, `thing:inner()` | | Minified items and what they hold. |
| `thing:style()`, `thing:set_style(style)` | | Ideology styles. |
| `thing:owners()`, `thing:assign_owner(pawn)`, `thing:unassign_owner(pawn)` | | Bed and room ownership. |
| `thing:storage_allows(def)`, `thing:storage_set_allowed(def, on)`, `thing:storage_priority()`, `thing:set_storage_priority(p)` | | Storage filters and priority. |

## Making things

These have no thing to act on, so they stay on `game.things`.

| Function | Returns | Notes |
|----------|---------|-------|
| `game.things.make(def[, opts])` | RimThing | Creates a thing without placing it. |
| `game.things.spawn_at(def, map, x, z[, opts])` | RimThing | Creates and places a thing. |
| `game.things.thing_set`, `thing_set_defs`, `stuff_options`, `random_stuff`, `roll_quality` | | Generation helpers. See the [reference](reference.md). |

`opts` for `make` and `spawn_at`: `stuff` (def name, defaults to the def's default material), `quality`, `count` (clamped to the stack limit).

Errors: a stale handle raises `RK2001`, an unknown def or damage def raises `RK3001`, a bad argument raises `RK1001`.

```lua
-- Drop a masterwork revolver next to a colonist
local pos = colonist.position
local gun = game.things.spawn_at("Gun_Revolver", colonist.map, pos.x + 1, pos.z, { quality = "Masterwork" })
```
