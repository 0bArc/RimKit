-- Tests that run inside the real game: rimkit mod test --in-game
local suite = game.itest.suite("sample")

suite.test("passes at once", function(t)
  t.expect(1 + 1).to_be(2)
end)

suite.test("waits for game ticks", function(t)
  local start = t.ticks()
  t.wait_ticks(30)
  t.expect(t.ticks() - start >= 30).to_be(true)
end)

suite.test("fails with a message", function(t)
  t.expect("a").to_be("b")
end)

suite.test("waits for a condition that never comes", function(t)
  t.wait_until(function() return false end, 20, "the colony never grew")
end)

suite.test("sees an event", function(t)
  local seen = t.watch("pawn.damaged")
  t.wait_until(function() return #seen > 0 end, 60, "no damage")
  t.expect(seen[1].dealt).to_be(4)
end)

suite.test("skips itself", function(t)
  t.skip("no map")
end)

suite.skip("is skipped by name", "not ready")

suite.test("raises an error", function(t)
  error("boom")
end)

suite.test("cleans up what it spawned", function(t)
  t.cleanup(function() game.log.info("cleanup ran") end)
  t.fail("on purpose")
end)
