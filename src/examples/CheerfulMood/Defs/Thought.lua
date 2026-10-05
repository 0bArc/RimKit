-- A situational thought whose state comes from Lua. rimkit mod sync turns this file into Defs/Thought.xml.
-- The class is named in the def mod extension: see game.classes.define("thought_worker", "clear_sky", ...) in Lua/main.lua.
def("ThoughtDef", "RimKitExample_ClearSky", {
  workerClass = "RimKit.ThoughtWorker_Lua",
  modExtensions = {
    { _class = "RimKit.DefModExtension_Lua", luaClass = "clear_sky" },
  },
  stages = {
    {
      label = "clear sky",
      description = "The sky is clear and the day feels lighter.",
      baseMoodEffect = 3,
    },
  },
})
