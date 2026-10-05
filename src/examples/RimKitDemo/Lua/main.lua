-- RimKit Demo
-- One file, six things a RimKit mod does. Read it top to bottom, copy what you need.
--
--   1. settings    game.config            a number the player can change in the mod options
--   2. a tweak     game.tweaks            change how fast colonists get hungry, no Harmony code
--   3. an event    game.events            react when a colonist dies
--   4. a window    game.widgets, game.input   F9 opens a table built from Lua tables
--   5. interop     game.interop           offer a function to other mods
--   6. a debug action  game.dev           a button in the F11 dev tools window
--
-- Strings are in Languages/English/Keyed/RimKitDemo.xml and read with game.ui.translate.
-- Tests are in Tests/ and run without the game: rimkit mod test

local HUNGER = "rimkitdemo.hunger"
local KEY = "RimKitDemo_Open"

-- ---- 1. a setting ---------------------------------------------------------------------------------------------------------------------
game.config.register(HUNGER, {
  type = "float",
  label = game.ui.translate("RimKitDemo_HungerSetting"),
  default = 1.0,
})

-- Keeps the setting inside a range that cannot break the game.
local function hunger_factor()
  local n = tonumber(game.config.get(HUNGER)) or 1.0
  return math.max(0.1, math.min(3.0, n))
end

-- ---- 2. a tweak -----------------------------------------------------------------------------------------------------------------------
-- t.value is what the game computed, the number you return replaces it. game.tweaks.list() shows every tweak point.
game.tweaks.on("pawn.hunger_rate", function(t)
  return t.value * hunger_factor()
end)

-- ---- 3. an event ----------------------------------------------------------------------------------------------------------------------
game.events.on("pawn.died", function(e)
  game.ui.message(game.ui.translate("RimKitDemo_Died", game.pawns.name(e.pawn)))
end)

-- ---- 4. a window ----------------------------------------------------------------------------------------------------------------------
-- "Shooting 12, Cooking 9": the highest skills of one pawn. A plain function, so it is easy to test.
local function best_skills(skills, count)
  local usable = {}
  for _, s in ipairs(skills or {}) do
    if not s.disabled then usable[#usable + 1] = s end
  end
  table.sort(usable, function(a, b) return a.level > b.level end)
  local parts = {}
  for i = 1, math.min(count or 3, #usable) do
    parts[#parts + 1] = (usable[i].label or usable[i].def) .. " " .. usable[i].level
  end
  return table.concat(parts, ", ")
end

local function colonist_rows(map)
  local rows = {}
  for _, handle in ipairs(game.maps.colonists(map.handle)) do
    rows[#rows + 1] = { game.pawns.name(handle), best_skills(game.pawns.skills(handle), 3) }
  end
  return rows
end

-- A view function returns a tree of tables. RimKit draws it and calls it again every refresh.
local function view(v)
  local map = game.current_map()
  if not map then
    return { type = "label", text = game.ui.translate("RimKitDemo_NoColony") }
  end
  return {
    type = "column",
    gap = 6,
    children = {
      { type = "label", text = game.ui.translate("RimKitDemo_Heading"), font = "medium" },
      {
        type = "table",
        columns = { { label = game.ui.translate("RimKitDemo_Name"), w = 150 }, { label = game.ui.translate("RimKitDemo_Skills") } },
        rows = colonist_rows(map),
      },
      { type = "slider", id = "hunger", label = game.ui.translate("RimKitDemo_HungerSlider"), min = 0.1, max = 3, step = 0.1, value = hunger_factor() },
    },
  }
end

local function on_event(e)
  if e.id == "hunger" then
    game.config.set(HUNGER, tostring(e.value))
  end
end

game.input.register_key(KEY, game.ui.translate("RimKitDemo_KeyLabel"), "F9")
game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY) then
    game.widgets.open(game.ui.translate("RimKitDemo_Title"), view, on_event, { width = 460, height = 340, refresh = 30 })
  end
end)

-- ---- 5. interop -----------------------------------------------------------------------------------------------------------------------
-- Another mod can call game.interop.get("rimkitdemo", "^1.0").best_skills(...) without knowing anything else about this one.
game.interop.publish("rimkitdemo", "1.0.0", { best_skills = best_skills, hunger_factor = hunger_factor })

-- ---- 6. a debug action ----------------------------------------------------------------------------------------------------------------
-- Shows in the F11 dev tools window (Development mode) under Actions.
game.dev.action("RimKit Demo: report the hunger setting", function()
  game.ui.message(game.ui.translate("RimKitDemo_Report", tostring(hunger_factor())))
end, "Prints the current hunger factor.")
