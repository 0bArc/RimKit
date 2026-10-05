-- RuleSet
-- A bundle of balance rules applied from one table, with one checkbox in the mod options to turn them all off again.
-- Shows game.defs.set and game.defs.restore (changes are journaled, so they can be undone), an options page, and settings.

local RULES = {
  { kind = "ThingDef", def = "Steel", path = "stackLimit", value = 300 },
  { kind = "ThingDef", def = "Silver", path = "stackLimit", value = 5000 },
  { kind = "ThingDef", def = "WoodLog", path = "stackLimit", value = 300 },
}

local function apply()
  for _, r in ipairs(RULES) do game.defs.set(r.kind, r.def, r.path, r.value) end
  return #RULES
end

local function revert()
  for _, r in ipairs(RULES) do game.defs.restore(r.kind, r.def, r.path) end
  return #RULES
end

game.options.page("rimkit.ruleset", game.ui.translate("ruleset_Title"), {
  { key = "ruleset.enabled", type = "bool", label = game.ui.translate("ruleset_Enabled"), default = true },
}, function(change)
  if change.key ~= "ruleset.enabled" then return end
  if change.value then apply() else revert() end
end)

game.events.on_load(function()
  if game.config.get_bool("ruleset.enabled") then apply() end
end)

game.interop.publish("ruleset", "1.0.0", { apply = apply, revert = revert, rules = function() return RULES end })
