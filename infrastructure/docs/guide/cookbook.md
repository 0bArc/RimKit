# Cookbook

One recipe for every row of the [mod ideas](../mod-ideas.md) list. Each is a starting point: copy it into `Lua/main.luau`, change the numbers, and read the kit page it links to. The recipes are short on purpose. They show which functions to reach for, not a finished mod.

How to read them:

- Events pass one payload table, see [events](../api/events.md) for the fields. Functions that take a pawn, thing, map or faction accept the wrapped object from an event or its handle.
- `local map = game.maps.current()` is the map the player is looking at. Recipes that say `map` mean that.
- Recipes that touch hooks or reflection need that [capability](capabilities.md) in `meta.capabilities`.
- A recipe tagged "needs Ideology" (or another DLC) should be wrapped in `if game.dlc.active("Ideology") then ... end`.
- A check in this repository verifies that every `game.*` function used on this page exists, and that every mod idea has a recipe.

## Quality of life

**Smart notifications.** Mute letters by label, and keep the rest. A hook on the letter stack is the full version, a message filter is the cheap one. [events](../api/events.md), [hooks](../api/hooks.md)

```lua
local MUTE = { ["Trader caravan"] = true }
game.events.on("letter.received", function(e)
  if MUTE[e.label] then game.log.info("muted letter: " .. e.label) end
end)
```

**Alert filter.** Add your own alert, for example low steel. [HUD](../api/hud.md)

```lua
game.alerts.add("my.low_steel", function()
  local steel = game.query.count_by_def({ map = game.maps.current(), def = "Steel" }).Steel or 0
  return { active = steel < 100, label = "Low steel", explanation = "Fewer than 100 steel on the map." }
end)
```

**Better pause.** Pause on a raid, a death or a finished project. [time](../api/time.md)

```lua
for _, name in ipairs({ "pawn.died", "research.finished", "pawn.downed" }) do
  game.events.on(name, function() game.time.set_paused(true) end)
end
game.events.on("incident.fired", function(e)
  if e.incident == "RaidEnemy" then game.time.set_paused(true) end
end)
```

**Speed profiles.** Slow down while colonists fight, speed up when nothing happens.

```lua
game.events.on("mental_state.started", function() game.time.set_speed("Normal") end)
game.events.on("time.day_changed", function() game.time.set_speed("Fast") end)
```

**One-click loadouts.** Save what a colonist carries and put it back later. [pawns](../api/pawns.md)

```lua
local saved = {}
function save_loadout(pawn)
  local g = game.pawns.gear(pawn)
  saved[pawn.handle] = g
end
function apply_loadout(pawn, weapon)
  game.pawns.equip(pawn, weapon)
end
```

**Bulk work priorities.** Apply one template to the selected colonists. [work and selection](../api/selection.md)

```lua
for _, p in ipairs(game.selection.pawns()) do
  game.work.set_priority(p, "Cooking", 1)
  game.work.set_priority(p, "Hauling", 3)
end
```

**Stack and haul tidy.** Find loose items of one kind and send a colonist to haul them. [jobs](../api/jobs.md), [queries](../api/query.md)

```lua
local map = game.maps.current()
local hauler = game.query.pawns({ map = map, faction = "player" })[1]
for _, item in ipairs(game.query.things({ map = map, def = "Steel" })) do
  game.jobs.haul(hauler, item, true)
end
```

**Colony stats panel.** Build the rows in a `view` function and return a `table` widget. [widgets](../api/widgets.md)

```lua
game.widgets.open("Colony", function()
  local wealth = game.economy.wealth()
  return { type = "table", columns = { { label = "Stat" }, { label = "Value" } },
           rows = { { "Silver", tostring(game.economy.silver()) }, { "Wealth", tostring(wealth.total) } } }
end, nil, { width = 320, height = 200, refresh = 60 })
```

**Quick-bind hotkeys.** Register a key and poll it each tick. [input](../api/input.md)

