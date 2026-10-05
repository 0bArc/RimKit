-- MoreChairs: the Defs, written in Lua. `rimkit mod sync` (and `rimkit mod ship`) turns this file into Defs/chairs.xml, which is the
-- ordinary Def XML the game reads. Edit this file, not the XML. def(kind, defName, fields, opts) writes one def: fields is a plain
-- table (a list becomes <li> items), opts.parent is the ParentName.
--
-- Chairs are sat on at tables and work stations because of building.isSittable and the Comfort stat. The picture of each chair is
-- Textures/Things/Building/Furniture/MoreChairs_<Id>.png in this mod, and it is also the build icon.

local function chair(id, label, description, stuff, cost, comfort, beauty, work, hp, skill)
  def("ThingDef", "MoreChairs_" .. id, {
    label = label,
    description = description,
    graphicData = {
      texPath = "Things/Building/Furniture/MoreChairs_" .. id,
      graphicClass = "Graphic_Single",
      drawSize = vec(1, 1),
    },
    altitudeLayer = "Building",
    rotatable = false,
    statBases = {
      MaxHitPoints = hp,
      WorkToBuild = work,
      Mass = 4,
      Flammability = stuff == "Woody" and 1.0 or 0.4,
      Beauty = beauty,
      Comfort = comfort,
    },
    socialPropernessMatters = true,
    stuffCategories = { stuff },
    costStuffCount = cost,
    pathCost = 20,
    fillPercent = 0.3,
    building = { isSittable = true },
    constructionSkillPrerequisite = skill,
  }, { parent = "FurnitureWithQualityBase" })
end

chair("Stool", "stool", "A plain wooden stool. Cheap and quick to make, and not very comfortable. Can be used at tables, work stations, and elsewhere.", "Woody", 20, 0.45, 1, 1500, 60, 0)
chair("Rocker", "rocking chair", "A wooden chair on curved runners. Gentle on the back, so colonists linger in it. Can be used at tables, work stations, and elsewhere.", "Woody", 50, 0.85, 5, 6000, 90, 3)
chair("Gamer", "gaming chair", "A padded chair with a tall back and wide arms, built from metal. The most comfortable seat a colony can make. Can be used at tables, work stations, and elsewhere.", "Metallic", 45, 0.92, 3, 9000, 120, 5)

-- A short mood boost for finishing a meal in one of the comfortable chairs. MoreChairs gives it from Lua, see Lua/main.lua.
def("ThoughtDef", "MoreChairs_ComfySeat", {
  thoughtClass = "Thought_Memory",
  durationDays = 0.4,
  stackLimit = 1,
  stages = {
    {
      label = "comfy seat",
      description = "I ate sitting somewhere really comfortable.",
      baseMoodEffect = 4,
    },
  },
})
