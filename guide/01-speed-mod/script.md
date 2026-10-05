# Episode 1: Speed up time

**Mod:** Turbo Time. Press `=` for faster, `-` for slower. About 12 minutes. **Finished mod:** [final/TurboTime](final/TurboTime).

**ON SCREEN** is what you do. **SAY** is a script to read or paraphrase. Code blocks are exactly what to type, in order. Each stage runs on its own, so show a result after each one.

## 0. Hook (0:00 to 0:40)

**ON SCREEN:** The game at Normal speed, then the finished mod: press `=` a few times, the colony speeds up, "Game speed boost: x4" appears.

**SAY:** RimWorld's top speed is not fast enough for me. Today we make a mod that adds more speed: equals to go faster, minus to go slower. About forty lines of Lua, no C#, nothing to compile, and by the end you can make any mod that changes a number in the game.

## 1. What you need (0:40 to 1:20)

**ON SCREEN:** VS Code, a terminal, the mod list with Harmony and RimKit enabled. Run `rimkit help`.

**SAY:** You need RimWorld, the Harmony mod, and RimKit, the toolkit that lets mods be written in Lua. Links below. I use VS Code with the RimKit extension because it completes the game's functions as I type.

## 2. Create the mod (1:20 to 2:30)

```text
rimkit mod create TurboTime
```

Open the folder in VS Code and show the tree. Then in `meta.lua`:

```lua
meta.name = "Turbo Time"
meta.package_id = "yourname.turbotime"
meta.description = "Press = for a faster game and - for a slower one."
meta.api_level = 1
meta.capabilities = { "hooks" }
```

**SAY:** One command makes the whole mod. `Lua` is where our code goes, `meta.lua` is the name and settings, `Tests` holds a first test, already written by `create`, that checks our work without the game, and the rest is what you need to publish later. The package id must be unique, so use your name. `api_level = 1` turns on strict mode: errors are raised, not hidden. Capabilities: RimKit makes every mod say what it is allowed to do. We will change how the game calculates time, which is a hook, so we declare hooks. If we forgot, RimKit would refuse and tell us why.

## 3. First version: double the speed (2:30 to 4:30)

`Lua/main.lua`:

```lua
game.tweaks.on("time.rate_multiplier", function(t)
  return t.value * 2
end)
```

**SAY:** Three lines. A tweak is a named place where a mod can change a number. This one is how fast time runs. The game gives us its answer in `t.value`, and whatever we return replaces it. So we return twice the game's answer. There are a dozen of these (hunger, plant growth, shooting accuracy), all the same shape.

```text
rimkit mod ship TurboTime
```

Start the game with the mod enabled, restart, start a colony, Normal speed.

**SAY:** Ship copies the mod into the Mods folder. The game only reads mods at start, so we restart. Normal speed is now twice as fast. Pause still pauses: zero times two is still zero.

## 4. Levels and keys (4:30 to 7:30)

Replace the file with:

```lua
local KEY_FASTER = "TurboTime_Faster"
local KEY_SLOWER = "TurboTime_Slower"
local LEVELS = { 1, 2, 3, 4, 6, 8, 10 }
local index = 1

local function boost()
  return LEVELS[index]
end

game.tweaks.on("time.rate_multiplier", function(t)
  if t.value <= 0 then return t.value end
  return t.value * boost()
end)

local function step(direction)
  local next_index = math.max(1, math.min(#LEVELS, index + direction))
  if next_index == index then return false end
  index = next_index
  game.ui.message("Game speed boost: x" .. boost())
  return true
end

game.input.register_key(KEY_FASTER, "Turbo Time: faster", "Equals")
game.input.register_key(KEY_SLOWER, "Turbo Time: slower", "Minus")

game.events.on_tick(function()
  if game.input.binding_just_pressed(KEY_FASTER) then step(1) end
  if game.input.binding_just_pressed(KEY_SLOWER) then step(-1) end
end)
```

**SAY:** `LEVELS` is the list of multipliers, `index` is where we are in it, `boost` reads the current one. The tweak multiplies by it, and the `if` keeps a paused game at zero. `step` moves up or down the list, stops at the ends and shows a message. `register_key` makes real key bindings the player can change in the options, and on every tick we ask whether the key was just pressed.

Ship, restart, press `=` repeatedly, then `-`.

## 5. A cap, a setting, proper text (7:30 to 9:30)

Add under `local index = 1`:

```lua
local CAP_SETTING = "turbotime.max_total"
local DEFAULT_CAP = 30

game.config.register(CAP_SETTING, {
  type = "float",
  label = "Turbo Time: highest total game speed",
  default = DEFAULT_CAP,
})

local function cap()
  local n = tonumber(game.config.get(CAP_SETTING))
  if not n or n < 1 then return DEFAULT_CAP end
  return n
end

local function scaled(value)
  if value <= 0 then return value end
  return math.min(value * boost(), cap())
end
```

and make the tweak `return scaled(t.value)`.

**SAY:** A setting is two calls: register it once so it shows up in the mod options, read it when you need it. The cap stops the game trying to simulate a thousand times speed. `scaled` is one small function that does the maths, which matters for testing.

For translatable text, create `Languages/English/Keyed/TurboTime.xml` (see `final/TurboTime`) and replace the typed strings with `game.ui.translate("TurboTime_Level", boost())` and so on. **SAY:** Now anyone can translate the mod by adding a file. The `{0}` is where the number goes.

## 6. Test without the game (9:30 to 11:00)

Add to the bottom of `main.lua`:

```lua
game.interop.publish("turbotime", "1.0.0", { boost = boost, scaled = scaled, step = step })
```

`Tests/turbo_test.lua`:

```lua
local t = game.test

t.describe("Turbo Time", function()
  t.it("a faster step multiplies the game's speed", function()
    t.mock("config.get", "30")
    local turbo = game.interop.get("turbotime")
    t.expect(turbo.step(1)).to_be(true)
    t.expect(turbo.scaled(3)).to_be(6)
  end)

  t.it("never speeds up a paused game", function()
    local turbo = game.interop.get("turbotime")
    t.expect(turbo.scaled(0)).to_be(0)
  end)
end)
```

```text
rimkit mod test TurboTime
```

**SAY:** This runs without the game. The test pretends the game answered 30 for the setting, steps up a level and checks that speed three becomes six, and that a paused game stays paused. A second instead of a restart. Break it on purpose (`value + boost()`), show the red line, put it back.

## 7. Wrap up (11:00 to 12:00)

**SAY:** That is a mod from nothing: one command to create it, a tweak, keys, a setting, translation and tests. The same shape works for hunger, plant growth or shooting accuracy. Next video we control the colonists themselves. The finished mod is linked below.

## Producer notes

- Keep the camera on the result for two seconds after every restart. Cut the restarts: say "restarting" and jump cut to the main menu.
- If `=` does nothing, another binding uses it: show Options, Key bindings and rebind.
- The cap setting (30) is why level ten does not melt the game. Mention it.
