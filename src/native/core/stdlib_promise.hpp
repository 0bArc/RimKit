#pragma once

// require("rimkit.promise"): a value that arrives later. Built into the core.

namespace rimlua {

inline const char* const kPromiseSource = R"LUA(
-- rimkit.promise
-- A promise stands for a value that is not there yet. It is pending, then resolved with a value, rejected with an error,
-- or cancelled.
--
--   local Promise = require("rimkit.promise")
--   Promise.delay(60)
--     :and_then(function() print("one second later") end)
--     :catch(function(err) print("failed: " .. tostring(err)) end)
--
-- Handlers run as soon as the promise settles. Inside Promise.async a function can wait with promise:await().

local Promise = {}
Promise.__index = Promise

local PENDING, RESOLVED, REJECTED, CANCELLED = "pending", "resolved", "rejected", "cancelled"

local function is_promise(value)
  return type(value) == "table" and getmetatable(value) == Promise
end

local function new_pending()
  return setmetatable({ status = PENDING, _value = nil, _entries = {}, _on_cancel = {}, _parent = nil }, Promise)
end

local resolve_with, reject_with, run_entry

-- Settles the promise and runs what was waiting on it.
local function settle(self, status, value)
  if self.status ~= PENDING then
    return
  end
  self.status = status
  self._value = value
  local entries = self._entries
  self._entries = {}
  local hooks = self._on_cancel
  self._on_cancel = {}
  if status == CANCELLED then
    for _, hook in ipairs(hooks) do
      pcall(hook)
    end
  end
  for _, entry in ipairs(entries) do
    run_entry(self, entry)
  end
end

-- Resolves a promise. A promise given as the value is followed: this one settles the way that one does.
function resolve_with(self, value)
  if self.status ~= PENDING then
    return
  end
  if is_promise(value) then
    value:_listen({
      ok = function(v)
        resolve_with(self, v)
      end,
      fail = function(e)
        reject_with(self, e)
      end,
      cancelled = function()
        self:cancel()
      end,
    })
    return
  end
  settle(self, RESOLVED, value)
end

function reject_with(self, err)
  settle(self, REJECTED, err)
end

-- entry.child is the promise made by and_then. The other fields are plain callbacks used inside this file.
function run_entry(parent, entry)
  local status, value = parent.status, parent._value
  if entry.child == nil then
    if status == RESOLVED then
      entry.ok(value)
    elseif status == REJECTED then
      entry.fail(value)
    else
      entry.cancelled()
    end
    return
  end
  local child = entry.child
  if status == CANCELLED then
    child:cancel()
    return
  end
  local handler = (status == RESOLVED) and entry.on_resolved or entry.on_rejected
  if handler == nil then
    if status == RESOLVED then
      resolve_with(child, value)
    else
      reject_with(child, value)
    end
    return
  end
  local ok, result = pcall(handler, value)
  if ok then
    resolve_with(child, result)
  else
    reject_with(child, result)
  end
end

function Promise:_listen(entry)
  if self.status == PENDING then
    table.insert(self._entries, entry)
  else
    run_entry(self, entry)
  end
end

-- Promise.new(function(resolve, reject, on_cancel) ... end)
-- The function runs at once. An error it raises rejects the promise. on_cancel(fn) runs fn if the promise is cancelled.
function Promise.new(executor)
  assert(type(executor) == "function", "Promise.new needs a function")
  local self = new_pending()
  local function resolve(value)
    resolve_with(self, value)
  end
  local function reject(err)
    reject_with(self, err)
  end
  local function on_cancel(fn)
    if self.status == CANCELLED then
      pcall(fn)
    elseif self.status == PENDING then
      table.insert(self._on_cancel, fn)
    end
  end
  local ok, err = pcall(executor, resolve, reject, on_cancel)
  if not ok then
    reject(err)
  end
  return self
end

function Promise.is(value)
  return is_promise(value)
end

-- A promise that is already resolved. A promise passed in is returned as it is.
function Promise.resolve(value)
  if is_promise(value) then
    return value
  end
  local self = new_pending()
  settle(self, RESOLVED, value)
  return self
end

function Promise.reject(err)
  local self = new_pending()
  settle(self, REJECTED, err)
  return self
end

-- Runs fn(...) and returns a promise of its result. An error rejects it.
function Promise.try(fn, ...)
  local args = table.pack(...)
  return Promise.new(function(resolve)
    resolve(fn(table.unpack(args, 1, args.n)))
  end)
end

-- Runs fn(...) in a coroutine, so it can use promise:await() and signal:wait(). Returns a promise of its result.
function Promise.async(fn, ...)
  local args = table.pack(...)
  return Promise.new(function(resolve, reject)
    local thread = coroutine.create(function()
      local ok, result = pcall(fn, table.unpack(args, 1, args.n))
      if ok then
        resolve(result)
      else
        reject(result)
      end
    end)
    local ok, err = coroutine.resume(thread)
    if not ok then
      reject(err)
    end
  end)
end

-- Resolves after the given number of game ticks (60 ticks is one second at normal speed). Cancelling it stops the wait.
function Promise.delay(ticks)
  return Promise.new(function(resolve, _, on_cancel)
    local cancelled = false
    on_cancel(function()
      cancelled = true
    end)
    game.timer.after(ticks, function()
      if not cancelled then
        resolve(ticks)
      end
    end)
  end)
end

-- Resolves with a list of every value once all promises resolve, or rejects with the first error.
function Promise.all(list)
  return Promise.new(function(resolve, reject, on_cancel)
    local total = #list
    if total == 0 then
      resolve({})
      return
    end
    local values, done = {}, 0
    for i, item in ipairs(list) do
      Promise.resolve(item):_listen({
        ok = function(v)
          values[i] = v
          done += 1
          if done == total then
            resolve(values)
          end
        end,
        fail = reject,
        cancelled = function() end,
      })
    end
    on_cancel(function()
      for _, item in ipairs(list) do
        if is_promise(item) then
          item:cancel()
        end
      end
    end)
  end)
end

-- Settles the way the first promise to settle does.
function Promise.race(list)
  return Promise.new(function(resolve, reject)
    for _, item in ipairs(list) do
      Promise.resolve(item):_listen({ ok = resolve, fail = reject, cancelled = function() end })
    end
  end)
end

-- promise:and_then(on_resolved, on_rejected) returns a new promise of what the handler returns.
-- Leave a handler out (nil) to pass the value or the error on.
function Promise:and_then(on_resolved, on_rejected)
  local child = new_pending()
  child._parent = self
  self:_listen({ child = child, on_resolved = on_resolved, on_rejected = on_rejected })
  return child
end

function Promise:catch(on_rejected)
  return self:and_then(nil, on_rejected)
end

-- Runs fn when the promise resolves, rejects or is cancelled, and passes the result on.
function Promise:finally(fn)
  local child = new_pending()
  child._parent = self
  self:_listen({
    ok = function(v)
      local ok, err = pcall(fn)
      if ok then
        resolve_with(child, v)
      else
        reject_with(child, err)
      end
    end,
    fail = function(e)
      local ok, err = pcall(fn)
      reject_with(child, ok and e or err)
    end,
    cancelled = function()
      pcall(fn)
      child:cancel()
    end,
  })
  return child
end

-- Pauses the running coroutine until the promise settles. Returns true and the value, or false and the error.
-- Only works inside a coroutine, for example in Promise.async.
function Promise:await()
  if self.status == RESOLVED then
    return true, self._value
  elseif self.status == REJECTED then
    return false, self._value
  elseif self.status == CANCELLED then
    return false, "cancelled"
  end
  assert(coroutine.isyieldable(), "promise:await only works inside a coroutine, for example in Promise.async")
  local thread = coroutine.running()
  self:_listen({
    ok = function(v)
      coroutine.resume(thread, true, v)
    end,
    fail = function(e)
      coroutine.resume(thread, false, e)
    end,
    cancelled = function()
      coroutine.resume(thread, false, "cancelled")
    end,
  })
  return coroutine.yield()
end

-- Cancels a pending promise and the promises waiting on it. A promise that has settled stays as it is.
function Promise:cancel()
  if self.status ~= PENDING then
    return
  end
  settle(self, CANCELLED, nil)
end

function Promise:get_status()
  return self.status
end

return Promise
)LUA";

}  // namespace rimlua
