=== ai | AI kit ===
`game.ai` reads and changes how pawns decide what to do: work givers and their order, think trees, duties, lords, and what pathfinding costs. The kit is Experimental in the [stability tiers](stability.md). Functions that act on one pawn take the pawn first.

Work givers decide which jobs a pawn looks for inside a work type, in priority order. `think_insert` and `think_remove` change the think tree that every pawn of a kind shares, so a change applies to all of them at once and stays until the game restarts. Insert only node classes that have a parameterless constructor.

```lua
-- Cleaning before hauling for everyone
for _, g in ipairs(game.ai.work_givers("Hauling")) do
  game.ai.set_work_giver_priority(g.def, g.priority - 5)
end

-- Stop colonists wandering when idle
game.ai.think_remove("Humanlike", "JobGiver_WanderColony")
```

{{functions}}

=== storyteller | Storyteller, incidents and quests ===
`game.storyteller`, `game.incidents` and `game.quests` control the story the game tells: which storyteller runs, how hard it is, what happens, and which quests exist. The kits are Experimental in the [stability tiers](stability.md). `game.incidents.try_fire` and `list` fire and list incidents.

`game.storyteller.difficulty()` returns every numeric and boolean difficulty setting by its field name, and `set_difficulty` changes one. Incidents take `points`, `faction` and `forced` options and run on the map you pass (the current map by default).

```lua
-- A custom storyteller: a raid every day sized to the colony
game.events.on("time.day_changed", function()
  local points = game.storyteller.threat_points()
  if game.incidents.can_fire("RaidEnemy", { points = points }) then
    game.raids.fire({ points = points })
  end
end)

-- Hand out a quest
local quest = game.quests.generate("OpportunitySite_ItemStash")
game.quests.accept(quest.id)
```

{{functions}}

=== research | Research and stats ===
`game.research` lists projects, sets progress and finishes them. `game.stats` reads any stat of a thing with the game's own explanation of where the value comes from. Both are Experimental in the [stability tiers](stability.md).

```lua
-- Finish everything that is available and cheap
for _, r in ipairs(game.research.list("available")) do
  if r.cost < 500 then game.research.finish(r.def) end
end

-- Why is this colonist so slow?
print(game.stats.explain(colonist, "MoveSpeed").text)
```

{{functions}}

=== world | World kit ===
`game.world` reads and changes the world map: tiles and their climate, settlements and other world objects, caravans, world pawns, and the maps behind tiles. The kit is Experimental in the [stability tiers](stability.md). `game.world.current` returns the world handle.

Tiles are integers. World objects come back as `RimObject` values, which you pass to `object_info`, `remove_object` and `travel`.

```lua
-- Where is the nearest forest to settle in?
local tile = game.world.find_tile({ biome = "TemperateForest", max_distance = 60 })

-- All hostile settlements
for _, s in ipairs(game.world.settlements()) do
  if game.factions.is_hostile(s.faction, game.factions.player()) then print(s.name, s.tile) end
end
```

{{functions}}

=== combat | Combat kit ===
`game.combat` reads attacks and armor, orders attacks, and makes explosions, fires and projectiles. The kit is Experimental in the [stability tiers](stability.md). Combat events (`projectile.hit`, `melee.hit`, `explosion.occurred`) are in the [event list](events.md).

```lua
-- Every ranged attack a colonist has
for _, v in ipairs(game.combat.verbs(colonist)) do
  if not v.melee then print(v.label, v.range, v.damage) end
end

-- A flare at the colonist's feet
local c = colonist:info()
game.combat.explode(game.maps.current(), c.x + 3, c.z, 2, { damage_def = "Flame", damage = 10 })
```

{{functions}}

=== build | Construction, power, bills and doors ===
Four kits cover building: `game.build` (blueprints, frames, instant building), `game.power` (power nets and batteries), `game.bills` (workbench bills and recipes) and `game.doors` and `game.traps`. They are Experimental in the [stability tiers](stability.md). The older `game.buildings.power_on`, `set_power` and `flick` keep working.

`blueprint` plans a building and colonists build it, `instant` builds it at once. `can_place` tells you why a spot does not work.

```lua
local map = game.maps.current()
local c = colonist:info()

-- Plan a wall line
for dx = 1, 5 do
  local ok = game.build.can_place(map, "Wall", c.x + dx, c.z + 4, { stuff = "WoodLog" })
  if ok.ok then game.build.blueprint(map, "Wall", c.x + dx, c.z + 4, { stuff = "WoodLog" }) end
end

-- Queue 10 simple meals on every stove
for _, thing in ipairs(game.query.things({ map = map, def = "FueledStove" })) do
  game.bills.add(thing, "CookMealSimple", { mode = "RepeatCount", count = 10 })
end
```