```lua
game.input.register_key("MyMod_Jump", "Jump to the first alert", "F6")
game.events.on_tick(function()
  if game.input.binding_just_pressed("MyMod_Jump") then
    local p = game.query.pawns({ map = game.maps.current(), faction = "player" })[1]
    if p then game.camera.jump_thing(p) end
  end
end)
```

**Save-scum guard.** Write a backup save before a risky action. [save](../api/save.md)

```lua
game.events.on("game.saving", function() game.log.info("saving, " .. #game.save.files() .. " save files") end)
game.timer.after(60000, function() game.save.now("backup_" .. game.time.ticks()) end)
```

## Colony management

**Auto-draft on threat.** Draft everyone when a raid starts, undraft when it is over. [pawns](../api/pawns.md)

```lua
local function draft_all(on)
  for _, p in ipairs(game.query.pawns({ map = game.maps.current(), faction = "player" })) do
    game.pawns.set_drafted(p, on)
  end
end
game.events.on("incident.fired", function(e) if e.incident == "RaidEnemy" then draft_all(true) end end)
```

**Shift scheduler.** Give night owls a night shift. [pawns](../api/pawns.md)

```lua
for _, p in ipairs(game.query.pawns({ map = game.maps.current(), faction = "player" })) do
  if game.pawns.has_trait(p, "NightOwl") then
    for hour = 0, 7 do game.pawns.set_assignment(p, hour, "Work") end
    for hour = 12, 17 do game.pawns.set_assignment(p, hour, "Sleep") end
  end
end
```

**Resource governor.** Suspend a bill when stock is high. [bills](../api/build.md)

```lua
local function govern(bench, index, def, limit)
  local have = game.query.count_by_def({ map = game.maps.current(), def = def })[def] or 0
  game.bills.suspend(bench, index, have > limit)
end
```

**Smart doctor.** Send the best doctor to the worst injured colonist. [jobs](../api/jobs.md)

```lua
game.events.on("pawn.downed", function(e)
  local doctor
  for _, p in ipairs(game.query.pawns({ map = game.maps.current(), faction = "player" })) do
    if game.pawns.skill(p, "Medicine") >= (doctor and game.pawns.skill(doctor, "Medicine") or 1) then doctor = p end
  end
  if doctor then game.jobs.tend(doctor, e.pawn) end
end)
```

**Immigration control.** Look at who is asking to join before the player decides. [quests](../api/storyteller.md)

```lua
game.events.on("quest.added", function(e) game.log.info("new quest: " .. e.name) end)
game.events.on("pawn.joined_colony", function(e)
  if game.pawns.has_trait(e.pawn, "Abrasive") then game.ui.message(e.pawn.name .. " is abrasive") end
end)
```

**Prisoner pipeline.** Recruit a prisoner once resistance is low. [pawns](../api/pawns.md)

```lua
game.events.on("time.day_changed", function()
  for _, p in ipairs(game.maps.prisoners(game.maps.current())) do
    if game.pawns.guest_info(p).resistance < 1 then game.pawns.recruit(p) end
  end
end)
```

**Trade assistant.** Show what a trader would pay for your stock. [economy](../api/economy.md)

```lua
game.events.on("trade.completed", function(e) game.ui.message("Trade done, silver: " .. game.economy.silver()) end)
local price = game.economy.price("Steel")
game.log.info("steel sells for " .. tostring(price.sell))
```

**Zone painter.** Create an area and paint its cells. [areas](../api/designators.md)

```lua
local map = game.maps.current()
game.areas.create(map, "Kitchen")
for x = 40, 44 do for z = 40, 44 do game.areas.set(map, "Kitchen", x, z, true) end end
```

**Power manager.** Charge batteries to a level. [power](../api/build.md)

```lua
game.events.on("time.hour_changed", function()
  for _, b in ipairs(game.query.things({ map = game.maps.current(), def = "Battery" })) do
    if (game.power.net(b).stored or 0) < 100 then game.log.info("battery low") end
  end
end)
```

**Farm planner.** Pick the crop with the best fertility match. [plants](../api/plants.md)

