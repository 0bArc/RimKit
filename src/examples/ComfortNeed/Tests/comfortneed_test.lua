-- Run with: rimkit mod test src/examples/ComfortNeed
local t = game.test

t.describe("ComfortNeed", function()
  t.it("defines the need when the mod loads", function()
    -- the call ran while the mod loaded, with the need's name and its options
    t.expect(game.interop.get("comfortneed").need).to_be("ComfortNeed_Comfort")
  end)

  t.it("restores comfort indoors and wears it down outdoors", function()
    local c = game.interop.get("comfortneed")
    t.expect(c.change_for(true) > 0).to_be(true)
    t.expect(c.change_for(false) < 0).to_be(true)
  end)

  t.it("raises the need by a step for a pawn under a roof", function()
    local need = t.class("need", "ComfortNeed_Comfort")
    t.mock("pawn.map", 1)
    t.mock("thing.info", { x = 5, z = 6 })
    t.mock("map.roof", "RoofConstructed")
    t.mock("pawn.set_need", true)
    need.interval(rim.wrap(3), 0.5)
    local set = t.calls("pawn.set_need")[1].args
    t.expect(set.def).to_be("ComfortNeed_Comfort")
    t.expect(math.abs(set.level - 0.51) < 0.0001).to_be(true)
  end)

  t.it("gives a bad thought at rock bottom", function()
    local need = t.class("need", "ComfortNeed_Comfort")
    t.mock("pawn.map", 1)
    t.mock("thing.info", { x = 5, z = 6 })
    t.mock("map.roof", nil)
    t.mock("pawn.set_need", true)
    t.mock("pawn.add_thought", true)
    need.interval(rim.wrap(3), 0.04)
    t.expect(#t.calls("pawn.add_thought")).to_be(1)
  end)
end)