{{functions}}

=== economy | Economy and raids ===
`game.economy` reads silver, wealth and prices, lists traders and their stock and calls orbital traders. `game.raids` lists raid strategies and arrival modes and sends raids. The kits are Experimental in the [stability tiers](stability.md). Sending gifts to a faction is `game.factions.send_gift`.

```lua
print("Silver:", game.economy.silver(), "Wealth:", game.economy.wealth().total)

-- Friendly allies send help when a raid arrives
game.events.on("incident.fired", function(e)
  if e.incident == "RaidEnemy" then
    for _, f in ipairs(game.factions.allies(game.factions.player())) do
      game.raids.fire({ faction = f, points = 400 })
    end
  end
end)
```

{{functions}}

=== generation | Generation and scenarios ===
`game.generation` exposes how maps and worlds are generated: terrain data, generation steps, features, rivers, roads, and base generation on a rectangle. `game.scenarios` reads the scenario of the running game and the game rules. The kits are Experimental in the [stability tiers](stability.md).

`base_gen` runs a base generation symbol such as `basePart_outdoors` or `ancientRuins` on a rectangle of the map, the same machinery the game uses for ruins and bases.

```lua
local map = game.maps.current()
local c = colonist:info()
game.generation.base_gen(map, "ancientRuins", c.x + 10, c.z + 10, c.x + 22, c.z + 20)
```

{{functions}}

=== plants | Plants and agriculture ===
`game.plants` reads and changes plants: growth, harvest, sowing, soil fertility and growing zones. The kit is Experimental in the [stability tiers](stability.md).

```lua
local map = game.maps.current()
-- A farm planner: where would potatoes grow?
local c = colonist:info()
for dx = 0, 10 do
  local can = game.plants.can_grow(map, "Plant_Potato", c.x + dx, c.z + 6)
  if can.ok then game.plants.sow(map, "Plant_Potato", c.x + dx, c.z + 6) end
end
```

{{functions}}

=== conditions | Game conditions and weather ===
`game.conditions` starts, lists and ends game conditions such as eclipses, cold snaps and heat waves. `game.weather` (more than get and set) reports the sky, the season and the weather defs. The kits are Experimental in the [stability tiers](stability.md). `game.weather.current` and `set` read and change the weather.

```lua
-- A short cold snap on the current map
game.conditions.start("ColdSnap", { duration = 30000 })
print(game.conditions.temperature_offset())
```

{{functions}}

=== save | Saving and persistence ===
`game.save` stores data for mods inside the save file, per game, per map or per world, and controls autosaves. The kit is Experimental in the [stability tiers](stability.md). `game.data` is a shorthand for the per-game store.

`put` and `fetch` store any table, string, number or boolean through JSON, so functions and game objects cannot be stored: keep a thing's id or position and look it up again. The raw `get`, `set`, `remove` and `keys` store strings. Handlers of the `game.saving` event are the place to write data just before a save, and `game.loaded` is the place to read it.

Keep a data version so a new mod version can migrate old saves:

```lua
local PKG = "my.mod"
game.events.on("game.loaded", function()
  local v = game.save.version(PKG)
  if v < 2 then
    local old = game.save.fetch(PKG, "game", "settings", nil, {})
    old.speed = old.speed or 1
    game.save.put(PKG, "game", "settings", old)
    game.save.set_version(PKG, 2)
  end
end)

-- Per map data
game.save.put(PKG, "map", "visited", { count = 3 }, game.maps.current())
```

Stored data lives in `RimKit.MapComponent_LuaData`, `RimKit.WorldComponent_LuaData` and `RimLuaKit.RimLuaModDataComponent`. Those names are written into saves and never change, see the [Standard](../standard/rks.md).

{{functions}}

=== widgets | Widgets and windows ===
`game.widgets` opens windows built from a tree of tables. You write a view function that returns the tree, RimKit draws it, and every interaction (click, change, selection, tab, reorder) comes back to your event handler with the current values of all inputs. The kit is Experimental in the [stability tiers](stability.md).

## How it works

The view function runs when the window opens, when something changes, and every `refresh` frames (default 15). It receives `{ state, window }` where `state` holds the current value of every input widget by its `id`. The event handler receives `{ window, id, kind, value, state }`. Keep your own data in Lua and describe it in the view.

## Widgets

