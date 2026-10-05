# Episode 2: Control your colonists

**Mod:** Commander. Select colonists, point the mouse at the ground, press `G`: they walk there as a group. `Y` drafts or undrafts them. About 12 minutes. **Finished mod:** [final/Commander](final/Commander).

Assumes episode 1: the viewer knows `rimkit mod create`, `ship`, restart and `rimkit mod test`. Recap in one line and move on.

## 0. Hook (0:00 to 0:40)

**ON SCREEN:** Drag-select five colonists, point at the ground, tap `G`: they walk over and stand in a block. Tap `Y`: they draft.

**SAY:** Last time we changed a number. Today we give orders. Select your colonists, point, press G, and they go. Vanilla makes you draft first and right click each one. We will do it with one key, in about fifty lines.

## 1. Create the mod (0:40 to 1:40)

```text
rimkit mod create Commander
```

`meta.lua`:

```lua
meta.name = "Commander"
meta.package_id = "yourname.commander"
meta.description = "Select colonists, point at the ground and press G: they walk there."
meta.api_level = 1
meta.capabilities = { }
```

**SAY:** `create` already wrote `api_level = 1`: strict mode, so a mistake raises an error you can read instead of failing quietly. The capabilities list is empty this time: we only read what is selected and give orders, which any mod may do. No hooks needed.

## 2. What is selected? (1:40 to 3:00)

```lua
game.input.register_key("Commander_Go", "Commander: send selected colonists to the mouse", "G")

game.events.on_tick(function()
  if game.input.binding_just_pressed("Commander_Go") then
    local pawns = game.selection.pawns()
    game.ui.message("Selected: " .. #pawns)
  end
end)
```

**SAY:** We register a key, G, and on every tick ask if it was just pressed. Then we ask what is selected: `game.selection.pawns()` gives a list of the selected people, and the hash sign is its length.

Ship, restart, select three colonists, press `G`: "Selected: 3". Select nobody: "Selected: 0".

## 3. Where is the mouse? (3:00 to 4:30)

```lua
game.events.on_tick(function()
  if game.input.binding_just_pressed("Commander_Go") then
    local mouse = game.input.mouse()
    if mouse.on_map then
      game.ui.message("Mouse is over cell " .. mouse.cell_x .. ", " .. mouse.cell_z)
    end
  end
end)
```

**SAY:** The map is a grid of cells. `game.input.mouse()` tells us where the mouse is on screen and which cell is under it. We check `on_map`, because over a menu there is no cell.

## 4. Send them there (4:30 to 6:30)

```lua
local KEY_GO = "Commander_Go"

local function send_selected_to_mouse()
  local mouse = game.input.mouse()
  if not mouse.on_map then return 0 end
  local pawns = game.selection.pawns()
  local sent = 0
  for _, pawn in ipairs(pawns) do
    if game.jobs.go_to(pawn, mouse.cell_x, mouse.cell_z) then sent = sent + 1 end
  end
  game.ui.message("Sent " .. sent .. " colonist(s).")
  return sent
end

game.input.register_key(KEY_GO, "Commander: send selected colonists to the mouse", "G")

game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY_GO) then send_selected_to_mouse() end
end)
```

**SAY:** For every selected colonist, `game.jobs.go_to` gives the order to walk to a cell. It returns true if they accepted, so we count how many did. The pawn always goes first: every function that acts on a person takes the person as its first argument. They all go to the same cell and bunch up, which looks silly.

## 5. Stand in a group (6:30 to 8:00)

Add above `send_selected_to_mouse`:

```lua
local function formation(x, z, n)
  local cells = {}
  for i = 0, n - 1 do
    local column = i % 3 - 1
    local row = math.floor(i / 3)
    cells[#cells + 1] = { x = x + column, z = z + row }
  end
  return cells
end
```

and use it in the loop:

```lua
local cells = formation(mouse.cell_x, mouse.cell_z, #pawns)
local sent = 0
for i, pawn in ipairs(pawns) do
  if game.jobs.go_to(pawn, cells[i].x, cells[i].z) then sent = sent + 1 end
end
```

**SAY:** `formation` gives each colonist their own cell around the target, three across, then the next row. It has no game calls in it, so it is easy to test.

## 6. Draft with one key (8:00 to 9:30)

```lua
local function toggle_draft_selected()
  local changed = 0
  for _, pawn in ipairs(game.selection.pawns()) do
    game.pawns.set_drafted(pawn, not game.pawns.drafted(pawn))
    changed = changed + 1
  end
  return changed
end

game.input.register_key("Commander_Draft", "Commander: draft or undraft the selected colonists", "Y")
```

and in the tick handler: `if game.input.binding_just_pressed("Commander_Draft") then toggle_draft_selected() end`.

**SAY:** For each selected colonist, read whether they are drafted and set the opposite. Every RimKit function takes the colonist itself, or the plain number behind it, so there is nothing to convert.

## 7. Strings and tests (9:30 to 11:00)

Show `final/Commander/Lua/main.lua`: text moved into a translation file, a "nothing selected" message, and `game.interop.publish` at the bottom for the tests. Run:

```text
rimkit mod test final/Commander
```

**SAY:** The tests pretend the game answered: mouse on cell fifty, sixty, three colonists selected. Did we send three orders to three different cells? Yes. Mouse not over the map: did we send nothing? Yes. All in a second, without starting RimWorld.

## 8. Wrap up (11:00 to 12:00)

**SAY:** We built a real control mod in about fifty lines: read the selection, read the mouse, give orders, draft. The same pieces make haul orders, tending, camera jumps, box select. The jobs page in the docs lists everything a colonist can be ordered to do. Next: publishing to the Workshop.

## Producer notes

- Show your hand on the keyboard after each restart, not only the screen. Box-select so several colonists are visible.
- If `G` or `Y` clash, rebind in Options, Key bindings.
- If a colonist does not move they may be downed or walled in: `go_to` returns false and the count in the message tells the truth. Say so.
- Dry run first: the mod passes its tests but has not been played on camera by the authors.
