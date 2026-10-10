-- Hot reload: running a mod's Lua again must leave exactly one copy of everything it registers.
local t = game.test

local function count(part)
  local n = 0
  for _, line in ipairs(t.logs()) do
    if line:find(part, 1, true) then n += 1 end
  end
  return n
end

describe("hot reload", function()
  it("runs on_load again", function()
    t.start()
    expect(count("fixture loaded")).to_be(1)
    expect(t.reload()).to_be(true)
    expect(count("fixture loaded")).to_be(2)
  end)

  it("keeps one handler per event after many reloads", function()
    t.start()
    for _ = 1, 4 do expect(t.reload()).to_be(true) end
    t.reset_mocks()

    local pawn = mock.pawn({ colonist = true })
    game.emit.pawn_damaged(pawn, 3)

    expect(count("damaged handler ran")).to_be(1)
  end)

  it("releases the host patch behind an event instead of counting it up", function()
    t.start()
    local before = t.subscriptions("pawn.damaged")
    for _ = 1, 5 do t.reload() end
    expect(t.subscriptions("pawn.damaged")).to_be(before)
    expect(t.subscriptions("thing.destroyed")).to_be(before)
  end)

  it("keeps exactly the mod's own handlers", function()
    t.start()
    for _ = 1, 3 do t.reload() end
    local mod = "rimkit.fixture.reload"
    expect(game.events.count("pawn.damaged", mod)).to_be(1)
    expect(game.events.count("thing.destroyed", mod)).to_be(1)
  end)

  it("events.off only removes the handlers of the mod that asks", function()
    t.start()
    game.events.on("pawn.damaged", function() game.log.info("test handler ran") end)
    expect(game.events.count("pawn.damaged")).to_be(2)
    game.events.off("pawn.damaged")
    expect(game.events.count("pawn.damaged", "rimkit.fixture.reload")).to_be(1)
    expect(game.events.count("pawn.damaged")).to_be(1)
  end)

  it("keeps one tick handler", function()
    t.start()
    t.reload()
    t.reload()
    t.reset_mocks()
    t.tick(1)
    expect(count("tick handler ran")).to_be(1)
  end)

  it("keeps one timer", function()
    local now = 0
    t.mock("find.tick", function() return now end)
    t.start()
    t.reload()
    t.reload()
    t.reset_mocks()
    t.mock("find.tick", function() return now end)
    now = 10
    t.tick(1)
    expect(count("timer ran")).to_be(1)
  end)

  it("a timer can start the next timer", function()
    local now = 0
    t.mock("find.tick", function() return now end)
    local runs = 0
    local function again()
      runs += 1
      game.timer.after(1, again)
    end
    game.timer.after(1, again)
    for step = 1, 40 do
      now = step * 2
      t.tick(1)
    end
    expect(runs).to_be(40)
  end)

  it("asks the host to take away what the old version added", function()
    t.start()
    t.reload()
    local calls = t.calls("dev.unregister_mod")
    expect(#calls).to_be(1)
    expect(calls[1].args.package_id).to_be("rimkit.fixture.reload")
  end)

  it("keeps one debug action and one published api", function()
    t.start()
    t.reload()
    t.reload()
    local found = 0
    for _, a in ipairs(game.dev.actions()) do
      if a.name == "fixture action" then found += 1 end
    end
    expect(found).to_be(1)
    expect(game.interop.has("rimkit.fixture.reload", "^1")).to_be(true)
  end)

  it("a reload asked for by an event handler finishes the handler first", function()
    t.start()
    game.events.on("faction.relation_changed", function()
      game.dev.reload("rimkit.fixture.reload")
      game.log.info("after reload call")
    end)
    t.emit("faction.relation_changed", {})
    expect(count("after reload call")).to_be(1)
    expect(count("fixture loaded")).to_be(2)
  end)

  it("a reload asked for by a tick handler waits for the next tick", function()
    t.start()
    local asked = false
    game.events.on_tick(function()
      if not asked then
        asked = true
        game.dev.reload("rimkit.fixture.reload")
        game.log.info("after tick reload call")
      end
    end)
    t.tick(1)
    expect(count("after tick reload call")).to_be(1)
    expect(count("fixture loaded")).to_be(1)
    t.tick(1)
    expect(count("fixture loaded")).to_be(2)
  end)

  it("has game.log.warn", function()
    t.start()
    game.log.warn("careful")
    expect(t.logged("careful")).to_be(true)
  end)

  it("rejects a mod that has no Lua loaded", function()
    expect(function() game.dev.reload("no.such.mod") end).to_error("RK3001")
  end)
end)
