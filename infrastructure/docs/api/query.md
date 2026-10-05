# Queries kit

`game.query` answers "give me all X where Y" in one host call, so a mod does not loop over thousands of objects in Lua. The kit is Experimental in the [stability tiers](stability.md). Every function is in the [reference](reference.md#queries).

| Function | Returns |
|----------|---------|
| `game.query.things(opts)` | Things on a map matching the filters. |
| `game.query.pawns(opts)` | Pawns matching the filters, on one map or all maps. |
| `game.query.count_by_def(opts)` | A table of thing def name to total count, stacks included. |
| `game.query.radius(map, x, z, radius, opts)` | Things within a radius of a cell. |
| `game.query.nearest(map, x, z, opts)` | The closest spawned thing, or nil. |

## Filters

`things` and `count_by_def` take `map` (required), `def`, `defs`, `group` (a game thing request group such as `HaulableEver`, `Weapon`, `Plant`, `Pawn`), `faction`, `area` (`{ x1, z1, x2, z2 }`), `spawned_only` (default true) and `limit`.

`pawns` takes `map` (all maps when omitted), `faction` (a faction, or `"player"`, `"hostile"`, `"neutral"`), `kind`, `humanlike`, `animal`, `colonist`, `prisoner`, `slave`, `downed`, `dead`, `drafted`, `area`, `include_unspawned` and `limit`.

Errors: an unknown def or group raises `RK3001` or `RK1001`, a missing map raises `RK2001`.

```lua
local map = game.maps.current()
-- How much steel and how many weapons does the colony hold?
local counts = game.query.count_by_def({ map = map, group = "HaulableEver" })
local steel = counts.Steel or 0
-- Hostile pawns near the first colonist
local c = game.things.info(game.maps.colonists(map)[1])
local danger = game.query.pawns({ map = map, faction = "hostile", area = { x1 = c.x - 20, z1 = c.z - 20, x2 = c.x + 20, z2 = c.z + 20 } })
```