```lua
local map = game.maps.current()
local function best_crop(x, z)
  local fertility = game.plants.fertility(map, x, z)
  return fertility > 1.2 and "Plant_Rice" or "Plant_Potato"
end
```

## Combat and threats

**Raid director.** Fire your own raid with your own points. [economy and raids](../api/economy.md)

```lua
game.events.on("time.day_changed", function()
  if game.raids.can_fire() then game.raids.fire({ points = game.storyteller.threat_points() * 0.5 }) end
end)
```

**Damage numbers.** Floating text for hits. [effects](../api/effects.md)

```lua
-- the filter runs before the function, so walls and animals never reach it
game.events.on("pawn.damaged", { humanlike = true, min_dealt = 1 }, function(e)
  game.effects.text(e.pawn, tostring(math.floor(e.dealt)), "orange")
end)
```

**Armor and stat inspector.** Explain one stat of the selected thing. [research and stats](../api/research.md)

```lua
local t = game.selection.inspected()
if t and t.thing then
  local why = game.stats.explain(t.thing, "ArmorRating_Sharp")
  game.log.info(game.json.encode(why))
end
```

**Kill feed.** One line per death. [events](../api/events.md)

```lua
game.events.on("pawn.died", function(e)
  game.hud.status("my.kill", e.pawn.name .. " died", "warning", "top_left")
end)
```

**Friendly fire rules.** Zero out damage from one faction with a hook. Needs `hooks`. [hooks](../api/hooks.md)

```lua
game.hooks.prefix("Verse.Thing", "TakeDamage", function(ctx)
  local info = ctx.args[1]
  if info and info.instigator and game.pawns.faction_is_player(info.instigator) then ctx:skip() end
end)
```

**Turret tuner.** Change a turret def with the def journal. [defs](../api/defs.md)

```lua
game.defs.set("ThingDef", "Turret_MiniTurret", "building.turretBurstCooldownTime", 2.5)
```

**Boss encounters.** Spawn a boss, watch its health, reward on death. [things](../api/things.md), [events](../api/events.md)

```lua
local map = game.maps.current()
game.pawns.generate({ kind = "Mech_Centipede", faction = "Mechanoid", map = map, x = 50, z = 50 })
game.events.on("pawn.died", function(e)
  if e.pawn.def == "Mech_Centipede" then game.things.spawn_at("Gold", map, 50, 50, { count = 50 }) end
end)
```

**Last stand mode.** Buff the survivors when the colony is nearly dead. [pawns](../api/pawns.md)

```lua
game.events.on("pawn.died", function()
  local alive = game.query.pawns({ map = game.maps.current(), faction = "player" })
  if #alive <= 2 then
    for _, p in ipairs(alive) do game.pawns.give_inspiration(p, "Frenzy_Shoot") end
  end
end)
```

## Storytelling and events

**Custom storyteller.** A storyteller comp that picks incidents from Lua. [Lua classes](../api/classes.md)

```lua
game.classes.define("storyteller", "my_teller", {
  incidents = function(map, tick)
    if tick % 60000 == 0 then return { { incident = "WandererJoin" } } end
  end,
})
```

**Scripted quests.** A quest node written in Lua, run by a quest script def. [Lua classes](../api/classes.md)

```lua
game.classes.define("quest_node", "give_reward", {
  run = function()
    local points = game.quests.slate_get("points") or 100
    game.quests.slate_set("reward_silver", math.floor(points * 2), "int")
  end,
})
```

**Chain events.** Each outcome starts the next. [incidents](../api/storyteller.md)

```lua
game.events.on("incident.fired", function(e)
  if e.incident == "TraderCaravanArrival" and e.success then
    game.incidents.queue("WandererJoin", 2500)
  end
end)
```

**Colony chronicle.** Append one line per notable event to saved data. [save](../api/save.md)

```lua
local PKG = "my.chronicle"
local function note(text)
  local log = game.save.fetch(PKG, "game", "log", nil, {})
  log[#log + 1] = game.time.date_text() .. ": " .. text
  game.save.put(PKG, "game", "log", log)
end
game.events.on("pawn.died", function(e) note(e.pawn.name .. " died") end)
game.events.on("incident.fired", function(e) note(e.incident) end)
```

