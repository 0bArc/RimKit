# Selection and camera kit

`game.selection` reads and changes what the player has selected, and `game.camera` moves the view. Both are Experimental in the [stability tiers](stability.md). Every function is in the [reference](reference.md#selection).

| Function | What it does |
|----------|--------------|
| `game.selection.first()`, `things()` | The first selected thing, and all selected things. |
| `game.selection.pawns()`, `zones()`, `count()` | Selected pawns, selected zones, how many objects are selected. |
| `game.selection.select(thing)`, `add(thing)`, `clear()` | Change the selection. |
| `game.selection.inspected()` | The single selected object: `{ kind = "thing", thing = ... }` or a zone. |
| `game.camera.jump_cell(map, x, z)`, `jump_thing(thing)` | Move the camera. |
| `game.camera.position()`, `mouse_cell()` | Where the camera looks and what is under the pointer. |

```lua
-- Select every idle colonist and look at the first
local idle = {}
for _, p in ipairs(game.query.pawns({ faction = "player", humanlike = true })) do
  if game.jobs.current(p) == nil then idle[#idle + 1] = p end
end
game.selection.clear()
for _, p in ipairs(idle) do game.selection.add(p) end
if idle[1] then game.camera.jump_thing(idle[1]) end
```
