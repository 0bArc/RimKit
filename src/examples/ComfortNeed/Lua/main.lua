-- ComfortNeed
-- A new need for colonists, written entirely in Lua with no XML. game.needs.define makes the need when the mod loads; the game
-- draws its bar, saves its level and adds it to pawns. The need class below runs about every 150 ticks.
-- Colonists indoors regain comfort, colonists outdoors lose it, and a colonist at rock bottom gets a bad thought.
-- Define the need at the top of the file, not in an event: a save made with the need needs the def to exist when it loads.

local NEED = "ComfortNeed_Comfort"

game.needs.define(NEED, {
  label = game.ui.translate("comfortneed_Label"),
  description = game.ui.translate("comfortneed_Description"),
  who = "colonists",
  base_level = 0.7,
  fall_per_day = 0.15,
})

-- Pure decision, so a test can call it: how much comfort changes in one interval.
local function change_for(indoors)
  if indoors then return 0.01 end
  return -0.005
end

game.classes.define("need", NEED, {
  interval = function(pawn, level)
    local indoors = false
    local map = game.pawns.map(pawn)
    if map then
      local cell = game.things.info(pawn)
      indoors = game.maps.roof(map, cell.x, cell.z) ~= nil
    end
    local next_level = math.max(0, math.min(1, level + change_for(indoors)))
    if next_level ~= level then game.pawns.set_need(pawn, NEED, next_level) end
    if next_level <= 0.05 then game.pawns.add_thought(pawn, "SleptOutside") end
  end,
})

game.interop.publish("comfortneed", "1.0.0", { change_for = change_for, need = NEED })
