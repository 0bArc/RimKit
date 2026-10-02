--[[
  Jobs Test. Harmony + RimKit + this mod. Select colonist, F10.
]]

local TICK_LOG_EVERY = 600
local tick_counter = 0
local window_opened = false

local function vlog(msg)
  if config.get_bool("verbose") then
    log.info("[JobsTest] " .. msg)
  end
end

local function wrap_handle(h)
  if not h or h == 0 then return nil end
  if type(h) == "userdata" then return h end
  return rim.wrap(h)
end

-- selected_pawn can lag; re-query on click
local function need_pawn()
  local h = rim.find and rim.find.selected and rim.find.selected()
  local p = wrap_handle(h)
  if p then return p end
  if selected_pawn then return selected_pawn end
  h = rim.find and rim.find.any_player_pawn and rim.find.any_player_pawn()
  p = wrap_handle(h)
  if p then
    log.info("[JobsTest] fallback colonist handle=" .. tostring(h))
    return p
  end
  ui.message("[JobsTest] click a colonist on the map (selection ring)")
  log.info("[JobsTest] FAIL: no selected_pawn")
  return nil
end

local function need_map()
  local map = game.current_map()
  if not map then
    ui.message("[JobsTest] no map")
    return nil
  end
  return map
end

local function status_body()
  local p = selected_pawn
  local c = control.get()
  local pos = p and p.position
  return table.concat({
    "F10 = this menu",
    "1) Click colonist  2) Control  3) Right-click ground (menu closes)",
    "selected=" .. (p and tostring(p.name) or "nil"),
    "controlled=" .. (c and tostring(c.name) or "nil"),
    "pos=" .. (pos and (tostring(pos.x) .. "," .. tostring(pos.z)) or "?"),
    "moving=" .. tostring(p and p.is_moving),
    "tick=" .. tostring(game.tick()),
    "Use LIVE selected line above (body text freezes at open)",
  }, "\n")
end

local function open_spawner_menu()
  local map = need_map()
  if not map then return end
  local p = need_pawn()
  local x, z
  if p and p.position then
    x, z = p.position.x + 2, p.position.z
  else
    ui.message("[JobsTest] select pawn first (spawn near them)")
    return
  end

  ui.float_menu({
    {
      label = "Spawn hostile gunner",
      action = function()
        local np = map:spawn_pawn("Mercenary_Gunner", "hostile", x, z)
        log.info("[JobsTest] spawn hostile => " .. tostring(np and np.name))
        ui.message("[JobsTest] hostile: " .. tostring(np and np.name or "FAIL"))
      end,
    },
    {
      label = "Spawn villager (neutral)",
      action = function()
        local np = map:spawn_pawn("Villager", "neutral", x, z)
        ui.message("[JobsTest] villager: " .. tostring(np and np.name or "FAIL"))
      end,
    },
    {
      label = "Spawn muffalo",
      action = function()
        local np = map:spawn_pawn("Muffalo", "neutral", x, z)
        ui.message("[JobsTest] muffalo: " .. tostring(np and np.name or "FAIL"))
      end,
    },
    {
      label = "Spawn pile: Steel x50",
      action = function()
        map:spawn_thing("Steel", x, z, 50)
        ui.message("[JobsTest] steel pile")
      end,
    },
    {
      label = "Spawn pile: MealSimple x20",
      action = function()
        map:spawn_thing("MealSimple", x, z, 20)
        ui.message("[JobsTest] meals")
      end,
    },
    {
      label = "Spawn JobsToken x5",
      action = function()
        if not defs.exists("ThingDef", "RimLua_JobsToken") then
          ui.message("[JobsTest] token Def missing (sync+restart)")
          return
        end
        map:spawn_thing("RimLua_JobsToken", x, z, 5)
        ui.message("[JobsTest] tokens")
      end,
    },
  })
end

