-- Defs for the in-game smoke suite, written in Lua. `rimkit mod defs tests/smoke/Defs` (the smoke runner does it) turns this file into
-- RimKit_Smoke.xml, which the game reads. Edit this file, not the XML.

-- A small building. The comps, class or extension differ per test.
local function building(def_name, label, description, thing_class, extra)
  local fields = {
    label = label,
    description = description,
    category = "Building",
    thingClass = thing_class,
    selectable = true,
    useHitPoints = true,
    rotatable = false,
    terrainAffordanceNeeded = "Light",
    drawerType = "MapMeshOnly",
    repairEffect = "Repair",
    leaveResourcesWhenKilled = false,
    graphicData = { texPath = "Things/Building/Furniture/LampStanding", graphicClass = "Graphic_Single" },
    size = vec(1, 1),
    altitudeLayer = "Building",
    passability = "PassThroughOnly",
    designationCategory = "Misc",
    statBases = { MaxHitPoints = 100, WorkToBuild = 100 },
    costList = { Steel = 5 },
  }
  for k, v in pairs(extra) do fields[k] = v end
  def("ThingDef", def_name, fields)
end

-- A building with a Lua comp (P4-06): the comp class is RimKit.ThingComp_Lua, the Lua class is named in luaClass.
building("RimKit_SmokeBox", "smoke box", "Smoke test building with a Lua comp.", "Building", {
  comps = { { _class = "RimKit.CompProperties_Lua", luaClass = "smoke_comp" } },
})

-- A building whose class (not a comp) is Lua: thingClass is RimKit.Building_Lua, the Lua class is in the def mod extension.
building("RimKit_SmokeLuaBuilding", "smoke lua building", "Smoke test building with a Lua class.", "RimKit.Building_Lua", {
  modExtensions = { { _class = "RimKit.DefModExtension_Lua", luaClass = "smoke_building" } },
})

-- A hediff with a Lua comp.
def("HediffDef", "RimKit_SmokeHediff", {
  label = "smoke hediff",
  description = "A hediff with a Lua component, used by the smoke test.",
  hediffClass = "HediffWithComps",
  isBad = false,
  comps = { { _class = "RimKit.HediffCompProperties_Lua", luaClass = "smoke_hediff" } },
})

-- A situational thought whose state comes from Lua.
def("ThoughtDef", "RimKit_SmokeThought", {
  workerClass = "RimKit.ThoughtWorker_Lua",
  modExtensions = { { _class = "RimKit.DefModExtension_Lua", luaClass = "smoke_thought" } },
  stages = {
    { label = "smoke test mood", description = "A mood effect from a Lua class.", baseMoodEffect = 1 },
  },
})

-- An incident worker in Lua.
def("IncidentDef", "RimKit_SmokeIncident", {
  label = "smoke incident",
  category = "Misc",
  targetTags = { "Map_PlayerHome" },
  workerClass = "RimKit.IncidentWorker_Lua",
  baseChance = 0,
  modExtensions = { { _class = "RimKit.DefModExtension_Lua", luaClass = "smoke_incident" } },
})

-- A plain incident def whose worker the test swaps for Lua at runtime (P4-11).
def("IncidentDef", "RimKit_SmokeIncident2", {
  label = "smoke incident two",
  category = "Misc",
  targetTags = { "Map_PlayerHome" },
  workerClass = "IncidentWorker",
  baseChance = 0,
})

-- A quest script whose only node is a Lua quest node.
def("QuestScriptDef", "RimKit_SmokeQuest", {
  autoAccept = true,
  questNameRules = { rulesStrings = { "questName->Smoke quest" } },
  questDescriptionRules = { rulesStrings = { "questDescription->A quest made by the smoke test." } },
  root = {
    _class = "QuestNode_Sequence",
    nodes = { { _class = "RimKit.QuestNode_Lua", luaClass = "smoke_quest_node" } },
  },
})

-- A custom data def (P4-10).
def("RimKit.LuaDataDef", "RimKit_SmokeRow1", {
  table = "smoke_table",
  values = { { key = "speed", value = 3 }, { key = "name", value = "first" } },
  list = { "a", "b" },
})
def("RimKit.LuaDataDef", "RimKit_SmokeRow2", {
  table = "smoke_table",
  values = { { key = "speed", value = 5 } },
})
