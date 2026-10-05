-- Run with: rimkit mod test src/examples/CheerfulMood
local t = game.test

t.describe("CheerfulMood", function()
  t.it("is active under a clear sky", function()
    t.mock("pawn.map", 1)
    t.mock("map.info", { weather = "Clear" })
    t.expect(t.class("thought_worker", "clear_sky").state(rim.wrap(5))).to_be(true)
  end)

  t.it("is hidden in the rain", function()
    t.mock("pawn.map", 1)
    t.mock("map.info", { weather = "Rain" })
    t.expect(t.class("thought_worker", "clear_sky").state(rim.wrap(5))).to_be(false)
  end)

  t.it("is hidden for a pawn that is not on a map", function()
    t.mock("pawn.map", nil)
    t.expect(t.class("thought_worker", "clear_sky").state(rim.wrap(5))).to_be(false)
  end)

  t.it("is hidden for a world pawn, whose map handle is 0", function()
    t.mock("pawn.map", 0)
    t.mock("map.info", function() error("RK2001: no map") end)
    t.expect(t.class("thought_worker", "clear_sky").state(rim.wrap(5))).to_be(false)
  end)

  t.it("offers the check to other mods", function()
    t.mock("map.info", { weather = "Clear" })
    t.expect(game.interop.get("cheerfulmood").is_clear(1)).to_be(true)
  end)
end)
