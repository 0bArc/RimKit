-- An incident whose worker is a Lua class. rimkit mod sync turns this file into Defs/Incident.xml.
-- The class comes from the def mod extension, see game.classes.define("incident", "lucky_day", ...) in Lua/main.lua.
def("IncidentDef", "RimKitExample_LuckyDay", {
  label = "lucky day",
  category = "Misc",
  targetTags = { "Map_PlayerHome" },
  workerClass = "RimKit.IncidentWorker_Lua",
  baseChance = 0.6,
  minRefireDays = 12,
  modExtensions = {
    { _class = "RimKit.DefModExtension_Lua", luaClass = "lucky_day" },
  },
})
