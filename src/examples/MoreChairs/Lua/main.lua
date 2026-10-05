-- MoreChairs
-- Three new chairs, defined entirely in Lua. Defs/chairs.lua describes the chairs (rimkit mod sync turns it into the XML the game
-- reads) and this file holds the behavior:
--   * sitting: a colonist who finishes a meal while sitting in a comfortable chair (comfort 0.8 or more) gets a short mood boost,
--   * a check at startup that every texture a def names is really in the mod, so a typo shows up in the log, not as a pink square.
-- The game itself makes colonists sit on the chairs, because the defs say building.isSittable.

local COMFY_COMFORT = 0.8
local THOUGHT = "MoreChairs_ComfySeat"

local CHAIRS = {
  MoreChairs_Stool = { texture = "Things/Building/Furniture/MoreChairs_Stool", comfort = 0.45 },
  MoreChairs_Rocker = { texture = "Things/Building/Furniture/MoreChairs_Rocker", comfort = 0.85 },
  MoreChairs_Gamer = { texture = "Things/Building/Furniture/MoreChairs_Gamer", comfort = 0.92 },
}

-- The comfortable chair a pawn is sitting in, as its def name, or nil. A pawn sits on the chair's own cell.
local function comfy_chair_under(pawn)
  local map = game.pawns.map(pawn)
  if not map or map == 0 then return nil end
  local at = game.things.info(pawn)
  for _, thing in ipairs(game.maps.things_at(map, at.x, at.z)) do
    local def = game.things.info(thing).def
    local chair = CHAIRS[def]
    if chair and chair.comfort >= COMFY_COMFORT then return def end
  end
  return nil
end

-- A meal is done when its Ingest job succeeds. A pawn who ate in a comfy chair gets the thought.
local function on_job_ended(e)
  local job = type(e.job) == "table" and e.job.def or e.job
  if job ~= "Ingest" or tostring(e.condition) ~= "Succeeded" then return false end
  if not comfy_chair_under(e.pawn) then return false end
  game.pawns.add_thought(e.pawn, THOUGHT)
  return true
end

game.events.on("job.ended", on_job_ended)

-- Returns the chairs whose texture is missing. A plain function, so a test can call it.
local function missing_textures()
  local missing = {}
  for def, chair in pairs(CHAIRS) do
    local info = game.graphics.texture_info(chair.texture)
    if not (info and info.exists) then missing[#missing + 1] = def end
  end
  table.sort(missing)
  return missing
end

game.events.on_load(function()
  local missing = missing_textures()
  if #missing == 0 then
    game.log.info("[MoreChairs] 3 chairs ready")
  else
    game.log.error("[MoreChairs] missing texture for " .. table.concat(missing, ", "))
  end
end)

game.interop.publish("morechairs", "1.1.0", { missing_textures = missing_textures, on_job_ended = on_job_ended, comfy_chair_under = comfy_chair_under })
