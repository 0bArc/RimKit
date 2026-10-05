-- A lamp with a Lua component. rimkit mod sync turns this file into Defs/Lamp.xml.
-- The comp class is RimKit.CompProperties_Lua, the Lua class is named in luaClass: see Lua/main.lua.
-- tickerType = "Rare" is what makes the game call tick_rare, about every 250 ticks. Buildings default to "Never".
def("ThingDef", "RimKitExample_GlowLamp", {
  label = "glow lamp",
  description = "A lamp that counts its own pulses and sparkles now and then. Its behavior is written in Lua.",
  category = "Building",
  thingClass = "Building",
  tickerType = "Rare",
  selectable = true,
  useHitPoints = true,
  rotatable = false,
  terrainAffordanceNeeded = "Light",
  drawerType = "MapMeshOnly",
  repairEffect = "Repair",
  leaveResourcesWhenKilled = false,
  graphicData = {
    texPath = "Things/Building/Furniture/LampStanding",
    graphicClass = "Graphic_Single",
  },
  size = vec(1, 1),
  altitudeLayer = "Building",
  passability = "PassThroughOnly",
  designationCategory = "Furniture",
  statBases = { MaxHitPoints = 100, WorkToBuild = 200 },
  costList = { Steel = 10 },
  comps = {
    { _class = "RimKit.CompProperties_Lua", luaClass = "glow_pulse" },
  },
})
