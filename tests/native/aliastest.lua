assert(game.pawns.name and game.maps.current and game.time.ticks and game.hooks.patch and game.ui.message and game.weather.set and game.events.on and game.log.info and game.factions.player and game.things.make and game.config.get)
assert(game.pawns.health_ratio and game.pawns.queue_surgery and game.jobs.start_job and game.pawns.any_player and game.selection.first)
-- canonical names do not warn
local a = game.pawns.name(5)
log.info("canonical name=" .. tostring(a))
-- the old name works and warns once
local b = rim.pawn.name(5)
local c = rim.pawn.name(5)
assert(a == b and b == c)
-- core shorthands do not warn
local t = game.tick()
events.on("pawn.died", function(e) log.info("C " .. e.pawn.name); if e.culprit then log.info("DEF " .. e.culprit.def) end end)
events.on("pawn_died", function(p) log.info("L " .. p.name) end)
events.on("pawn_died", function(p) end)
log.info("ALIAS OK")

-- game.pawns kit (native-only functions, no deprecation) and game.ui.translate
for _, name in ipairs({ "skills", "skill_info", "set_passion", "add_skill_xp", "needs", "need", "set_need", "traits",
  "has_trait", "add_trait", "remove_trait", "thoughts", "memories", "add_thought", "remove_thought", "opinion_of",
  "relations", "has_relation", "add_relation", "remove_relation", "backstory", "set_backstory", "capacities",
  "capacity", "timetable", "set_assignment", "genes", "has_gene", "add_gene", "remove_gene", "xenotype" }) do
  assert(type(game.pawns[name]) == "function", "game.pawns." .. name .. " missing")
end
local skills = game.pawns.skills(5)
assert(skills[1].def == "Shooting" and skills[1].level == 7 and skills[1].passion == "Major")
game.pawns.add_trait(5, "Beauty", 2)
assert(game.ui.translate("TameAnomalies_Key") == "translated")
-- game.time kit, game.version helpers, game-update watch helpers
for _, name in ipairs({ "now", "speed", "set_speed", "paused", "set_paused", "date_text", "ticks" }) do
  assert(type(game.time[name]) == "function", "game.time." .. name .. " missing")
end
assert(game.time.ticks_per_day == 60000 and game.time.ticks_per_hour == 2500)
for _, name in ipairs({ "rimworld", "rimkit", "api_level", "at_least" }) do
  assert(type(game.version[name]) == "function", "game.version." .. name .. " missing")
end
assert(type(game.version.rimkit()) == "string" and game.version.api_level() == 0)
assert(type(game.hooks.check_target) == "function" and type(game.hooks.check_events) == "function")
-- game.factions and game.things kits
for _, name in ipairs({ "info", "goodwill", "set_goodwill", "adjust_goodwill", "relation", "set_relation", "is_hostile",
  "leader", "members", "hostiles", "allies", "defs", "player", "list", "name", "of_def" }) do
  assert(type(game.factions[name]) == "function", "game.factions." .. name .. " missing")
end
for _, name in ipairs({ "make", "spawn_at" }) do
  assert(type(game.things[name]) == "function", "game.things." .. name .. " missing")
end
-- things are objects: methods and properties on RimThing, nothing that takes a thing on game.things
local thing = rim.wrap_thing(1)
for _, name in ipairs({ "info", "quality", "set_quality", "stuff", "forbidden", "set_forbidden", "rotation", "set_rotation",
  "comps", "has_comp", "damage", "heal", "destroy_with", "destroy", "despawn" }) do
  assert(type(thing[name]) == "function", "RimThing:" .. name .. " missing")
  assert(game.things[name] == nil, "game.things." .. name .. " should be gone")
end
for _, name in ipairs({ "hp", "max_hp", "stack", "position", "map", "faction", "spawned", "is_pawn", "is_building", "id", "def", "label" }) do
  local ok = pcall(function() return thing[name] end)
  assert(ok, "RimThing." .. name .. " property missing")
end
-- events take an optional filter table, effects take a thing as the anchor, ui.say exists
game.events.on("pawn.damaged", { humanlike = true, min_dealt = 1 }, function() end)
local okf, errf = pcall(function() game.events.on("pawn.damaged", { humanlike = true }) end)
assert(not okf and tostring(errf):find("RK1001"), "a filter without a function must raise RK1001")
assert(type(game.ui.say) == "function" and type(game.effects.text) == "function")
local fx = pcall(function() game.effects.text(rim.wrap_thing(1), "5", "red") end)
assert(fx, "effects.text accepts a thing and a colour name")
-- one function per event: on_ plus the event name with the dot as an underscore
assert(type(game.events.on_pawn_damaged) == "function", "game.events.on_pawn_damaged missing")
assert(type(game.events.on_mental_state_broke) == "function" and game.events.on_nothing == nil)
game.events.on_pawn_damaged(function() end, { humanlike = true })
game.events.on_pawn_died(function() end)
-- the runtime is Luau: its syntax, its libraries and its sandbox
assert(_VERSION == "Luau", "the runtime must be Luau, got " .. tostring(_VERSION))
local total: number = 0
for i = 1, 5 do
  if i == 3 then continue end
  total += i
