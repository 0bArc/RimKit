# Maps kit

`game.maps` reads and changes the state of a map: cells, rooms, zones, designations, groups of pawns and reachability. The map (a `RimMap` or its handle) is the first argument and cells are integer `x`, `z`. The kit is Experimental in the [stability tiers](stability.md). Every function is listed with its parameters in the [reference](reference.md#maps).

The functions `current`, `list`, `width`, `height`, `pawns`, `colonists`, `prisoners`, `things`, `things_of_def`, `nutrition` and `spawn` read the map and place things on it.

## What you can do

| Area | Functions |
|------|-----------|
| The map | `info` (size, biome, owner, temperature, weather, wealth), `edge_cell`, `drop_spot`, `cell_near` |
| Terrain and roofs | `terrain`, `set_terrain`, `roof`, `set_roof` |
| Fog, light, heat, snow | `fogged`, `unfog`, `temperature`, `light`, `snow`, `set_snow` |
| Filth | `filth`, `clear_filth` |
| Walking | `walkable`, `standable`, `in_bounds`, `reachable` |
| What is where | `things_at`, `room`, `rooms`, `zones`, `zone_at`, `areas` |
| Orders | `designations`, `add_designation`, `remove_designations` |
| Groups and plumbing | `lords` (raids and visitors), `components` |

Errors: a stale map handle raises `RK2001`, a cell outside the map raises `RK1001`, an unknown def raises `RK3001`.

```lua
-- Roof and floor a 3 by 3 patch next to the first colonist
local map = game.maps.current()
local c = game.maps.colonists(map)[1]:info()
for dx = 1, 3 do
  for dz = 0, 2 do
    game.maps.set_terrain(map, c.x + dx, c.z + dz, "WoodPlankFloor")
    game.maps.set_roof(map, c.x + dx, c.z + dz, "RoofConstructed")
  end
end

-- Is the colony walled in? Count the enclosed rooms by role
local roles = {}
for _, room in ipairs(game.maps.rooms(map)) do
  roles[room.role or "none"] = (roles[room.role or "none"] or 0) + 1
end
```

Cells outside the home area load lazily, so a loop over a whole large map is slow. Use [queries](query.md) for filtered searches.
