-- CheerfulMood
-- A mood thought whose condition is written in Lua. The Def in Defs/Thought.xml gives the thought the class "clear_sky".
-- The game asks the state function for every colonist now and then: true shows the thought, false hides it.
-- Shows a thought worker, reading the weather of the pawn's own map, and a function offered to other mods.

local function is_clear(map)
  return game.maps.info(map).weather == "Clear"
end

game.classes.define("thought_worker", "clear_sky", {
  state = function(pawn)
    local map = game.pawns.map(pawn)
    if not map or map == 0 then return false end   -- 0: the pawn is not on a map (a world pawn)
    return is_clear(map)
  end,
})

game.interop.publish("cheerfulmood", "1.0.0", { is_clear = is_clear })
