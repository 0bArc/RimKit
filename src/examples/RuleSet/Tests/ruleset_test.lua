-- Run with: rimkit mod test src/examples/RuleSet
local t = game.test

t.describe("RuleSet", function()
  t.it("applies every rule once", function()
    t.mock("defs.set", "ok")
    local n = game.interop.get("ruleset").apply()
    t.expect(n).to_be(3)
    local calls = t.calls("defs.set")
    t.expect(#calls).to_be(3)
    t.expect(calls[1].args.def).to_be("Steel")
    t.expect(calls[1].args.path).to_be("stackLimit")
  end)

  t.it("undoes exactly the rules it applied", function()
    t.mock("defs.restore", 1)
    game.interop.get("ruleset").revert()
    local calls = t.calls("defs.restore")
    t.expect(#calls).to_be(3)
    t.expect(calls[2].args.def).to_be("Silver")
  end)

  t.it("applies on load when the option is on", function()
    t.mock("ui.translate", function(a) return a.key end)
    t.mock("config.get", "true")
    t.mock("defs.set", "ok")
    t.start()
    t.expect(#t.calls("defs.set")).to_be(3)
  end)

  t.it("leaves the game alone on load when the option is off", function()
    t.mock("config.get", "false")
    t.mock("defs.set", "ok")
    t.start()
    t.expect(#t.calls("defs.set")).to_be(0)
  end)

end)