| `type` | What it is | Main fields |
|--------|------------|-------------|
| `column`, `row` | Layout. A row shares its width, children can set `w` | `children`, `gap` |
| `label` | Text | `text`, `font` (tiny, small, medium), `color`, `align`, `wrap` |
| `button` | A button, event `click` | `id`, `text`, `enabled` |
| `checkbox` | A toggle, event `change` | `id`, `text`, `value` |
| `slider` | A number, event `change` | `id`, `label`, `min`, `max`, `step`, `value` |
| `text` | A text field, event `change` | `id`, `value`, `multiline`, `max_length` |
| `dropdown` | A choice, event `change` | `id`, `options` (strings or `{ value, label }`), `value` |
| `list` | A scrolling list, event `select`, and `reorder` with shift-drag when `reorder = true` | `id`, `items` (strings or `{ id, label, tip }`), `selected`, `height` |
| `table` | Columns and rows, cells are strings or nodes | `columns`, `rows` |
| `tabs` | Tabs with content, event `tab` | `id`, `tabs` (`{ id, label, content }`) |
| `scroll` | A fixed height scroll area | `height`, `child` |
| `progress` | A bar | `value` (0 to 1), `text`, `color` |
| `image` | A texture from a mod's Textures folder | `texture`, `size` |
| `space`, `separator` | Spacing and a line | `size` |

Every node accepts `tip` (a tooltip), `enabled`, `h` (fixed height) and `id`. Colors are `#rrggbb` or a theme name (`accent`, `good`, `warning`, `bad`, `muted`), see [hud](hud.md).

```lua
local items = { "Steel", "Silver", "Gold" }
local win
win = game.widgets.open("Stock taker", function(v)
  local counts = game.query.count_by_def({ map = game.maps.current(), group = "HaulableEver" })
  local rows = {}
  for _, name in ipairs(items) do rows[#rows + 1] = { name, tostring(counts[name] or 0) } end
  return {
    type = "column",
    children = {
      { type = "label", text = "Stock", font = "medium" },
      { type = "table", columns = { { label = "Item" }, { label = "Count", w = 80 } }, rows = rows },
      { type = "button", id = "refresh", text = "Refresh now" },
    },
  }
end, function(e)
  if e.id == "refresh" then game.widgets.invalidate(e.window) end
end, { width = 360, height = 300, refresh = 60 })
```

`game.widgets.confirm` and `prompt` open a yes or no dialog and a text input. Both take callbacks.

{{functions}}

=== gizmos | Gizmos ===
`game.gizmos` adds buttons, toggles and sliders to the gizmo bar that appears when something is selected. The kit is Experimental in the [stability tiers](stability.md).

`target` picks what gets the gizmo: `pawn`, `colonist`, `building`, `item`, `plant`, `thing` or `any`, and `defs` narrows it to specific defs. The `visible` function decides per thing each frame. With several things selected the game merges identical gizmos into one, and one click runs the callback for each selected thing.

```lua
-- A "Drop everything" button on colonists
game.gizmos.add("drop_all", { label = "Drop all", desc = "Drops everything this colonist carries", target = "colonist", icon = "UI/Commands/Halt" },
  function(pawn)
    for _, item in ipairs(game.pawns.gear(pawn).inventory) do game.pawns.drop(pawn, item.thing) end
  end,
  function(pawn) return #game.pawns.gear(pawn).inventory > 0 end)

-- A toggle stored in the save
game.gizmos.add_toggle("auto_tend", { label = "Auto tend", target = "colonist" },
  function(pawn) return game.save.fetch("my.mod", "game", "auto_tend_" .. pawn:info().id, nil, false) end,
  function(pawn) local k = "auto_tend_" .. pawn:info().id
    game.save.put("my.mod", "game", k, not game.save.fetch("my.mod", "game", k, nil, false)) end)
```

Icons are texture paths inside a mod's `Textures` folder, or the game's own paths such as `UI/Commands/Attack`. Hotkeys are `KeyBindingDef` names, see [input](input.md).

{{functions}}

=== tabs | Tabs and columns ===
`game.tabs` adds screens to the game: a button on the main tab bar, a tab on the inspect pane (next to Health and Needs), and a column in a pawn table (Work, Animals, ...). All three show a [widget](widgets.md) view or a cell callback. The kit is Experimental in the [stability tiers](stability.md).