end
assert(total == 12, "continue and += must work")
local label = if total > 10 then "big" else "small"
assert(label == "big", "if-expressions must work")
assert(`total is {total}` == "total is 12", "string interpolation must work")
for k, v in { a = 1 } do assert(k == "a" and v == 1) end
assert(table.find({ 5, 6, 7 }, 6) == 2 and math.clamp(9, 0, 5) == 5 and typeof(buffer.create(4)) == "buffer")
assert(loadstring == nil and getfenv == nil and setfenv == nil and os == nil and io == nil and dofile == nil, "sandbox")
assert(require == nil or type(require) == "function")
-- built-in libraries: rimkit.signal and rimkit.promise
local Signal = require("rimkit.signal")
local Promise = require("rimkit.promise")
assert(require("rimkit").signal == Signal and require("rimkit").promise == Promise, "also reachable from require(\"rimkit\")")
assert(rimkit.signal == Signal and rimkit.promise == Promise, "the global rimkit")
do
  local sig = Signal.new()
  local got = {}
  local c1 = sig:connect(function(a, b) table.insert(got, a .. b) end)
  sig:once(function(a) table.insert(got, "once" .. a) end)
  sig:connect(function() error("handler errors must not stop the others") end)
  sig:connect(function(a) table.insert(got, "last" .. a) end)
  sig:fire("x", "y")
  sig:fire("z", "w")
  assert(table.concat(got, ",") == "xy,oncex,lastx,zw,lastz", table.concat(got, ","))
  assert(sig:connection_count() == 3 and c1.connected)
  c1:disconnect()
  assert(not c1.connected and sig:connection_count() == 2)
  local waited
  coroutine.wrap(function() waited = { sig:wait() } end)()
  sig:fire(1, 2)
  assert(waited and waited[1] == 1 and waited[2] == 2, "signal:wait returns what fire sent")
  sig:destroy()
  sig:fire("ignored")
  assert(not pcall(sig.connect, sig, function() end), "a destroyed signal refuses connect")
end
do
  local seen
  Promise.resolve(2):and_then(function(v) return v * 3 end):and_then(function(v) seen = v end)
  assert(seen == 6, "and_then chains")
  local err
  Promise.reject("boom"):catch(function(e) err = e end)
  assert(err == "boom")
  local thrown
  Promise.new(function() error("in executor", 0) end):catch(function(e) thrown = e end)
  assert(thrown == "in executor")
  local order = {}
  Promise.resolve(1):finally(function() table.insert(order, "finally") end):and_then(function(v) table.insert(order, "after" .. v) end)
  assert(table.concat(order, ",") == "finally,after1")
  local allv
  Promise.all({ Promise.resolve(1), 2, Promise.resolve(3) }):and_then(function(v) allv = v end)
  assert(allv and allv[1] == 1 and allv[2] == 2 and allv[3] == 3, "all")
  local late
  local resolve_later
  local waiting = Promise.new(function(resolve) resolve_later = resolve end)
  waiting:and_then(function(v) late = v end)
  assert(late == nil and waiting.status == "pending")
  resolve_later("now")
  assert(late == "now" and waiting.status == "resolved")
  local cancelled_hook, child_ran = false, false
  local p = Promise.new(function(_, _, on_cancel) on_cancel(function() cancelled_hook = true end) end)
  p:and_then(function() child_ran = true end)
  p:cancel()
  assert(p.status == "cancelled" and cancelled_hook and not child_ran)
  local ok, value
  Promise.async(function()
    local a, b = Promise.resolve(41):await()
    local sum = Promise.new(function(resolve) resolve(b + 1) end)
    return select(2, sum:await())
  end):and_then(function(v) ok, value = true, v end)
  assert(ok and value == 42, "async and await")
  local failed
  Promise.async(function() error("async failure", 0) end):catch(function(e) failed = e end)
  assert(failed == "async failure")
  local raced
  Promise.race({ Promise.new(function() end), Promise.resolve("first") }):and_then(function(v) raced = v end)
  assert(raced == "first")
end
log.info("PAWNS OK")
assert(type(game.hooks.before) == "table" and type(game.hooks.after) == "table", "game.hooks.before and after sugar")
