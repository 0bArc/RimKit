# Signal and Promise

Two small libraries are built into RimKit, so a mod needs no extra files for them. Both are Experimental in the [stability tiers](stability.md).

```lua
local Signal = rimkit.signal
local Promise = rimkit.promise
```

`rimkit` is a global, the same table as `require("rimkit")`. `require("rimkit.signal")` and `require("rimkit.promise")` return the same modules. The editor types `rimkit.signal` and `rimkit.promise` completely, so prefer that form.

## Signal

A signal is an event your own code owns: a colony alarm, a mod setting that changed, a job that finished. Connect functions to it and fire it.

```lua
local Signal = rimkit.signal

local alarm = Signal.new()
local connection = alarm:connect(function(reason)
  game.ui.message("Alarm: " .. reason)
end)

alarm:fire("raid incoming")   -- the message shows
connection:disconnect()
alarm:fire("raid incoming")   -- nothing happens
```

| Call | Does |
|------|------|
| `Signal.new()` | A new signal |
| `signal:connect(fn)` | Runs `fn` on every fire. Returns a connection |
| `signal:once(fn)` | Runs `fn` on the next fire only |
| `signal:fire(...)` | Calls every handler with the arguments |
| `signal:wait()` | Pauses the running coroutine until the next fire and returns what it fired with |
| `signal:connection_count()` | How many handlers are connected |
| `signal:disconnect_all()` | Disconnects every handler |
| `signal:destroy()` | Disconnects everything and ignores later fires |
| `connection:disconnect()` | Stops this handler. `connection.connected` is then false |

A handler that raises an error is logged and the other handlers still run. `wait` only works inside a coroutine: use `coroutine.wrap` or `Promise.async`.

## Promise

A promise stands for a value that is not there yet. It is pending, then resolved with a value, rejected with an error, or cancelled.

```lua
local Promise = rimkit.promise

-- One second later (60 ticks), without a timer callback in the middle of your code.
Promise.delay(60)
  :and_then(function() game.ui.message("one second") end)
  :catch(function(err) game.log.error(tostring(err)) end)
```

| Call | Does |
|------|------|
| `Promise.new(function(resolve, reject, on_cancel) ... end)` | Runs the function at once. An error it raises rejects the promise |
| `Promise.resolve(value)`, `Promise.reject(err)` | An already settled promise |
| `Promise.try(fn, ...)` | Runs `fn` and returns a promise of its result |
| `Promise.async(fn, ...)` | Runs `fn` in a coroutine, so it can call `await` and `signal:wait()` |
| `Promise.delay(ticks)` | Resolves after the given number of game ticks. Cancelling it stops the wait |
| `Promise.all(list)` | Resolves with every value, or rejects with the first error |
| `Promise.race(list)` | Settles like the first promise that settles |
| `promise:and_then(on_resolved, on_rejected)` | A new promise of what the handler returns. A promise returned from a handler is followed |
| `promise:catch(on_rejected)` | Handles an error |
| `promise:finally(fn)` | Runs `fn` however the promise ends, and passes the result on |
| `promise:await()` | Inside a coroutine: returns `true, value` or `false, error` |
| `promise:cancel()` | Cancels a pending promise and the promises waiting on it |
| `promise.status` | `"pending"`, `"resolved"`, `"rejected"` or `"cancelled"` |

Handlers run as soon as the promise settles, not on a later tick.

### Waiting in a straight line

`Promise.async` runs a function in a coroutine, so asynchronous steps read like normal code:

```lua
local Promise = rimkit.promise

Promise.async(function()
  game.ui.message("Starting")
  Promise.delay(120):await()          -- two seconds
  local ok, value = Promise.resolve(5):await()
  game.ui.message("Done: " .. tostring(value))
  return value
end):catch(function(err)
  game.log.error("failed: " .. tostring(err))
end)
```

### Signals and promises together

```lua
local Signal, Promise = rimkit.signal, rimkit.promise
local colonist_arrived = Signal.new()

Promise.async(function()
  local name = colonist_arrived:wait()   -- waits for the next fire
  game.ui.message(name .. " arrived")
end)

colonist_arrived:fire("Ana")
```

An error raised in a coroutine that nobody catches goes to `Promise.async`'s promise, so end a chain with `:catch` to see it in the log.