**Random encounter packs.** A letter with branching choices. [HUD](../api/hud.md)

```lua
game.hud.letter("A stranger", "They ask to join.", { "Welcome", "Turn away" }, "stranger")
game.events.on("letter.choice", function(p)
  if p.tag == "stranger" and p.index == 1 then game.pawns.generate({ kind = "Villager" }) end
end)
```

**Seasonal festivals.** Give everyone a mood boost at the start of a quadrum. Needs a thought def in XML. [pawns](../api/pawns.md)

```lua
game.events.on("time.quadrum_changed", function()
  for _, p in ipairs(game.query.pawns({ map = game.maps.current(), faction = "player" })) do
    game.pawns.add_thought(p, "AteLavishMeal")
  end
end)
```

**Difficulty scaler.** Lower threat after deaths, raise it when things are calm. [storyteller](../api/storyteller.md)

```lua
local deaths = 0
game.events.on("pawn.died", function() deaths = deaths + 1 end)
game.events.on("time.year_changed", function()
  game.storyteller.set_difficulty("threatScale", deaths > 3 and 0.8 or 1.2)
  deaths = 0
end)
```

## Pawns, social and mind

**Personality engine.** Quirks from rules, added as traits. [pawns](../api/pawns.md)

```lua
game.events.on("pawn.birthday", function(e)
  if e.age == 18 and game.pawns.skill(e.pawn, "Shooting") >= 10 then game.pawns.add_trait(e.pawn, "Bloodlust") end
end)
```

**Relationship drama.** Make a rivalry visible with a thought. [pawns](../api/pawns.md)

```lua
game.events.on("relation.formed", function(e)
  if e.relation == "Rival" then game.pawns.add_thought(e.pawn, "RivalDied", e.other) end
end)
```

**Backstory editor.** Reroll a childhood backstory. [pawns](../api/pawns.md)

```lua
game.pawns.set_backstory(game.selection.pawns()[1], "childhood", "Urbworld5")
```

**Mood explainer.** List the thoughts behind a mood. [pawns](../api/pawns.md)

```lua
for _, t in ipairs(game.pawns.thoughts(game.selection.pawns()[1])) do
  game.log.info(t.label .. " " .. t.mood_offset)
end
```

**Skill coach.** Suggest the weakest skill. [pawns](../api/pawns.md)

```lua
local function weakest(pawn)
  local worst
  for _, s in ipairs(game.pawns.skills(pawn)) do
    if not s.disabled and (not worst or s.level < worst.level) then worst = s end
  end
  return worst and worst.def
end
```

**Memorials.** Place a plaque where someone died. [things](../api/things.md)

```lua
game.events.on("pawn.died", function(e)
  local c = e.pawn:info()
  game.things.spawn_at("Grave", game.maps.current(), c.x, c.z)
end)
```

**Legacy system.** Children inherit a trait from a parent. Needs Biotech. [Biotech](../api/dlc.md)

```lua
game.events.on("pawn.born", function(e)
  for _, r in ipairs(game.pawns.relations(e.pawn)) do
    if r.def == "Parent" then
      for _, t in ipairs(game.pawns.traits(r.other)) do game.pawns.add_trait(e.pawn, t.def) return end
    end
  end
end)
```

**Mental break rules.** Start a custom break and clear it later. [pawns](../api/pawns.md)

```lua
game.events.on("mental_state.started", function(e)
  game.timer.after(1800, function() game.pawns.clear_mental_state(e.pawn) end)
end)
```

## World, factions and economy

**Living world.** Let settlements gain goodwill over time. [world](../api/world.md), [factions](../api/factions.md)

```lua
game.events.on("time.quadrum_changed", function()
  for _, s in ipairs(game.world.settlements()) do game.log.info(s.label) end
end)
```

**Diplomacy overhaul.** Tribute: pay silver, gain goodwill. [factions](../api/factions.md)

