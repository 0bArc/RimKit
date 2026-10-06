-- Run with: rimkit mod test
-- Tests run against a mock host, so no game is needed. Mock what the game would answer, call your code, check the result.
local t = game.test

t.describe("ReflectProbe", function()
  t.it("tells the player when developer_reflect is off", function()
    t.mock("reflect.static_get", function() error("RK4002: developer_reflect is off") end)
    t.start()
    t.expect(t.logged("developer_reflect")).to_be(true)
  end)
end)
