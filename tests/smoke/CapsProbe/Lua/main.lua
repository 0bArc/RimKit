-- Declares only "files". Reflection and hooks must be refused with RK4001, and the result is published for the smoke suite.
local function code_of(fn)
  local ok, err = pcall(fn)
  if ok then return "allowed" end
  return tostring(err):match("RK%d+") or "other"
end

local results = {
  reflect = code_of(function() return game.reflect.type("Verse.Thing") end),
  hooks = code_of(function() game.hooks.postfix("Verse.Thing", "get_MaxHitPoints", function() end) end),
  eval = code_of(function() return game.dev.eval("1") end),
}
game.interop.publish("rk_caps_probe", "1.0.0", { results = function() return results end })
