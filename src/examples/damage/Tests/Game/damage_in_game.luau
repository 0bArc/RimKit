-- Tests that run inside the real game. They are not part of `rimkit mod test`.
-- Run them with: rimkit mod test --in-game
-- or, in a game with Development mode on, from the dev tools console: game.itest.load("yourname.damage"); game.itest.run({ mods = { "yourname.damage" } })

local suite = game.itest.suite("damage")

suite.test("a hurt colonist raises pawn.damaged with the damage dealt", function(t)
  local pawn = t.spawn_pawn()
  local hits = t.watch("pawn.damaged")

  pawn:damage(10, "Cut")

  t.wait_until(function() return #hits > 0 end, 60, "pawn.damaged never fired")
  t.expect(hits[1].dealt > 0).to_be(true)
end)

suite.test("the mod listens for damage exactly once", function(t)
  t.expect(game.events.count("pawn.damaged", "yourname.damage")).to_be(1)
end)

suite.test("hot reload leaves one damage handler and keeps the mod working", function(t)
  local mod = "yourname.damage"
  local before = game.events.count("pawn.damaged", mod)

  game.dev.reload(mod)
  t.wait_ticks(5)

  t.expect(game.events.count("pawn.damaged", mod)).to_be(before)
  local pawn = t.spawn_pawn()
  local hits = t.watch("pawn.damaged")
  pawn:damage(5, "Cut")
  t.wait_until(function() return #hits > 0 end, 60, "pawn.damaged stopped firing after the reload")
end)
