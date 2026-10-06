-- Calls kit functions through the mocked host and lets the test inspect the requests.


game.maps.set_roof(5, 3, 4, "RoofConstructed")
game.maps.set_roof(5, 3, 4)
game.query.things({ map = 5, def = "Steel", faction = "player" })
game.query.things({ map = 5, def = "Steel" })
game.pawns.set_assignments({ 11, 12 }, 3, "Work")
game.pawns.kinds()

local ok, err = pcall(function() return game.maps.terrain(5, 99999, 4) end)
log.info("RESULT error " .. tostring(err):match("RK%d+"))

local ok2, err2 = pcall(function() return game.maps.terrain() end)
log.info("RESULT nosubject " .. tostring(err2):match("RK%d+"))

local leader = game.factions.leader(3)
log.info("RESULT leader " .. type(leader))
local members = game.factions.members(3)
log.info("RESULT members " .. type(members[1]) .. " " .. #members)
-- a wrapped object works as the subject of the next call
game.factions.info(leader)

-- version helpers and version-aware hooks (the mocked host reports RimWorld 1.6)
log.info("RESULT at_least 1.5 " .. tostring(game.version.at_least("1.5")))
log.info("RESULT at_least 1.7 " .. tostring(game.version.at_least("1.7")))
game.version.hook("postfix", {
  ["1.5"] = { type = "A", method = "Old" },
  ["1.6"] = { type = "A", method = "New" },
  ["1.7"] = { type = "A", method = "Future" },
}, function() end)
local none = game.version.hook("postfix", { ["1.7"] = { type = "A", method = "Future" } }, function() end)
log.info("RESULT none " .. tostring(none))

-- callbacks: the first function argument gets id 1, the second 2
game.widgets.open("T", function(v) return { type = "label", text = "n=" .. tostring(v.state.n) } end, function(e) end)
game.gizmos.add("g", { label = "G" }, function(x) return x * 2 end)
game.hud.status("s", "text")
log.info("RESULT json " .. game.json.encode({ a = 1 }))

-- Lua classes: define registers one callback per function, the host sends { n, a } so nil arguments keep their place
local defined = game.classes.define("comp", "my_comp", { tick = function(...) return select("#", ...) end, note = "not a function" })
log.info("RESULT defined " .. tostring(defined))

-- tweaks: a named tweak installs a postfix on the catalog target
game.tweaks.on("pawn.hunger_rate", function(t) return t.value * 2 end)
local ok3, err3 = pcall(function() game.tweaks.on("no.such_tweak", function() end) end)
log.info("RESULT tweak " .. tostring(err3):match("RK%d+") .. " " .. #game.tweaks.list())

-- legacy functions accept a wrapped object or a plain handle for every subject
game.pawns.set_drafted(leader, true)
game.pawns.set_drafted(21, false)
game.pawns.give_hediff(leader, "Flu", 0.5)
leader.hp = 10
game.anomaly.knock_out(leader, 1.0)
game.anomaly.start_capture(leader, 22, 23)
game.work.set_priority(leader, "Cooking", 2)
game.maps.spawn(leader, "Steel", 1, 2)
log.info("RESULT handles done")

-- needs defined from Lua: the options table travels as JSON next to the def name
game.needs.define("MyNeed", { label = "My need", who = "humanlike", fall_per_day = 0.2 })
game.needs.remove("MyNeed")

-- bind_op functions take a wrapped object as the subject too
game.pawns.drafted(leader)
local _ = leader.def
game.maps.width(leader)
