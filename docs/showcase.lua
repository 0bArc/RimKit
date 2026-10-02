-- RimKit showcase snippets
-- Copy any block into a mod Lua/main.lua (needs Harmony + RimKit).
-- Prefer bound tables: rim.pawn.*, anomaly.*, ui.*, data.*
-- rim.invoke is escape-only: api.list / reflect.*

------------------------------------------------------------------------
-- 1) Hello / lifecycle
------------------------------------------------------------------------

rim.on_load(function()
  rim.log("[Showcase] loaded")
  rim.message("[Showcase] RimKit is running")
end)

rim.on_tick(function()
  -- fires periodically while playing
end)

------------------------------------------------------------------------
-- 2) Find / selected pawn (handles)
------------------------------------------------------------------------

local h = rim.find.selected()
if h then
  rim.message("selected " .. tostring(rim.pawn.name(h)))
  rim.message("humanlike=" .. tostring(rim.pawn.is_humanlike(h)))
  rim.message("player faction=" .. tostring(rim.pawn.faction_is_player(h)))
end

local map = rim.find.current_map()
if map then
  rim.message("map nutrition=" .. tostring(rim.map.nutrition(map)))
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
-- 4) Harmony prefix (JobGiver)
------------------------------------------------------------------------

rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(pawn)
  -- return true = skip vanilla (let them starve path continue)
  -- return false = block / your logic
  if not pawn.is_colonist then
    return true
  end
  log.info("GetFood think: " .. tostring(pawn.name) .. " hunger=" .. tostring(pawn.hunger))
  return true
end

-- same idea, string form:
-- rim.events.prefix["RimWorld.JobGiver_GetFood.TryGiveJob"] = function(pawn) return true end

------------------------------------------------------------------------
-- 5) Save data + panel UI
------------------------------------------------------------------------

local PACK = "yourname.mymod"

rim.on_load(function()
  local n = tonumber(data.get(PACK, "opens") or "0") or 0
  data.set(PACK, "opens", tostring(n + 1))
end)

ui.panel({
  title = "RimKit",
  body = "Bound APIs, sandboxed Lua, no C# required for gameplay scripts.",
  checks = { "Verbose" },
  list = { "data.get/set", "ui.panel", "rim.pawn.*", "anomaly.*" },
})

------------------------------------------------------------------------
-- 6) Keybind (needs Defs/KeyBindings/*.xml for the defName)
------------------------------------------------------------------------

rim.on_tick(function()
  if input.binding_just_pressed("YourMod_OpenPanel") then
    ui.panel({ title = "Hotkey", body = "Pressed." })
  end
end)

------------------------------------------------------------------------
-- 7) Anomaly: recruit / capture / float menu
------------------------------------------------------------------------

-- F-key style claim (select entity first)
local h = rim.find.selected()
if h and anomaly.is_entity(h) == true then
  anomaly.recruit(h)  -- draftable like an animal
end

-- Right-click options (human colonist selected, entity under cursor)
ui.on_map_float_menu(function(ctx)
  local clicked, hauler = ctx.clicked, ctx.hauler
  if not clicked or not hauler then return nil end
  if rim.pawn.is_humanlike(hauler) ~= true then return nil end
  if anomaly.is_entity(hauler) == true then return nil end
  if anomaly.is_entity(clicked) ~= true then return nil end

  local opts = {}
  if rim.pawn.faction_is_player(clicked) ~= true then
    opts[#opts + 1] = {
      label = "Recruit (RimKit)",
      on_click = function()
        anomaly.recruit(clicked)
      end,
    }
  end

  local platform = anomaly.find_platform(hauler, clicked)
  if platform and platform ~= 0 then
    opts[#opts + 1] = {
      label = "Capture (holding platform)",
      on_click = function()
        anomaly.start_capture(hauler, clicked, platform)
      end,
    }
  end
  return (#opts > 0) and opts or nil
end)

------------------------------------------------------------------------
-- 8) Health / work / building (handles)
------------------------------------------------------------------------

-- health.has_hediff(h, "Flu")
-- health.tend(h, 0.7)
-- work.set_priority(h, "Doctor", 1)
-- building.set_power(h, true)

------------------------------------------------------------------------
-- 9) Escape hatch (not for gameplay ops)
------------------------------------------------------------------------

-- rim.invoke("api.list")
-- rim.invoke("reflect.get", { h = h, member = "Label" })
-- denied: rim.invoke("pawn.is_humanlike", { h = h })  -- use rim.pawn.is_humanlike(h)

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
