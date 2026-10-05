# Anomaly kit

Control Anomaly DLC entities (Revenant, Shambler, Bulbfreak and others): find them, recruit them into a commandable army, knock them out, capture them on holding platforms, release them. Tier: Experimental. Needs the Anomaly DLC.

```lua
local entity = game.anomalies:get("Revenant")
if entity then
  entity:recruit()
end

for _, e in ipairs(game.anomalies:list() or {}) do
  game.log.info(e.name)
end
```

## `game.anomalies` (objects)

| Method | Returns |
|--------|---------|
| `:get(name [, map])` | The first living entity whose kind, def or label matches (case-sensitive), or `nil`. The current map if `map` is omitted |
| `:list([map])` | All entities on the map as `RimEntity` |
| `:find(predicate [, map])` | The first entity where `predicate(entity)` is truthy |

## `RimEntity`

Wraps a pawn handle. Properties: `handle`, `name`, `kind`.

| Method | Does |
|--------|------|
| `:is_entity()` | True for an Anomaly entity |
| `:recruit()` | Makes it a commandable pawn of the player faction. Returns true on success |
| `:knock_out([severity])` | Applies anesthetic. Returns true if the effect is on the entity |
| `:release()` | Frees a recruited entity and sets it hostile. Returns false, and changes nothing, for an entity that is not recruited |

`release` only acts on entities that RimKit recruited. A wild or foreign entity is left alone.

## `game.anomaly` (handles)

The same operations on integer handles, plus platform and capture helpers:

| Function | Returns |
|----------|---------|
| `game.anomaly.dlc_active()` | boolean |
| `game.anomaly.is_entity(h)` | boolean |
| `game.anomaly.list_on_map([map])` | array of handles |
| `game.anomaly.get_on_map(name [, map])` | handle or 0 |
| `game.anomaly.recruit(h)` | boolean |
| `game.anomaly.release_to_hostile(h)` | boolean |
| `game.anomaly.knock_out(h [, severity])` | boolean |
| `game.anomaly.find_platform(hauler, entity)` | handle of a free holding platform the hauler can reach, or 0 |
| `game.anomaly.start_capture(hauler, entity [, platform])` | boolean. The entity must be down first |

## Recruited pawns

"Recruited" means the pawn is controllable: `game.pawns.is_controllable(h)`. RimKit stores this in the save, restores the faction, drafter and draft state on load, and marks the pawn with a hidden hediff (`RimLua_Controllable`, defined in XML). Recruited pawns appear in the player's colonist bar for orders. A recruited pawn that is downed gets normal vanilla behavior again.

Other functions work on any controllable pawn: `game.pawns.make_controllable`, `release_control`, `is_controllable`.

## Right-click menus

```lua
game.ui.on_map_float_menu(function(ctx)
  -- ctx.clicked and ctx.hauler are handles
  return { { label = "Do thing", on_click = function() end, disabled = false } }
end)
```

One handler list is collected per clicked pawn. The hauler is the first selected humanlike colonist. Return `nil` for no options.

## Limits

- Entity detection is a heuristic (race flag, then def name, then component names). A mod that adds look-alikes can fool it.
- Capture works only on downed entities.
- The engagement behavior of recruited pawns (hostile search radius 55, melee when adjacent, ranged attacks within 14, abilities first) is fixed in the host and not yet exposed to Lua.
