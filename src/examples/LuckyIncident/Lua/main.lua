-- LuckyIncident
-- A new incident whose behavior is written in Lua. The Def in Defs/Incident.xml names the class "lucky_day";
-- RimKit runs the functions below when the storyteller considers the incident.
-- Shows game.classes.define for an incident, dropping items, and a letter.

game.classes.define("incident", "lucky_day", {
  -- Only fire when there is someone to be lucky.
  can_fire = function(parms)
    return #game.query.pawns({ map = game.maps.current(), faction = "player" }) > 0
  end,

  -- Returning true tells the game the incident happened.
  execute = function(parms)
    local map = game.maps.current()
    local cell = game.maps.drop_spot(map)
    game.things.spawn_at("Silver", map, cell.x, cell.z, { count = 150 })
    game.hud.letter(
      game.ui.translate("luckyincident_Label"),
      game.ui.translate("luckyincident_Text"),
      { game.ui.translate("luckyincident_Thanks") },
      "lucky_day")
    return true
  end,
})
