-- Run with: rimkit mod test src/examples/ColonyStats
local t = game.test

local function stats()
  t.mock("ui.translate", function(a) return a.key end)
  t.mock("find.current_map", 1)
  t.mock("map.colonists", { 11, 12, 13 })
  t.mock("economy.silver", 120)
  t.mock("economy.wealth", { total = 5000.4 })
  t.mock("map.nutrition", 20.55)
  return game.interop.get("colonystats")
end

t.describe("ColonyStats", function()
  t.it("counts colonists and reads silver, wealth and food", function()
    local rows = stats().rows()
    t.expect(rows[1][2]).to_be("3")
    t.expect(rows[2][2]).to_be("120")
    t.expect(rows[3][2]).to_be("5000")
    t.expect(rows[4][2]).to_be("20.6")
  end)

  t.it("opens a refreshing window on the first press and closes it on the second", function()
    local stat = stats()
    t.mock("widgets.open", 7)
    t.mock("widgets.close", true)
    stat.toggle()
    t.expect(#t.calls("widgets.open")).to_be(1)
    stat.toggle()
    t.expect(#t.calls("widgets.close")).to_be(1)
  end)
end)
