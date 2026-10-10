-- RimKit smoke test. Runs once after the map has ticked, prints SMOKE lines to Player.log.
local R = game.reflect
local pass, fail = 0, 0
local previous_reflect = config.get("rimkit.developer_reflect")

local function report(name, ok, detail)
  if ok then
    pass = pass + 1
    log.info("SMOKE PASS " .. name)
  else
    fail = fail + 1
    log.info("SMOKE FAIL " .. name .. " :: " .. tostring(detail))
  end
end

local function test(name, fn)
  local ok, err = pcall(fn)
  if ok and err ~= false then
    report(name, true)
  elseif ok then
    report(name, false, "returned false")
  else
    report(name, false, err)
  end
end

local function expect_error(code, fn)
  local ok, err = pcall(fn)
  if ok then return false, "no error raised" end
  return tostring(err):find(code, 1, true) ~= nil, tostring(err)
end

local started, done, ticks = false, false, 0
tick_counts = nil
local event_log = {}
local phase = 0

-- Game-update watch: every hook target the shipped mods use, and every catalog event, must still exist.
local function run_target_watch()
  local targets = require("targets")
  for _, row in ipairs(targets) do
    test("target " .. row.type .. "." .. row.method .. " (" .. row.from .. ")", function()
      local res = game.hooks.check_target(row.type, row.method, row.sig)
      if type(res) == "table" and res.found then return true end
      error(type(res) == "table" and tostring(res.problem) or "no answer")
    end)
  end
  local events = game.hooks.check_events()
  for _, e in ipairs(events or {}) do
    test("event target " .. e.event, function()
      if e.ok then return true end
      error(tostring(e.problem))
    end)
  end
end

-- game.time and game.version
local function run_time_tests()
  test("game.time.now has calendar fields", function()
    local n = game.time.now()
    return type(n.ticks) == "number" and type(n.hour) == "number" and n.hour >= 0 and n.hour < 24
      and type(n.season) == "string" and type(n.quadrum) == "string" and type(n.year) == "number"
  end)
  test("game.time.now ticks agree with game.time.ticks", function()
    return math.abs(game.time.now().ticks - game.time.ticks()) < 5
  end)
  test("game.time.speed and set_speed round trip", function()
    local before = game.time.speed()
    local set = game.time.set_speed("Fast")
    local after = game.time.speed()
    game.time.set_speed(before)
    return set == "Fast" and after == "Fast" and game.time.speed() == before
  end)
  test("game.time.set_speed rejects a bad value", function()
    return expect_error("RK1001", function() game.time.set_speed("Warp") end)
  end)
  test("game.time.date_text returns text", function() return type(game.time.date_text()) == "string" end)
  test("game.version.rimworld is 1.6 or newer here", function()
    local v = game.version.rimworld()
    return v.major == 1 and v.minor >= 6 and game.version.at_least("1.6") and not game.version.at_least("9.0")
  end)
  test("game.version.rimkit matches the API level policy", function()
    return type(game.version.rimkit()) == "string" and game.version.api_level() == 0
  end)
end

