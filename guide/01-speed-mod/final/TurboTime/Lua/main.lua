-- Turbo Time
-- Press = for a faster game, - for a slower one. The boost multiplies the game's own speed (Normal, Fast, Superfast, Ultrafast),
-- and Paused stays paused. A cap in the mod options stops a big boost from freezing the game.

local KEY_FASTER = "TurboTime_Faster"
local KEY_SLOWER = "TurboTime_Slower"
local CAP_SETTING = "turbotime.max_total"
local LEVELS = { 1, 2, 3, 4, 6, 8, 10 }   -- boost multipliers, first must be 1
local DEFAULT_CAP = 30

local index = 1                            -- which level is active

-- A setting the player can change in the mod options.
game.config.register(CAP_SETTING, {
  type = "float",
  label = game.ui.translate("TurboTime_CapSetting"),
  default = DEFAULT_CAP,
})

local function cap()
  local n = tonumber(game.config.get(CAP_SETTING))
  if not n or n < 1 then return DEFAULT_CAP end
  return n
end

local function boost()
  return LEVELS[index]
end

-- The game asks how fast time runs. t.value is its answer, what we return replaces it.
-- A paused game answers 0 and stays 0.
local function scaled(value)
  if value <= 0 then return value end
  return math.min(value * boost(), cap())
end

game.tweaks.on("time.rate_multiplier", function(t)
  return scaled(t.value)
end)

-- Moves to the next level, up or down, and stops at the ends.
local function step(direction)
  local next_index = math.max(1, math.min(#LEVELS, index + direction))
  if next_index == index then return false end
  index = next_index
  game.ui.message(game.ui.translate("TurboTime_Level", boost()))
  return true
end

game.input.register_key(KEY_FASTER, game.ui.translate("TurboTime_FasterKey"), "Equals")
game.input.register_key(KEY_SLOWER, game.ui.translate("TurboTime_SlowerKey"), "Minus")

game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY_FASTER) then step(1) end
  if game.input.binding_just_pressed(KEY_SLOWER) then step(-1) end
end)

-- Other mods and the tests can read and drive the boost through here.
game.interop.publish("turbotime", "1.0.0", {
  boost = boost,
  scaled = scaled,
  step = step,
})
