-- Run with: rimkit mod test src/examples/LuckyIncident
local t = game.test

t.describe("LuckyIncident", function()
  t.it("can fire only when the colony has someone in it", function()
    local incident = t.class("incident", "lucky_day")
    t.mock("find.current_map", 1)
    t.mock("query.pawns", { 4 })
    t.expect(incident.can_fire({})).to_be(true)
    t.mock("query.pawns", {})
    t.expect(incident.can_fire({})).to_be(false)
  end)

  t.it("drops silver and sends a letter", function()
    local incident = t.class("incident", "lucky_day")
    t.mock("find.current_map", 1)
    t.mock("map.drop_spot", { x = 30, z = 40 })
    t.mock("thing.spawn_at", 99)
    t.mock("hud.letter", true)
    t.mock("ui.translate", function(a) return a.key end)
    t.expect(incident.execute({})).to_be(true)
    local spawn = t.calls("thing.spawn_at")[1].args
    t.expect(spawn.def).to_be("Silver")
    t.expect(spawn.x).to_be(30)
    t.expect(#t.calls("hud.letter")).to_be(1)
  end)
end)