```lua
-- A colony overview in the main tab bar
game.tabs.add_main("overview", "Overview", function(v)
  return { type = "column", children = {
    { type = "label", text = "Colonists: " .. #game.query.pawns({ faction = "player", humanlike = true }) },
    { type = "label", text = "Silver: " .. game.economy.silver() },
  } }
end, nil, { width = 420, height = 300 })

-- An inspect tab on every pawn with its skills
game.tabs.add_inspect("skills_tab", "Skills", function(v)
  local rows = {}
  for _, s in ipairs(game.pawns.skills(v.subject)) do rows[#rows + 1] = { s.def, tostring(s.level) } end
  return { type = "table", columns = { { label = "Skill" }, { label = "Level", w = 70 } }, rows = rows }
end, nil, { target = "pawn" })

-- A column in the Work table
game.tabs.add_column("Work", "mood_col", "Mood", function(pawn)
  local m = game.pawns.mind_summary(pawn).mood
  return { text = string.format("%d%%", (m or 0) * 100), color = (m or 0) < 0.3 and "bad" or "good" }
end)
```

Tabs are added for the running game and are gone after a restart, so register them again at startup. Architect entries are tools, see [designators](designators.md). A bill or recipe screen is a widget view over [`game.bills`](build.md).

{{functions}}

=== designators | Designators and areas ===
`game.designators` defines click and drag tools, and `game.areas` makes and edits allowed areas. The kit is Experimental in the [stability tiers](stability.md).

A tool has a `designate` callback and an optional `can_designate` that returns true, false or the reason a cell is not allowed. Set `drag = "box"` for rectangle and line dragging, and `category` to put the tool in an architect menu such as `Orders`. `target = "thing"` makes the callbacks receive a thing instead of a cell.

```lua
game.designators.add("mark_zone", { label = "Mark zone", desc = "Paint the Watch area", icon = "UI/Designators/ZoneCreate_Stockpile", drag = "box", color = "accent", category = "Orders" },
  function(cell) game.areas.set(cell.map, "Watch", cell.x, cell.z, true) end,
  function(cell) return game.maps.walkable(cell.map, cell.x, cell.z) or "Not walkable" end)
game.areas.create(game.maps.current(), "Watch")
```

{{functions}}

=== graphics | Graphics and materials ===
`game.graphics` changes how things and pawns look: textures, colors, shaders, pawn bodies, hair and heads, extra render layers. `game.materials` creates materials, sets their properties and loads shaders from asset bundles. The kits are Experimental in the [stability tiers](stability.md).

Textures live in a mod's `Textures` folder and are named without the extension. `set_def_graphic` swaps the texture of a thing def and redraws the things on the map. `add_render_node` adds a texture layer to every pawn of a race through the 1.6 render tree. A custom shader comes from an asset bundle in the mod folder (`load_bundle`) and is then used by name in `set_def_graphic`, `materials.create` and overlays.

```lua
game.graphics.set_def_graphic("Steel", "MyMod/Steel", { color = "#aabbcc" })
game.graphics.set_hair_color(colonist, "#ff4400")
game.graphics.set_body_type(colonist, "Fat")

local shaders = game.materials.load_bundle("my.mod", "Bundles/glow")
local mat = game.materials.create(shaders[1], { texture = "MyMod/Glow", color = "#44ccff" })
```

Animation: `play_animation` plays a vanilla animation def on a pawn where the game version has pawn animations, and raises `RK3003` otherwise.

{{functions}}

=== effects | Effects and overlays ===
`game.effects` makes visual and audible feedback: flecks, effecters, floating text, camera shake, and overlays drawn on the map (highlighted cells, lines, circles, marked things). The kit is Experimental in the [stability tiers](stability.md).

Overlays stay until their `ticks` run out or you clear them, and return an id for `clear`.

`text`, `fleck` and `effecter` also take a thing or pawn as the first argument, so you do not unpack its cell: `game.effects.text(pawn, "12", "red")`. `text` accepts the color names `red`, `orange`, `yellow`, `green`, `blue`, `white`, `gray`, `purple` and `cyan`, and the theme names such as `good` and `warning`, as well as hex.

```lua
local map = game.maps.current()
-- Show the reach of a turret
local t = turret:info()
game.effects.circle(map, t.x, t.z, 25, { color = "warning" })

-- Highlight every cell of a stockpile for a few seconds
local cells = {}
for _, c in ipairs(game.areas.cells(map, "Watch")) do cells[#cells + 1] = c end
game.effects.highlight_cells(map, cells, { color = "accent", ticks = 300 })
game.effects.text(map, t.x, t.z, "Nice!", "good")
```

{{functions}}

=== input | Input ===
`game.input` registers key bindings at runtime and reads keys, chords and the mouse. The kit is Experimental in the [stability tiers](stability.md). `game.input.binding_just_pressed` reads a binding.

`register_key` creates a binding the player can rebind in the game's key options. Keys are Unity key names (`K`, `F9`, `LeftArrow`), `key_names()` lists them. For text, use `game.widgets.prompt`.

