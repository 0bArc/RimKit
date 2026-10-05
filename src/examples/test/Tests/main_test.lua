-- Run with: rimkit mod test
-- Tests run against a mock host, so no game is needed. Mock what the game would answer, call your code, check the result.
local t = game.test

t.describe("test", function()
  t.it("says it loaded", function()
    t.start()                                  -- runs the mod's on_load handlers
    t.expect(t.logged("loaded")).to_be(true)   -- what the mod logged or showed
  end)
end)