```lua
function pay_tribute(faction, silver)
  local have = game.query.count_by_def({ map = game.maps.current(), def = "Silver" }).Silver or 0
  if have >= silver then
    -- take the silver with stack:destroy_with() on the stacks, then raise goodwill
    game.factions.adjust_goodwill(faction, game.factions.player(), silver / 10)
  end
end
```

**Caravan manager.** Watch caravans and list them. [world](../api/world.md)

```lua
game.events.on("caravan.formed", function(e)
  for _, c in ipairs(game.world.caravans()) do game.log.info(c.label .. " carries " .. c.pawns .. " pawns") end
end)
```

**Dynamic prices.** Scale a market value with the season. [defs](../api/defs.md), [stats](../api/research.md)

```lua
game.events.on("time.quadrum_changed", function()
  local winter = game.weather.season().season == "Winter"
  game.stats.add_factor("MarketValue", winter and 1.3 or 1.0, { target = "item" })
end)
```

**Contracts board.** Generate a quest from a script and offer it. [quests](../api/storyteller.md)

```lua
function offer_contract()
  local q = game.quests.generate("OpportunitySite_ItemStash", game.storyteller.threat_points())
  if q then game.quests.accept(q.id) end
end
```

**Site and loot packs.** Create a world object on a tile. [world](../api/world.md)

```lua
local tile = game.world.find_tile({ biome = "TemperateForest" })
if tile then game.world.create_object("Site", tile) end
```

**Faction leaders.** Read and keep a leader. [factions](../api/factions.md)

```lua
for _, f in ipairs(game.factions.hostiles(game.factions.player())) do
  local leader = game.factions.leader(f)
  if leader then game.log.info(f.name .. " is led by " .. leader.name) end
end
```

**Biome events.** Start a game condition per season. [conditions](../api/conditions.md)

```lua
game.events.on("time.quadrum_changed", function()
  if game.weather.season().season == "Winter" then game.conditions.start("ColdSnap", { duration = 20000 }) end
end)
```

## Rules, balance and sandbox

**Stat tweaker.** Live edits of any stat, with a way back. [defs](../api/defs.md)

```lua
game.defs.set("ThingDef", "Steel", "stackLimit", 300)
-- later: game.defs.restore("ThingDef", "Steel", "stackLimit")
```

**Hardcore rules.** Count deaths and end the run. [time](../api/time.md)

```lua
game.events.on("pawn.died", function(e)
  if e.pawn.is_colonist then game.ui.message("Permadeath: " .. e.pawn.name) end
end)
```

**Challenge scenarios.** Start conditions from a scenario part written in Lua. [scenarios](../api/generation.md), [Lua classes](../api/classes.md)

```lua
game.classes.define("scen_part", "poor_start", {
  post_game_start = function()
    game.economy.give_silver(-game.economy.silver())
  end,
  summary = function() return "Start with no silver" end,
})
```

**Cheat console.** The dev tools console is `game.dev.eval`. Add your own command as a debug action. [dev](../api/dev.md)

```lua
game.dev.action("Spawn 100 steel", function()
  local p = game.maps.current()
  game.things.spawn_at("Steel", p, 50, 50, { count = 100 })
end)
```

**Map editor.** Change terrain and roofs. [maps](../api/maps.md)

```lua
local map = game.maps.current()
for x = 10, 14 do game.maps.set_terrain(map, x, 10, "Concrete") end
game.maps.set_roof(map, 12, 10, "RoofConstructed")
```

**Pawn forge.** Generate a pawn with chosen skills. [pawns](../api/pawns.md)

```lua
local p = game.pawns.generate({ kind = "Colonist" })
game.pawns.set_skill(p, "Shooting", 15)
game.pawns.add_trait(p, "Tough")
```

**Rule sets.** Bundle tweaks in one table and apply them. [defs](../api/defs.md)

```lua
local RULES = { { "ThingDef", "Steel", "stackLimit", 300 }, { "ThingDef", "Silver", "stackLimit", 5000 } }
for _, r in ipairs(RULES) do game.defs.set(r[1], r[2], r[3], r[4]) end
```

