local FOOD_THRESHOLD = 10.0
local EMERGENCY_HUNGER = 0.12

rim.on_load(function()
  log.info("[HelloLua] LOADED OK")
  player.send_message("[HelloLua] LOADED OK")
end)

function on_pawn_spawned(pawn)
  if pawn.is_colonist then
    log.info("[HelloLua] colonist spawned: " .. tostring(pawn.name))
  end
end

events.on("pawn_died", function(pawn)
  log.info("[HelloLua] died: " .. tostring(pawn.name))
end)

rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(pawn)
  local name = tostring(pawn.name or "?")
  local map = pawn.map
  local hunger = pawn.hunger or 1
  local nutrition = map and map.nutrition or 0

  log.info(string.format(
    "[HelloLua] THINK GetFood pawn=%s colonist=%s hunger=%.2f nutrition=%.1f",
    name, tostring(pawn.is_colonist), hunger, nutrition
  ))

  if not pawn.is_humanlike then
    return true
  end
  if not pawn.is_colonist then
    return true
  end
  if not map then
    return true
  end

  if nutrition < FOOD_THRESHOLD and hunger <= EMERGENCY_HUNGER then
    return true
  end

  if nutrition < FOOD_THRESHOLD then
    log.info(string.format("[HelloLua] BLOCK food job (nutrition %.1f < %.1f)", nutrition, FOOD_THRESHOLD))
    player.send_message("[HelloLua] blocked food job for " .. name)
    return false
  end

  return true
end
