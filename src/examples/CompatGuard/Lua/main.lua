-- CompatGuard
-- Works with another mod only when it is installed, and survives its absence. The example target is the WorkTab mod;
-- change PKG and TYPE to the mod and class you want to cooperate with.
-- Shows game.mods.active, game.mods.type_exists, game.hooks.in_mod (needs the "hooks" capability) and game.interop.

local PKG = "fluffy.worktab"
local TYPE = "WorkTab.PriorityManager"

local seen = 0

local function hook_other_mod()
  if not game.mods.active(PKG) then
    game.log.info("[CompatGuard] " .. PKG .. " is not installed, nothing to do")
    return false
  end
  if not game.mods.type_exists(PKG, TYPE) then
    game.log.info("[CompatGuard] " .. PKG .. " has no " .. TYPE .. " in this version, skipping")
    return false
  end
  game.hooks.in_mod(PKG, "postfix", TYPE, "Get", function(ctx) seen = seen + 1 end)
  game.log.info("[CompatGuard] hooked " .. TYPE)
  return true
end

game.events.on_load(hook_other_mod)

-- Other mods can ask how often the hook ran.
game.interop.publish("compatguard", "1.0.0", { hook = hook_other_mod, seen = function() return seen end })
