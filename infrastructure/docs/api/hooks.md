# Hooks

Direct Harmony access: run Lua before or after any game method, change its arguments or result, skip it, catch its exceptions, or replace one call inside it. Tier: **Advanced**. See [stability](stability.md).

Prefer an [event](events.md) when one exists, and a [kit](overview.md) function when you only want to read or set a value. Use a hook when you need to change how the game itself behaves, for example the hit chance in the example.

## Finding what to hook

The game's methods, their parameter names and the best targets are listed in the [game inventory](../concepts/game-inventory.md). You can also browse any type with `game.reflect.members("Verse.Pawn")` (needs the reflect setting).

## Writing a hook

```lua
-- Short forms. Return the hook id (0 on failure).
game.hooks.prefix(typeName, methodName, fn [, opts])
game.hooks.postfix(typeName, methodName, fn [, opts])
game.hooks.finalizer(typeName, methodName, fn [, opts])
game.hooks.remove(hookId)
game.hooks.list()                       -- array of { id, kind, type, method }

-- Combined form. One patch with any mix of prefix, postfix and finalizer.
local ids = game.hooks.patch{
  type = "Verse.Thing", method = "TakeDamage", sig = { "Verse.DamageInfo" },
  priority = 600, before = { "other.mod.id" }, after = {},
  prefix = function(ctx) end, postfix = function(ctx) end, finalizer = function(ctx) end,
}
-- ids.prefix, ids.postfix, ids.finalizer

-- Assignment sugar: a prefix is "before", a postfix is "after". No options unless you assign a table.
game.hooks.before["RimWorld.JobGiver_GetFood"].TryGiveJob = function(ctx) end
game.hooks.after["Verse.Thing"].Destroy = { fn = function(ctx) end, sig = { "Verse.DestroyMode" } }
```

### Options

| Option | Meaning |
|--------|---------|
| `sig` | Array of parameter type names that selects one overload, for example `{ "Verse.DamageInfo" }`. Use `int`, `float`, `bool`, `string` for primitives. Append `&` for ref or out parameters. |
| `priority` | Harmony priority. Higher runs first. Default 400. |
| `before`, `after` | Harmony ids of other mods to run before or after. |

Without `sig`, a method name that has several overloads is rejected and the log lists every signature, so you can pick one. A property name such as `"CurTimeSpeed"` selects its getter. `".ctor"` selects a constructor.

Several Lua hooks on one method share a single Harmony patch. They run by priority, highest first, then by registration order. Removing the last hook removes the patch.

### Context

Every callback receives a context table.

| Field | Meaning |
|-------|---------|
| `method` | Full name of the patched method |
| `phase` | `prefix`, `postfix`, `finalizer` or `replace_call` |
| `pawn` | Primary `RimPawn` (instance, else first pawn argument) |
| `instance` | `__instance` as a wrapped value. For static methods it is the primary pawn or `nil`. |
| `args` | Array of arguments, 1 based |
| `named` | The same arguments keyed by parameter name |
| `arg_names`, `arg_modes` | Parameter names, and `in`, `ref` or `out` per parameter |
| `has_result`, `result` | The method return value. In a prefix it is the default value. In a postfix it is the real result. Absent for `void` methods. |
| `exception` | Finalizer only. Text of the exception, or `nil` |
| `state` | Value stored by `ctx:set_state` earlier in the same call by a hook from the same `game.hooks.patch` (prefix, postfix and finalizer share one slot) |

Pawn properties are also available on the context when a primary pawn exists (`ctx.is_colonist`).

### Changing behaviour

Call these with a colon.

| Call | Effect |
|------|--------|
| `ctx:set_result(v)` | Sets the return value. In a prefix this also skips the original. |
| `ctx:skip([v])` | Prefix only. Skips the original, optionally with a return value. |
| `ctx:set_arg(i, v)` | Replaces argument `i` before the original runs. Writes ref and out arguments in a postfix. |
| `ctx:set_state(v)` | Keeps a value for the later phases of the same call. The slot is shared by the hooks of one `game.hooks.patch` call. Hooks registered separately do not share state. |
| `ctx:suppress()` | Finalizer only. Swallows the exception. |

`return false` from a prefix still skips the original (compatibility form).

Values you pass are converted to the declared type of the parameter or result: numbers to any numeric type, names to enums and defs (`"Shooting"` to `SkillDef`), `{x=, y=, z=}` to `IntVec3`, wrapped objects back to the game object, arrays to lists. A value that does not fit is refused, a warning is logged and the game value is kept.

A skipped prefix on a method that returns a value type returns the default value unless you set a result.

```lua
game.hooks.patch{
  type = "Verse.Thing", method = "TakeDamage", sig = { "Verse.DamageInfo" },
  prefix = function(ctx)
    local t = ctx.instance
    if t.def == "Wall" then ctx:skip() end   -- walls take no damage
  end,
}

game.hooks.postfix("RimWorld.StatWorker", "GetValueUnfinalized", function(ctx)
  ctx:set_result(ctx.result * 1.1)
end, { sig = { "RimWorld.StatRequest", "bool" } })
```

### Call replacement

`replace_call` swaps one call inside a method body for a Lua function. It is a restricted, safe form of a transpiler.

```lua
game.hooks.replace_call{
  type = "RimWorld.Pawn_Foo", method = "Bar",       -- method whose body is rewritten
  sig = {},                                          -- optional overload of that method
  call = "Verse.Rand.Range", call_sig = { "float", "float" },
  nth = 1,                                           -- replace only the first match, 0 or nil for all
  fn = function(ctx)
    return ctx.args[1]                               -- value used instead of the call
  end,
}
```

The function returns `nil` to run the original call as written. It returns a value to use instead. For a call that returns nothing, any non-nil return skips the call. Instance calls receive the target as `ctx.instance`. Ref and out parameters and instance calls on structs are not supported. The Lua function runs on whatever thread runs the patched method.

### Safety

- Hooks are Advanced. A hook that skips originals or writes results can break saves. Keep filters narrow and re-test after game updates.
- A hook that triggers itself is cut off after 8 nested levels.
- A Lua error in a hook is logged and the game continues with the original behaviour.
- Harmony stays a workshop dependency. Do not ship it inside your mod.
