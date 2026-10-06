#pragma once

// require("rimkit.signal"): a small event library for a mod's own code. Built into the core, so a mod needs no files for it.

namespace rimlua {

inline const char* const kSignalSource = R"LUA(
-- rimkit.signal
-- A signal is an event you own. Connect functions to it, fire it, wait for it.
--
--   local Signal = require("rimkit.signal")
--   local died = Signal.new()
--   local conn = died:connect(function(name) print(name .. " died") end)
--   died:fire("Ana")
--   conn:disconnect()
--
-- A handler that raises an error is logged and the other handlers still run.

local Connection = {}
Connection.__index = Connection

local Signal = {}
Signal.__index = Signal

local function report(err)
  local g = game
  if g and g.log and g.log.error then
    g.log.error("[signal] handler error: " .. tostring(err))
  end
end

-- A connection: connected is true until disconnect() is called.
function Connection:disconnect()
  if not self.connected then
    return
  end
  self.connected = false
  local signal = self._signal
  self._signal = nil
  if signal then
    local handlers = signal._handlers
    local at = table.find(handlers, self)
    if at then
      table.remove(handlers, at)
    end
  end
end

function Connection:is_connected()
  return self.connected
end

function Signal.new()
  return setmetatable({ _handlers = {}, _waiting = {}, _destroyed = false }, Signal)
end

function Signal.is(value)
  return type(value) == "table" and getmetatable(value) == Signal
end

-- Runs fn every time the signal fires. Returns a connection.
function Signal:connect(fn)
  assert(type(fn) == "function", "signal:connect needs a function")
  assert(not self._destroyed, "signal:connect on a destroyed signal")
  local connection = setmetatable({ connected = true, _fn = fn, _signal = self, _once = false }, Connection)
  table.insert(self._handlers, connection)
  return connection
end

-- Runs fn the next time the signal fires, then disconnects.
function Signal:once(fn)
  local connection = Signal.connect(self, fn)
  connection._once = true
  return connection
end

-- Calls every connected handler with the arguments, then resumes the coroutines that are waiting.
function Signal:fire(...)
  if self._destroyed then
    return
  end
  local snapshot = table.clone(self._handlers)
  for _, connection in ipairs(snapshot) do
    if connection.connected then
      if connection._once then
        connection:disconnect()
      end
      local ok, err = pcall(connection._fn, ...)
      if not ok then
        report(err)
      end
    end
  end
  if #self._waiting > 0 then
    local waiting = self._waiting
    self._waiting = {}
    for _, thread in ipairs(waiting) do
      local ok, err = coroutine.resume(thread, ...)
      if not ok then
        report(err)
      end
    end
  end
end

-- Pauses the running coroutine until the signal fires, and returns the arguments it fired with.
-- Only works inside a coroutine (coroutine.wrap, or promise.async).
function Signal:wait()
  assert(coroutine.isyieldable(), "signal:wait only works inside a coroutine, for example in promise.async")
  table.insert(self._waiting, coroutine.running())
  return coroutine.yield()
end

function Signal:connection_count()
  return #self._handlers
end

function Signal:disconnect_all()
  local handlers = self._handlers
  self._handlers = {}
  for _, connection in ipairs(handlers) do
    connection.connected = false
    connection._signal = nil
  end
end

-- Disconnects everything. A destroyed signal ignores fire and refuses connect.
function Signal:destroy()
  self:disconnect_all()
  self._waiting = {}
  self._destroyed = true
end

return Signal
)LUA";

}  // namespace rimlua
