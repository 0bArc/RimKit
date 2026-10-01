--[[
  Jobs Test — RimLuaKit Phase 1–3 smoke mod.
  Enable: Harmony, RimLuaKit, Jobs Test.
  In colony: wait ~2s for debug window, or press F8.
]]

local TICK_LOG_EVERY = 600
local tick_counter = 0
local window_opened = false

local function vlog(msg)
  if config.get_bool("verbose") then
    log.info("[JobsTest] " .. msg)
  end
end

local function status_body()
  local map = game.current_map()
  local pawn = selected_pawn
  local lines = {
    "F8 = reopen this window",
    "Options → Mod settings → RimLuaKit for config",
    "tick=" .. tostring(game.tick()),
    "map=" .. (map and ("ok nut=" .. tostring(map.nutrition)) or "nil"),
    "selected=" .. (pawn and tostring(pawn.name) or "nil"),
    "verbose=" .. tostring(config.get_bool("verbose")),
    "token_def=" .. tostring(defs.exists("ThingDef", "RimLua_JobsToken")),
  }
  return table.concat(lines, "\n")
end

local function need_pawn()
  local p = selected_pawn
  if not p then
    ui.message("[JobsTest] select a pawn first")
    log.info("[JobsTest] FAIL: no selected_pawn")
    return nil
  end
  return p
end

local function open_debug_window()
  log.info("[JobsTest] opening debug window")
  ui.window({
    title = "Jobs Test (RimLuaKit)",
    body = status_body(),
    buttons = {
      {
        label = "Start wave job on selected",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local ok = p:start_job("wave")
          log.info("[JobsTest] start_job wave => " .. tostring(ok))
          ui.message("[JobsTest] wave job => " .. tostring(ok))
        end,
      },
      {
        label = "Start dig-log job (long)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local ok = jobs.start(p, "dig_log")
          log.info("[JobsTest] dig_log => " .. tostring(ok))
          ui.message("[JobsTest] dig_log => " .. tostring(ok))
        end,
      },
      {
        label = "Give Component x3",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          p:give_item("Component", 3)
          log.info("[JobsTest] gave Component x3 to " .. tostring(p.name))
          ui.message("[JobsTest] gave components")
        end,
      },
      {
        label = "Give JobsToken (if Def loaded)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          if not defs.exists("ThingDef", "RimLua_JobsToken") then
            ui.message("[JobsTest] RimLua_JobsToken missing — sync+restart")
            log.info("[JobsTest] FAIL: RimLua_JobsToken not in DefDatabase")
            return
          end
          p:give_item("RimLua_JobsToken", 1)
          log.info("[JobsTest] gave RimLua_JobsToken")
        end,
      },
      {
        label = "Find Soil cells (limit 8)",
        on_click = function()
          local map = game.current_map()
          if not map then
            ui.message("[JobsTest] no map")
            return
          end
          local cells = map:find_cells({ terrain = "Soil", limit = 8 })
          log.info("[JobsTest] find_cells count=" .. tostring(#cells))
          for i, c in ipairs(cells) do
            vlog("  cell[" .. i .. "]=" .. tostring(c.x) .. "," .. tostring(c.z))
          end
          ui.message("[JobsTest] cells=" .. tostring(#cells))
        end,
      },
      {
        label = "Faction check (selected)",
        on_click = function()
          local p = need_pawn()
          if not p then return end
          local pf = game.player_faction()
          local f = p.faction
          local hostile = (f and pf) and f:is_hostile(pf) or false
          log.info("[JobsTest] faction=" .. tostring(f and f.name) .. " hostile_to_player=" .. tostring(hostile))
          ui.message("[JobsTest] hostile=" .. tostring(hostile))
        end,
      },
      {
        label = "Float menu demo",
        on_click = function()
          ui.float_menu({
            {
              label = "Log hello",
              action = function()
                log.info("[JobsTest] float: hello")
                ui.message("[JobsTest] float hello")
              end,
            },
            {
              label = "Letter ping",
              action = function()
                ui.letter("Jobs Test", "Float menu letter OK")
                log.info("[JobsTest] float: letter sent")
              end,
            },
            {
              label = "Toggle verbose config",
              action = function()
                local v = not config.get_bool("verbose")
                config.set("verbose", v)
                log.info("[JobsTest] verbose => " .. tostring(v))
                ui.message("[JobsTest] verbose=" .. tostring(v))
              end,
            },
          })
        end,
      },
      {
        label = "Write runtime ThingDef XML",
        on_click = function()
          defs.register_thing({
            defName = "RimLua_RuntimeProbe",
            label = "runtime probe",
            description = "Written at runtime; restart to load.",
            package_id = "stratware.jobs",
            stackLimit = 10,
          })
          ui.message("[JobsTest] wrote RimLua_RuntimeProbe.xml — restart game")
        end,
      },
      {
        label = "Refresh status (reopen)",
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
  label = "Jobs Test: auto-open debug window",
})

jobs.register("wave", {
  can_do = function(pawn)
    local ok = pawn.is_colonist == true
    vlog("wave.can_do " .. tostring(pawn.name) .. " => " .. tostring(ok))
    return ok
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
    if n == 1 or n % 30 == 0 then
      log.info("[JobsTest] dig_log tick=" .. n .. " pawn=" .. tostring(pawn.name))
    end
    if n >= 90 then
      dig_ticks[h] = nil
      log.info("[JobsTest] dig_log DONE " .. tostring(pawn.name))
      ui.message("[JobsTest] dig_log done")
      return true
    end
    return false
  end,
})

function on_pawn_spawned(pawn)
  log.info("[JobsTest] on_pawn_spawned " .. tostring(pawn.name) .. " colonist=" .. tostring(pawn.is_colonist))
  if pawn.is_colonist and config.get_bool("verbose") then
    ui.message("[JobsTest] spawned " .. tostring(pawn.name))
  end
end

events.on("pawn_died", function(pawn)
  log.info("[JobsTest] pawn_died " .. tostring(pawn.name))
end)

rim.on_load(function()
  log.info("[JobsTest] LOADED OK package=stratware.jobs")
  player.send_message("[JobsTest] loaded — F8 opens debug UI")
  ui.letter("Jobs Test", "RimLuaKit test mod ready. Press F8 or wait for auto window.")

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
    vlog("heartbeat tick=" .. tostring(game.tick()) .. " selected=" .. tostring(selected_pawn and selected_pawn.name or "nil (click a colonist)"))
  end

  if input.binding_just_pressed("RimLua_JobsOpenDebug") then
    log.info("[JobsTest] F8 pressed — open window")
    open_debug_window()
  end
end)
