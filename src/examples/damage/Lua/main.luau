-- damage
-- Floats the damage number above a humanlike pawn when it gets hurt: red for your colonists, orange for everyone else.
-- Press F8 to hurt the selected pawns, so there is something to watch. The key can be changed in the game's key options.
-- Read the docs for every game.* function: infrastructure/docs/api, or hover over it in VS Code with the RimKit extension.

local Signal = rimkit.signal


local HURT_AMOUNT = 10

-- hurt_requested fires with the selected pawns every time the key goes down. The key code only fires it, and what happens next is
-- connected below, so another part of the mod (or a test) can listen to the same signal without touching the key code.
local hurt_requested = Signal.new()


hurt_requested:connect(function(pawns)
  for _, pawn in ipairs(pawns) do
    pawn:damage(HURT_AMOUNT, "Cut")
  end
end)

game.events.on_load(function()
  game.log.info("[damage] loaded")
  game.ui.say("damage_Loaded")
  game.input.register_key("Damage_HurtSelected", "Hurt the selected pawns", "F8")
end)

-- Every tick, ask whether the key went down this frame.
game.events.on_tick(function()
  if game.input.binding_just_pressed("Damage_HurtSelected") then
    hurt_requested:fire(game.selection.pawns())
  end
end)

-- pawn.damaged is hot, so the filter does the cheap checks before this function runs: only humanlike pawns, only real damage.
-- The handler gets the pawn first and the whole payload (dealt, damage) second. The editor types both from the event name.
game.events.on_pawn_damaged(function(pawn, e)
  game.effects.text(pawn, tostring(math.floor(e.dealt)), pawn.is_colonist and "red" or "orange")
end, { humanlike = true, min_dealt = 1 })
