-- Run with: rimkit mod test guide/01-speed-mod/final/TurboTime
local t = game.test

t.describe("Turbo Time", function()
  t.it("starts at normal speed", function()
    local turbo = game.interop.get("turbotime")
    t.expect(turbo.boost()).to_be(1)
    t.expect(turbo.scaled(3)).to_be(3)
  end)

  t.it("a faster step multiplies the game's speed", function()
    t.mock("config.get", "30")
    local turbo = game.interop.get("turbotime")
    t.expect(turbo.step(1)).to_be(true)       -- level 2
    t.expect(turbo.boost()).to_be(2)
    t.expect(turbo.scaled(3)).to_be(6)
  end)

  t.it("never speeds up a paused game", function()
    local turbo = game.interop.get("turbotime")
    t.expect(turbo.scaled(0)).to_be(0)
  end)

  t.it("the cap stops a big boost", function()
    t.mock("config.get", "10")
    local turbo = game.interop.get("turbotime")
    for _ = 1, 10 do turbo.step(1) end         -- top level
    t.expect(turbo.scaled(6)).to_be(10)
  end)

  t.it("stops at the ends of the list", function()
    local turbo = game.interop.get("turbotime")
    for _ = 1, 10 do turbo.step(-1) end
    t.expect(turbo.boost()).to_be(1)
    t.expect(turbo.step(-1)).to_be(false)
  end)

  t.it("the tweak uses the same scaling", function()
    t.mock("config.get", "30")
    local turbo = game.interop.get("turbotime")
    turbo.step(1)
    t.expect(t.tweak("time.rate_multiplier")({ value = 3 })).to_be(6)
  end)
end)
