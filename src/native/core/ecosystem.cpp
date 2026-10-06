// Ecosystem layer (Phase 6): per-mod profiling, declared capabilities, hot reload, mod interop and the headless test host.
// The Lua surface is game.profiler, game.dev, game.interop, game.test and game.mods.capabilities.
#include "engine.hpp"
#include "version_data.hpp"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <tuple>
#include <string>
#include <vector>

namespace rimlua {

namespace fs = std::filesystem;
namespace rj = rimlua::json;

namespace {

constexpr int kBudgetWindowTicks = 600;
const char* const kKindNames[6] = {"tick", "event", "hook", "timer", "ui", "load"};

std::string read_file(const fs::path& p) {
    std::ifstream f(p, std::ios::binary);
    if (!f) {
        return std::string();
    }
    std::stringstream ss;
    ss << f.rdbuf();
    return ss.str();
}

// Newest write time of any .lua file under a folder, in file clock ticks.
long long newest_lua_time(const std::string& dir) {
    long long newest = 0;
    std::error_code ec;
    for (auto it = fs::recursive_directory_iterator(dir, ec); !ec && it != fs::recursive_directory_iterator(); it.increment(ec)) {
        if (!it->is_regular_file(ec) || (it->path().extension() != ".lua" && it->path().extension() != ".luau")) {
            continue;
        }
        const long long t = static_cast<long long>(fs::last_write_time(it->path(), ec).time_since_epoch().count());
        newest = std::max(newest, t);
    }
    return newest;
}

// Which capability an op needs. Empty means no capability is needed.
std::string capability_for_op(const std::string& op) {
    if (op.rfind("reflect.", 0) == 0) {
        return "reflect";
    }
    if (op == "defs.write_xml" || op == "patch.write" || op == "defs.write_thing" || op == "defs.write_hediff" ||
        op == "defs.write_recipe" || op == "dev.export_defs" || op == "dev.bundle") {
        return "files";
    }
    return std::string();
}

// Lua parts, in chunks below the compiler's string literal limit.
const char* kInteropLua = R"LUA(
local game = game
local registry, waiting = {}, {}

local function parse(v)
  local a, b, c = tostring(v or ""):match("^(%d+)%.?(%d*)%.?(%d*)")
  if not a then return nil end
  return { tonumber(a), tonumber(b) or 0, tonumber(c) or 0 }
end

local function cmp(x, y)
  for i = 1, 3 do
    if x[i] ~= y[i] then return x[i] < y[i] and -1 or 1 end
  end
  return 0
end

-- Requirement forms: "1.2.0" (exact), ">=1.2", "^1.2" (same major, at least 1.2), "~1.2" (same major and minor, at least 1.2).
local function satisfies(have, req)
  local h = parse(have)
  if not h then return false end
  req = tostring(req or "")
  if req == "" or req == "*" then return true end
  local op, rest = req:match("^([%^~>=<]*)%s*(.+)$")
  local r = parse(rest)
  if not r then return false end
  if op == "" then return cmp(h, r) == 0 end
  if op == ">=" then return cmp(h, r) >= 0 end
  if op == ">" then return cmp(h, r) > 0 end
  if op == "<=" then return cmp(h, r) <= 0 end
  if op == "<" then return cmp(h, r) < 0 end
  if op == "^" then return h[1] == r[1] and cmp(h, r) >= 0 end
  if op == "~" then return h[1] == r[1] and h[2] == r[2] and cmp(h, r) >= 0 end
  return false
end

local interop = {}
interop.satisfies = satisfies

function interop.publish(name, version, api)
  if type(name) ~= "string" or name == "" then error("RK1001: interop.publish needs a name", 2) end
  if not parse(version) then error("RK1001: interop.publish needs a version like 1.2.0", 2) end
  if type(api) ~= "table" then error("RK1001: interop.publish needs a table of functions", 2) end
  local owner = game.dev.current_mod()
  local existing = registry[name]
  if existing and existing.owner ~= owner and existing.owner ~= "" then
    error("RK1001: interop name " .. name .. " is already published by " .. existing.owner, 2)
  end
  registry[name] = { owner = owner, version = version, api = api }
  for _, fn in ipairs(waiting[name] or {}) do pcall(fn, api, version) end
  return true
end

function interop.get(name, requirement)
  local e = registry[name]
  if not e then return nil, "not published" end
  if not satisfies(e.version, requirement) then
    return nil, "version " .. e.version .. " does not satisfy " .. tostring(requirement)
  end
  return e.api, e.version
end

function interop.has(name, requirement) return interop.get(name, requirement) ~= nil end

-- Calls fn when the API is published, now if it already is. Handles any load order.
function interop.when(name, requirement, fn)
  local api, version = interop.get(name, requirement)
  if api then fn(api, version) return end
  waiting[name] = waiting[name] or {}
  table.insert(waiting[name], function(a, v)
    if satisfies(v, requirement) then fn(a, v) end
  end)
end

-- Calls a published function and returns ok, result. A missing mod or function is a plain false, never an error.
function interop.call(name, fn, ...)
  local e = registry[name]
  if not e or type(e.api[fn]) ~= "function" then return false, "not available" end
  return pcall(e.api[fn], ...)
end

function interop.list()
  local out = {}
  for name, e in pairs(registry) do
    local fns = {}
    for k, v in pairs(e.api) do if type(v) == "function" then fns[#fns + 1] = k end end
    table.sort(fns)
    out[#out + 1] = { name = name, version = e.version, owner = e.owner, functions = fns }
  end
  table.sort(out, function(a, b) return a.name < b.name end)
  return out
end

function interop.unpublish_mod(mod)
  for name, e in pairs(registry) do if e.owner == mod then registry[name] = nil end end
end

game.interop = interop
)LUA";

const char* kDevLua = R"LUA(
local game = game
local dev = game.dev
local actions = {}

-- Debug actions: named functions a player or author can run from the dev tools window.
function dev.action(name, fn, description)
  if type(name) ~= "string" or type(fn) ~= "function" then error("RK1001: dev.action needs a name and a function", 2) end
  actions[name] = { name = name, fn = fn, mod = dev.current_mod(), description = description or "" }
  return true
end

function dev.actions()
  local out = {}
  for _, a in pairs(actions) do out[#out + 1] = { name = a.name, mod = a.mod, description = a.description } end
  table.sort(out, function(a, b) return a.name < b.name end)
  return out
end

function dev.run(name)
  local a = actions[name]
  if not a then error("RK3001: no debug action " .. tostring(name), 2) end
  local ok, err = pcall(a.fn)
  if not ok then return false, tostring(err) end
  return true
end

local function show(v, depth)
  depth = depth or 0
  local t = type(v)
  if t == "string" then return string.format("%q", v) end
  if t ~= "table" then return tostring(v) end
  if depth >= 3 then return "{...}" end
  local parts, n = {}, 0
  for k, x in pairs(v) do
    n = n + 1
    if n > 40 then parts[#parts + 1] = "..." break end
    local key = type(k) == "string" and k:match("^[%a_][%w_]*$") and k or "[" .. show(k, depth + 1) .. "]"
    parts[#parts + 1] = key .. " = " .. show(x, depth + 1)
  end
  return "{ " .. table.concat(parts, ", ") .. " }"
end
dev.show = show

-- Runs a Lua expression or statement and returns ok, text. Needs Development mode and a mod that may use it.
function dev.eval(code)
  local fn, err = __rk_compile("return " .. code)
  if not fn then fn, err = __rk_compile(code) end
  if not fn then return false, tostring(err) end
  local res = table.pack(pcall(fn))
  if not res[1] then return false, tostring(res[2]) end
  local out = {}
  for i = 2, res.n do out[#out + 1] = show(res[i]) end
  return true, table.concat(out, ", ")
end

-- Event recorder: keeps the last events with the tick they arrived.
local recorded, recording, subscribed = {}, false, {}
local max_recorded = 500

-- Records the events whose name contains filter (all events when there is no filter: that installs a patch for every event, so give a filter).
function dev.record_start(filter)
  recording = true
  for _, e in ipairs(game.events.list()) do
    local name = type(e) == "table" and e.name or e
    if not subscribed[name] and (not filter or tostring(name):find(filter, 1, true)) then
      subscribed[name] = true
      game.events.on(name, function(payload)
        if not recording then return end
        recorded[#recorded + 1] = { tick = game.tick(), name = name, payload = show(payload) }
        if #recorded > max_recorded then table.remove(recorded, 1) end
      end)
    end
  end
  return true
end

function dev.record_stop() recording = false return #recorded end
function dev.record_log() return recorded end
function dev.record_clear() recorded = {} return true end
function dev.recording() return recording end
)LUA";

const char* kTestLua = R"LUA(
local game = game
local T = { suites = {}, stats = { pass = 0, fail = 0 } }
local current

local function deep_equal(a, b, seen)
  if a == b then return true end
  if type(a) ~= "table" or type(b) ~= "table" then return false end
  for k, v in pairs(a) do if not deep_equal(v, b[k]) then return false end end
  for k in pairs(b) do if a[k] == nil then return false end end
  return true
end

local function show(v) return game.dev.show(v) end

local Expect = {}
Expect.__index = Expect
local function fail(msg) error({ rk_test = msg }, 0) end

function Expect:to_be(x) if self.v ~= x then fail("expected " .. show(x) .. " but got " .. show(self.v)) end end
function Expect:to_equal(x) if not deep_equal(self.v, x) then fail("expected " .. show(x) .. " but got " .. show(self.v)) end end
function Expect:to_be_nil() if self.v ~= nil then fail("expected nil but got " .. show(self.v)) end end
function Expect:to_be_truthy() if not self.v then fail("expected a truthy value but got " .. show(self.v)) end end
function Expect:to_be_falsy() if self.v then fail("expected a falsy value but got " .. show(self.v)) end end
function Expect:to_be_close(x, eps) if type(self.v) ~= "number" or math.abs(self.v - x) > (eps or 1e-6) then fail("expected about " .. x .. " but got " .. show(self.v)) end end
function Expect:to_contain(x)
  if type(self.v) == "string" then
    if not self.v:find(x, 1, true) then fail("expected " .. show(self.v) .. " to contain " .. show(x)) end
    return
  end
  for _, item in pairs(self.v) do if deep_equal(item, x) then return end end
  fail("expected " .. show(self.v) .. " to contain " .. show(x))
end
function Expect:to_have_length(n) if #self.v ~= n then fail("expected length " .. n .. " but got " .. #self.v) end end
function Expect:to_error(part)
  local ok, err = pcall(self.v)
  if ok then fail("expected an error but the call succeeded") end
  if part and not tostring(err):find(part, 1, true) then fail("expected an error containing " .. show(part) .. " but got " .. show(err)) end
end

-- Both expect(x):to_be(1) and expect(x).to_be(1) work.
function T.expect(v)
  local obj = { v = v }
  for name, fn in pairs(Expect) do
    if type(fn) == "function" then
      obj[name] = function(first, ...)
        if first == obj then return fn(obj, ...) end
        return fn(obj, first, ...)
      end
    end
  end
  return obj
end

function T.describe(name, body)
  local suite = { name = name, cases = {}, before = nil }
  T.suites[#T.suites + 1] = suite
  local prev = current
  current = suite
  body()
  current = prev
end

function T.it(name, fn)
  if not current then error("RK1001: game.test.it must be inside game.test.describe", 2) end
  current.cases[#current.cases + 1] = { name = name, fn = fn }
end

function T.before_each(fn) if current then current.before = fn end end

-- Mocks: replace what the host would answer for an op. A function gets the argument table, any other value is returned as is.
function T.mock(op, answer)
  if not __rk_op_known(op) then error("RK1001: t.mock(\"" .. tostring(op) .. "\"): the host has no op with that name. Check the spelling against the API reference.", 2) end
  __rk_mock_enable(true)
  if type(answer) == "function" then __rk_mock[op] = answer else __rk_mock[op] = function() return answer end end
end

function T.calls(op)
  local out = {}
  for _, c in ipairs(__rk_mock_calls) do if not op or c.op == op then out[#out + 1] = c end end
  return out
end

-- Log capture: everything a mod logs while a test runs, so a test can check what the player would have seen.
local logged = {}
local wrapped = false
local function capture()
  if wrapped then return end
  wrapped = true
  T.capture_definitions()
  for _, holder in ipairs({ log, game.log, rim }) do
    if type(holder) == "table" then
      for _, name in ipairs({ "info", "error", "log", "message" }) do
        local orig = holder[name]
        if type(orig) == "function" then
          holder[name] = function(msg, ...)
            logged[#logged + 1] = tostring(msg)
            return orig(msg, ...)
          end
        end
      end
    end
  end
  local ui = game.ui
  if type(ui) == "table" and type(ui.message) == "function" then
    local orig = ui.message
    ui.message = function(msg, ...)
      logged[#logged + 1] = tostring(msg)
      return orig(msg, ...)
    end
  end
end
-- Lua classes and tweaks a mod defined, so a test can call their functions directly: t.class("comp", "my_comp").tick(7)
local classes, tweaks = {}, {}
function T.capture_definitions()
  local c = game.classes
  if type(c) == "table" and type(c.define) == "function" then
    local orig = c.define
    c.define = function(family, name, methods)
      classes[family .. ":" .. name] = methods
      return orig(family, name, methods)
    end
  end
  local tw = game.tweaks
  if type(tw) == "table" and type(tw.on) == "function" then
    local orig = tw.on
    tw.on = function(name, fn, ...)
      tweaks[name] = fn
      return orig(name, fn, ...)
    end
  end
end
function T.class(family, name) return classes[family .. ":" .. name] end
function T.tweak(name) return tweaks[name] end
T.capture = capture
function T.logs() return logged end
function T.logged(part)
  for _, l in ipairs(logged) do if l:find(part, 1, true) then return true end end
  return false
end

function T.reset_mocks()
  for _, reg in ipairs(__rk_registered_events) do reg.calls = 0 end
  __rk_test_errors_reset()
  for i = #logged, 1, -1 do logged[i] = nil end
  for k in pairs(__rk_mock) do __rk_mock[k] = nil end
  for i = #__rk_mock_calls, 1, -1 do __rk_mock_calls[i] = nil end
end

function T.emit(name, payload) __rk_test_emit(name, payload or {}) end
function T.tick(n) for _ = 1, (n or 1) do __rk_test_tick() end end
function T.start() capture() __rk_test_start() end

function T.run()
  local pass, failed = 0, 0
  for _, suite in ipairs(T.suites) do
    for _, case in ipairs(suite.cases) do
      capture()
      T.reset_mocks()
      __rk_mock_enable(true)
      local ok, err = pcall(function()
        if suite.before then suite.before() end
        case.fn()
      end)
      local label = suite.name .. " > " .. case.name
      if ok then
        pass = pass + 1
        rim.log("TEST PASS " .. label)
      else
        failed = failed + 1
        local msg = type(err) == "table" and err.rk_test or tostring(err)
        rim.log("TEST FAIL " .. label .. " :: " .. msg)
      end
    end
  end
  T.reset_mocks()
  __rk_mock_enable(false)
  rim.log("TESTS pass=" .. pass .. " fail=" .. failed)
  return failed
end

T._deep_equal, T._show, T._fail = deep_equal, show, fail

game.test = T
)LUA";

const char* kTestSugarLua = R"LUA(
local T = game.test
local deep_equal, show, fail = T._deep_equal, T._show, T._fail

-- ---- A friendlier test API: mock objects, game.emit, spies, and plain describe, it and expect.
-- T.globals() puts these in the test files' scope: describe, it, before_each, expect, mock, spy, effects, and game.emit.
local objects, next_handle = {}, 1000

local function is_mock(v)
  return type(v) == "userdata" and objects[v.handle] ~= nil
end

local pawn_answers = {
  ["pawn.is_humanlike"] = function(o) return o.humanlike end,
  ["pawn.is_colonist"] = function(o) return o.colonist end,
  ["pawn.name"] = function(o) return o.name end,
  ["pawn.pos"] = function(o) return o.position and (o.position.x .. "," .. o.position.z) or "" end,
  ["pawn.map"] = function(o) return o.map end,
  ["thing.def"] = function(o) return o.def end,
  ["thing.label"] = function(o) return o.name end,
  ["thing.hp"] = function(o) return o.hp end,
  ["thing.max_hp"] = function(o) return o.max_hp end,
  ["thing.stack"] = function(o) return o.stack end,
  ["thing.pos"] = function(o) return o.position and (o.position.x .. "," .. o.position.z) or "" end,
  ["thing.map"] = function(o) return o.map end,
  ["thing.spawned"] = function(o) return true end,
  ["thing.info"] = function(o) return { id = o.handle, def = o.def, label = o.name, hp = o.hp, max_hp = o.max_hp, is_pawn = o.kind == "pawn", is_building = false, spawned = true } end,
}

-- A mock answers for every mock object by handle. It is reinstalled after each test resets the mocks.
local function install_answers()
  for op, answer in pairs(pawn_answers) do
    if __rk_mock[op] == nil then
      T.mock(op, function(args)
        local o = objects[args.h]
        if o then return answer(o) end
        return nil
      end)
    end
  end
end

local mock = {}

-- mock.pawn({ humanlike = true, colonist = false, position = { x = 10, z = 10 }, map = 1, name = "Mock" })
function mock.pawn(opts)
  opts = opts or {}
  next_handle += 1
  objects[next_handle] = {
    kind = "pawn",
    handle = next_handle,
    humanlike = opts.humanlike ~= false,
    colonist = opts.colonist == true,
    name = opts.name or "Mock",
    position = opts.position == nil and { x = 10, z = 10 } or opts.position,
    map = opts.map or 1,
    def = opts.def or "Human",
    hp = opts.hp or 100,
    max_hp = opts.max_hp or 100,
    stack = 1,
  }
  install_answers()
  return rim.wrap(next_handle)
end

-- mock.thing({ def = "Steel", hp = 100, stack = 75, position = { x = 5, z = 5 } })
function mock.thing(opts)
  opts = opts or {}
  next_handle += 1
  objects[next_handle] = {
    kind = "thing",
    handle = next_handle,
    name = opts.name or opts.def or "Thing",
    position = opts.position == nil and { x = 10, z = 10 } or opts.position,
    map = opts.map or 1,
    def = opts.def or "Steel",
    hp = opts.hp or 100,
    max_hp = opts.max_hp or 100,
    stack = opts.stack or 1,
  }
  install_answers()
  return rim.wrap_thing(next_handle)
end

local function ref(object)
  local o = objects[object.handle]
  return { ["$h"] = o.handle, ["$k"] = o.kind }
end

-- game.emit.pawn_damaged(pawn, 7.8) sends the event pawn.damaged with that pawn. A second argument that is a number fills the
-- number the event is about (dealt for damaged, amount for the rest), and a table adds fields: game.emit.pawn_damaged(pawn, { dealt = 7 }).
local numeric_field = { damaged = "dealt" }
local emit_api = setmetatable({}, {
  __index = function(_, key)
    local domain, name
    for _, multi in ipairs({ "mental_state", "world_object" }) do
      if key:sub(1, #multi + 1) == multi .. "_" then domain, name = multi, key:sub(#multi + 2) end
    end
    if not domain then domain, name = key:match("^([a-z]+)_(.+)$") end
    if not domain then error("RK1001: game.emit." .. key .. ": write it as <domain>_<event>, for example pawn_damaged", 2) end
    local event = domain .. "." .. name
    return function(subject, extra)
      local payload = {}
      if is_mock(subject) then
        payload[objects[subject.handle].kind] = ref(subject)
      elseif type(subject) == "table" then
        extra = subject
      end
      if type(extra) == "number" then extra = { [numeric_field[name] or "amount"] = extra } end
      if type(extra) == "table" then
        for k, v in pairs(extra) do payload[k] = is_mock(v) and ref(v) or v end
      end
      if name == "damaged" and payload.dealt and payload.damage == nil then
        payload.damage = { amount = payload.dealt, def = "Cut" }
      end
      __rk_test_emit(event, payload)
    end
  end,
})

-- Spies: spy.effects.text stands for the host op effects.text. expect(effects.text).to_have_been_called({ ... }) checks its calls.
local spy = setmetatable({}, {
  __index = function(_, domain)
    return setmetatable({}, { __index = function(_, name) return { __spy = true, op = domain .. "." .. name } end })
  end,
})

local function call_matches(call, match)
  for key, want in pairs(match) do
    if key == "target" then
      local o = is_mock(want) and objects[want.handle]
      if not o or not o.position or call.args.x ~= o.position.x or call.args.z ~= o.position.z then return false end
    else
      local got = call.args[key]
      if type(want) == "table" then
        if not deep_equal(got, want) then return false end
      elseif got ~= want and tostring(got) ~= tostring(want) then
        return false
      end
    end
  end
  return true
end

local function list_calls(calls)
  if #calls == 0 then return "no calls" end
  local rows = {}
  for _, c in ipairs(calls) do rows[#rows + 1] = show(c.args) end
  return table.concat(rows, "; ")
end

local SpyExpect = {}
function SpyExpect.to_have_been_called(self, match)
  local calls = T.calls(self.v.op)
  if #calls == 0 then fail("expected " .. self.v.op .. " to be called, but it was not") end
  if match == nil then return end
  for _, c in ipairs(calls) do if call_matches(c, match) then return end end
  fail("expected " .. self.v.op .. " to be called with " .. show(match) .. ", but it was called with: " .. list_calls(calls))
end
function SpyExpect.not_to_have_been_called(self, match)
  local calls = T.calls(self.v.op)
  if match == nil then
    if #calls > 0 then fail("expected " .. self.v.op .. " not to be called, but it was: " .. list_calls(calls)) end
    return
  end
  for _, c in ipairs(calls) do
    if call_matches(c, match) then fail("expected " .. self.v.op .. " not to be called with " .. show(match) .. ", but it was") end
  end
end
function SpyExpect.to_have_been_called_times(self, n)
  local calls = T.calls(self.v.op)
  if #calls ~= n then fail("expected " .. self.v.op .. " to be called " .. n .. " time(s), but it was called " .. #calls .. ": " .. list_calls(calls)) end
end

local function expect_any(v)
  if type(v) == "table" and v.__spy then
    local obj = { v = v }
    for name, fn in pairs(SpyExpect) do
      obj[name] = function(first, ...)
        if first == obj then return fn(obj, ...) end
        return fn(obj, first, ...)
      end
    end
    return obj
  end
  return T.expect(v)
end

function T.errors() return __rk_test_errors() end
function T.handler_calls(event)
  local n = 0
  for _, reg in ipairs(__rk_registered_events) do if reg.event == event then n += reg.calls end end
  return n
end

function T.globals()
  local g = _G
  g.describe, g.it, g.before_each = T.describe, T.it, T.before_each
  g.expect, g.mock, g.spy, g.effects = expect_any, mock, spy, spy.effects
  game.emit = emit_api
end

-- ---- Generating tests from what a mod registers.
local SUBJECT = {
  pawn = "pawn", hediff = "pawn", job = "pawn", mental_state = "pawn", skill = "pawn", thought = "pawn", relation = "pawn",
  interaction = "pawn", inspiration = "pawn", ability = "pawn", bill = "pawn", plant = "pawn", recipe = "pawn", construction = "pawn",
  royalty = "pawn", ideo = "pawn", thing = "thing", explosion = "thing", power = "thing", designation = "thing", zone = "thing",
}
local MULTI = { "mental_state", "world_object" }

local function split_event(event)
  for _, multi in ipairs(MULTI) do
    if event:sub(1, #multi + 1) == multi .. "." then return multi, event:sub(#multi + 2) end
  end
  return event:match("^([a-z_]+)%.(.+)$")
end

local function literal(v)
  if type(v) == "string" then return string.format("%q", v) end
  return tostring(v)
end

local function table_text(t, order)
  local keys = {}
  for k in pairs(t) do keys[#keys + 1] = k end
  table.sort(keys)
  if #keys == 0 then return "" end
  local parts = {}
  for _, k in ipairs(keys) do parts[#parts + 1] = k .. " = " .. literal(t[k]) end
  return "{ " .. table.concat(parts, ", ") .. " }"
end

-- Plans one event registration: the subject options and extra fields that satisfy the filter, and what breaks each key.
local function plan(reg)
  local filter = reg.filter or {}
  local ok_subject, ok_extra = {}, {}
  local breaks = {}
  local keys = {}
  for k in pairs(filter) do keys[#keys + 1] = k end
  table.sort(keys)
  for _, k in ipairs(keys) do
    local v = filter[k]
    if k == "humanlike" or k == "colonist" then
      ok_subject[k] = v
      breaks[#breaks + 1] = { why = "a pawn that is " .. (v and "not " or "") .. k, subject = { [k] = not v }, extra = {} }
    elseif k == "def" then
      local first = type(v) == "table" and v[1] or v
      ok_subject.def = first
      breaks[#breaks + 1] = { why = "a different def", subject = { def = "SomethingElse" }, extra = {} }
    elseif k == "min_dealt" then
      ok_extra.dealt = v + 1
      breaks[#breaks + 1] = { why = "damage below " .. v, subject = {}, extra = { dealt = math.max(0, v - 1) } }
    elseif k ~= "pawn" and type(v) ~= "table" then
      ok_extra[k] = v
      local other = type(v) == "number" and v + 1 or type(v) == "boolean" and not v or "other"
      breaks[#breaks + 1] = { why = k .. " other than " .. tostring(v), subject = {}, extra = { [k] = other } }
    end
  end
  return ok_subject, ok_extra, breaks
end

function T.generate(mod)
  local out = {}
  local function add(s) out[#out + 1] = s end
  add("-- Generated by rimkit mod gen-tests from what the mod registers. Edit it, or delete it and generate again.")
  add("-- Run with: rimkit mod test")
  add("")
  add("describe(" .. string.format("%q", mod) .. ", function()")
  add("  it(\"loads without errors\", function()")
  add("    game.test.start()")
  add("    expect(game.test.errors()).to_be(0)")
  add("  end)")
  local per_event, seen = {}, {}
  for _, reg in ipairs(__rk_registered_events) do per_event[reg.event] = (per_event[reg.event] or 0) + 1 end
  for _, reg in ipairs(__rk_registered_events) do
    local domain, name = split_event(reg.event)
    if domain then
      local ok_subject, ok_extra, breaks = plan(reg)
      local key = reg.event .. table_text(reg.filter or {})
      if not seen[key] then
        seen[key] = true
        local kind = SUBJECT[domain]
        local emit_call = "game.emit." .. domain .. "_" .. name
        local function body(subject_opts, extra)
          if kind then
            add("    local subject = mock." .. kind .. "(" .. table_text(subject_opts) .. ")")
            local extra_text = table_text(extra)
            add("    " .. emit_call .. "(subject" .. (extra_text ~= "" and (", " .. extra_text) or "") .. ")")
          else
            add("    " .. emit_call .. "(" .. table_text(extra) .. ")")
          end
        end
        add("")
        add("  it(" .. string.format("%q", reg.event .. ": handles a matching event") .. ", function()")
        body(ok_subject, ok_extra)
        add("    expect(game.test.errors()).to_be(0)")
        add("    expect(game.test.handler_calls(" .. string.format("%q", reg.event) .. ") >= 1).to_be(true)")
        add("  end)")
        if per_event[reg.event] == 1 then
          for _, b in ipairs(breaks) do
            local subject_opts, extra = {}, {}
            for k, v in pairs(ok_subject) do subject_opts[k] = v end
            for k, v in pairs(ok_extra) do extra[k] = v end
            for k, v in pairs(b.subject) do subject_opts[k] = v end
            for k, v in pairs(b.extra) do extra[k] = v end
            add("")
            add("  it(" .. string.format("%q", reg.event .. ": ignores " .. b.why) .. ", function()")
            body(subject_opts, extra)
            add("    expect(game.test.handler_calls(" .. string.format("%q", reg.event) .. ")).to_be(0)")
            add("  end)")
          end
        end
      end
    end
  end
  add("end)")
  return table.concat(out, string.char(10)) .. string.char(10)
end

-- Writes the generated file one line at a time through the log, for the command line tool to pick up.
function T.print_generated(mod)
  for line in T.generate(mod):gmatch("([^\n]*)\n") do rim.log("RKGEN:" .. line) end
end

)LUA";

const char* kSaveLua = R"LUA(
local game = game
local save = game.save
if type(save) ~= "table" then return end

-- Runs the steps after the data version recorded in the save, in order. A failing step stops the run and keeps the
-- version of the last step that worked, so the next load tries again. steps is a list of functions: steps[2] upgrades 1 to 2.
function save.migrate(package_id, steps)
  local v = save.version(package_id)
  for i = v + 1, #steps do
    local ok, err = pcall(steps[i])
    if not ok then error("RK5002: migration " .. i .. " of " .. package_id .. " failed: " .. tostring(err), 2) end
    save.set_version(package_id, i)
    v = i
  end
  return v
end

-- Records the mod version that last wrote this save and returns the previous one (nil for a new save).
function save.stamp(package_id, version)
  version = version or game.mods.version(package_id)
  local previous = save.fetch(package_id, "game", "_mod_version", nil, nil)
  if previous ~= version then save.put(package_id, "game", "_mod_version", version) end
  return previous
end

-- Removes everything a mod stored in the game and world scopes, for a clean my save button before uninstalling.
function save.purge(package_id)
  local n = 0
  for _, scope in ipairs({ "game", "world" }) do
    for _, key in ipairs(save.keys(package_id, scope)) do
      save.remove(package_id, scope, key)
      n = n + 1
    end
  end
  return n
end
)LUA";

}  // namespace

// ---- profiling

void Engine::prof_add(const std::string& mod, int kind, double us) {
    if (kind < 0 || kind > 5) {
        return;
    }
    ModProf& p = mod_prof_[mod];
    p.us[kind] += us;
    p.calls[kind] += 1;
}

// ---- mod info and capabilities

void Engine::load_mod_info(const std::string& package_id, const std::string& lua_dir) {
    if (package_id.empty()) {
        return;
    }
    ModInfo& info = mods_[package_id];
    if (std::find(info.lua_dirs.begin(), info.lua_dirs.end(), lua_dir) == info.lua_dirs.end()) {
        info.lua_dirs.push_back(lua_dir);
    }
    if (info.lua_dir.empty()) {
        info.lua_dir = lua_dir;
        info.root = fs::path(lua_dir).parent_path().string();
    }
    info.newest_mtime = 0;
    for (const std::string& d : info.lua_dirs) {
        info.newest_mtime = std::max(info.newest_mtime, newest_lua_time(d));
    }
    if (info.lua_dir != lua_dir) {
        return;
    }
    const std::string text = read_file(fs::path(info.root) / "About" / "RimKit.json");
    rj::Value root;
    if (text.empty() || !rj::parse(text, root) || !root.is_object()) {
        return;
    }
    info.name = root.get_string("name", info.name);
    info.version = root.get_string("version", info.version);
    info.budget_us = static_cast<double>(root.get_int("perf_budget_us", static_cast<long long>(info.budget_us)));
    info.api_level = static_cast<int>(root.get_int("api_level", 0));
    if (info.api_level >= 1) {
        info.declared = true;  // api level 1: no declaration means no capabilities
    }
    const rj::Value* caps = root.find("capabilities");
    if (caps && caps->is_array()) {
        info.declared = true;
        info.caps.clear();
        for (const rj::Value& c : caps->items) {
            if (c.is_string()) {
                info.caps.insert(c.s);
            }
        }
    }
}

void Engine::require_capability(const std::string& cap, const std::string& what) {
    if (current_mod_.empty()) {
        return;
    }
    auto it = mods_.find(current_mod_);
    if (it == mods_.end() || !it->second.declared || it->second.caps.count(cap)) {
        return;
    }
    throw sol::error("RK4001: mod " + current_mod_ + " did not declare the capability '" + cap + "' that " + what +
                     " needs. Add it to meta.capabilities and run rimkit mod sync.");
}

void Engine::check_capability_for_op(const std::string& op) {
    const std::string cap = capability_for_op(op);
    if (!cap.empty()) {
        require_capability(cap, op);
    }
}

// ---- mock host (tests only)

bool Engine::try_mock(const std::string& op, const sol::table& args, sol::object& out) {
    check_capability_for_op(op);
    sol::state& L = *lua_;
    sol::object calls_obj = L["__rk_mock_calls"];
    if (calls_obj.is<sol::table>()) {
        sol::table calls = calls_obj.as<sol::table>();
        sol::table row = L.create_table();
        row["op"] = op;
        row["args"] = args;
        calls[calls.size() + 1] = row;
    }
    sol::object mocks_obj = L["__rk_mock"];
    if (mocks_obj.is<sol::table>()) {
        sol::object fn = mocks_obj.as<sol::table>()[op];
        if (fn.is<sol::protected_function>()) {
            sol::protected_function pf = fn.as<sol::protected_function>();
            sol::protected_function_result r = pf(args);
            if (!r.valid()) {
                sol::error e = r;
                throw sol::error(e.what());
            }
            out = r.return_count() > 0 ? r.get<sol::object>(0) : sol::make_object(L, sol::lua_nil);
            return true;
        }
    }
    out = sol::make_object(L, sol::lua_nil);
    return true;
}

// ---- reload and watch

int Engine::reload_mod(const std::string& package_id) {
    auto it = mods_.find(package_id);
    if (it == mods_.end() || it->second.lua_dir.empty() || in_reload_) {
        return 1;
    }
    in_reload_ = true;
    {
        // The old version's gizmos, alerts, tabs, columns, tools, status lines and settings pages go first; the new version adds its own.
        sol::table a = lua_->create_table();
        a["package_id"] = package_id;
        host_call("dev.unregister_mod", a);
    }
    const std::vector<std::string> lua_dirs = it->second.lua_dirs;
    auto mine = [&](const ModFn& f) { return f.mod == package_id; };
    on_load_.erase(std::remove_if(on_load_.begin(), on_load_.end(), mine), on_load_.end());
    on_tick_.erase(std::remove_if(on_tick_.begin(), on_tick_.end(), mine), on_tick_.end());
    timers_.erase(std::remove_if(timers_.begin(), timers_.end(), [&](const TimerEntry& t) { return t.mod == package_id; }), timers_.end());
    for (auto& kv : event_handlers_) {
        kv.second.erase(std::remove_if(kv.second.begin(), kv.second.end(), mine), kv.second.end());
    }
    std::vector<int> hook_ids;
    for (const auto& kv : hooks_) {
        if (kv.second.mod == package_id) {
            hook_ids.push_back(kv.first);
        }
    }
    for (int id : hook_ids) {
        unregister_hook_lua(id);
    }
    std::vector<int> ui_ids;
    for (const auto& kv : ui_callback_mod_) {
        if (kv.second == package_id) {
            ui_ids.push_back(kv.first);
        }
    }
    for (int id : ui_ids) {
        ui_callbacks_.erase(id);
        ui_callback_mod_.erase(id);
    }
    for (size_t i = map_float_menu_mods_.size(); i > 0; --i) {
        if (map_float_menu_mods_[i - 1] == package_id && i - 1 < map_float_menu_handlers_.size()) {
            map_float_menu_handlers_.erase(map_float_menu_handlers_.begin() + static_cast<long>(i - 1));
            map_float_menu_mods_.erase(map_float_menu_mods_.begin() + static_cast<long>(i - 1));
        }
    }

    // Forget cached modules from this mod so require reads the new files.
    sol::table loaded = (*lua_)["package"]["loaded"];
    std::vector<std::string> stale;
    std::error_code ec;
    for (const std::string& lua_dir : lua_dirs)
    for (auto fit = fs::recursive_directory_iterator(lua_dir, ec); !ec && fit != fs::recursive_directory_iterator(); fit.increment(ec)) {
        if (!fit->is_regular_file(ec) || (fit->path().extension() != ".lua" && fit->path().extension() != ".luau")) {
            continue;
        }
        std::string rel = fs::relative(fit->path(), lua_dir, ec).generic_string();
        rel = rel.substr(0, rel.size() - fit->path().extension().string().size());
        for (char& c : rel) {
            if (c == '/') {
                c = '.';
            }
        }
        stale.push_back(rel);
    }
    for (const std::string& name : stale) {
        loaded[name] = sol::lua_nil;
    }

    reset_mod_budget(package_id);
    mod_prof_[package_id].over_budget = false;
    const size_t first_new = on_load_.size();
    const std::string prev = swap_mod(package_id);
    int rc = 0;
    for (const std::string& lua_dir : lua_dirs) {
        if (load_directory(lua_dir) != 0) {
            rc = 2;
        }
    }
    for (size_t i = first_new; i < on_load_.size(); ++i) {
        ModScope scope(*this, on_load_[i].mod, PROF_LOAD);
        sol::protected_function_result r = on_load_[i].fn();
        if (!r.valid()) {
            log_lua_error("[RimKit] on_load (reload)", r);
            record_mod_error(package_id, "on_load");
        }
    }
    swap_mod(prev);
    in_reload_ = false;
    if (callbacks_.log) {
        const std::string msg = "[RimKit] reloaded " + package_id + (rc == 0 ? "" : " with errors");
        callbacks_.log(msg.c_str());
    }
    return rc;
}

// Runs every tick: the file watcher once a second and the per-mod budget once per window.
void Engine::watch_tick() {
    const int tick = current_tick();
    if (dev_watch_ && ++watch_counter_ >= 60) {
        watch_counter_ = 0;
        std::vector<std::string> changed;
        for (auto& kv : mods_) {
            long long now = 0;
            for (const std::string& d : kv.second.lua_dirs) {
                now = std::max(now, newest_lua_time(d));
            }
            if (kv.second.newest_mtime != 0 && now > kv.second.newest_mtime) {
                changed.push_back(kv.first);
            }
        }
        for (const std::string& mod : changed) {
            reload_mod(mod);
        }
    }
    for (auto& kv : mod_prof_) {
        ModProf& p = kv.second;
        if (tick - p.window_base_tick < kBudgetWindowTicks) {
            continue;
        }
        double total = 0;
        for (int k = 0; k < 5; ++k) {
            total += p.us[k];
        }
        const int span = std::max(1, tick - p.window_base_tick);
        p.avg_tick_us = (total - p.window_base_us) / span;
        p.window_base_us = total;
        p.window_base_tick = tick;
        auto mit = mods_.find(kv.first);
        const double budget = mit != mods_.end() ? mit->second.budget_us : 300.0;
        const bool over = !kv.first.empty() && p.avg_tick_us > budget;
        if (over && !p.over_budget && callbacks_.log) {
            const std::string msg = "[RimKit] mod " + kv.first + " uses " + std::to_string(static_cast<int>(p.avg_tick_us)) +
                                    " us per tick on average, over its budget of " + std::to_string(static_cast<int>(budget)) +
                                    " us. See game.profiler.report().";
            callbacks_.log(msg.c_str());
            emit_event_ex("mod.over_budget", "{\"mod\":\"" + kv.first + "\",\"avg_us\":" + std::to_string(p.avg_tick_us) + "}");
        }
        p.over_budget = over;
    }
}

// ---- Lua surface

void Engine::bind_ecosystem() {
    sol::state& L = *lua_;
    sol::table game = L["game"];

    L["__rk_mock"] = L.create_table();
    L["__rk_mock_calls"] = L.create_table();
    L["__rk_registered_events"] = L.create_table();
    L["__rk_test_errors"] = [this]() { return test_errors_; };
    L["__rk_test_errors_reset"] = [this]() { test_errors_ = 0; };
    auto need_test_mode = [this]() {
        if (!test_mode_) {
            throw sol::error("RK4001: game.test only works under rimkit mod test. In the game it would fake events and answers, so it refuses.");
        }
    };
    L["__rk_op_known"] = [this](const std::string& op) { return is_known_op(op); };
    L["__rk_mock_enable"] = [this, need_test_mode](bool on) {
        need_test_mode();
        mock_active_ = on;
    };
    L["__rk_test_emit"] = [this, need_test_mode](const std::string& name, const sol::object& payload) {
        need_test_mode();
        emit_event_ex(name, lua_value_json(payload));
    };
    L["__rk_test_tick"] = [this, need_test_mode]() {
        need_test_mode();
        call_on_tick();
    };
    L["__rk_test_start"] = [this, need_test_mode]() {
        need_test_mode();
        call_on_load();
    };

    // The only way to compile a string. Gated: Development mode, and the RimKit dev tools or a mod that declared "dev".
    L["__rk_compile"] = [this](const std::string& code, sol::this_state ts) -> std::tuple<sol::object, sol::object> {
        sol::state_view sv(ts);
        if (!test_mode_) {
            const bool allowed = current_mod_.empty() || current_mod_ == "stratware.rimkit" ||
                                 (mods_.count(current_mod_) && mods_[current_mod_].caps.count("dev"));
            if (!allowed) {
                throw sol::error("RK4001: mod " + current_mod_ + " did not declare the capability 'dev' that evaluating code needs.");
            }
            sol::object mode = host_call("dev.mode", sv.create_table());
            if (!(mode.is<bool>() && mode.as<bool>())) {
                throw sol::error("RK4001: evaluating code needs Development mode. Turn it on in the game options.");
            }
        }
        sol::load_result lr = sv.load(code, "=console");
        if (!lr.valid()) {
            sol::error e = lr;
            return std::make_tuple(sol::make_object(sv, sol::lua_nil), sol::make_object(sv, std::string(e.what())));
        }
        sol::protected_function pf = lr;
        return std::make_tuple(sol::make_object(sv, pf), sol::make_object(sv, sol::lua_nil));
    };

    sol::table profiler = L.create_table();
    profiler["report"] = [this]() {
        sol::state& S = *lua_;
        std::vector<std::pair<double, std::string>> order;
        for (const auto& kv : mod_prof_) {
            double total = 0;
            for (int k = 0; k < 6; ++k) {
                total += kv.second.us[k];
            }
            order.emplace_back(total, kv.first);
        }
        std::sort(order.begin(), order.end(), [](const auto& a, const auto& b) { return a.first > b.first; });
        sol::table out = S.create_table();
        int n = 1;
        for (const auto& o : order) {
            const ModProf& p = mod_prof_[o.second];
            sol::table row = S.create_table();
            row["mod"] = o.second.empty() ? "(unattributed)" : o.second;
            for (int k = 0; k < 6; ++k) {
                row[std::string(kKindNames[k]) + "_us"] = p.us[k];
                row[std::string(kKindNames[k]) + "_calls"] = p.calls[k];
            }
            row["total_us"] = o.first;
            row["avg_tick_us"] = p.avg_tick_us;
            auto mit = mods_.find(o.second);
            row["budget_us"] = mit != mods_.end() ? mit->second.budget_us : 300.0;
            row["over_budget"] = p.over_budget;
            out[n++] = row;
        }
        return out;
    };
    profiler["reset"] = [this]() { mod_prof_.clear(); };
    game["profiler"] = profiler;

    sol::object dev_obj = game["dev"];
    sol::table dev = dev_obj.is<sol::table>() ? dev_obj.as<sol::table>() : L.create_table();
    dev["current_mod"] = [this]() { return current_mod_; };
    dev["watch"] = [this](bool on) {
        dev_watch_ = on;
        return on;
    };
    dev["reload"] = [this](const std::string& package_id) {
        require_capability("dev", "game.dev.reload");
        const int rc = reload_mod(package_id);
        if (rc == 1) {
            throw sol::error("RK3001: mod " + package_id + " has no Lua folder loaded");
        }
        return rc == 0;
    };
    game["dev"] = dev;

    sol::object mods_obj = game["mods"];
    sol::table mods = mods_obj.is<sol::table>() ? mods_obj.as<sol::table>() : L.create_table();
    mods["version"] = [this](const std::string& package_id) -> std::string {
        auto mit = mods_.find(package_id);
        return mit == mods_.end() ? std::string() : mit->second.version;
    };
    mods["capabilities"] = [this]() {
        sol::state& S = *lua_;
        sol::table out = S.create_table();
        int n = 1;
        for (const auto& kv : mods_) {
            sol::table row = S.create_table();
            row["mod"] = kv.first;
            row["name"] = kv.second.name;
            row["version"] = kv.second.version;
            row["declared"] = kv.second.declared;
            row["budget_us"] = kv.second.budget_us;
            sol::table caps = S.create_table();
            int c = 1;
            for (const std::string& cap : kv.second.caps) {
                caps[c++] = cap;
            }
            row["capabilities"] = caps;
            auto pit = mod_prof_.find(kv.first);
            row["disabled"] = !mod_enabled(kv.first);
            row["over_budget"] = pit != mod_prof_.end() && pit->second.over_budget;
            out[n++] = row;
        }
        return out;
    };
    game["mods"] = mods;

    sol::protected_function_result r1 = L.safe_script(kInteropLua, sol::script_pass_on_error);
    sol::protected_function_result r2 = L.safe_script(kDevLua, sol::script_pass_on_error);
    sol::protected_function_result r3 = L.safe_script(kTestLua, sol::script_pass_on_error);
    if (r3.valid()) {
        r3 = L.safe_script(kTestSugarLua, sol::script_pass_on_error);
    }
    sol::protected_function_result r4 = L.safe_script(kSaveLua, sol::script_pass_on_error);
    for (sol::protected_function_result* r : {&r1, &r2, &r3, &r4}) {
        if (!r->valid()) {
            log_lua_error("[RimKit] ecosystem bootstrap", *r);
        }
    }
}

}  // namespace rimlua
