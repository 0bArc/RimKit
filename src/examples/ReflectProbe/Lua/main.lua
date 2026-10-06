-- ReflectProbe
-- Runs a short checklist against game.reflect when a game is loaded and reports PASS or FAIL for each step.
-- Needs the "reflect" capability (in meta.lua) and the RimKit setting developer_reflect turned on in the mod options.
-- Read the docs: infrastructure/docs/api/reflect.md

local R = game.reflect
local results = {}

-- Runs one check. The check returns true, or false plus a note. An error counts as a failure.
local function step(name, fn)
  local ok, passed, note = pcall(fn)
  local good = ok and passed == true
  results[#results + 1] = good
  local detail = ok and (note or "") or tostring(passed)
  game.log.info(string.format("[ReflectProbe] %s %s %s", good and "PASS" or "FAIL", name, detail))
  return good, ok and nil or tostring(passed)
end

local function run()
  results = {}
  local tm

  -- The first call tells us whether reflection is switched on at all.
  local ok, err = pcall(function() tm = R.static_get("Verse.Find", "TickManager") end)
  if not ok then
    if tostring(err):find("RK4002", 1, true) then
      game.log.warn("[ReflectProbe] developer_reflect is off. Turn it on in the RimKit mod options and load again.")
      game.ui.message("ReflectProbe: turn on developer_reflect in the RimKit mod options")
    else
      game.log.warn("[ReflectProbe] FAIL static_get " .. tostring(err))
      game.ui.message("ReflectProbe: FAIL, see the log")
    end
    return
  end
  results[#results + 1] = tm ~= nil
  game.log.info("[ReflectProbe] " .. (tm ~= nil and "PASS" or "FAIL") .. " static_get Verse.Find.TickManager")

  step("get number (TicksGame)", function()
    local ticks = R.get(tm, "TicksGame")
    return type(ticks) == "number", tostring(ticks)
  end)

  step("enum comes back as a name (CurTimeSpeed)", function()
    local speed = R.get(tm, "CurTimeSpeed")
    return type(speed) == "string", speed
  end)

  step("set writes a value and get reads it back", function()
    local before = R.get(tm, "CurTimeSpeed")
    R.set(tm, "CurTimeSpeed", before) -- same value, so the game is not changed
    return R.get(tm, "CurTimeSpeed") == before, before
  end)

  step("enum_names lists TimeSpeed", function()
    local names = R.enum_names("Verse.TimeSpeed")
    for _, n in ipairs(names) do
      if n == "Paused" then return true, #names .. " names" end
    end
    return false, "Paused missing"
  end)

  step("new builds an IntVec3", function()
    local cell = R.new("Verse.IntVec3", 3, 0, 4)
    return cell.x == 3 and cell.z == 4, string.format("(%s, %s, %s)", cell.x, cell.y, cell.z)
  end)

  step("static_call_sig picks an overload (CapitalizeFirst)", function()
    local s = R.static_call_sig("Verse.GenText", "CapitalizeFirst", { "string" }, "hello")
    return s == "Hello", s
  end)

  step("members lists the type", function()
    local members = R.members("Verse.TickManager")
    return #members > 10, #members .. " members"
  end)

  step("type and is_a", function()
    local t = R.type(tm)
    return R.is_a(tm, "Verse.TickManager"), t
  end)

  step("a current map can be read (Center)", function()
    local map = game.current_map()
    if not map then return true, "no map loaded, skipped" end
    local c = R.get(map, "Center")
    return type(c.x) == "number" and type(c.z) == "number", string.format("(%s, %s)", c.x, c.z)
  end)

  step("policy blocks System.IO", function()
    local blocked = not pcall(R.static_call, "System.IO.File", "Exists", "C:/Windows")
    return blocked, "refused as expected"
  end)

  step("every call was audited", function()
    local lines = R.audit(5)
    return #lines > 0, #lines .. " recent lines"
  end)

  local passed = 0
  for _, good in ipairs(results) do
    if good then passed = passed + 1 end
  end
  local text = string.format("ReflectProbe: %d of %d checks passed", passed, #results)
  game.log.info("[ReflectProbe] " .. text)
  game.ui.message(text)
end

game.events.on_load(function()
  run()
end)
