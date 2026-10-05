-- SmartPause
-- Pauses the game when something important happens. Each trigger has a checkbox in the mod options.
-- Shows named events, settings, the pause and message functions, and a function offered to other mods.

local TRIGGERS = {
  { key = "smartpause.raid", label = "smartpause_OptRaid" },
  { key = "smartpause.death", label = "smartpause_OptDeath" },
  { key = "smartpause.downed", label = "smartpause_OptDowned" },
  { key = "smartpause.research", label = "smartpause_OptResearch" },
}

for _, trigger in ipairs(TRIGGERS) do
  game.config.register(trigger.key, { type = "bool", default = true, label = game.ui.translate(trigger.label) })
end

local function pause(reason)
  if game.time.paused() then return false end
  game.time.set_paused(true)
  game.ui.message(game.ui.translate("smartpause_Paused", reason))
  return true
end

game.events.on("incident.fired", function(e)
  if e.incident == "RaidEnemy" and e.success and game.config.get_bool("smartpause.raid") then
    pause(game.ui.translate("smartpause_ReasonRaid"))
  end
end)

game.events.on("pawn.died", function(e)
  if game.config.get_bool("smartpause.death") and game.pawns.is_colonist(e.pawn) then
    pause(game.ui.translate("smartpause_ReasonDeath", e.pawn.name))
  end
end)

game.events.on("pawn.downed", function(e)
  if game.config.get_bool("smartpause.downed") and game.pawns.is_colonist(e.pawn) then
    pause(game.ui.translate("smartpause_ReasonDowned", e.pawn.name))
  end
end)

game.events.on("research.finished", function(e)
  if game.config.get_bool("smartpause.research") then
    pause(game.ui.translate("smartpause_ReasonResearch", e.project))
  end
end)

game.interop.publish("smartpause", "1.0.0", { pause = pause })
