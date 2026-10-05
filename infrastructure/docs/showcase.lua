-- RimKit showcase snippets
-- Copy any block into a mod Lua/main.lua (needs Harmony + RimKit).
-- Prefer bound tables: rim.pawn.*, anomaly.*, ui.*, data.*
-- rim.invoke is escape-only: api.list / reflect.*

------------------------------------------------------------------------
-- 1) Hello / lifecycle
------------------------------------------------------------------------

game.events.on_load(function()
  game.log.info("[Showcase] loaded")
  game.ui.message("[Showcase] RimKit is running")
end)

game.events.on_tick(function()
  -- fires periodically while playing
end)

------------------------------------------------------------------------
-- 2) Find / selected pawn (handles)
------------------------------------------------------------------------

local h = game.selection.first()
if h then
  game.ui.message("selected " .. tostring(game.pawns.name(h)))
  game.ui.message("humanlike=" .. tostring(game.pawns.is_humanlike(h)))
  game.ui.message("player faction=" .. tostring(game.pawns.faction_is_player(h)))
end

local map = game.maps.current()
if map then
  game.ui.message("map nutrition=" .. tostring(game.maps.nutrition(map)))
end

------------------------------------------------------------------------
-- 3) OO pawn (userdata) + events
------------------------------------------------------------------------

function on_pawn_spawned(pawn)
  if pawn.is_colonist then
    log.info("colonist spawned: " .. tostring(pawn.name))
  end
end

events.on("pawn_died", function(pawn)
  log.info("died: " .. tostring(pawn.name))
end)

------------------------------------------------------------------------
-- 4) Harmony prefix (Experimental; ctx table)
------------------------------------------------------------------------

rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(ctx)
  local pawn = ctx.pawn or ctx.instance
  -- return true = continue vanilla; false = skip
  if not pawn or not pawn.is_colonist then
    return true
  end
  log.info("GetFood think: " .. tostring(pawn.name) .. " hunger=" .. tostring(pawn.hunger))
  return true
end

-- same idea, string form:
-- rim.events.prefix["RimWorld.JobGiver_GetFood.TryGiveJob"] = function(ctx) return true end

------------------------------------------------------------------------
-- 5) Save data + panel UI
------------------------------------------------------------------------

local PACK = "yourname.mymod"

game.events.on_load(function()
  local n = tonumber(game.data.get(PACK, "opens") or "0") or 0
  game.data.set(PACK, "opens", tostring(n + 1))
end)

game.ui.panel({
  title = "RimKit",
  body = "Bound APIs, sandboxed Lua, no C# required for gameplay scripts.",
  checks = { "Verbose" },
  list = { "game.data.get/set", "game.ui.panel", "rim.pawn.*", "anomaly.*" },
})

------------------------------------------------------------------------
-- 6) Keybind (needs Defs/KeyBindings/*.xml for the defName)
------------------------------------------------------------------------

game.events.on_tick(function()
  if game.input.binding_just_pressed("YourMod_OpenPanel") then
    game.ui.panel({ title = "Hotkey", body = "Pressed." })
  end
end)

------------------------------------------------------------------------
-- 7) Anomaly kit (Experimental): game.anomalies + RimEntity
------------------------------------------------------------------------
local rk = require("rimkit")
rk.assert_api(0)

-- Preferred: collection OO
local entity = game.anomalies:get("Revenant")
if entity then
  entity:recruit()
end

-- Or selected handle + wrap
local h = game.selection.first()
if h and game.anomaly.is_entity(h) == true then
  rim.wrap_entity(h):recruit()
end

-- Flat alias still supported
-- game.anomaly.recruit(h)

-- Right-click options (human colonist selected, entity under cursor)
game.ui.on_map_float_menu(function(ctx)
  local clicked, hauler = ctx.clicked, ctx.hauler
  if not clicked or not hauler then return nil end
  if game.pawns.is_humanlike(hauler) ~= true then return nil end
  if game.anomaly.is_entity(hauler) == true then return nil end
  if game.anomaly.is_entity(clicked) ~= true then return nil end

  local opts = {}
  if game.pawns.faction_is_player(clicked) ~= true then
    opts[#opts + 1] = {
      label = "Recruit (RimKit)",
      on_click = function()
        rim.wrap_entity(clicked):recruit()
      end,
    }
  end

  local platform = game.anomaly.find_platform(hauler, clicked)
  if platform and platform ~= 0 then
    opts[#opts + 1] = {
      label = "Capture (holding platform)",
      on_click = function()
        game.anomaly.start_capture(hauler, clicked, platform)
      end,
    }
  end
  return (#opts > 0) and opts or nil
end)

------------------------------------------------------------------------
-- 8) Health / work / building (handles)
------------------------------------------------------------------------

-- game.pawns.has_hediff(h, "Flu")
-- game.pawns.tend(h, 0.7)
-- game.work.set_priority(h, "Doctor", 1)
-- game.buildings.set_power(h, true)

------------------------------------------------------------------------
-- 9) Escape hatch (not for gameplay ops)
------------------------------------------------------------------------

-- rim.invoke("api.list")
-- rim.invoke("reflect.get", { h = h, member = "Label" })
-- denied: rim.invoke("pawn.is_humanlike", { h = h })  -- use game.pawns.is_humanlike(h)

------------------------------------------------------------------------
-- 10) meta.lua (identity; rimkit mod sync -> About.xml)
------------------------------------------------------------------------

--[[
local meta = require("host.metadata")
meta.name = "My Mod"
meta.author = "Team Stratware.win"
meta.package_id = "yourname.mymod"
meta.version = "1.6"
meta.description = "Short description."
meta.depends = {
  { id = "brrainz.harmony", name = "Harmony",
    steam = "steam://url/CommunityFilePage/2009463077" },
  { id = "stratware.rimkit", name = "RimKit",
    steam = "steam://url/CommunityFilePage/3811629229",
    download = "https://steamcommunity.com/sharedfiles/filedetails/?id=3811629229" },
}
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
]]
