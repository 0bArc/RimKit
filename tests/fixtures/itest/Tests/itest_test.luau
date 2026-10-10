-- The in-game test runner, driven here with a fake game clock: every frame of the real game becomes t.tick(1).
local t = game.test

local clock = 0
local function frame()
  clock += 1
  t.tick(1)
end

local function run_all(opts)
  local report
  opts = opts or {}
  opts.on_done = function(r) report = r end
  game.itest.run(opts)
  for _ = 1, 400 do
    if report then break end
    frame()
  end
  return report
end

local function status_of(report, name)
  for _, row in ipairs(report.tests) do
    if row.name == name then return row end
  end
end

describe("in-game test runner", function()
  before_each(function()
    pcall(game.itest.abort)
    clock = 0
    t.mock("find.tick", function() return clock end)
    t.mock("dev.mode", true)
    t.mock("time.paused", false)
    t.start()
    expect(game.itest.load("rimkit.fixture.itest")).to_be(1)
  end)

  it("loads the tests of a mod and lists the suites", function()
    local suites = game.itest.suites()
    expect(#suites).to_be(1)
    expect(suites[1].name).to_be("sample")
    expect(suites[1].mod).to_be("rimkit.fixture.itest")
    expect(suites[1].tests).to_be(9)
  end)

  it("runs every test and counts them", function()
    local report = run_all({ mods = { "rimkit.fixture.itest" } })
    expect(report ~= nil).to_be(true)
    expect(report.pass).to_be(2)
    expect(report.skip).to_be(2)
    expect(report.fail).to_be(5)
  end)

  it("passes a test that waits for ticks", function()
    local report = run_all({ filter = "waits for game ticks" })
    expect(report.pass).to_be(1)
    expect(report.fail).to_be(0)
    expect(status_of(report, "waits for game ticks").ticks >= 30).to_be(true)
  end)

  it("reports why a test failed", function()
    local report = run_all({ filter = "fails with a message" })
    local row = status_of(report, "fails with a message")
    expect(row.status).to_be("fail")
    expect(row.message).to_contain("expected")
  end)

  it("times out a wait with the message the test gave", function()
    local report = run_all({ filter = "never comes" })
    local row = status_of(report, "waits for a condition that never comes")
    expect(row.status).to_be("fail")
    expect(row.message).to_be("the colony never grew")
  end)

  it("turns an error into a failure", function()
    local report = run_all({ filter = "raises an error" })
    expect(status_of(report, "raises an error").message).to_contain("boom")
  end)

  it("skips on request", function()
    local report = run_all({ filter = "skips itself" })
    expect(status_of(report, "skips itself").status).to_be("skip")
    expect(status_of(report, "skips itself").message).to_be("no map")
  end)

  it("sees an event that fires while the test waits", function()
    local report
    game.itest.run({
      filter = "sees an event",
      on_done = function(r) report = r end,
    })
    frame()
    frame()
    t.emit("pawn.damaged", { dealt = 4 })
    for _ = 1, 20 do
      if report then break end
      frame()
    end
    expect(report.pass).to_be(1)
  end)

  it("runs the cleanup of a test that failed", function()
    run_all({ filter = "cleans up" })
    expect(t.logged("cleanup ran")).to_be(true)
  end)

  it("logs one RKTEST line per test and a summary", function()
    run_all({ mods = { "rimkit.fixture.itest" } })
    expect(t.logged("RKTEST START 9 tests")).to_be(true)
    expect(t.logged("RKTEST PASS sample > passes at once")).to_be(true)
    expect(t.logged("RKTEST FAIL sample > fails with a message")).to_be(true)
    expect(t.logged("RKTEST DONE pass=2 fail=5 skip=2")).to_be(true)
  end)

  it("writes the report when asked", function()
    run_all({ filter = "passes at once", report = "fixture.json" })
    local calls = t.calls("dev.write_report")
    expect(#calls).to_be(1)
    expect(calls[1].args.path).to_be("fixture.json")
    expect(calls[1].args.text).to_contain("\"pass\":1")
  end)

  it("closes the game when asked, with a failing exit code", function()
    run_all({ mods = { "rimkit.fixture.itest" }, quit = true })
    local calls = t.calls("dev.quit")
    expect(#calls).to_be(1)
    expect(calls[1].args.code).to_be(1)
  end)

  it("refuses a second run while one is going", function()
    game.itest.run({})
    expect(function() game.itest.run({}) end).to_error("RK3003")
    for _ = 1, 60 do frame() end
  end)

  it("needs Development mode to load tests", function()
    t.mock("dev.mode", false)
    expect(function() game.itest.load("rimkit.fixture.itest") end).to_error("RK4001")
  end)

  it("forgets the suites of a mod when it is reloaded", function()
    expect(#game.itest.suites()).to_be(1)
    t.reload()
    expect(#game.itest.suites()).to_be(0)
  end)
end)
