-- Run with: rimkit mod test src/examples/RimKitDemo
local t = game.test

t.describe("RimKit Demo", function()
  t.it("scales hunger by the setting", function()
    t.mock("config.get", "0.5")
    t.expect(t.tweak("pawn.hunger_rate")({ value = 0.2 })).to_be_close(0.1)
  end)

  t.it("keeps a silly setting inside a safe range", function()
    local hunger = t.tweak("pawn.hunger_rate")
    t.mock("config.get", "1000")
    t.expect(hunger({ value = 1 })).to_be_close(3.0)
    t.mock("config.get", "-5")
    t.expect(hunger({ value = 1 })).to_be_close(0.1)
  end)

  t.it("treats a missing setting as normal speed", function()
    t.mock("config.get", nil)
    t.expect(t.tweak("pawn.hunger_rate")({ value = 0.3 })).to_be_close(0.3)
  end)

  t.it("says who died", function()
    t.mock("pawn.name", "Ana")
    t.mock("ui.translate", "Ana has died.")
    t.emit("pawn.died", { pawn = 7 })
    t.expect(t.logged("Ana")).to_be(true)
  end)

  t.it("offers its formatter to other mods", function()
    t.expect(game.interop.has("rimkitdemo", "^1.0")).to_be(true)
    local api = game.interop.get("rimkitdemo")
    t.expect(api.best_skills({
      { def = "Shooting", label = "Shooting", level = 12 },
      { def = "Cooking", label = "Cooking", level = 4 },
      { def = "Melee", label = "Melee", level = 9 },
      { def = "Art", label = "Artistic", level = 7 },
    }, 3)).to_be("Shooting 12, Melee 9, Artistic 7")
  end)

  t.it("skips skills a pawn cannot do", function()
    local api = game.interop.get("rimkitdemo")
    t.expect(api.best_skills({ { def = "Melee", level = 15, disabled = true }, { def = "Cooking", level = 2 } }, 3)).to_be("Cooking 2")
  end)
end)