```lua
game.input.register_key("MyMod_Overview", "Open the overview", "F9")
game.events.on_tick(function()
  if game.input.binding_just_pressed("MyMod_Overview") then print("pressed") end
  if game.input.chord_pressed("ctrl+shift+K") then print("chord") end
end)
```

{{functions}}

=== audio | Audio ===
`game.audio` defines sounds from files, plays them, controls music, volumes and sustained sounds. The kit is Experimental in the [stability tiers](stability.md). `game.audio.play` plays a sound def by name.

`define` takes clip paths inside a mod's `Sounds` folder, without the extension, and makes a sound def you play with `play` or `play_at`. A looping sound is defined in XML with `sustain` and started with `sustainer_start`.

```lua
game.audio.define("MyMod_Ding", { "MyMod/ding1", "MyMod/ding2" }, { volume = 0.8, pitch = 1.1 })
game.audio.play("MyMod_Ding")
game.audio.set_volume("music", 0.3)
game.audio.play_song(game.audio.songs()[1].def)
```

{{functions}}

=== hud | Alerts, HUD, letters, themes and options ===
`game.alerts` adds alerts, `game.hud` shows status lines, sends letters with answer buttons and holds colour presets and the default font, and `game.options` adds settings pages for your mod. The kits are Experimental in the [stability tiers](stability.md).

## Alerts

The `check` function runs regularly. It returns `true`, `false` or `{ active, label, explanation, culprits }`, where culprits are things the player can click to jump to. Register alerts after `game.ready`.

```lua
game.alerts.add("low_steel", function()
  local steel = game.query.count_by_def({ map = game.maps.current(), def = "Steel" }).Steel or 0
  return { active = steel < 100, label = "Low steel", explanation = "Steel is below 100." }
end, { priority = "High" })
```

## Letters with choices

`game.hud.letter(label, text, choices, tag)` sends a letter with one button per choice. Answers raise the `letter.choice` event, so they still work after saving and loading:

```lua
game.hud.letter("A stranger arrives", "They ask to join you.", { "Welcome them", "Turn them away" }, "stranger")
game.events.on("letter.choice", function(e)
  if e.tag == "stranger" and e.index == 1 then print("welcomed") end
end)
```

## Themes

`game.hud.colors()` lists the colour presets, `set_color` changes or adds one, and `set_font` sets the default widget font. Presets work wherever a colour is accepted.

## Options pages

`game.options.page` adds a tab with your fields to RimKit's entry in the mod settings. Values are saved with the settings, read with `game.options.get` or `game.config.get`, and every change raises `options.changed` and calls your `on_change`.

```lua
game.options.page("my.mod", "My mod", {
  { key = "mymod.enabled", type = "bool", label = "Enabled", default = true },
  { key = "mymod.rate", type = "float", label = "Rate", min = 0, max = 10, step = 0.5, default = 2 },
  { key = "mymod.mode", type = "choice", label = "Mode", choices = { "fast", "slow" }, default = "fast" },
}, function(change) print(change.key, change.value) end)
```

{{functions}}

=== classes | Lua classes ===
`game.classes` lets a mod write the behavior of game classes in Lua. The game normally asks for a C# subclass (a `ThingComp`, an `IncidentWorker`, a `StatPart`). RimKit ships one proxy class per family that calls Lua functions by name, so XML names the proxy and the Lua side supplies the behavior. The kit is Advanced in the [stability tiers](stability.md).

There are two steps. First define the Lua class when the mod loads, then point the XML at it.

```lua
-- Lua/main.lua
game.classes.define("comp", "glow_pulse", {
  spawn = function(thing, respawning) game.log.info("spawned " .. thing.def) end,
  tick_rare = function(thing) game.effects.mark(thing) end,
  inspect_string = function(thing) return "Pulsing" end,
})

game.classes.define("incident", "lucky_day", {
  can_fire = function(parms) return true end,
  execute = function(parms)
    game.hud.letter("Lucky day", "Everything is fine.", {})
    return true
  end,
})
```

```xml
<!-- A building with a Lua component -->
<comps>
  <li Class="RimKit.CompProperties_Lua">
    <luaClass>glow_pulse</luaClass>
  </li>
</comps>

<!-- An incident, thought, need or condition whose class comes from Lua -->
<workerClass>RimKit.IncidentWorker_Lua</workerClass>
<modExtensions>
  <li Class="RimKit.DefModExtension_Lua">
    <luaClass>lucky_day</luaClass>
  </li>
</modExtensions>
```

The newer families, and how XML names them:

