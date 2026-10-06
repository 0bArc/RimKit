-- Tame Anomalies
-- Recruit anomaly entities into a commandable army, capture them on holding platforms, release them again.
--
--   F6               recruit the selected entity
--   F7               release every entity this mod recruited
--   right click      with a colonist selected: recruit, knock out, capture, or release an entity
--
-- An entity is "ours" while game.pawns.is_controllable is true. The host keeps that state in the save
-- (GameComponent_PawnControl), so this mod stores nothing of its own.
-- Needs the Anomaly DLC. Strings live in Languages/<lang>/Keyed/TameAnomalies.lua.

local rk = require("rimkit")
rk.assert_api(0)

local KEY_RECRUIT = "RimLua_TameAnomalyClaim"
local KEY_RELEASE = "RimLua_TameAnomalyRelease"

local function say(key, ...)
  game.ui.message(game.ui.translate(key, ...))
end

local function is_entity(h)
  return game.anomaly.is_entity(h) == true
end

local function is_ours(h)
  return game.pawns.is_controllable(h) == true
end

-- A real colonist: not a prisoner, slave or entity. Controlled entities also count as colonists to the game.
local function is_colonist_hauler(h)
  return game.pawns.is_colonist(h) == true and not is_entity(h)
end

local function display_name(entity)
  local name = entity.name
  if name == nil or name == "" then
    return game.ui.translate("TameAnomalies_Unnamed")
  end
  return name
end

-- The one place that recruits. Used by the hotkey and the right-click menu.
local function recruit(h)
  if not is_entity(h) then
    say("TameAnomalies_NotAnEntity")
    return false
  end
  if is_ours(h) then
    say("TameAnomalies_AlreadyOurs")
    return false
  end
  local entity = rim.wrap_entity(h)
  if entity:recruit() ~= true then
    say("TameAnomalies_RecruitFailed", display_name(entity))
    return false
  end
  say("TameAnomalies_Recruited", display_name(entity))
  return true
end

local function release(h)
  local entity = rim.wrap_entity(h)
  local name = display_name(entity)
  if entity:release() ~= true then
    say("TameAnomalies_ReleaseFailed", name)
    return false
  end
  say("TameAnomalies_Released", name)
  return true
end

local function recruit_selected()
  local h = game.selection.first()
  if not h then
    say("TameAnomalies_NoSelection")
    return
  end
  recruit(h)
end

local function release_all()
  local released = 0
  for _, entity in ipairs(game.anomalies:list() or {}) do
    if is_ours(entity.handle) and entity:release() == true then
      released = released + 1
    end
  end
  if released == 0 then
    say("TameAnomalies_NothingToRelease")
  else
    say("TameAnomalies_ReleasedCount", released)
  end
end

-- Right-click menu. Only a real colonist can act on an entity.
game.ui.on_map_float_menu(function(ctx)
  local clicked, hauler = ctx.clicked, ctx.hauler
  if not clicked or not hauler or clicked == hauler then
    return nil
  end
  if not is_colonist_hauler(hauler) or not is_entity(clicked) then
    return nil
  end

  if is_ours(clicked) then
    return {
      { label = game.ui.translate("TameAnomalies_MenuRelease"), on_click = function() release(clicked) end },
    }
  end

  local options = {}
  -- Entities that already belong to the player (for example a captured one) are not offered for recruiting.
  if game.pawns.faction_is_player(clicked) ~= true then
    options[#options + 1] = {
      label = game.ui.translate("TameAnomalies_MenuRecruit"),
      on_click = function() recruit(clicked) end,
    }
  end

  local platform = game.anomaly.find_platform(hauler, clicked)
  if not platform or platform == 0 then
    options[#options + 1] = { label = game.ui.translate("TameAnomalies_MenuNoPlatform"), disabled = true }
  else
    options[#options + 1] = {
      label = game.ui.translate("TameAnomalies_MenuKnockOut"),
      on_click = function()
        local entity = rim.wrap_entity(clicked)
        if entity:knock_out() == true then
          say("TameAnomalies_KnockedOut", display_name(entity))
        else
          say("TameAnomalies_KnockOutFailed", display_name(entity))
        end
      end,
    }
    options[#options + 1] = {
      label = game.ui.translate("TameAnomalies_MenuCapture"),
      on_click = function()
        if game.anomaly.start_capture(hauler, clicked, platform) == true then
          say("TameAnomalies_CaptureStarted")
        else
          say("TameAnomalies_CaptureFailed")
        end
      end,
    }
  end

  return #options > 0 and options or nil
end)

game.events.on_load(function()
  game.log.info("[TameAnomalies] ready, RimKit " .. tostring(rk.version))
  -- Hot reload while developing: with Development mode on, saving a file in this mod reloads its Lua in the running game.
  if game.dev.mode() then
    game.dev.watch(true)
    game.log.info("[TameAnomalies] hot reload is on")
  end
end)

game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY_RECRUIT) then
    recruit_selected()
  end
  if game.input.binding_just_pressed(KEY_RELEASE) then
    release_all()
  end
end)
