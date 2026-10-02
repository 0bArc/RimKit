-- recruit/capture for humans; draft entities like animals

local claimed = {}

local function is_human_colonist(h)
  if not h then
    return false
  end
  if anomaly.is_entity(h) == true then
    return false
  end
  return rim.pawn.is_humanlike(h) == true
end

local function claim_selected()
  local h = rim.find.selected()
  if not h then
    rim.message("[TameAnomalies] select an entity first")
    return
  end
  if anomaly.is_entity(h) ~= true then
    rim.message("[TameAnomalies] select an anomaly, not a colonist")
    return
  end
  anomaly.recruit(h)
  claimed[h] = true
  rim.message("[TameAnomalies] recruited " .. tostring(h) .. " (command like a drafted animal)")
end

local function release_all()
  local list = anomaly.list_on_map() or {}
  local n = 0
  if type(list) == "table" then
    for _, h in pairs(list) do
      if type(h) == "number" then
        anomaly.release_to_hostile(h)
        n = n + 1
      end
    end
  end
  claimed = {}
  rim.message("[TameAnomalies] released " .. tostring(n))
end

-- float menu: human hauler only
ui.on_map_float_menu(function(ctx)
  local clicked = ctx.clicked
  local hauler = ctx.hauler
  if not clicked or not hauler then
    return nil
  end

  if not is_human_colonist(hauler) then
    return nil
  end
  if anomaly.is_entity(clicked) ~= true then
    return nil
  end
  if clicked == hauler then
    return nil
  end

  local opts = {}
  local already = rim.pawn.faction_is_player(clicked) == true

  if not already then
    opts[#opts + 1] = {
      label = "Recruit to colony (RimKit)",
      on_click = function()
        anomaly.recruit(clicked)
        claimed[clicked] = true
        rim.message("[TameAnomalies] recruited via float menu")
      end,
    }
  end

  local platform = anomaly.find_platform(hauler, clicked)
  if not platform or platform == 0 then
    opts[#opts + 1] = {
      label = "Cannot capture: no holding platform",
      disabled = true,
    }
  else
    opts[#opts + 1] = {
      label = "Knock out (anesthetic)",
      on_click = function()
        anomaly.knock_out(clicked)
        rim.message("[TameAnomalies] knocked out")
      end,
    }
    opts[#opts + 1] = {
      label = "Capture (holding platform)",
      on_click = function()
        local ok = anomaly.start_capture(hauler, clicked, platform)
        rim.message(ok and "[TameAnomalies] capture started" or "[TameAnomalies] capture failed (down entity first?)")
      end,
    }
  end

  if #opts == 0 then
    return nil
  end
  return opts
end)

rim.on_load(function()
  rim.log("[TameAnomalies] humans recruit/capture; entities = commandable army only")
end)

rim.on_tick(function()
  if input.binding_just_pressed("RimLua_TameAnomalyClaim") then
    claim_selected()
  end
  if input.binding_just_pressed("RimLua_TameAnomalyRelease") then
    release_all()
  end
end)