**Replay recorder.** Save one snapshot per day. [save](../api/save.md)

```lua
game.events.on("time.day_changed", function()
  local days = game.save.fetch("my.replay", "game", "days", nil, {})
  days[#days + 1] = { tick = game.time.ticks(), colonists = #game.query.pawns({ map = game.maps.current(), faction = "player" }) }
  game.save.put("my.replay", "game", "days", days)
end)
```

## UI and presentation

**Custom tabs and windows.** A main tab with a widget view. [tabs](../api/tabs.md)

```lua
game.tabs.add_main("my_tab", "My tab", function() return { type = "label", text = "Hello" } end)
```

**Gizmo packs.** A button on selected colonists. [gizmos](../api/gizmos.md)

```lua
game.gizmos.add("my.rest", { label = "Rest", desc = "Send to bed", target = "pawn" }, function(thing)
  game.pawns.set_need(thing, "Rest", 1.0)
end)
```

**Overlays.** Outline cells with a colour. [effects](../api/effects.md)

```lua
local map = game.maps.current()
local cells = {}
for x = 10, 20 do cells[#cells + 1] = { x = x, z = 10 } end
game.effects.highlight_cells(map, cells, { color = "accent", ticks = 600 })
```

**HUD themes.** Change a colour and the font. [HUD](../api/hud.md)

```lua
game.hud.set_color("accent", "#ffaa00")
game.hud.set_font("Small")
```

**Tutorials and onboarding.** A letter that walks through the first steps. [HUD](../api/hud.md)

```lua
game.events.on("game.ready", function()
  game.hud.letter("Welcome", "Press F6 to jump to a colonist.", { "Got it" }, "tutorial_1")
end)
```

**Accessibility pack.** Bigger text and an audio cue on alerts. [audio](../api/audio.md)

```lua
game.hud.set_font("Medium")
game.events.on("incident.fired", function() game.audio.play_at(game.maps.current(), "Message_Alert", 50, 50) end)
```

**Mod settings hub.** One options page for your mod. [HUD](../api/hud.md)

```lua
game.options.page("my.mod", "My mod", {
  { key = "my.mod.enabled", type = "bool", label = "Enabled", default = true },
  { key = "my.mod.speed", type = "float", label = "Speed", default = 1 },
})
```

**Stream overlay.** Write colony state to a file a streaming tool can read. Needs `files`. [save](../api/save.md)

```lua
game.events.on("time.hour_changed", function()
  local state = { colonists = #game.query.pawns({ map = game.maps.current(), faction = "player" }), silver = game.economy.silver() }
  game.util.write_export("my.stream", "state.json", game.json.encode(state))
end)
```

## Content and DLC

**Ideology rules.** Add a precept to your ideoligion. Needs Ideology. [DLC](../api/dlc.md)

```lua
if game.dlc.active("Ideology") then game.ideology.add_precept(game.ideology.list()[1].id, "Meat_Preferred") end
```

**Royalty extras.** Grant a title with its rewards. Needs Royalty. [DLC](../api/dlc.md)

```lua
if game.dlc.active("Royalty") then game.royalty.set_title(game.selection.pawns()[1], "Knight", nil, true, true) end
```

**Biotech lab.** Change a pawn's xenotype and list gene defs. Needs Biotech. [DLC](../api/dlc.md)

```lua
if game.dlc.active("Biotech") then game.biotech.set_xenotype(game.selection.pawns()[1], "Hussar") end
```

**Anomaly expansions.** Raise the monolith level and read the codex. Needs Anomaly. [DLC](../api/dlc.md)

```lua
if game.dlc.active("Anomaly") then game.anomaly.set_monolith_level(2) end
```

**Odyssey travel.** Read the engine fuel and the space maps. Needs Odyssey. [DLC](../api/dlc.md)

