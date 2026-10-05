-- Commander
-- Select colonists, point the mouse at the ground and press G: they walk there and stand in a loose group.
-- Press Y to draft or undraft the selected colonists. Nothing here needs the game to be drafted first.

local KEY_GO = "Commander_Go"
local KEY_DRAFT = "Commander_Draft"

-- Where each of n pawns stands around a target cell: the target first, then a three wide block around it.
-- A plain function with no game calls, so it is easy to test.
local function formation(x, z, n)
  local cells = {}
  for i = 0, n - 1 do
    local column = i % 3 - 1       -- -1, 0, 1
    local row = math.floor(i / 3)  -- 0, 1, 2, ...
    cells[#cells + 1] = { x = x + column, z = z + row }
  end
  return cells
end

local function send_selected_to_mouse()
  local mouse = game.input.mouse()
  if not mouse.on_map then return 0 end
  local pawns = game.selection.pawns()
  if #pawns == 0 then
    game.ui.message(game.ui.translate("Commander_NoneSelected"))
    return 0
  end
  local cells = formation(mouse.cell_x, mouse.cell_z, #pawns)
  local sent = 0
  for i, pawn in ipairs(pawns) do
    if game.jobs.go_to(pawn, cells[i].x, cells[i].z) then sent = sent + 1 end
  end
  game.ui.message(game.ui.translate("Commander_Sent", sent))
  return sent
end

local function toggle_draft_selected()
  local changed = 0
  for _, pawn in ipairs(game.selection.pawns()) do
    game.pawns.set_drafted(pawn, not game.pawns.drafted(pawn))
    changed = changed + 1
  end
  return changed
end

game.input.register_key(KEY_GO, game.ui.translate("Commander_GoKey"), "G")
game.input.register_key(KEY_DRAFT, game.ui.translate("Commander_DraftKey"), "Y")

game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY_GO) then send_selected_to_mouse() end
  if game.input.binding_just_pressed(KEY_DRAFT) then toggle_draft_selected() end
end)

game.interop.publish("commander", "1.0.0", {
  formation = formation,
  send_selected_to_mouse = send_selected_to_mouse,
  toggle_draft_selected = toggle_draft_selected,
})
