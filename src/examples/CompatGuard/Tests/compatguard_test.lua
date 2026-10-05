-- Run with: rimkit mod test src/examples/CompatGuard
local t = game.test

t.describe("CompatGuard", function()
  t.it("does nothing when the other mod is not installed", function()
    t.mock("mods.active", false)
    t.expect(game.interop.get("compatguard").hook()).to_be(false)
    t.expect(t.logged("is not installed")).to_be(true)
  end)

  t.it("skips a version of the other mod that lacks the type", function()
    t.mock("mods.active", true)
    t.mock("mods.type_exists", false)
    t.expect(game.interop.get("compatguard").hook()).to_be(false)
    t.expect(t.logged("skipping")).to_be(true)
  end)

  t.it("hooks the other mod when it is there", function()
    t.mock("mods.active", true)
    t.mock("mods.type_exists", true)
    t.expect(game.interop.get("compatguard").hook()).to_be(true)
    t.expect(t.logged("hooked WorkTab.PriorityManager")).to_be(true)
  end)

  t.it("starts at zero calls", function()
    t.expect(game.interop.get("compatguard").seen()).to_be(0)
  end)
end)