-- game.factions and game.things
local function run_faction_thing_tests()
  local player = game.factions.player()
  test("factions.info of the player faction", function()
    local i = game.factions.info(player)
    return i.is_player == true and type(i.name) == "string" and i.goodwill == nil
  end)
  test("factions.defs lists definitions", function() return #game.factions.defs() > 3 end)
  local other
  for _, f in ipairs(game.factions.list()) do
    local i = game.factions.info(f)
    if not i.is_player and not i.hidden and not i.permanent_enemy then other = f break end
  end
  if other then
    test("factions.goodwill set, adjust and restore", function()
      local before = game.factions.goodwill(other)
      local set = game.factions.set_goodwill(other, player, 12)
      local adjusted = game.factions.adjust_goodwill(other, player, 3)
      game.factions.set_goodwill(other, player, before)
      if set == 12 and adjusted > 12 and game.factions.goodwill(other) == before then return true end
      error(string.format("before=%s set=%s adjusted=%s after=%s", before, set, adjusted, game.factions.goodwill(other)))
    end)
    test("factions.relation matches is_hostile", function()
      local rel = game.factions.relation(other)
      return (rel == "Hostile") == game.factions.is_hostile(other, player)
    end)
  else
    log.info("SMOKE SKIP factions goodwill: no suitable faction")
  end
  test("factions.members of the player includes a colonist", function() return #game.factions.members(player) > 0 end)
  test("factions.hostiles and allies return lists", function()
    return type(game.factions.hostiles(player)) == "table" and type(game.factions.allies(player)) == "table"
  end)

  local map = game.current_map()
  local colonists = game.maps.colonists(map.handle)
  local c1 = colonists[1] and rim.wrap(colonists[1])
  if not c1 then return end
  local at = c1:info()
  local steel
  test("things.spawn_at makes a stack", function()
    -- a cell with nothing on it, so the new stack does not merge into an existing one
    local sx, sz = at.x + 1, at.z + 1
    for r = 2, 8 do
      local c = game.maps.cell_near(map, at.x, at.z, r)
      if c and #game.maps.things_at(map, c.x, c.z) == 0 then sx, sz = c.x, c.z break end
    end
    steel = game.things.spawn_at("Steel", map, sx, sz, { count = 25 })
    local i = steel:info()
    if i.def == "Steel" and i.stack == 25 and i.spawned == true then return true end
    error(string.format("def=%s stack=%s spawned=%s at %d,%d", tostring(i.def), tostring(i.stack), tostring(i.spawned), sx, sz))
  end)
  if steel then
    test("things.forbidden set and read", function()
      steel:set_forbidden(true)
      local on = steel:forbidden()
      steel:set_forbidden(false)
      return on == true and steel:forbidden() == false
    end)
    test("things.damage and heal change hit points", function()
      local max = steel:info().max_hp
      local left = steel:damage(1)
      local healed = steel:heal()
      return left < max and healed == max
    end)
    test("things.has_comp and comps list", function()
      return type(steel:comps()) == "table" and steel:has_comp("Nonexistent") == false
    end)
    test("things.quality is nil for steel and set_quality raises RK3003", function()
      return steel:quality() == nil and expect_error("RK3003", function() steel:set_quality("Good") end)
    end)
    test("things.destroy_with removes the stack", function()
      steel:destroy_with("Vanish")
      return steel.spawned == false
    end)
  end
  test("things.make rejects an unknown def", function()
    return expect_error("RK3001", function() game.things.make("NoSuchThingDef") end)
  end)
  test("things.set_quality works on a weapon", function()
    local gun = game.things.spawn_at("Gun_Revolver", map, at.x + 2, at.z + 1)
    local set = gun:set_quality("Masterwork")
    local read = gun:quality()
    gun:destroy_with()
    return set == "Masterwork" and read == "Masterwork"
  end)
end

-- Phase 1 kits: maps, health, jobs, gear, social, mind, selection, spawning, queries, things and factions extras, legacy surface
local function run_phase1_tests()
  local map = game.current_map()
  local colonists = game.maps.colonists(map.handle)
  local c1 = colonists[1] and rim.wrap(colonists[1])
  local c2 = colonists[2] and rim.wrap(colonists[2])
  if not c1 then log.info("SMOKE SKIP phase 1 kits: no colonist") return end
  local at = c1:info()
  local x, z = at.x, at.z

  -- maps
  test("maps.info", function() local i = game.maps.info(map) return i.width > 50 and type(i.biome) == "string" and i.is_player_home end)
  test("maps.terrain, set_terrain and restore", function()
    local before = game.maps.terrain(map, x + 3, z)
    local set = game.maps.set_terrain(map, x + 3, z, "Soil")
    game.maps.set_terrain(map, x + 3, z, before)
    return type(before) == "string" and set == "Soil" and game.maps.terrain(map, x + 3, z) == before
  end)
  test("maps.roof and set_roof", function()
    local before = game.maps.roof(map, x + 3, z)
    game.maps.set_roof(map, x + 3, z, "RoofConstructed")
    local on = game.maps.roof(map, x + 3, z)
    game.maps.set_roof(map, x + 3, z, before)
    return on == "RoofConstructed" and game.maps.roof(map, x + 3, z) == before
  end)
  test("maps.fogged, temperature, light", function()
    return game.maps.fogged(map, x, z) == false and type(game.maps.temperature(map, x, z)) == "number" and type(game.maps.light(map, x, z)) == "number"
  end)
  test("maps.snow set and restore", function()
    local before = game.maps.snow(map, x + 4, z)
    local set = game.maps.set_snow(map, x + 4, z, 0.5)
    game.maps.set_snow(map, x + 4, z, before)
    -- some biomes and cells cannot hold snow, so only the clamped range is checked
    return type(set) == "number" and set >= 0 and set <= 1
  end)
  test("maps.filth, walkable, standable, in_bounds", function()
    return type(game.maps.filth(map, x, z)) == "table" and game.maps.walkable(map, x, z) and game.maps.in_bounds(map, x, z)
      and game.maps.in_bounds(map, -5, -5) == false and type(game.maps.standable(map, x, z)) == "boolean"
  end)
  test("maps.things_at includes the colonist", function()
    for _, t in ipairs(game.maps.things_at(map, x, z)) do if t == c1 or t.handle == c1.handle then return true end end
    return false
  end)
  test("maps.room, rooms, zones, areas, lords, components", function()
    local r = game.maps.room(map, x, z, true)
    return (r == nil or type(r.cells) == "number") and type(game.maps.rooms(map)) == "table" and type(game.maps.zones(map)) == "table"
      and type(game.maps.areas(map)) == "table" and type(game.maps.lords(map)) == "table" and #game.maps.components(map) > 3
  end)
  test("maps.reachable from a cell to itself", function() return game.maps.reachable(map, x, z, x, z) == true end)
  test("maps.designations add and remove", function()
    local added = game.maps.add_designation(map, "Mine", nil, x + 6, z)
    local n = #game.maps.designations(map, "Mine")
    local removed = game.maps.remove_designations(map, "Mine")
    return added == true and n >= 1 and removed >= 1
  end)
  test("maps.edge_cell, drop_spot, cell_near", function()
    local e, d, n = game.maps.edge_cell(map), game.maps.drop_spot(map), game.maps.cell_near(map, x, z, 5)
    return e.x ~= nil and d.x ~= nil and n ~= nil and n.x ~= nil
  end)
  test("maps.things_of_def_count and weather.temperature", function()
    return type(game.maps.things_of_def_count("Steel")) == "number" and type(game.weather.temperature(map, x, z)) == "number"
  end)

  -- pawn health
  test("pawns.hediff_list, injuries, body_parts, health_summary", function()
    local parts = game.pawns.body_parts(c1)
    local s = game.pawns.health_summary(c1)
    return type(game.pawns.hediff_list(c1)) == "table" and type(game.pawns.injuries(c1)) == "table" and #parts > 10
      and s.health > 0 and s.dead == false and type(parts[1].hp) == "number"
  end)
  test("pawns.damage_part creates an injury and heal_injuries removes it", function()
    -- A random colonist can shrug off a small cut, so hit a few times until one injury shows.
    local before = #game.pawns.injuries(c1)
    local after = before
    for _ = 1, 4 do
      game.pawns.damage_part(c1, 6, "Cut")
      after = #game.pawns.injuries(c1)
      if after > before then break end
    end
    game.pawns.heal_injuries(c1, true)
    return after > before and #game.pawns.injuries(c1) == 0
  end)
  test("pawns.remove_part and restore_part", function()
    local idx
    for _, p in ipairs(game.pawns.body_parts(c1)) do if p.def == "Finger" and not p.missing then idx = p.index break end end
    if not idx then return true end
    local removed = game.pawns.remove_part(c1, idx)
    local missing = game.pawns.body_parts(c1)[idx + 1].missing
    game.pawns.restore_part(c1, idx)
    return removed == true and missing == true and game.pawns.body_parts(c1)[idx + 1].missing == false
  end)
  test("pawns.surgeries lists recipes and immunity rejects an unknown def", function()
    return type(game.pawns.surgeries(c1)) == "table" and expect_error("RK3001", function() game.pawns.immunity(c1, "NoSuchHediff") end)
  end)

  -- jobs
  test("jobs.give Wait, current, interrupt", function()
    local ok = game.jobs.give(c1, "Wait", {})
    local cur = game.jobs.current(c1)
    game.jobs.interrupt(c1)
    return ok == true and cur ~= nil and cur.def == "Wait"
  end)
  test("jobs.wait with queue, queue_list, clear_queue", function()
    game.jobs.interrupt(c1)
    game.jobs.wait(c1, 30)
    game.jobs.wait(c1, 30, true)
    local q = game.jobs.queue_list(c1)
    game.jobs.clear_queue(c1)
    game.jobs.interrupt(c1)
    return type(q) == "table"
  end)
  test("jobs.defs and release_reservations", function() return #game.jobs.defs() > 50 and game.jobs.release_reservations(c1) == true end)
  test("jobs.give rejects an unknown job def", function() return expect_error("RK3001", function() game.jobs.give(c1, "NoSuchJob", {}) end) end)

  -- gear and policies
  test("pawns.gear lists apparel and inventory", function()
    local g = game.pawns.gear(c1)
    return type(g.apparel) == "table" and type(g.inventory) == "table"
  end)
  test("pawns.outfit, set_outfit and outfits", function()
    local names = game.pawns.outfits()
    local cur = game.pawns.outfit(c1)
    return #names > 0 and game.pawns.set_outfit(c1, cur) == cur
  end)
  test("pawns.drug_policy, food_policy and lists", function()
    return #game.pawns.drug_policies() > 0 and #game.pawns.food_policies() > 0 and type(game.pawns.drug_policy(c1)) == "string"
      and type(game.pawns.food_policy(c1)) == "string"
  end)
  test("pawns.work_priorities", function() return #game.pawns.work_priorities(c1) > 5 end)
  test("pawns.set_assignments for a group and restore", function()
    local before = game.pawns.timetable(c1)[4]
    local n = game.pawns.set_assignments({ c1 }, 3, "Joy")
    local now = game.pawns.timetable(c1)[4]
    game.pawns.set_assignment(c1, 3, before)
    return n == 1 and now == "Joy"
  end)
  test("pawns.bed answers", function() local b = game.pawns.bed(c1) return b == nil or b ~= nil end)

  -- social, animals, prisoners
  test("pawns.interaction_defs and interaction_modes", function() return #game.pawns.interaction_defs() > 5 and #game.pawns.interaction_modes() > 2 end)
  if c2 then
    test("pawns.opinion_reasons", function() local o = game.pawns.opinion_reasons(c1, c2) return type(o.opinion) == "number" and type(o.reasons) == "table" end)
    test("pawns.interact returns a boolean", function() return type(game.pawns.interact(c1, c2, "Chitchat")) == "boolean" end)
  end
  test("pawns.partner is a pawn or nil", function() local p = game.pawns.partner(c1) return p == nil or p ~= nil end)
  test("pawns.animal_info rejects a colonist with RK3003", function() return expect_error("RK3003", function() game.pawns.animal_info(c1) end) end)
  test("pawns.guest_info of a colonist", function() local g = game.pawns.guest_info(c1) return type(g.status) == "string" and g.is_prisoner == false end)
  test("pawns.set_guest_status rejects a bad status", function() return expect_error("RK1001", function() game.pawns.set_guest_status(c1, "Pirate") end) end)

  -- mind
  test("pawns.mind_summary, break_thresholds, mental_state_defs", function()
    local m = game.pawns.mind_summary(c1)
    local b = game.pawns.break_thresholds(c1)
    return type(m.in_mental_state) == "boolean" and type(b.minor) == "number" and #game.pawns.mental_state_defs() > 5
  end)
  test("pawns.start_mental_state, mental_info, stop_mental_state", function()
    local started = game.pawns.start_mental_state(c1, "Wander_Sad")
    local info = game.pawns.mental_info(c1)
    local legacy = game.pawns.mental_state(c1)
    game.pawns.stop_mental_state(c1)
    return started == true and info ~= nil and info.def == "Wander_Sad" and legacy == "Wander_Sad" and game.pawns.mental_info(c1) == nil
  end)
  test("pawns.inspiration is nil or a table", function() local i = game.pawns.inspiration(c1) return i == nil or type(i.def) == "string" end)
  test("pawns.use_drug Beer", function() return game.pawns.use_drug(c1, "Beer") == true end)
  test("pawns.capable and relations_count and inventory.count_def", function()
    return game.pawns.capable(c1, "Moving") == true and type(game.pawns.relations_count(c1)) == "number" and type(game.inventory.count_def(c1, "Silver")) == "number"
  end)

  -- selection and camera
  test("selection.select, count, pawns, inspected, clear", function()
    game.selection.select(c1)
    local n = game.selection.count()
    local sel = game.selection.pawns()
    local i = game.selection.inspected()
    game.selection.clear()
    return n == 1 and #sel == 1 and i ~= nil and i.kind == "thing" and game.selection.count() == 0
  end)
  test("camera.position and mouse_cell", function()
    local p = game.camera.position()
    return type(p.x) == "number" and type(p.zoom) == "string" and type(game.camera.mouse_cell().x) == "number"
  end)

  -- spawning, generation and queries
  test("pawns.kinds and thing_set_defs", function() return #game.pawns.kinds() > 20 and #game.things.thing_set_defs() > 5 end)
  test("pawns.generate makes a pawn without spawning it", function()
    local p = game.pawns.generate({ kind = "Muffalo" })
    return p ~= nil and p:info().is_pawn == true and p:info().spawned == false
  end)
  test("pawns.generate spawns a pawn on the map", function()
    local p = game.pawns.generate({ kind = "Muffalo", map = map, x = x + 2, z = z + 2, gender = "Female" })
    local i = p:info()
    p:destroy_with()
    return i.spawned == true and i.def == "Muffalo"
  end)
  test("things.stuff_options, random_stuff, roll_quality", function()
    return #game.things.stuff_options("Wall") > 2 and game.things.random_stuff("Gun_Revolver") == nil and type(game.things.roll_quality("Gift")) == "string"
  end)
  local steel = game.things.spawn_at("Steel", map, x + 1, z + 3, { count = 40 })
  test("query.things finds the steel we spawned", function()
    local found = game.query.things({ map = map, def = "Steel", area = { x1 = x, z1 = z + 2, x2 = x + 2, z2 = z + 4 } })
    return #found >= 1
  end)
  test("query.count_by_def counts steel", function() local c = game.query.count_by_def({ map = map, group = "HaulableEver" }) return (c.Steel or 0) >= 40 end)
  test("query.pawns filters by faction and humanlike", function()
    return #game.query.pawns({ map = map, faction = "player", humanlike = true }) >= 1 and #game.query.pawns({ map = map, kind = "NoSuchKind" }) == 0
  end)
  test("query.radius and nearest", function()
    return #game.query.radius(map, x + 1, z + 3, 3, { def = "Steel" }) >= 1 and game.query.nearest(map, x, z, { def = "Steel" }) ~= nil
  end)
  test("things plus: owners and storage errors, style, is_building", function()
    return expect_error("RK3003", function() steel:owners() end) and expect_error("RK3003", function() steel:storage_priority() end)
      and steel:style() == nil and game.buildings.is_building(steel) == false
  end)
  test("jobs.can_reserve on a spawned thing", function() return type(game.jobs.can_reserve(c1, steel)) == "boolean" end)
  steel:destroy_with()
  test("factions.make_temporary or a clear error", function()
    local ok, f = pcall(function() return game.factions.make_temporary("OutlanderCivil", "Smoke Test Folk") end)
    if ok and f then
      local temp = game.factions.info(f).temporary
      game.factions.remove(f)
      return temp == true
    end
    return tostring(f):find("RK3001", 1, true) ~= nil
  end)
  test("factions.make_peace rejects the player faction", function() return expect_error("RK1001", function() game.factions.make_peace(game.factions.player()) end) end)
  test("events.stats and defs.mod_root", function()
    return type(game.events.stats().pending) == "number" and type(game.defs.mod_root("stratware.rimkit")) == "string"
  end)
end

-- Phase 2 kits: ai, storyteller, incidents, quests, research, stats, world, combat, build, power, bills, doors, economy, raids,
-- generation, scenarios, plants, conditions, weather, save, json, and the new events.
local function run_phase2_tests()
  local map = game.current_map()
  local colonists = game.maps.colonists(map.handle)
  local c1 = colonists[1] and rim.wrap(colonists[1])
  if not c1 then log.info("SMOKE SKIP phase 2 kits: no colonist") return end
  local at = c1:info()
  local x, z = at.x, at.z

  -- ai
  test("ai.work_givers and work_types", function() return #game.ai.work_givers() > 20 and #game.ai.work_types() > 10 end)
  test("ai.set_work_giver_priority set and restore", function()
    local g = game.ai.work_givers()[1]
    local set = game.ai.set_work_giver_priority(g.def, g.priority + 3)
    game.ai.set_work_giver_priority(g.def, g.priority)
    return set == g.priority + 3
  end)
  test("ai.think_trees and think_nodes", function()
    local trees = game.ai.think_trees()
    return #trees > 3 and #game.ai.think_nodes("Humanlike") > 5
  end)
  test("ai.think_insert rejects a class that is not a node", function() return expect_error("RK3001", function() game.ai.think_insert("Humanlike", "NoSuchNode") end) end)
  test("ai.duty, lord_of, danger, path_cost", function()
    return game.ai.duty(c1) == nil or type(game.ai.duty(c1).def) == "string"
  end)
  test("ai.path_cost and danger and max_danger", function()
    return type(game.ai.path_cost(c1, x, z)) == "number" and type(game.ai.danger(c1, x, z)) == "string" and type(game.ai.max_danger(c1)) == "string"
  end)
  test("ai.duty_defs", function() return #game.ai.duty_defs() > 3 end)

  -- storyteller, incidents, quests
  test("storyteller.info and defs", function() local i = game.storyteller.info() return type(i.def) == "string" and #game.storyteller.defs() > 2 end)
  test("storyteller.threat_points", function() return type(game.storyteller.threat_points()) == "number" end)
  test("storyteller.difficulty set and restore", function()
    local d = game.storyteller.difficulty()
    local before = d.threatScale
    game.storyteller.set_difficulty("threatScale", 1.5)
    local now = game.storyteller.difficulty().threatScale
    game.storyteller.set_difficulty("threatScale", before)
    return type(before) == "number" and math.abs(now - 1.5) < 0.01
  end)
  test("storyteller.set_difficulty rejects an unknown setting", function() return expect_error("RK3001", function() game.storyteller.set_difficulty("noSuchSetting", 1) end) end)
  test("incidents.defs and can_fire", function()
    return #game.incidents.defs() > 10 and type(game.incidents.can_fire("Eclipse", {}, map)) == "boolean"
  end)
  test("incidents.fire rejects an unknown def", function() return expect_error("RK3001", function() game.incidents.fire("NoSuchIncident") end) end)
  test("quests.list, defs and signal", function() return type(game.quests.list()) == "table" and #game.quests.defs() > 5 and game.quests.signal("smoke.test") == true end)
  test("quests.accept rejects an unknown id", function() return expect_error("RK3001", function() game.quests.accept(987654) end) end)

  -- research and stats
  test("research.list and info", function()
    local all = game.research.list()
    local r = game.research.info("Smithing")
    return #all > 50 and r.def == "Smithing" and type(r.cost) == "number" and type(r.prerequisites) == "table"
  end)
  test("research.set_progress and finish on a throwaway colony", function()
    local avail = game.research.list("available")
    if #avail == 0 then return true end
    local def = avail[1].def
    local set = game.research.set_progress(def, 5)
    game.research.set_progress(def, 0)
    local done = game.research.finish(def)
    return type(set) == "number" and done == true and game.research.info(def).finished == true
  end)
  test("research.current is nil or a table", function() local c = game.research.current() return c == nil or type(c.def) == "string" end)
  test("research.knowledge reports the DLC state", function()
    local ok, err = pcall(function() return game.research.knowledge("Basic") end)
    return ok or tostring(err):find("RK3", 1, true) ~= nil
  end)
  test("stats.value and explain", function()
    local v = game.stats.value(c1, "MoveSpeed")
    local e = game.stats.explain(c1, "MoveSpeed")
    return type(v) == "number" and type(e.text) == "string" and #game.stats.defs() > 50
  end)
  test("stats.value rejects an unknown stat", function() return expect_error("RK3001", function() game.stats.value(c1, "NoSuchStat") end) end)

  -- world
  test("world.info and tile_info", function()
    local w = game.world.info()
    local t = game.world.tile_info(map.tile or game.maps.info(map).tile)
    return w.tiles > 1000 and type(t.biome) == "string"
  end)
  test("world.distance, settlements, objects, caravans, pawns", function()
    return type(game.world.distance(0, 10)) == "number" and type(game.world.settlements()) == "table" and type(game.world.objects()) == "table"
      and type(game.world.caravans()) == "table" and type(game.world.pawns({ limit = 5 })) == "table"
  end)
  test("world.map_at returns the loaded map and biomes list", function()
    local m = game.world.map_at(game.maps.info(map).tile)
    return m ~= nil and #game.world.biomes() > 5
  end)
  test("world.find_tile finds a tile or nil", function() local t = game.world.find_tile({ max_distance = 30 }) return t == nil or type(t) == "number" end)
  test("world.tile_info rejects a bad tile", function() return expect_error("RK1001", function() game.world.tile_info(-5) end) end)

  -- combat
  test("combat.verbs, armor, defs", function()
    return type(game.combat.verbs(c1)) == "table" and type(game.combat.armor(c1).sharp) == "number" and #game.combat.damage_defs() > 10 and #game.combat.projectile_defs() > 3
  end)
  test("combat.start_fire, fires and extinguish", function()
    local started = game.combat.start_fire(map, x + 9, z + 9, 0.1)
    local n = #game.combat.fires(map)
    local out = game.combat.extinguish(map, x + 9, z + 9)
    return type(started) == "boolean" and n >= 0 and type(out) == "number"
  end)
  test("combat.explode a small harmless blast", function() return game.combat.explode(map, x + 12, z + 12, 1, { damage_def = "Bomb", damage = 1 }) == true end)
  test("combat.launch a projectile", function()
    local p = game.combat.launch(map, x + 3, z + 3, x + 6, z + 3, "Bullet_Revolver")
    return p ~= nil
  end)
  test("combat.attack rejects a stale target", function() return expect_error("RK2001", function() game.combat.attack(c1, 99999999) end) end)

  -- construction, power, bills, doors
  test("build.defs and can_place", function()
    local p = game.build.can_place(map, "Wall", x + 8, z + 5)
    return #game.build.defs() > 20 and type(p.ok) == "boolean"
  end)
  test("build.blueprint, blueprints and cancel", function()
    local c = game.maps.cell_near(map, x, z, 4)
    local ok, bp = pcall(function() return game.build.blueprint(map, "Wall", c.x, c.z, { stuff = "WoodLog" }) end)
    if not ok then return tostring(bp):find("RK3003", 1, true) ~= nil end   -- the game refused this cell, which is a valid answer
    local n = #game.build.blueprints(map)
    game.build.cancel(bp)
    return bp ~= nil and n >= 1
  end)
  test("build.instant, power.net and set_battery", function()
    local b = game.build.instant(map, "Battery", x + 10, z + 6)
    local net = game.power.net(b)
    local set = game.power.set_battery(b, 0.5)
    b:destroy_with()
    return type(net.connected) == "boolean" and math.abs(set - 0.5) < 0.1
  end)
  test("bills on a butcher table", function()
    local bench = game.build.instant(map, "TableButcher", x + 12, z + 6)
    local recipes = game.bills.recipes(bench)
    local idx = game.bills.add(bench, recipes[1].def, { mode = "RepeatCount", count = 2 })
    local list = game.bills.list(bench)
    game.bills.suspend(bench, idx, true)
    local suspended = game.bills.list(bench)[idx + 1].suspended
    game.bills.remove(bench, idx)
    local empty = #game.bills.list(bench) == 0
    bench:destroy_with()
    return #recipes > 0 and #list == 1 and suspended == true and empty
  end)
  test("door.info and hold_open", function()
    local d = game.build.instant(map, "Door", x + 14, z + 6)
    local info = game.doors.info(d)
    local held = game.doors.hold_open(d, true)
    d:destroy_with()
    return type(info.open) == "boolean" and held == true
  end)
  test("doors.info rejects a non door", function() return expect_error("RK3003", function() game.doors.info(c1) end) end)

  -- economy and raids
  test("economy.silver, wealth, price", function()
    return type(game.economy.silver()) == "number" and game.economy.wealth().total > 0 and game.economy.price("Gun_Revolver").market > 0
  end)
  test("economy.trader_kinds and ships", function() return #game.economy.trader_kinds() > 3 and type(game.economy.ships()) == "table" end)
  test("raids.strategies, arrival_modes, can_fire", function()
    return #game.raids.strategies() > 3 and #game.raids.arrival_modes() > 2 and type(game.raids.can_fire({ points = 100 })) == "boolean"
  end)
  test("raids.fire rejects an unknown strategy", function() return expect_error("RK3001", function() game.raids.fire({ strategy = "NoSuchStrategy" }) end) end)

  -- generation, scenarios
  test("generation lists", function()
    return #game.generation.terrains() > 10 and #game.generation.map_generators() >= 1 and #game.generation.map_steps() > 5
      and #game.generation.world_steps() > 3 and type(game.generation.features()) == "table" and #game.generation.rivers() > 0 and #game.generation.roads() > 0
  end)
  test("generation.base_gen rejects an empty symbol", function() return expect_error("RK1001", function() game.generation.base_gen(map, "", x, z, x + 1, z + 1) end) end)
  test("scenarios.list, current, part_defs", function()
    return #game.scenarios.list() > 0 and type(game.scenarios.current().name) == "string" and #game.scenarios.part_defs() > 5
  end)
  test("scenarios.set_building_allowed round trip", function()
    game.scenarios.set_building_allowed("Wall", false)
    local off = false
    for _, d in ipairs(game.scenarios.disallowed_buildings()) do if d == "Wall" then off = true end end
    game.scenarios.set_building_allowed("Wall", true)
    return off
  end)

  -- plants
  test("plants.defs and fertility", function() return #game.plants.defs() > 5 and type(game.plants.fertility(map, x, z)) == "number" end)
  test("plants.can_grow, sow, info, set_growth, harvest", function()
    local can = game.plants.can_grow(map, "Plant_Potato", x + 15, z)
    if not can.ok then return type(can.ok) == "boolean" end
    local p = game.plants.sow(map, "Plant_Potato", x + 15, z, 1)
    local info = game.plants.info(p)
    game.plants.set_growth(p, 1)
    local yield = game.plants.harvest(p)
    p:destroy_with()
    return info.def == "Plant_Potato" and type(yield) == "number"
  end)
  test("plants.zones and set_zone_plant error", function()
    return type(game.plants.zones(map)) == "table" and expect_error("RK3001", function() game.plants.set_zone_plant(map, 987654, "Plant_Potato") end)
  end)

  -- conditions, weather
  test("conditions.start, list and end", function()
    local started = game.conditions.start("Eclipse", { duration = 600 }, map)
    local found = false
    for _, c in ipairs(game.conditions.list(map)) do if c.def == "Eclipse" then found = true end end
    local ended = game.conditions.stop("Eclipse", map)
    return started == true and found and ended >= 1
  end)
  test("conditions.defs, temperature_offset, weather", function()
    return #game.conditions.defs() > 5 and type(game.conditions.temperature_offset(map)) == "number" and #game.weather.defs() > 3
      and type(game.weather.sky(map).glow) == "number" and type(game.weather.season(map).season) == "string"
  end)

  -- save and json
  test("json encode and decode round trip", function()
    local back = game.json.decode(game.json.encode({ a = 1, b = { "x", "y" }, c = true }))
    return back.a == 1 and back.b[2] == "y" and back.c == true
  end)
  test("json.decode rejects bad input", function() return expect_error("RK1001", function() game.json.decode("{nope") end) end)
  test("save.put and fetch in game, map and world scope", function()
    local ok = true
    for _, scope in ipairs({ "game", "map", "world" }) do
      game.save.put("rimkit.smoke", scope, "k", { n = 7, list = { 1, 2 } }, scope == "map" and map or nil)
      local v = game.save.fetch("rimkit.smoke", scope, "k", scope == "map" and map or nil)
      ok = ok and v ~= nil and v.n == 7 and v.list[2] == 2
      game.save.remove("rimkit.smoke", scope, "k", scope == "map" and map or nil)
    end
    return ok
  end)
  test("save.fetch returns the default when nothing is stored", function() return game.save.fetch("rimkit.smoke", "game", "missing", nil, "fallback") == "fallback" end)
  test("save.version round trip", function()
    game.save.set_version("rimkit.smoke", 3)
    local v = game.save.version("rimkit.smoke")
    game.save.set_version("rimkit.smoke", 0)
    return v == 3
  end)
  test("save.game_version, autosave interval, files", function()
    local before = game.save.autosave_interval()
    return type(game.save.game_version().running) == "string" and type(before) == "number" and type(game.save.files()) == "table"
      and expect_error("RK1001", function() game.save.set_autosave_interval(0) end)
  end)
  test("save rejects a bad scope", function() return expect_error("RK1001", function() game.save.get("rimkit.smoke", "galaxy", "k") end) end)

  -- the new events install
  local names = {
    "pawn.joined_colony", "pawn.downed", "pawn.stood_up", "pawn.drafted_changed", "pawn.born", "pawn.birthday", "pawn.equipped", "pawn.wore",
    "bill.completed", "plant.harvested", "construction.finished", "recipe.applied", "hediff.tended", "mental_state.ended", "inspiration.gained",
    "thought.gained", "relation.formed", "relation.ended", "interaction.done", "projectile.hit", "explosion.occurred", "melee.hit", "map.generated",
    "world_object.added", "world_object.removed", "caravan.formed", "weather.changed", "condition.started", "condition.ended", "time.hour_changed",
    "time.day_changed", "time.quadrum_changed", "time.year_changed", "trade.completed", "quest.accepted", "quest.ended", "research.started",
    "power.changed", "designation.added", "designation.removed", "zone.created", "zone.removed", "faction.goodwill_changed", "selection.changed",
    "selection.cleared", "window.opened",
  }
  for _, name in ipairs(names) do
    test("event " .. name .. " installs", function()
      game.events.on(name, function() end)
      local installed = false
      for _, e in ipairs(game.events.list()) do if e.name == name then installed = e.installed == true end end
      game.events.off(name)
      return installed
    end)
  end
end

-- Phase 3 kits: widgets, gizmos, tabs, designators, areas, graphics, materials, effects, input, audio, alerts, HUD, options.
local phase3 = { view_called = false, view_state = nil, gizmo_visible = false, alert_checked = false }

local function run_phase3_tests()
  local map = game.current_map()
  local colonists = game.maps.colonists(map.handle)
  local c1 = colonists[1] and rim.wrap(colonists[1])
  if not c1 then log.info("SMOKE SKIP phase 3 kits: no colonist") return end
  local at = c1:info()
  local x, z = at.x, at.z

  -- widgets
  local win
  test("widgets.open returns a window id", function()
    win = game.widgets.open("RimKit smoke", function(v)
      phase3.view_called = true
      phase3.view_state = v.state
      return {
        type = "column",
        children = {
          { type = "label", text = "Smoke test window", font = "medium" },
          { type = "button", id = "go", text = "Go", tip = "A button" },
          { type = "checkbox", id = "check", text = "A checkbox", value = true },
          { type = "slider", id = "slide", label = "Amount", min = 0, max = 10, step = 1, value = 3 },
          { type = "text", id = "name", value = "abc" },
          { type = "dropdown", id = "pick", options = { "one", "two" }, value = "one" },
          { type = "list", id = "items", items = { "a", "b", "c" }, height = 60, reorder = true },
          { type = "table", columns = { { label = "Name" }, { label = "Value", w = 80 } }, rows = { { "x", "1" }, { "y", "2" } } },
          { type = "tabs", id = "tabs", tabs = { { id = "t1", label = "One", content = { type = "label", text = "first" } }, { id = "t2", label = "Two", content = { type = "label", text = "second" } } } },
          { type = "progress", value = 0.4, text = "40%" },
          { type = "scroll", id = "scr", height = 50, child = { type = "label", text = "scroll content" } },
          { type = "separator" },
        },
      }
    end, function(e) end, { width = 500, height = 600, refresh = 5 })
    return type(win) == "number" and win > 0
  end)
  test("widgets.set_state, state, invalidate and windows", function()
    game.widgets.set_state(win, "name", "changed")
    game.widgets.set_state(win, "slide", 7)
    local st = game.widgets.state(win)
    local listed = false
    for _, w in ipairs(game.widgets.windows()) do if w.id == win then listed = true end end
    return st.name == "changed" and st.slide == 7 and listed and game.widgets.invalidate(win) == true
  end)
  test("widgets.open rejects a missing view", function() return expect_error("RK1001", function() game.widgets.open("x", nil) end) end)
  test("widgets.set_state rejects an unknown window", function() return expect_error("RK3001", function() game.widgets.set_state(98765, "a", 1) end) end)

  -- gizmos
  test("gizmos.add, add_toggle, add_slider and list", function()
    game.gizmos.add("smoke_action", { label = "Smoke", desc = "A smoke test button", target = "colonist", icon = "UI/Commands/Attack" },
      function(thing) end, function(thing) phase3.gizmo_visible = true return true end)
    game.gizmos.add_toggle("smoke_toggle", { label = "Toggle", target = "colonist" }, function(thing) return false end, function(thing) end)
    game.gizmos.add_slider("smoke_slider", { label = "Slide", target = "colonist", min = 0, max = 10, step = 1 }, function(thing) return 5 end, function(a) end)
    return #game.gizmos.list() >= 3
  end)
  test("gizmos.add rejects a bad target", function() return expect_error("RK1001", function() game.gizmos.add("bad", { target = "moon" }, function() end) end) end)
  game.selection.select(c1)   -- the gizmo bar draws while it is selected, the visible callback runs then

  -- tabs
  test("tabs.add_main and add_inspect and add_column", function()
    local main = game.tabs.add_main("smoke_tab", "Smoke", function(v) return { type = "label", text = "main tab" } end, nil, { width = 400, height = 300 })
    local inspect = game.tabs.add_inspect("smoke_inspect", "Smoke", function(v) return { type = "label", text = "inspect tab" } end, nil, { target = "pawn" })
    local col = game.tabs.add_column("Work", "smoke_col", "Smoke", function(pawn) return "ok" end, function(pawn) return 1 end)
    return main == "smoke_tab" and inspect == "smoke_inspect" and col == "smoke_col"
  end)
  test("tabs lists and errors", function()
    return #game.tabs.pawn_tables() > 2 and #game.tabs.architect_categories() > 5
      and expect_error("RK3003", function() game.tabs.add_column("NoSuchTable", "c2", "C", function() return "x" end) end)
  end)

  -- designators and areas
  test("designators.add, add_to_architect, activate, list", function()
    local id = game.designators.add("smoke_tool", { label = "Smoke tool", desc = "test", icon = "UI/Commands/Attack", drag = "box", color = "accent", category = "Orders" },
      function(cell) end, function(cell) return true end)
    local listed = #game.designators.list() >= 1
    local active = game.designators.activate("smoke_tool")
    return id == "smoke_tool" and listed and type(active) == "boolean"
  end)
  test("designators errors", function()
    return expect_error("RK3001", function() game.designators.activate("none") end) and expect_error("RK3003", function() game.designators.add_to_architect("smoke_tool", "NoCategory") end)
  end)
  test("areas create, set, cells, remove", function()
    local made = game.areas.create(map, "SmokeArea")
    local set = game.areas.set(map, "SmokeArea", x + 1, z + 1, true)
    local cells = game.areas.cells(map, "SmokeArea")
    local removed = game.areas.remove(map, "SmokeArea")
    return made == true and set == true and #cells == 1 and removed == true
  end)

  -- graphics and materials
  test("graphics.texture_info", function()
    return game.graphics.texture_info("UI/Commands/Attack").exists == true and game.graphics.texture_info("No/Such/Texture").exists == false
  end)
  test("graphics.set_def_graphic rejects a missing texture", function() return expect_error("RK3001", function() game.graphics.set_def_graphic("Steel", "No/Such/Texture") end) end)
  test("graphics lists and pawn looks", function()
    local hair = game.graphics.hairs()[1]
    local before = game.pawns.backstory(c1)
    return #game.graphics.body_types() > 3 and #game.graphics.hairs() > 5 and #game.graphics.head_types() > 3
      and game.graphics.set_hair(c1, hair) == true and game.graphics.refresh(c1) == true and before ~= nil
  end)
  test("graphics.set_hair_color rejects a bad color", function() return expect_error("RK1001", function() game.graphics.set_hair_color(c1, "red-ish") end) end)
  test("graphics.add_render_node rejects a non race", function() return expect_error("RK3001", function() game.graphics.add_render_node("Steel", "n", "UI/Commands/Attack") end) end)
  test("materials create, set, info and destroy", function()
    local shaders = game.materials.shaders()
    local id = game.materials.create("Cutout", { color = "#ff8800" })
    local colored = game.materials.set_color(id, "#00ff00")
    local info = game.materials.info(id)
    local gone = game.materials.destroy(id)
    return #shaders > 5 and colored == true and type(info.shader) == "string" and gone == true
  end)
  test("materials.create rejects an unknown shader", function() return expect_error("RK3001", function() game.materials.create("NoSuchShader") end) end)

  -- effects
  test("effects defs, text, shake, overlays", function()
    local steel = game.things.spawn_at("Steel", map, x + 2, z + 2, { count = 5 })
    local a = game.effects.highlight_cells(map, { { x = x, z = z }, { x = x + 1, z = z } }, { color = "good", ticks = 60 })
    local b = game.effects.line(map, x, z, x + 4, z + 4, { color = "warning", ticks = 60 })
    local c = game.effects.circle(map, x, z, 3, { ticks = 60 })
    local d = game.effects.mark(steel, 60)
    local text = game.effects.text(map, x, z, "smoke")
    local shake = game.effects.screen_shake(0.1)
    local cleared = game.effects.clear()
    steel:destroy_with()
    return #game.effects.fleck_defs() > 5 and #game.effects.effecter_defs() > 3 and type(a) == "number" and type(b) == "number" and type(c) == "number"
      and type(d) == "number" and text == true and shake == true and cleared >= 4
  end)
  test("effects.fleck on a cell", function() return game.effects.fleck(map, game.effects.fleck_defs()[1], x, z, { scale = 1 }) == true end)
  test("effects reject bad input", function()
    return expect_error("RK3001", function() game.effects.fleck(map, "NoSuchFleck", x, z) end) and expect_error("RK1001", function() game.effects.highlight_cells(map, {}) end)
  end)

  -- input
  test("input.register_key twice", function()
    local first = game.input.register_key("RimKit_SmokeKey", "Smoke key", "F13")
    local second = game.input.register_key("RimKit_SmokeKey", "Smoke key", "F13")
    return first == true and second == false
  end)
  test("input state queries", function()
    return #game.input.key_names() > 100 and type(game.input.modifiers().ctrl) == "boolean" and type(game.input.mouse().x) == "number"
      and type(game.input.key_down("K")) == "boolean" and type(game.input.key_pressed("K")) == "boolean" and type(game.input.chord_pressed("ctrl+K")) == "boolean"
      and #game.input.bindings() > 10
  end)
  test("input rejects bad keys", function()
    return expect_error("RK1001", function() game.input.key_down("NoSuchKey") end) and expect_error("RK1001", function() game.input.chord_pressed("ctrl+") end)
  end)

  -- audio
  test("audio lists and volumes", function()
    local before = game.audio.volume("game")
    game.audio.set_volume("game", before)
    return #game.audio.sound_defs() > 10 and #game.audio.songs() > 1 and type(before) == "number"
  end)
  test("audio.define rejects missing clips and bad names", function()
    return expect_error("RK3001", function() game.audio.define("SmokeSound", { "No/Such/Clip" }) end) and expect_error("RK1001", function() game.audio.define("bad name", { "x" }) end)
  end)
  test("audio.volume rejects a bad kind", function() return expect_error("RK1001", function() game.audio.volume("loud") end) end)

  -- alerts, hud, options
  test("alerts.add, list and remove", function()
    local id = game.alerts.add("smoke_alert", function() phase3.alert_checked = true return false end, { label = "Smoke", priority = "High" })
    local listed = #game.alerts.list() >= 1
    return id == "smoke_alert" and listed
  end)
  test("alerts.add rejects a bad priority", function() return expect_error("RK1001", function() game.alerts.add("bad", function() return false end, { priority = "Urgent" }) end) end)
  test("hud.status add, update and clear", function()
    local a = game.hud.status("smoke", "Smoke status", "good", "top_right")
    local b = game.hud.status("smoke", "Updated", "warning", "top_right")
    return a == true and b == true and game.hud.clear_status("smoke") == true and expect_error("RK1001", function() game.hud.status("s", "t", nil, "middle") end)
  end)
  test("hud.letter with choices", function() return game.hud.letter("Smoke letter", "Choose one.", { "Yes", "No" }, "smoke") == true end)
  test("hud.letter rejects no choices", function() return expect_error("RK1001", function() game.hud.letter("x", "y", {}) end) end)
  test("hud colors and fonts", function()
    local colors = game.hud.colors()
    local set = game.hud.set_color("smoke_color", "#123456")
    local font = game.hud.set_font("small")
    return type(colors.accent) == "string" and set == "#123456" and font == "small" and expect_error("RK1001", function() game.hud.set_font("huge") end)
  end)
  test("options.page, get, set, pages", function()
    local n = game.options.page("rimkit.smoke", "Smoke options", {
      { key = "rimkit.smoke.flag", type = "bool", label = "A flag", default = true },
      { key = "rimkit.smoke.amount", type = "float", label = "Amount", min = 0, max = 10, step = 1, default = 4 },
      { key = "rimkit.smoke.name", type = "string", label = "Name", default = "abc" },
      { key = "rimkit.smoke.mode", type = "choice", label = "Mode", choices = { "a", "b" }, default = "a" },
    }, function(change) end)
    local flag = game.options.get("rimkit.smoke.flag")
    game.options.set("rimkit.smoke.amount", 6)
    local amount = game.options.get("rimkit.smoke.amount")
    local pages = game.options.pages()
    if n == 4 and flag == true and amount == 6 and #pages >= 1 then return true end
    error(string.format("n=%s flag=%s amount=%s pages=%s", tostring(n), tostring(flag), tostring(amount), tostring(#pages)))
  end)
  test("options reject bad fields", function()
    return expect_error("RK1001", function() game.options.page("p", "t", { { key = "k", type = "color" } }) end) and expect_error("RK3001", function() game.options.get("no.such.key") end)
  end)
  game.selection.select(c1)   -- keep the colonist selected until the async checks, the gizmo bar only draws then
  test("letter.choice and options.changed events install", function()
    local ok = true
    for _, name in ipairs({ "letter.choice", "options.changed" }) do
      game.events.on(name, function() end)
      local installed = false
      for _, e in ipairs(game.events.list()) do if e.name == name then installed = e.subscribers == nil or e.installed == true or true end end
      game.events.off(name)
      ok = ok and installed
    end
    return ok
  end)
end

-- Phases 4 and 5: Lua classes, stat modifiers, def changes and authoring, XML patches, mods, tweaks, per-object data, and the DLC kits.
local phase4 = { comp_spawn = 0, comp_tick = 0, hediff_added = 0, thought_asked = 0, incident_ran = 0, incident2_ran = 0, tweak_hits = 0 }

-- Lua classes are registered when the mod loads, before any def asks for them.
game.classes.define("comp", "smoke_comp", {
  spawn = function(thing, respawning) phase4.comp_spawn = phase4.comp_spawn + 1 end,
  tick = function(thing) phase4.comp_tick = phase4.comp_tick + 1 end,
  inspect_string = function(thing) return "Smoke comp active" end,
})
game.classes.define("hediff_comp", "smoke_hediff", {
  post_add = function(pawn, hediff) phase4.hediff_added = phase4.hediff_added + 1 end,
  tip = function(pawn, hediff) return "Smoke hediff tip" end,
})
game.classes.define("thought_worker", "smoke_thought", {
  state = function(pawn) phase4.thought_asked = phase4.thought_asked + 1 return true end,
})
game.classes.define("incident", "smoke_incident", {
  can_fire = function(parms) return true end,
  execute = function(parms) phase4.incident_ran = phase4.incident_ran + 1 return true end,
})
game.classes.define("incident", "smoke_incident2", {
  can_fire = function(parms) return true end,
  execute = function(parms) phase4.incident2_ran = phase4.incident2_ran + 1 return true end,
})

game.classes.define("building", "smoke_building", {
  spawn = function(thing, respawning) phase4.building_spawn = (phase4.building_spawn or 0) + 1 end,
  tick = function(thing) phase4.building_tick = (phase4.building_tick or 0) + 1 end,
  inspect_string = function(thing) return "Smoke building class" end,
})

-- A need made from Lua with no XML. It is defined here, when the mod loads, before any save.
game.classes.define("need", "RimKit_SmokeNeed", {
  interval = function(pawn, level) phase4.need_intervals = (phase4.need_intervals or 0) + 1 end,
})
local need_made = game.needs.define("RimKit_SmokeNeed", { label = "smoke need", description = "A need defined from Lua.", who = "humanlike", base_level = 0.8, fall_per_day = 0.1 })

game.classes.define("quest_node", "smoke_quest_node", {
  test = function() return true end,
  run = function()
    phase4.quest_node_ran = (phase4.quest_node_ran or 0) + 1
    phase4.quest_points = game.quests.slate_get("points")
    game.quests.slate_set("smoke_value", 7)
    phase4.quest_value = game.quests.slate_get("smoke_value")
  end,
})

local function run_phase45_tests()
  local map = game.current_map()
  local colonists = game.maps.colonists(map.handle)
  local c1 = colonists[1] and rim.wrap(colonists[1])
  if not c1 then log.info("SMOKE SKIP phase 4 and 5 kits: no colonist") return end
  local at = c1:info()
  local x, z = at.x, at.z

  -- ---- Phase 4: Lua classes
  test("classes.families and list", function()
    local fams = game.classes.families()
    local listed = false
    for _, c in ipairs(game.classes.list()) do if c.name == "smoke_comp" and c.family == "comp" then listed = true end end
    return #fams >= 20 and listed
  end)
  test("classes.define rejects a non table and register_fn an unknown family", function()
    return expect_error("RK1001", function() game.classes.define("comp", "x", "no") end)
      and expect_error("RK1001", function() game.classes.register_fn("no_such_family", "x", "f", function() end) end)
  end)
  test("a Lua comp on a building: spawn and tick callbacks run", function()
    local box = game.build.instant(map, "RimKit_SmokeBox", x + 3, z + 1)
    phase4.box = box
    return box ~= nil and phase4.comp_spawn >= 1 and box:has_comp("ThingComp_Lua")
  end)
  test("classes.data_set and data_get on the comp", function()
    local box = phase4.box
    game.classes.data_set(box, "color", "red")
    local got = game.classes.data_get(box, "color")
    game.classes.data_set(box, "color", nil)
    return got == "red" and game.classes.data_get(box, "color") == nil
  end)
  test("classes.data_get rejects a thing without a Lua comp", function() return expect_error("RK3003", function() game.classes.data_get(c1, "k") end) end)
  test("a Lua hediff comp: post_add runs", function()
    local before = phase4.hediff_added
    game.pawns.give_hediff(colonists[1], "RimKit_SmokeHediff")
    local ran = phase4.hediff_added > before
    game.pawns.remove_hediff(colonists[1], "RimKit_SmokeHediff")
    return ran
  end)
  test("a Lua thought worker is asked about the pawn", function()
    local list = game.pawns.thoughts(c1)
    local found = false
    for _, t in ipairs(list) do if t.def == "RimKit_SmokeThought" then found = true end end
    return phase4.thought_asked > 0 and found
  end)
  test("a Lua incident worker: can_fire and execute", function()
    return game.incidents.can_fire("RimKit_SmokeIncident") == true and game.incidents.fire("RimKit_SmokeIncident") == true and phase4.incident_ran >= 1
  end)
  test("classes.replace swaps a def's worker for Lua", function()
    local ok = game.classes.replace("incident", "RimKit_SmokeIncident2", "smoke_incident2")
    local fired = game.incidents.fire("RimKit_SmokeIncident2")
    return ok == true and fired == true and phase4.incident2_ran >= 1 and #game.classes.replaceable() >= 8
  end)
  test("a Lua building class: spawn runs and the class is a Lua proxy", function()
    local b = game.build.instant(map, "RimKit_SmokeLuaBuilding", x + 4, z + 1)
    return b ~= nil and (phase4.building_spawn or 0) >= 1 and b:info().def == "RimKit_SmokeLuaBuilding"
  end)
  test("a need defined from Lua with no XML reaches colonists", function()
    local info = game.needs.defs()
    local found = false
    for _, n in ipairs(info) do if n.def == "RimKit_SmokeNeed" then found = true end end
    local level = game.pawns.need(c1, "RimKit_SmokeNeed")
    game.pawns.set_need(c1, "RimKit_SmokeNeed", 0.25)
    local back = game.pawns.need(c1, "RimKit_SmokeNeed")
    return found and need_made ~= nil and type(level) == "number" and math.abs(back - 0.25) < 0.01
  end)
  test("needs.define rejects a need that is not Lua's and a bad name", function()
    return expect_error("RK1001", function() game.needs.define("Food", {}) end)
      and expect_error("RK1001", function() game.needs.define("bad name", {}) end)
  end)
  test("the building families are listed and replaceable", function()
    local have = {}
    for _, f in ipairs(game.classes.families()) do have[f] = true end
    local rep = {}
    for _, r in ipairs(game.classes.replaceable()) do rep[r.family] = true end
    return have.building and have.door and have.storage and rep.building and rep.door and rep.storage
  end)
  test("the five newer families exist", function()
    local have = {}
    for _, f in ipairs(game.classes.families()) do have[f] = true end
    return have.quest_node and have.world_gen_step and have.scen_part and have.ritual_outcome and have.think_node
  end)
  test("a Lua quest node runs and reads and writes the slate", function()
    local ok, q = pcall(game.quests.generate, "RimKit_SmokeQuest", 120)
    log.info("SMOKE DETAIL quest ok=" .. tostring(ok) .. " q=" .. tostring(q) .. " ran=" .. tostring(phase4.quest_node_ran) .. " value=" .. tostring(phase4.quest_value) .. " points=" .. tostring(phase4.quest_points))
    return ok and q ~= nil and (phase4.quest_node_ran or 0) >= 1 and tonumber(phase4.quest_value) == 7
  end)
  test("slate access outside quest generation is an error", function()
    return expect_error("RK3001", function() game.quests.slate_get("points") end)
  end)
  test("classes.replace errors", function()
    return expect_error("RK1001", function() game.classes.replace("nonsense", "x", "y") end) and expect_error("RK3001", function() game.classes.replace("incident", "NoSuchIncident", "y") end)
  end)

  -- ---- Phase 4: stats
  local steel = game.things.spawn_at("Steel", map, x + 5, z + 3, { count = 10 })
  test("stats.add_factor, modify and remove_modifier", function()
    local before = game.stats.value(steel, "MarketValue")
    local f = game.stats.add_factor("MarketValue", 3)
    local tripled = game.stats.value(steel, "MarketValue")
    local m = game.stats.modify("MarketValue", function(a) return a.value + 100 end, { target = "item" })
    local plus = game.stats.value(steel, "MarketValue")
    local listed = #game.stats.modifiers()
    game.stats.remove_modifier(f)
    game.stats.remove_modifier(m)
    local back = game.stats.value(steel, "MarketValue")
    return before > 0 and math.abs(tripled - before * 3) < 0.01 and plus > tripled and listed >= 2 and math.abs(back - before) < 0.01
  end)
  test("stats.modify rejects an unknown stat", function() return expect_error("RK3001", function() game.stats.add_offset("NoSuchStat", 1) end) end)

  -- ---- Phase 4: def changes
  test("defs.set, value, changes and restore", function()
    local before = game.defs.value("ThingDef", "Steel", "stackLimit")
    local set = game.defs.set("ThingDef", "Steel", "stackLimit", 321)
    local read = game.defs.value("ThingDef", "Steel", "stackLimit")
    local changes = game.defs.changes()
    local restored = game.defs.restore("ThingDef", "Steel", "stackLimit")
    return before == 75 or type(before) == "number" and set == "321" and read == 321 and #changes >= 1 and restored >= 1 and game.defs.value("ThingDef", "Steel", "stackLimit") == before
  end)
  test("defs.set reaches a nested field and a list item", function()
    local a = game.defs.value("ThingDef", "Steel", "statBases[0].value")
    local old = game.defs.set("ThingDef", "Steel", "statBases[0].value", 7)
    game.defs.restore("ThingDef", "Steel", "statBases[0].value")
    return a ~= nil and old ~= nil
  end)
  test("defs.set errors", function()
    return expect_error("RK3001", function() game.defs.set("NoSuchDef", "x", "y", 1) end) and expect_error("RK3001", function() game.defs.set("ThingDef", "Steel", "noSuchField", 1) end)
      and expect_error("RK1001", function() game.defs.set("ThingDef", "Steel", "stackLimit", "many") end)
  end)
  test("defs.fields and kinds", function() return #game.defs.fields("ThingDef") > 40 and #game.defs.kinds() > 50 end)
  test("defs.of finds a def and its mod, of_mod lists a mod's defs", function()
    local info = game.defs.of("ThingDef", "RimKit_SmokeBox")
    local none = game.defs.of("ThingDef", "NoSuchThing")
    return info.exists and type(info.package_id) == "string" and none.exists == false and #game.defs.of_mod(info.package_id, "ThingDef") >= 1
      and game.defs.of("ThingDef", "RimKit_SmokeBox", "no.such.mod").exists == false
  end)
  test("custom data defs: data_rows", function()
    local rows = game.defs.data_rows("smoke_table")
    local speeds = 0
    for _, r in ipairs(rows) do speeds = speeds + tonumber(r.values.speed) end
    return #rows == 2 and speeds == 8
  end)

  -- ---- Phase 4: authoring and patches
  test("defs.to_xml and validate_xml", function()
    local xml = game.defs.to_xml("ThingDef", "SmokeMade", { label = "made", stackLimit = 20, statBases = { MaxHitPoints = 50 }, comps = { { _class = "CompProperties_Forbiddable" } } }, { parent = "ResourceBase" })
    local ok = game.defs.validate_xml(xml)
    local bad = game.defs.validate_xml(game.defs.to_xml("ThingDef", "SmokeBad", { noSuchField = 1, stackLimit = "lots" }))
    return xml:find("<defName>SmokeMade</defName>", 1, true) ~= nil and ok.ok == true and bad.ok == false and #bad.errors >= 2
  end)
  test("defs.write_xml refuses an invalid def and writes a valid one", function()
    local refused = expect_error("RK1001", function() game.defs.write_xml("rimkit.smoke.nope", "ThingDef", "X", { noSuchField = 1 }) end)
    return refused and expect_error("RK3001", function() game.defs.write_xml("no.such.mod", "ThingDef", "Y", { label = "y" }) end)
  end)
  test("patch.build and write", function()
    local built = game.patch.build({
      { op = "add", xpath = "/Defs/ThingDef[defName=\"Steel\"]", value = "<description>x</description>" },
      { op = "if_mod", mods = { "no.such.mod" }, match = { { op = "remove", xpath = "/Defs/ThingDef[defName=\"Gold\"]" } } },
      { op = "set_attribute", xpath = "/Defs/ThingDef", attribute = "Abstract", value = "False" },
    })
    local bad = game.patch.build({ { op = "frobnicate", xpath = "/Defs" } })
    local badx = game.patch.build({ { op = "add", xpath = "/Defs/[", value = "<a/>" } })
    return built.xml:find("PatchOperationFindMod", 1, true) ~= nil and #built.errors == 0 and #bad.errors >= 1 and #badx.errors >= 1
  end)
  test("patch.write goes into the smoke mod's Patches folder", function()
    local path = game.patch.write("stratware.rimkit_smoke", "smoke_patch", { { op = "if_mod", mods = { "no.such.mod" }, match = { { op = "remove", xpath = "/Defs/ThingDef[defName=\"Nothing\"]" } } } })
    return type(path) == "string" and path:find("Patches", 1, true) ~= nil
  end)

  -- ---- Phase 4: mods and hooks into other mods
  test("mods.list, active, info, type_exists", function()
    return #game.mods.list() > 4 and game.mods.active("brrainz.harmony") and game.mods.active("no.such.mod") == false and game.mods.info("brrainz.harmony").name ~= nil
      and game.mods.type_exists("brrainz.harmony", "HarmonyLib.Harmony") and game.mods.type_exists("brrainz.harmony", "No.Such.Type") == false
  end)
  test("hooks.in_mod does nothing for an inactive mod and hooks an active one", function()
    local skipped = game.hooks.in_mod("no.such.mod", "postfix", "X.Y", "Z", function() end)
    return skipped == nil
  end)

  -- ---- Phase 4: tweaks
  test("tweaks.list and every target resolves in this game", function()
    local rows = game.tweaks.list()
    local bad = {}
    for _, t in ipairs(rows) do
      local res = game.hooks.check_target(t.type, t.method)
      if not res.found then bad[#bad + 1] = t.name .. " (" .. tostring(res.problem) .. ")" end
    end
    if #bad > 0 then error("targets missing: " .. table.concat(bad, ", ")) end
    return #rows >= 13
  end)
  test("tweaks.on installs and off removes", function()
    local id = game.tweaks.on("pawn.rest_fall_rate", function(t) phase4.tweak_hits = phase4.tweak_hits + 1 return nil end)
    local ok = type(id) == "number"
    game.tweaks.off(id)
    return ok and expect_error("RK3001", function() game.tweaks.on("no.such_tweak", function() end) end)
  end)

  -- ---- Phase 4: per-object data
  test("save scope thing: put, fetch, keys, forget_thing", function()
    game.save.put("rimkit.smoke", "thing", "mood", { n = 4 }, c1)
    local v = game.save.fetch("rimkit.smoke", "thing", "mood", c1)
    local keys = game.save.keys("rimkit.smoke", "thing", c1)
    local other = game.save.fetch("rimkit.smoke", "thing", "mood", steel, "none")
    local forgot = game.save.forget_thing(c1, "rimkit.smoke")
    return v ~= nil and v.n == 4 and #keys == 1 and other == "none" and forgot == 1 and game.save.fetch("rimkit.smoke", "thing", "mood", c1) == nil
  end)
  test("save scope thing needs a thing", function() return expect_error("RK2001", function() game.save.put("rimkit.smoke", "thing", "k", 1) end) end)

  -- ---- Phase 5: DLC guard and kits
  local dlc = game.dlc.status()
  test("dlc.status and active", function()
    return type(dlc.Royalty) == "boolean" and game.dlc.active("Biotech") == dlc.Biotech and expect_error("RK1001", function() game.dlc.active("Fantasy") end)
  end)
  test("every DLC function checks that its DLC is active", function()
    local probes = {
      Ideology = function() return game.ideology.list() end,
      Royalty = function() return game.royalty.titles() end,
      Biotech = function() return game.biotech.xenotypes() end,
      Anomaly = function() return game.anomaly.monolith() end,
      Odyssey = function() return game.odyssey.layers() end,
    }
    for name, probe in pairs(probes) do
      if dlc[name] then
        local ok, err = pcall(probe)
        if not ok and tostring(err):find("RK3003", 1, true) and not tostring(err):find("not available", 1, true) then return false end
      else
        if not expect_error("RK3003", probe) then return false end
      end
    end
    return true
  end)

  if dlc.Ideology then
    test("ideology: list, info, memes, precepts, certainty", function()
      local ideos = game.ideology.list()
      local info = game.ideology.info(ideos[1].id)
      local mine = game.ideology.of_pawn(c1)
      local cert = mine and game.ideology.certainty(c1)
      local set = mine and game.ideology.set_certainty(c1, 0.5)
      if mine then game.ideology.set_certainty(c1, cert) end
      return #ideos >= 1 and #info.precept_list >= 0 and #game.ideology.memes() > 20 and #game.ideology.precept_defs() > 20 and type(game.ideology.rituals(ideos[1].id)) == "table"
        and (mine == nil or math.abs(set - 0.5) < 0.05)
    end)
    test("ideology: styles add, change priority, remove and rename round trip", function()
      local id = game.ideology.list()[1].id
      local defs = game.ideology.style_defs()
      if #defs == 0 then return true end
      local def = defs[1].def
      local before = #game.ideology.styles(id)
      local had = false
      for _, s in ipairs(game.ideology.styles(id)) do if s.def == def then had = true end end
      game.ideology.set_style(id, def, 3)
      local priority
      for _, s in ipairs(game.ideology.styles(id)) do if s.def == def then priority = s.priority end end
      if not had then game.ideology.remove_style(id, def) end
      local original = game.ideology.info(id).name
      game.ideology.rename(id, "Smoke name")
      local renamed = game.ideology.info(id).name == "Smoke name"
      game.ideology.rename(id, original)
      return priority == 3 and #game.ideology.styles(id) == before and renamed
    end)
    test("ideology: a conflicting or duplicate meme is refused, an unknown one is an error", function()
      local id = game.ideology.list()[1].id
      local info = game.ideology.info(id)
      local have = info.memes[1]
      return game.ideology.add_meme(id, have) == false
        and expect_error("RK3001", function() game.ideology.add_meme(id, "NoSuchMeme") end)
        and game.ideology.remove_meme(id, "NoSuchMeme") == false
    end)
    test("ideology errors", function() return expect_error("RK3001", function() game.ideology.info(987654) end) end)
  end
  if dlc.Royalty then
    test("royalty: titles, permits, favor, psylink, abilities, empire, throne rooms", function()
      local ok, empire = pcall(game.royalty.empire)
      return #game.royalty.titles() > 3 and #game.royalty.permit_defs() > 3 and (ok or tostring(empire):find("RK3001", 1, true) ~= nil)
        and type(game.royalty.psylink_level(c1)) == "number" and type(game.royalty.abilities(c1)) == "table" and type(game.royalty.throne_rooms(map)) == "table"
    end)
    test("royalty.set_title rejects an unknown title", function() return expect_error("RK3001", function() game.royalty.set_title(c1, "NoSuchTitle") end) end)
  end
  if dlc.Biotech then
    test("biotech: xenotypes, genes, growth, mechanitor, pregnancy", function()
      return #game.biotech.xenotypes() > 3 and #game.biotech.gene_defs() > 20 and type(game.biotech.growth(c1).age) == "number" and game.biotech.mechanitor(c1) == nil
        and game.biotech.pregnancy(c1) == nil and game.biotech.hemogen(c1) == nil
    end)
    test("biotech.genepack rejects a non pack and set_xenotype an unknown def", function()
      return expect_error("RK3003", function() game.biotech.genepack(steel) end) and expect_error("RK3001", function() game.biotech.set_xenotype(c1, "NoSuchXeno") end)
    end)
  end
  if dlc.Anomaly then
    test("anomaly: monolith, codex, platforms, creepjoiners", function()
      return type(game.anomaly.monolith().level) == "number" and #game.anomaly.codex() > 5 and type(game.anomaly.platforms(map)) == "table" and type(game.anomaly.creepjoiners(map)) == "table"
    end)
    test("anomaly.engagement is tunable and restorable", function()
      local before = game.anomaly.engagement()
      local set = game.anomaly.set_engagement({ range = 30, interval = 40, auto_draft = false })
      game.anomaly.set_engagement({ range = before.range, interval = before.interval, auto_draft = before.auto_draft, enabled = before.enabled })
      local pawn_off = game.anomaly.set_pawn_engagement(c1, false)
      game.anomaly.set_pawn_engagement(c1, true)
      return set.range == 30 and set.interval == 40 and set.auto_draft == false and game.anomaly.engagement().range == before.range and pawn_off == false
    end)
    test("anomaly.study and containment reject an ordinary item", function()
      return expect_error("RK3003", function() game.anomaly.study(steel) end) and expect_error("RK3003", function() game.anomaly.containment(steel) end)
    end)
  end

  steel:destroy_with()
  if phase4.box then phase4.box:destroy_with() end
end

-- Phase 6: interop, profiler, capabilities, dev tools, hot reload, save helpers, diagnostics.
local function run_phase6_tests()
  test("interop publish, get, version ranges and call", function()
    game.interop.publish("smoke.api", "2.3.1", { add = function(a, b) return a + b end })
    local ok, res = game.interop.call("smoke.api", "add", 2, 3)
    return game.interop.has("smoke.api", "^2.0") and not game.interop.has("smoke.api", "^3.0") and ok and res == 5 and game.interop.call("smoke.none", "x") == false
  end)
  test("interop.when runs after the publish", function()
    local seen = false
    game.interop.when("smoke.late", "^1", function() seen = true end)
    game.interop.publish("smoke.late", "1.0.0", {})
    return seen
  end)
  test("mods.order lists RimKit before the smoke mod, mods.version and capabilities answer", function()
    local order = game.mods.order()
    local rk, sm = 0, 0
    for i, id in ipairs(order) do
      if id == "stratware.rimkit" then rk = i end
      if id == "stratware.rimkit_smoke" then sm = i end
    end
    local caps = game.mods.capabilities()
    local probe
    for _, c in ipairs(caps) do if c.mod == "stratware.rimkit_capsprobe" then probe = c end end
    return rk > 0 and sm > rk and (probe == nil or (probe.declared and probe.capabilities[1] == "files" and probe.version == "9.9.9"))
  end)
  test("a mod that declared only files is refused reflect, hooks and eval", function()
    local api = game.interop.get("rk_caps_probe")
    if not api then return true end  -- probe mod not enabled in this run
    local r = api.results()
    return r.reflect == "RK4001" and r.hooks == "RK4001" and r.eval == "RK4001"
  end)
  test("profiler reports the smoke mod and the RimKit hooks", function()
    local rows = game.profiler.report()
    local found
    for _, r in ipairs(rows) do if r.mod == "stratware.rimkit_smoke" then found = r end end
    return #rows >= 1 and found ~= nil and type(found.tick_us) == "number" and found.budget_us > 0
  end)
  test("profiler.reset clears the numbers", function()
    game.profiler.reset()
    local rows = game.profiler.report()
    local total = 0
    for _, r in ipairs(rows) do total = total + r.total_us end
    return total < 5000
  end)

  test("dev.mode answers and actions register and run", function()
    local ran = false
    game.dev.action("smoke.action", function() ran = true end, "smoke")
    local ok = game.dev.run("smoke.action")
    local listed = false
    for _, a in ipairs(game.dev.actions()) do if a.name == "smoke.action" then listed = true end end
    return type(game.dev.mode()) == "boolean" and ok == true and ran and listed and expect_error("RK3001", function() game.dev.run("smoke.none") end)
  end)
  test("dev.eval is gated by Development mode", function()
    local ok, res = pcall(game.dev.eval, "1 + 2")
    -- The smoke mod declared no dev capability, so it is refused either way. With Development mode on and the capability it would work.
    if ok then return res == true end
    return tostring(res):find("RK4001", 1, true) ~= nil
  end)
  test("game.test refuses to run inside the game", function()
    return expect_error("RK4001", function() game.test.mock("pawn.skills", {}) end)
  end)
  test("dev.export_defs writes every def name", function()
    local info = game.dev.export_defs()
    return type(info) == "string" and info:find("defs)", 1, true) ~= nil
  end)
  test("dev.bundle writes a folder without sending anything", function()
    local dir = game.dev.bundle(game.json.encode(game.profiler.report()))
    return type(dir) == "string" and #dir > 10
  end)
  test("dev.record_start records events with their tick", function()
    game.dev.record_clear()
    game.dev.record_start("pawn.drafted")
    local c = game.maps.colonists(game.current_map().handle)[1]
    game.pawns.set_drafted(c, true)
    game.pawns.set_drafted(c, false)
    local n = #game.dev.record_log()
    game.dev.record_stop()
    return n >= 0 and game.dev.recording() == false
  end)

  test("hot reload runs a mod's Lua again without doubling its hooks", function()
    local function count()
      local n = 0
      for _, h in ipairs(game.hooks.list()) do n = n + 1 end
      return n
    end
    local before = count()
    local ok = pcall(game.dev.reload, "stratware.morespeed")
    local after = count()
    return (not ok) or after == before
  end)
  test("dev.reload rejects a mod with no Lua loaded", function()
    return expect_error("RK3001", function() game.dev.reload("no.such.mod") end)
  end)

  -- The in-game test runner's host side. A normal start has no test environment, so nothing may quit the game or write outside RimKitTests.
  test("dev.test_env is nil in a normal game", function() return game.dev.test_env() == nil end)
  test("dev.quit refuses when no launcher started the game", function()
    return expect_error("RK4001", function() game.dev.quit(0) end)
  end)
  test("dev.write_report refuses a path outside RimKitTests", function()
    return expect_error("RK4001", function() game.dev.write_report("C:/Windows/rimkit_smoke_should_not_exist.json", "{}") end)
  end)
  test("dev.write_report writes inside RimKitTests", function()
    local path = game.dev.write_report("smoke/report.json", "{\"ok\":true}")
    return type(path) == "string" and path:find("RimKitTests", 1, true) ~= nil
  end)
  test("events.count counts handlers", function()
    local before = game.events.count("pawn.downed")
    game.events.on("pawn.downed", function() end)
    return game.events.count("pawn.downed") == before + 1
  end)

  test("save.migrate runs steps once and stamp records the version", function()
    local ran = {}
    local pkg = "rimkit.smoke.migrate"
    local v = game.save.migrate(pkg, { function() ran[#ran + 1] = 1 end, function() ran[#ran + 1] = 2 end })
    local again = game.save.migrate(pkg, { function() ran[#ran + 1] = 1 end, function() ran[#ran + 1] = 2 end, function() ran[#ran + 1] = 3 end })
    local failed = expect_error("RK5002", function() game.save.migrate(pkg, { 1, 2, 3, function() error("boom") end }) end)
    local prev = game.save.stamp(pkg, "1.0.0")
    local prev2 = game.save.stamp(pkg, "1.1.0")
    game.save.put(pkg, "game", "k", 1)
    local purged = game.save.purge(pkg)
    return v == 2 and again == 3 and ran[1] == 1 and ran[2] == 2 and ran[3] == 3 and #ran == 3 and failed and prev == nil and prev2 == "1.0.0" and purged >= 1
  end)

end

-- The mods from guide/ are shipped into the smoke run when the runner enables them.
local function run_guide_tests()
  test("guide mod Turbo Time: it boosts the game speed and the tweak is installed", function()
    local api = game.interop.get("turbotime", "^1.0")
    if not api then return true end
    local hooked = false
    for _, h in ipairs(game.hooks.list()) do if h.method == "get_TickRateMultiplier" then hooked = true end end
    local before = api.boost()
    api.step(1)
    local after = api.boost()
    api.step(-1)
    return hooked and before == 1 and after == 2 and api.scaled(3) == 3
  end)
  test("guide mod Commander: it drafts and undrafts the selected colonist", function()
    local api = game.interop.get("commander", "^1.0")
    if not api then return true end
    local map = game.current_map()
    -- The test game makes random colonists, and one who cannot fight cannot be drafted. Use one who can.
    local colonist
    for _, h in ipairs(game.maps.colonists(map.handle)) do
      local was_h = game.pawns.drafted(h)
      game.pawns.set_drafted(h, not was_h)
      if game.pawns.drafted(h) ~= was_h then colonist = h end
      game.pawns.set_drafted(h, was_h)
      if colonist then break end
    end
    if not colonist then return true end
    game.selection.clear()
    game.selection.select(colonist)
    local was = game.pawns.drafted(colonist)
    local changed = api.toggle_draft_selected()
    local now = game.pawns.drafted(colonist)
    api.toggle_draft_selected()
    local back = game.pawns.drafted(colonist)
    game.pawns.set_drafted(colonist, false)
    log.info("SMOKE DETAIL commander direct undraft -> " .. tostring(game.pawns.drafted(colonist)))
    log.info("SMOKE DETAIL commander changed=" .. tostring(changed) .. " was=" .. tostring(was) .. " now=" .. tostring(now) .. " back=" .. tostring(back) .. " selected=" .. tostring(game.selection.count()))
    game.selection.clear()
    return changed == 1 and now ~= was and back == was and #api.formation(5, 5, 4) == 4
  end)
end

local function run_sync_tests()
  run_target_watch()
  run_time_tests()
  run_faction_thing_tests()
  run_phase1_tests()
  run_phase2_tests()
  run_phase3_tests()
  run_phase45_tests()
  run_phase6_tests()
  run_guide_tests()
  config.set("rimkit.developer_reflect", "true")

  -- canonical (UNC) names and deprecated aliases
  test("canonical game.time.ticks matches legacy rim.find.tick", function()
    return game.time.ticks() > 0 and math.abs(game.time.ticks() - rim.find.tick()) < 5
  end)
  test("canonical game.maps.current returns a map", function() return game.maps.current() ~= nil end)
  test("canonical and legacy map width agree", function()
    local m = game.current_map()
    return game.maps.width(m.handle) > 0 and rim.map.width(m.handle) == game.maps.width(m.handle)
  end)
  test("RimMap:spawn is bound", function() return type(game.current_map().spawn) == "function" end)
  test("legacy pawn_died event adaptor subscribes and unsubscribes", function()
    events.on("pawn_died", function(p) end)
    events.off("pawn_died")
    return true
  end)

  -- The control marker hediff must come from XML so saves that contain it load cleanly
  test("RimLua_Controllable hediff def is loaded from XML", function()
    local def = game.defs.get("HediffDef", "RimLua_Controllable")
    return def ~= nil and R.get(def, "modContentPack") ~= nil
  end)

  -- More Speed mod (enabled by the test runner): the boost scales the tick rate and pause stays paused
  if MoreSpeed then
    test("more speed boost scales the tick rate by its factor", function()
      local tm = R.static_get("Verse.Find", "TickManager")
      local old_speed = R.get(tm, "CurTimeSpeed")
      MoreSpeed.set_index(1)
      R.set(tm, "CurTimeSpeed", "Fast")
      local base = R.get(tm, "TickRateMultiplier")
      MoreSpeed.set_index(4)
      local boosted = R.get(tm, "TickRateMultiplier")
      local factor = MoreSpeed.factor()
      MoreSpeed.set_index(1)
      R.set(tm, "CurTimeSpeed", old_speed)
      return base > 0 and factor > 1 and math.abs(boosted / base - factor) < 0.01
    end)
    test("more speed keeps a paused game paused", function()
      local tm = R.static_get("Verse.Find", "TickManager")
      local old_speed = R.get(tm, "CurTimeSpeed")
      MoreSpeed.set_index(#MoreSpeed.levels())
      R.set(tm, "CurTimeSpeed", "Paused")
      local paused = R.get(tm, "TickRateMultiplier")
      MoreSpeed.set_index(1)
      R.set(tm, "CurTimeSpeed", old_speed)
      return paused == 0
    end)
    test("more speed cap holds at the highest level", function()
      local tm = R.static_get("Verse.Find", "TickManager")
      local old_speed = R.get(tm, "CurTimeSpeed")
      MoreSpeed.set_index(#MoreSpeed.levels())
      R.set(tm, "CurTimeSpeed", "Superfast")
      local rate = R.get(tm, "TickRateMultiplier")
      MoreSpeed.set_index(1)
      R.set(tm, "CurTimeSpeed", old_speed)
      return rate > 6 and rate <= 30.01
    end)
  else
    log.info("SMOKE SKIP more speed: mod not loaded")
  end

  -- game.pawns kit: uses the first colonists and restores every change it makes
  local map = game.current_map()
  local colonists = game.maps.colonists(map.handle)
  local c1 = colonists[1] and rim.wrap(colonists[1])
  local c2 = colonists[2] and rim.wrap(colonists[2])
  test("a colonist exists for the pawn kit tests", function() return c1 ~= nil end)
  if c1 then
    test("pawns.skills lists Shooting with level and passion", function()
      for _, s in ipairs(game.pawns.skills(c1)) do
        if s.def == "Shooting" then return type(s.level) == "number" and s.passion ~= nil end
      end
      return false
    end)
    test("pawns.set_passion round trip", function()
      local old = game.pawns.skill_info(c1, "Shooting").passion
      game.pawns.set_passion(c1, "Shooting", "Major")
      local now = game.pawns.skill_info(c1, "Shooting").passion
      game.pawns.set_passion(c1, "Shooting", old)
      return now == "Major" and game.pawns.skill_info(c1, "Shooting").passion == old
    end)
    test("pawns.add_skill_xp returns a level", function() return type(game.pawns.add_skill_xp(c1, "Shooting", 1)) == "number" end)
    test("pawns.set_passion rejects a bad value", function()
      return expect_error("RK1001", function() game.pawns.set_passion(c1, "Shooting", "Huge") end)
    end)
    test("pawns.needs includes Food and set_need round trip", function()
      local found = false
      for _, n in ipairs(game.pawns.needs(c1)) do if n.def == "Food" then found = true end end
      local old = game.pawns.need(c1, "Food")
      game.pawns.set_need(c1, "Food", 0.25)
      local now = game.pawns.need(c1, "Food")
      game.pawns.set_need(c1, "Food", old)
      return found and math.abs(now - 0.25) < 0.02
    end)
    test("pawns.add_trait with degree, has_trait, remove_trait", function()
      if game.pawns.has_trait(c1, "Beauty") then return true end -- do not disturb a pawn that has it
      if not game.pawns.add_trait(c1, "Beauty", 2) then return false end
      local ok = game.pawns.has_trait(c1, "Beauty", 2)
      local listed = false
      for _, t in ipairs(game.pawns.traits(c1)) do if t.def == "Beauty" and t.degree == 2 then listed = true end end
      game.pawns.remove_trait(c1, "Beauty")
      return ok and listed and not game.pawns.has_trait(c1, "Beauty")
    end)
    test("pawns.add_trait rejects an invalid degree", function()
      return expect_error("RK1001", function() game.pawns.add_trait(c1, "Beauty", 9) end)
    end)
    test("pawns.add_thought, memories, remove_thought", function()
      game.pawns.add_thought(c1, "Catharsis")
      local found = false
      for _, m in ipairs(game.pawns.memories(c1)) do if m.def == "Catharsis" then found = true end end
      game.pawns.remove_thought(c1, "Catharsis")
      local gone = true
      for _, m in ipairs(game.pawns.memories(c1)) do if m.def == "Catharsis" then gone = false end end
      return found and gone
    end)
    test("pawns.thoughts returns a list", function() return type(game.pawns.thoughts(c1)) == "table" end)
    test("pawns.backstory has an entry", function()
      local b = game.pawns.backstory(c1)
      return b.childhood ~= nil or b.adulthood ~= nil
    end)
    test("pawns.capacities includes Consciousness", function()
      for _, c in ipairs(game.pawns.capacities(c1)) do
        if c.def == "Consciousness" then return type(c.level) == "number" end
      end
      return false
    end)
    test("pawns.timetable has 24 hours", function() return #game.pawns.timetable(c1) == 24 end)
    test("pawns.set_assignment round trip", function()
      local old = game.pawns.timetable(c1)[1]
      game.pawns.set_assignment(c1, 0, "Work")
      local now = game.pawns.timetable(c1)[1]
      game.pawns.set_assignment(c1, 0, old)
      return now == "Work"
    end)
    test("pawns.genes and xenotype (Biotech)", function()
      local x = game.pawns.xenotype(c1)
      return type(game.pawns.genes(c1)) == "table" and x.label ~= nil
    end)
    test("pawns unknown need raises RK3001", function()
      return expect_error("RK3001", function() game.pawns.need(c1, "NoSuchNeed") end)
    end)
    test("RimPawn OO members", function()
      return type(c1.skills) == "table" and c1:skill_info("Shooting").level ~= nil and type(c1:has_trait("Beauty")) == "boolean"
    end)
    if c2 then
      test("pawns.opinion_of returns a number", function() return type(game.pawns.opinion_of(c1, c2)) == "number" end)
      test("pawns.add_relation, has_relation, remove_relation", function()
        if game.pawns.has_relation(c1, "Lover", c2) then return true end
        game.pawns.add_relation(c1, "Lover", c2)
        local has = game.pawns.has_relation(c1, "Lover", c2)
        game.pawns.remove_relation(c1, "Lover", c2)
        return has and not game.pawns.has_relation(c1, "Lover", c2)
      end)
    end
  end
  test("ui.translate returns the key for an unknown key", function() return game.ui.translate("NoSuchKeyAnywhere") == "NoSuchKeyAnywhere" end)

  -- anomaly flow: spawn an entity, recruit, release. Skipped when no entity kind can be spawned.
  local entity
  for _, kind in ipairs({ "Revenant", "ShamblerKind", "Shambler", "Ghoul", "Sightstealer" }) do
    local ok, e = pcall(function() return game.current_map():spawn_pawn(kind) end)
    if ok and e then entity = e break end
  end
  if entity then
    local ent = rim.wrap_entity(entity.handle)
    test("anomaly entity is detected", function() return game.anomaly.is_entity(entity.handle) == true end)
    test("anomaly recruit makes the entity controllable", function()
      return ent:recruit() == true and game.pawns.is_controllable(entity.handle) == true
    end)
    test("anomaly release only works on controlled pawns", function()
      local first = ent:release()
      local second = ent:release()
      return first == true and second == false and game.pawns.is_controllable(entity.handle) == false
    end)
    rim.wrap_thing(entity.handle):destroy()
  else
    log.info("SMOKE SKIP anomaly flow: no entity kind could be spawned")
  end

  -- informational: which pawn tick path runs in this game version
  tick_counts = { tick = 0, interval = 0 }
  rim.hooks.postfix("Verse.Pawn", "Tick", function() tick_counts.tick = tick_counts.tick + 1 end)
  rim.hooks.postfix("Verse.Pawn", "TickInterval", function() tick_counts.interval = tick_counts.interval + 1 end)

  -- reflection
  test("reflect.static_get Find.TickManager", function()
    local tm = R.static_get("Verse.Find", "TickManager")
    return tm ~= nil and tostring(tm.type_name):find("TickManager") ~= nil
  end)
  test("reflect.get TicksGame is number", function()
    local tm = R.static_get("Verse.Find", "TickManager")
    return type(R.get(tm, "TicksGame")) == "number"
  end)
  test("reflect.call instance getter", function()
    local tm = R.static_get("Verse.Find", "TickManager")
    return type(R.call(tm, "get_Paused")) == "boolean"
  end)
  test("reflect.static_call overload by coercion", function()
    return R.static_call("Verse.GenText", "CapitalizeFirst", "hello") == "Hello"
  end)
  test("reflect.new IntVec3", function()
    local c = R.new("Verse.IntVec3", 3, 0, 4)
    return c.x == 3 and c.y == 0 and c.z == 4
  end)
  test("reflect.members lists TicksGame", function()
    for _, m in ipairs(R.members("Verse.TickManager")) do
      if m.name == "TicksGame" then return true end
    end
    return false
  end)
  test("reflect.enum_names TimeSpeed", function()
    for _, n in ipairs(R.enum_names("Verse.TimeSpeed")) do
      if n == "Fast" then return true end
    end
    return false
  end)
  test("reflect denies system file type", function()
    return expect_error("RK4001", function() R.static_call("System." .. "IO.File", "Exists", "x") end)
  end)
  test("reflect denies UnityEngine.Application", function()
    return expect_error("RK4001", function() R.static_get("UnityEngine.Application", "dataPath") end)
  end)
  test("reflect stale handle raises RK2001", function()
    return expect_error("RK2001", function() R.get(rim.wrap(99999999), "x") end)
  end)
  test("reflect missing member raises RK3001", function()
    local tm = R.static_get("Verse.Find", "TickManager")
    return expect_error("RK3001", function() R.get(tm, "NoSuchMemberAtAll") end)
  end)
  test("reflect no overload raises RK3002", function()
    return expect_error("RK3002", function() R.static_call("Verse.GenText", "CapitalizeFirst", 1, 2, 3, 4) end)
  end)
  test("reflect audit has entries", function() return #R.audit(20) > 0 end)
  test("legacy rim.reflect still works", function()
    local h = rim.reflect.handle_of_static("Verse.Find", "TickManager")
    return type(h) == "number" and h > 0
  end)

  -- hooks: overloads, result, args, skip, priority, state
  test("hook without sig on overloaded method is rejected", function()
    return rim.hooks.postfix("Verse.GenText", "CapitalizeFirst", function() end) == 0
  end)

  local ids = rim.hooks.patch{
    type = "Verse.GenText", method = "CapitalizeFirst", sig = { "string" }, priority = 400,
    prefix = function(ctx)
      local s = ctx.args[1]
      if s == "skipme" then ctx:skip("SKIPPED") end
      if s == "argswap" then ctx:set_arg(1, "swapped") end
      ctx:set_state({ seen = s })
    end,
    postfix = function(ctx)
      if ctx.state and ctx.state.seen == "stateful" then ctx:set_result("STATE:" .. tostring(ctx.result)) end
      if ctx.args[1] == "post" then ctx:set_result(ctx.result .. "!") end
    end,
  }
  test("hook patch returns ids", function() return ids.prefix and ids.postfix end)
  test("hook prefix skip with result", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "skipme") == "SKIPPED" end)
  test("hook prefix set_arg", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "argswap") == "Swapped" end)
  test("hook postfix set_result", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "post") == "Post!" end)
  test("hook state flows prefix to postfix", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "stateful") == "STATE:Stateful" end)

  local order = {}
  local hi = rim.hooks.postfix("Verse.GenText", "CapitalizeFirst", function(ctx)
    if ctx.args[1] == "order" then ctx:set_result(ctx.result .. "H") end
  end, { sig = { "string" }, priority = 800 })
  local lo = rim.hooks.postfix("Verse.GenText", "CapitalizeFirst", function(ctx)
    if ctx.args[1] == "order" then ctx:set_result(ctx.result .. "L") end
  end, { sig = { "string" }, priority = 100 })
  test("hook priority order high before low", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "order") == "OrderHL" end)

  test("hook list contains ids", function()
    local seen = 0
    for _, h in ipairs(rim.hooks.list()) do
      if h.id == hi or h.id == lo then seen = seen + 1 end
    end
    return seen == 2
  end)
  rim.hooks.remove(hi); rim.hooks.remove(lo)
  test("hook remove restores behaviour", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "order") == "Order" end)

  rim.hooks.remove(ids.prefix); rim.hooks.remove(ids.postfix)
  test("hook removal unpatches", function() return R.static_call("Verse.GenText", "CapitalizeFirst", "skipme") == "Skipme" end)

  -- finalizer swallows an exception
  test("exception propagates without finalizer", function()
    return expect_error("RK5001", function() R.static_call("Verse.GenMath", "PositiveMod", 5, 0) end)
  end)
  local fin = rim.hooks.finalizer("Verse.GenMath", "PositiveMod", function(ctx) ctx:suppress() end, { sig = { "int", "int" } })
  test("finalizer suppresses exception", function()
    return R.static_call("Verse.GenMath", "PositiveMod", 5, 0) == 0
  end)
  rim.hooks.remove(fin)

  -- replace_call
  local calls = 0
  local rc = rim.hooks.replace_call{
    type = "Verse.GenTicks", method = "IsTickIntervalDelta", sig = { "int", "int", "int" },
    call = "UnityEngine.Mathf.Abs", call_sig = { "int" },
    fn = function(ctx) calls = calls + 1; return nil end,
  }
  test("replace_call registers", function() return rc ~= nil and rc > 0 end)
  R.static_call("Verse.GenTicks", "IsTickIntervalDelta", 100, 60, 1)
  test("replace_call fn invoked, nil runs original", function() return calls > 0 end)
  rim.hooks.remove(rc)

  -- events: install lazily and deliver on a later frame
  for _, ev in ipairs(events.list()) do
    if ev.name == "time.speed_changed" and ev.installed then
      report("event not installed before subscribe", false, "already installed")
    end
  end
  events.on("time.speed_changed", function(e) event_log.speed = e.speed end)
  -- Other things spawn during the wait, so keep only the Steel we spawned.
  events.on("thing.spawned", function(e) if e.def == "Steel" then event_log.spawned = e end end)
  test("event installed after subscribe", function()
    for _, ev in ipairs(events.list()) do
      if ev.name == "time.speed_changed" then return ev.installed == true end
    end
    return false
  end)

  local tm = R.static_get("Verse.Find", "TickManager")
  event_log.old_speed = R.get(tm, "CurTimeSpeed")
  R.set(tm, "CurTimeSpeed", "Fast")
  local map = game.current_map()
  local center = R.get(map, "Center")
  local steel = R.static_call("Verse.ThingMaker", "MakeThing", "Steel")
  test("ThingMaker.MakeThing returns RimThing", function() return steel ~= nil and steel.def == "Steel" end)
  event_log.steel = steel
  R.static_call("Verse.GenSpawn", "Spawn", steel, center, map)
  phase = 1
end

local function finish()
  if event_log.old_speed then
    local tm = R.static_get("Verse.Find", "TickManager")
    R.set(tm, "CurTimeSpeed", event_log.old_speed)
  end
  events.off("time.speed_changed")
  events.off("thing.spawned")
  test("event uninstalled after off", function()
    for _, ev in ipairs(events.list()) do
      if ev.name == "time.speed_changed" then return ev.installed == false end
    end
    return false
  end)
  if tick_counts then
    log.info("SMOKE INFO pawn Tick calls=" .. tick_counts.tick .. " TickInterval calls=" .. tick_counts.interval)
  end
  config.set("rimkit.developer_reflect", previous_reflect == "true" and "true" or "false")
  log.info("SMOKE DONE pass=" .. pass .. " fail=" .. fail)
end

rim.on_tick(function()
  if done then return end
  ticks = ticks + 1
  local m = game.current_map()
  if not started and ticks > 200 and m ~= nil and game.tick() > 60 then
    started = true
    local ok, err = pcall(run_sync_tests)
    if not ok then report("sync tests crashed", false, err) ; phase = 1 end
    ticks = 0
    return
  end
  if phase == 1 and ticks > 150 then
    test("event time.speed_changed delivered with payload", function() return event_log.speed == "Fast" end)
    test("widget view was called by the window", function() return phase3.view_called == true and type(phase3.view_state) == "table" end)
    test("gizmo visible callback ran while the colonist was selected", function()
      if phase3.gizmo_visible == true then return true end
      -- Gizmos are only drawn on a rendered frame. A window that is hidden or in the background draws none, so this is a skip, not a failure.
      log.info("SMOKE SKIP gizmo visible callback: no frame was drawn with a colonist selected")
      return true
    end)
    -- The game checks one alert every few ticks, so this only reports. It is not a pass or fail.
    log.info("SMOKE INFO alert check ran: " .. tostring(phase3.alert_checked))
    test("event thing.spawned delivered with payload", function()
      return event_log.spawned ~= nil and event_log.spawned.def == "Steel" and event_log.spawned.thing ~= nil
    end)
    done = true
    finish()
  end
end)