| Family | XML | Lua functions |
|--------|-----|---------------|
| `quest_node` | `<li Class="RimKit.QuestNode_Lua"><luaClass>my_node</luaClass></li>` inside a quest script's nodes | `run()`, `test()` (returns false to make the quest script fail its test run) |
| `world_gen_step` | A `WorldGenStepDef` whose `worldGenStep` has `Class="RimKit.WorldGenStep_Lua"`, plus the def mod extension | `generate(seed)` |
| `scen_part` | A `ScenPartDef` with `<scenPartClass>RimKit.ScenPart_Lua</scenPartClass>`, plus the def mod extension | `post_game_start()`, `post_world_generate()`, `generate_into_map(map)`, `post_map_generate(map)`, `tick()`, `summary(tag)` |
| `ritual_outcome` | A `RitualOutcomeEffectDef` with `<workerClass>RimKit.RitualOutcomeEffectWorker_Lua</workerClass>`, plus the def mod extension. Needs Ideology | `apply(progress, participants)` |
| `think_node` | `<li Class="RimKit.ThinkNode_Conditional_Lua"><luaClass>my_check</luaClass><subNodes>...</subNodes></li>` in a think tree | `satisfied(pawn)` returns true to run the sub nodes |
| `building` | A `ThingDef` with `<thingClass>RimKit.Building_Lua</thingClass>` and the def mod extension | `spawn(thing, respawning)`, `despawn(thing, mode)`, `destroy(thing, mode)`, `tick(thing)`, `tick_rare(thing)`, `tick_long(thing)`, `damaged(thing, amount)`, `inspect_string(thing)` (appended to the game's own text) |
| `door` | A door `ThingDef` with `<thingClass>RimKit.Building_Door_Lua</thingClass>` and the def mod extension | `can_open(door, pawn)` and `blocks(door, pawn)` (return a boolean to decide who may pass), `tick(door)` |
| `storage` | A storage building `ThingDef` with `<thingClass>RimKit.Building_Storage_Lua</thingClass>` and the def mod extension | `received(building, thing)`, `lost(building, thing)` |

A thing only gets its tick functions when its def asks for them: set `<tickerType>Normal</tickerType>` (every tick), `Rare` (about every 250 ticks) or `Long` on the `ThingDef`. Buildings default to `Never`, so `tick`, `tick_rare` and `tick_long` do nothing without it.

A building class is more than a component: it owns the whole thing, so it can decide who walks through a door, react to what a storage building receives, or count its own ticks, and the data it keeps is saved with `game.classes.data_set`. `game.classes.replace("building", "MyBuilding", "my_class")` swaps an existing building def to the Lua class until the game restarts. The def itself (graphics, stats, cost) is still XML, because the game needs it before any Lua runs.

### Needs from Lua

`game.needs.define(def, opts)` makes a need with no XML. It creates a real `NeedDef` when your mod loads, which is before any save loads, so the game draws the need bar, saves the level and adds the need to pawns. The behavior is a `need` class: `interval(pawn, level)` runs about every 150 ticks, and `fall_per_day` lets the game lower the level for you.

```lua
game.needs.define("MyMod_Comfort", {
  label = "comfort",
  description = "How settled a colonist feels.",
  who = "colonists",            -- colonists, colonists_and_prisoners, humanlike or everyone
  base_level = 0.7,
  fall_per_day = 0.2,
  major = false,
})

game.classes.define("need", "MyMod_Comfort", {
  interval = function(pawn, level)
    if level < 0.1 then game.pawns.add_thought(pawn, "AteLavishMeal") end
  end,
})
```

Call `define` at the top of `main.lua`, not inside an event: a save that was made with the need needs the def to exist when it loads. Calling it again with the same name updates the need. It refuses a name that belongs to the game or another mod. `game.needs.remove` hides the need and takes it from every pawn until the game restarts. Read and set the level with `game.pawns.need` and `game.pawns.set_need`.

`game.classes.families()` lists the families (comp, hediff_comp, incident, condition, storyteller, stat_part, stat_worker, work_giver, job_giver, place_worker, recipe_worker, thought_worker, mental_state_worker, need, ability, damage_worker, projectile, verb, gen_step, thing_set_maker, quest_part, lord_toil, gene, quest_node, world_gen_step, scen_part, ritual_outcome, think_node, building, door, storage) and `game.classes.list()` shows which functions each registered class defines. A function you leave out runs the game's own behavior. Arguments are passed as the game gives them, with game objects wrapped, and nil arguments keep their position.

Data on a Lua comp is saved with the object: `game.classes.data_set(thing, "key", "value")` and `data_get`. Use [`game.save`](save.md) with the `thing` scope for data on things that have no Lua comp.

To change a def that already exists, `game.classes.replace("incident", "RaidEnemy", "my_raid")` swaps its worker for the Lua proxy until the game restarts. `replaceable()` shows which families support this.

Quest nodes, world gen steps, scenario parts, ritual outcomes and conditional think nodes are covered by the last five families. A Lua quest node reads and writes the quest slate with `game.quests.slate_get`, `slate_set` and `slate_set_object`. Lua classes run on the main thread. A function that errors is logged with the mod name and the game falls back to its own behavior for that call.

{{functions}}

=== defs | Defs: changing and writing ===
`game.defs` reads, changes and writes game definitions. The kit is Advanced in the [stability tiers](stability.md).

Runtime changes (`set`) edit the loaded def, record what the value was, and can be undone with `restore`. They last until the game restarts. Use them for balance mods and for tweaks that depend on settings.

```lua
game.defs.set("ThingDef", "Steel", "stackLimit", 1000)
game.defs.set("ThingDef", "Steel", "statBases[0].value", 2)   -- nested fields and list items
print(game.defs.value("ThingDef", "Steel", "stackLimit"))     -- 1000
game.defs.restore("ThingDef", "Steel", "stackLimit")
for _, c in ipairs(game.defs.changes()) do print(c.kind, c.def, c.path) end
```

`of` and `of_mod` answer which mod a def comes from, so a patch mod can check before it changes something.

Authoring turns a Lua table into Def XML, checked against the game's own types (fields, values, enums and def references), and writes it to the mod's `Defs` folder. The game loads it at the next start.

```lua
local xml = game.defs.to_xml("ThingDef", "MyMeal", {
  label = "my meal", stackLimit = 20,
  statBases = { MaxHitPoints = 50 },
  comps = { { _class = "CompProperties_Forbiddable" } },
}, { parent = "ResourceBase" })

local check = game.defs.validate_xml(xml)
if not check.ok then for _, e in ipairs(check.errors) do print(e) end end

game.defs.write_xml("my.mod.id", "ThingDef", "MyMeal", { label = "my meal", stackLimit = 20 })
```

Custom data tables are defs of type `RimKit.LuaDataDef`. Rows carry a table name, a dictionary of values and a list, and `game.defs.data_rows("table")` reads them:

```xml
<RimKit.LuaDataDef>
  <defName>Row1</defName>
  <table>my_table</table>
  <values><li><key>speed</key><value>3</value></li></values>
  <list><li>a</li></list>
</RimKit.LuaDataDef>
```

{{functions}}

=== patch | XML patches and other mods ===
`game.patch` builds XML patch files from Lua and `game.mods` asks which mods are installed. Both are Advanced in the [stability tiers](stability.md).

```lua
game.patch.write("my.mod.id", "tweaks", {
  { op = "set_attribute", xpath = "/Defs/ThingDef[defName=\"Steel\"]", attribute = "Abstract", value = "False" },
  { op = "if_mod", mods = { "other.mod" }, match = {
      { op = "add", xpath = "/Defs/ThingDef[defName=\"Gold\"]", value = "<description>Shiny.</description>" } } },
})
```

Operations: add, replace, remove, insert, set_attribute, add_attribute, remove_attribute, test, conditional, sequence, if_mod, add_mod_extension. `build` returns the XML and a list of errors without writing, so bad operations and bad XPath are caught before the game loads them.

To hook another mod's code, check it is there first. `game.hooks.in_mod(package_id, kind, type, method, fn)` installs the hook only when that mod is active and its type exists, and does nothing otherwise.

```lua
if game.mods.active("other.mod") and game.mods.type_exists("other.mod", "Other.Core.Manager") then
  game.hooks.in_mod("other.mod", "postfix", "Other.Core.Manager", "Refresh", function(ctx) end)
end
```

{{functions}}

=== dlc | DLC kits ===
Every DLC function checks that its expansion is active and raises `RK3003` when it is not, so a mod can call them safely and handle the error, or check `game.dlc.status()` first. The kits are Experimental in the [stability tiers](stability.md).

```lua
local dlc = game.dlc.status()
if dlc.Ideology then
  for _, ideo in ipairs(game.ideology.list()) do
    print(ideo.name, #ideo.memes, ideo.pawns)
  end
end

if dlc.Anomaly then
  -- Tame Anomalies tuning: look further, check more often, never auto draft
  game.anomaly.set_engagement({ range = 80, interval = 15, auto_draft = false })
end
```

Ideology covers ideoligions, memes (add and remove, with the game's own conflict checks), precepts, roles, certainty, rituals, style categories and renaming. Royalty covers titles, honor, permits, psylink level, abilities, the Empire and throne rooms. Biotech covers xenotypes, gene definitions, mechanitors and their mechs, pregnancy, growth points, hemogen and gene packs. Anomaly covers the monolith, studies, containment, platforms, the codex, creepjoiners and how controlled pawns pick fights. Odyssey covers planet layers, gravship engines (fuel, launch range, cooldown, substructure, linked components), space maps and the gravship in flight. Launching, travel and orbital sites are not exposed yet, so it is still the least complete.

The `game.anomalies` object and the `game.anomaly` recruit, capture and release functions are on the [Anomaly kit](anomaly.md) page.

{{functions}}

=== dev | Developer tools ===
`game.dev` holds the tools for building a mod: Development mode, hot reload, debug actions, an event recorder, an expression evaluator, def name export for the editor and the diagnostics bundle. The kit is Advanced in the [stability tiers](stability.md). The window that uses them (F11) and the profiler are described in [performance and diagnostics](../guide/performance.md).

```lua
-- A debug action shows up in the dev tools window
game.dev.action("Spawn 100 steel", function()
  local map = game.maps.current()
  game.things.spawn_at("Steel", map, 50, 50, { count = 100 })
end, "Drops steel in the middle of the map")

-- Reload this mod's Lua whenever a file changes
game.dev.watch(true)

-- Record the events of ten seconds of play, then read them
game.dev.record_start("pawn.")
game.timer.after(600, function()
  game.dev.record_stop()
  for _, e in ipairs(game.dev.record_log()) do print(e.tick, e.name, e.payload) end
end)
```

`mode`, `export_defs` and `bundle` are kit functions below. The others are written in Lua:

| Function | What it does |
|----------|--------------|
| `game.dev.action(name, fn, description?)`, `actions()`, `run(name)` | Debug actions |
| `game.dev.eval(code)` | Runs a Lua expression or statement and returns `ok, text`. Needs Development mode and the `dev` capability |
| `game.dev.watch(on)`, `game.dev.reload(package_id)` | Hot reload |
| `game.dev.record_start(filter?)`, `record_stop()`, `record_log()`, `record_clear()`, `recording()` | Event recorder. A filter keeps the events whose name contains it and installs only those. Without one every event is installed, which costs a patch each |
| `game.dev.open_tools()` | Opens the dev tools window |
| `game.dev.current_mod()`, `game.dev.show(value)` | The running mod's package id, a value as text |
| `game.profiler.report()`, `game.profiler.reset()` | Time per mod, see [performance](../guide/performance.md) |

{{functions}}

=== interop | Mod interop ===
`game.interop` lets mods offer functions to each other with versions, without a shared assembly and without caring about load order. The functions are written in Lua, so they have no kit table. The API is Experimental in the [stability tiers](stability.md).

A mod publishes a table of functions under a global name and a semantic version. Another mod asks for it with a version requirement:

```lua
-- Mod A
game.interop.publish("colonyreport", "1.2.0", {
  best_skills = function(skills, count) ... end,
})

-- Mod B, loaded before or after A
game.interop.when("colonyreport", "^1.0", function(api, version)
  print(api.best_skills(skills, 3))
end)

-- or ask once, safely
local ok, text = game.interop.call("colonyreport", "best_skills", skills, 3)   -- false when A is missing
```

| Function | What it does |
|----------|--------------|
| `publish(name, version, api)` | Offers a table of functions. A name belongs to the first mod that publishes it |
| `get(name, requirement?)` | The table, or `nil` and the reason (not published, or the version does not fit) |
| `has(name, requirement?)` | Whether `get` would succeed |
| `when(name, requirement, fn)` | Runs `fn(api, version)` now if the API is there, or when it is published |
| `call(name, fn, ...)` | Calls one function. Returns `false` instead of raising when the mod, the function or the version is missing |
| `list()` | Every published API with its owner, version and function names |
| `satisfies(version, requirement)` | The version check on its own |

Requirements: `"1.2.0"` exact, `">=1.2"`, `">1.2"`, `"<2.0"`, `"<=1.9"`, `"^1.2"` (same major, at least 1.2) and `"~1.2"` (same major and minor, at least 1.2). Follow semantic versioning in what you publish: a breaking change needs a new major version, so mods asking for `^1` keep working.

To see which mods are installed and in what order, use `game.mods.list()`, `game.mods.active(id)` and `game.mods.order()`. To hook another mod's C# code, use `game.hooks.in_mod`. Both are on the [patch and mods page](patch.md).