```lua
if game.dlc.active("Odyssey") then
  for _, e in ipairs(game.odyssey.engines(game.maps.current())) do
    local info = game.odyssey.engine_info(e.engine)
    game.log.info(info.name .. " fuel " .. info.fuel .. "/" .. info.max_fuel)
  end
end
```

**Entity tamer.** Recruit an anomaly entity. Needs Anomaly. [DLC](../api/dlc.md)

```lua
if game.dlc.active("Anomaly") then
  local e = game.anomaly.get_on_map("Revenant")
  if e then game.anomaly.recruit(e) end
end
```

**New factions and cultures.** Write a faction def from a table. [defs](../api/defs.md)

```lua
game.defs.write_xml("my.mod", "FactionDef", "MyTribe", { label = "My tribe", pawnSingular = "tribal", pawnsPlural = "tribals" })
```

**Animal husbandry.** Train an animal and set its master. [pawns](../api/pawns.md)

```lua
local animal = game.query.pawns({ map = game.maps.current(), kind = "Muffalo" })[1]
if animal then game.pawns.train(animal, "Obedience"); game.pawns.set_master(animal, game.selection.pawns()[1]) end
```

## Tools for modders

**Event recorder and replay.** Record events in the dev tools, replay them in a test. [dev](../api/dev.md), [testing](testing.md)

```lua
-- in a test
local t = game.test
t.emit("pawn.died", { pawn = 1 })
```

**Hook inspector.** List live hooks with their cost. [hooks](../api/hooks.md)

```lua
for _, h in ipairs(game.hooks.list()) do game.log.info(h.type .. "." .. h.method .. " " .. h.avg_us .. " us") end
```

**Live reflection browser.** The F11 window has a Reflect tab. In code, `members` lists what an object has. [reflect](../api/reflect.md)

```lua
for _, m in ipairs(game.reflect.members("Verse.TickManager")) do game.log.info(m.name .. " " .. m.kind) end
```

**Def explorer.** Find which mod defined a def. [defs](../api/defs.md)

```lua
local info = game.defs.of("ThingDef", "Steel")
game.log.info(info.def .. " comes from " .. tostring(info.mod))
```

**Performance profiler.** Per mod time. [performance](performance.md)

```lua
for _, r in ipairs(game.profiler.report()) do game.log.info(r.mod .. " " .. r.avg_tick_us .. " us per tick") end
```

**Test harness.** A test with a mocked game. [testing](testing.md)

```lua
local t = game.test
t.describe("my mod", function()
  t.it("warns on low medicine", function()
    t.mock("query.count_by_def", { MedicineIndustrial = 2 })
    t.emit("time.day_changed", {})
    t.expect(t.logged("Low on medicine")).to_be(true)
  end)
end)
```

**Mod pack manager.** List active mods and switch one off. [interop](../api/interop.md)

```lua
for _, id in ipairs(game.mods.order()) do game.log.info(id) end
if game.mods.active("some.mod") then game.mods.deactivate("some.mod") end
```

## A few more small ones

**A new incident in Lua.** An `IncidentDef` with `RimKit.IncidentWorker_Lua` and a `game.classes.define("incident", ...)`. [Lua classes](../api/classes.md)

**Make shooters more accurate.** One tweak, no Harmony. [tweak catalog](../api/tweaks.md)

```lua
game.tweaks.on("shot.hit_factor_shooter", function(t) return math.min(1, t.value + 0.15) end)
```

**Count what is in stock and warn when low.** [queries](../api/query.md)

```lua
game.events.on("time.day_changed", function()
  local counts = game.query.count_by_def({ map = game.maps.current(), def = "MedicineIndustrial" })
  if (counts.MedicineIndustrial or 0) < 5 then game.ui.message("Low on medicine") end
end)
```

**Offer your mod's functions to other mods.** `game.interop.publish`. [interop](../api/interop.md)

**Ship a patch from a setting.** `game.patch.write("my.mod", "patch", { { op = "set_attribute", ... } })` writes an XML patch applied at the next start. [patch](../api/patch.md)

**Test without the game.** `rimkit mod test`, see [testing](testing.md).
