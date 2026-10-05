assert(game.pawns.name and game.maps.current and game.time.ticks and game.hooks.patch and game.ui.message and game.weather.set and game.events.on and game.log.info and game.factions.player and game.things.hp and game.config.get)
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
for _, name in ipairs({ "info", "quality", "set_quality", "stuff", "forbidden", "set_forbidden", "rotation", "set_rotation",
  "comps", "has_comp", "damage", "heal", "destroy_with", "make", "spawn_at", "def", "label", "destroy", "hp" }) do
  assert(type(game.things[name]) == "function", "game.things." .. name .. " missing")
end
log.info("PAWNS OK")