local function open_debug_window()
  log.info("[JobsTest] opening fun menu")
  ui.window({
    title = "RimKit Fun / Control",
    body = status_body(),
    buttons = {
      {
        label = "Control selected (draft + right-click walk)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          p:draft(true)
          local ok = control.claim(p, true)
          log.info("[JobsTest] controlling " .. tostring(p.name) .. " ok=" .. tostring(ok))
          ui.message("[JobsTest] controlling " .. tostring(p.name) .. " - right-click map to walk")
        end,
      },
      {
        label = "Release control",
        on_click = function()
          control.clear()
          local p = need_pawn()
          if p then p:draft(false) end
          ui.message("[JobsTest] control cleared")
        end,
      },
      {
        label = "Pickup gun (AssaultRifle)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local ok = p:equip_weapon("Gun_AssaultRifle")
          log.info("[JobsTest] equip_weapon => " .. tostring(ok))
          ui.message(ok and "[JobsTest] assault rifle equipped" or "[JobsTest] equip FAIL")
        end,
      },
      {
        label = "Pickup revolver",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local ok = p:equip_weapon("Gun_Revolver")
          ui.message(ok and "[JobsTest] revolver equipped" or "[JobsTest] equip FAIL")
        end,
      },
      {
        label = "Shoot nearest hostile (job)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          p:equip_weapon("Gun_AssaultRifle")
          local n = p:shoot_hostiles(false)
          log.info("[JobsTest] shoot_hostiles jobs n=" .. tostring(n))
          if n == 0 then
            ui.message("[JobsTest] no hostiles - spawn one first")
          else
            ui.message("[JobsTest] attacking nearest hostile")
          end
        end,
      },
      {
        label = "Kill all hostiles (INSTANT)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          p:equip_weapon("Gun_AssaultRifle")
          local n = p:shoot_hostiles(true)
          log.info("[JobsTest] instant massacre n=" .. tostring(n))
          ui.message("[JobsTest] killed " .. tostring(n) .. " hostiles")
        end,
      },
      {
        label = "Spawner menu...",
        on_click = function()
          open_spawner_menu()
        end,
      },
      {
        label = "Wander nearby",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local ok = p:wander(14)
          ui.message(ok and "[JobsTest] wandering" or "[JobsTest] wander FAIL (no cell)")
        end,
      },
      {
        label = "Walk to Soil cell",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local map = p.map or need_map()
          if not map then return end
          local cells = map:find_cells({ terrain = "Soil", limit = 40 })
          if not cells or #cells == 0 then
            ui.message("[JobsTest] no Soil cells")
            return
          end
          local pos = p.position
          local best, bestD
          for i = 1, #cells do
            local c = cells[i]
            local dx = c.x - (pos and pos.x or c.x)
            local dz = c.z - (pos and pos.z or c.z)
            local d = dx * dx + dz * dz
            if not bestD or d < bestD then
              bestD = d
              best = c
            end
          end
          local ok = p:walk_to(best.x, best.z)
          ui.message(ok and ("[JobsTest] walking to " .. best.x .. "," .. best.z) or "[JobsTest] walk FAIL (unreachable)")
        end,
      },
      {
        label = "Stop",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          p:stop()
          ui.message("[JobsTest] stopped")
        end,
      },
      {
        label = "Give ComponentIndustrial x3",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          p:give_item("ComponentIndustrial", 3)
          ui.message("[JobsTest] components given")
        end,
      },
      {
        label = "Start wave job",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local ok = p:start_job("wave")
          ui.message(ok and "[JobsTest] wave started" or "[JobsTest] wave FAIL")
        end,
      },
      {
        label = "Refresh menu",
        on_click = function()
          open_debug_window()
        end,
      },
    },
  })
end

config.register("verbose", {
  type = "bool",
  default = true,
  label = "Jobs Test: verbose logs",
})

config.register("auto_window", {
  type = "bool",
  default = true,
  label = "Jobs Test: auto-open fun menu",
})

jobs.register("wave", {
  can_do = function(pawn)
    return pawn.is_colonist == true
  end,
  execute = function(pawn)
    log.info("[JobsTest] wave.execute " .. tostring(pawn.name))
    ui.message("[JobsTest] " .. tostring(pawn.name) .. " waved")
    return true
  end,
})

local dig_ticks = {}
jobs.register("dig_log", {
  can_do = function(pawn)
    return pawn.is_humanlike == true
  end,
  execute = function(pawn)
    local h = pawn.handle
    dig_ticks[h] = (dig_ticks[h] or 0) + 1
    local n = dig_ticks[h]
    if n >= 90 then
      dig_ticks[h] = nil
      return true
    end
    return false
  end,
})

function on_pawn_spawned(pawn)
  log.info("[JobsTest] spawned " .. tostring(pawn.name))
end

events.on("pawn_died", function(pawn)
  log.info("[JobsTest] died " .. tostring(pawn.name))
end)

rim.on_load(function()
  log.info("[JobsTest] LOADED - F10 opens fun menu")
  player.send_message("[JobsTest] F10 = fun menu. Click colonist, then Control.")
  ui.letter("Jobs Test", "F10 opens the fun menu. Select a colonist, Control, right-click to walk.")

  if config.get_bool("auto_window") then
    timer.after(120, function()
      if not window_opened then
        window_opened = true
        open_debug_window()
      end
    end)
  end
end)

rim.on_tick(function()
  tick_counter = tick_counter + 1
  if tick_counter % TICK_LOG_EVERY == 0 then
    vlog("heartbeat selected=" .. tostring(selected_pawn and selected_pawn.name))
  end

  if input.binding_just_pressed("RimLua_JobsOpenDebug") then
    log.info("[JobsTest] F10 - open menu")
    open_debug_window()
  end
end)
