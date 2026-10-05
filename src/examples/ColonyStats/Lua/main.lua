-- ColonyStats
-- Press F8 for a small window with live colony numbers. Shows reading the game (colonists, silver, wealth, food),
-- a widget table that refreshes by itself, a rebindable key, and a function offered to other mods.

local KEY = "ColonyStats_Toggle"
local window

-- The rows of the table. A plain function with no UI in it, so a test can call it.
local function rows()
  local map = game.maps.current()
  local wealth = game.economy.wealth()
  return {
    { game.ui.translate("colonystats_Colonists"), tostring(#game.maps.colonists(map)) },
    { game.ui.translate("colonystats_Silver"), tostring(game.economy.silver()) },
    { game.ui.translate("colonystats_Wealth"), string.format("%.0f", wealth.total) },
    { game.ui.translate("colonystats_Food"), string.format("%.1f", game.maps.nutrition(map)) },
  }
end

local function view()
  return {
    type = "table",
    columns = { { label = game.ui.translate("colonystats_Stat") }, { label = game.ui.translate("colonystats_Value"), w = 100 } },
    rows = rows(),
  }
end

local function toggle()
  if window then
    game.widgets.close(window)
    window = nil
    return
  end
  window = game.widgets.open(game.ui.translate("colonystats_Title"), view, nil, { width = 300, height = 190, refresh = 60 })
end

game.input.register_key(KEY, game.ui.translate("colonystats_Key"), "F8")

game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY) then toggle() end
end)

-- Other mods can read the same numbers.
game.interop.publish("colonystats", "1.0.0", { rows = rows, toggle = toggle })
