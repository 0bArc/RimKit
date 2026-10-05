# Typed reflection (game.reflect)

Advanced tier. `game.reflect` reads and writes fields, calls methods and builds objects in Verse, RimWorld and UnityEngine with real Lua values. It is the escape hatch that makes the whole game reachable while domain kits grow. Prefer a named kit op when one exists. See [API stability](stability.md) and the [RimKit Standard](../standard/rks.md).

It is off by default. Turn on the RimKit setting `developer_reflect` (mod options) to use it. Every use is audited.

```lua
local R = game.reflect

local tm   = R.static_get("Verse.Find", "TickManager")      -- RimObject
local now  = R.get(tm, "TicksGame")                         -- number
R.set(tm, "CurTimeSpeed", "Fast")                           -- enum by name

local map    = game.current_map()
local center = R.get(map, "Center")                         -- { x=, y=, z= }
local steel  = R.static_call("Verse.ThingMaker", "MakeThing", "Steel")   -- RimThing
R.static_call("Verse.GenSpawn", "Spawn", steel, center, map)

local cell = R.new("Verse.IntVec3", 3, 0, 4)
for _, m in ipairs(R.members("Verse.TickManager")) do print(m.name, m.kind) end
```

## Functions

| Function | Purpose |
|----------|---------|
| `get(obj, member)`, `set(obj, member, value)` | Instance field or property |
| `static_get(type, member)`, `static_set(type, member, value)` | Static field or property |
| `call(obj, method, ...)`, `call_sig(obj, method, sig, ...)` | Instance method |
| `static_call(type, method, ...)`, `static_call_sig(type, method, sig, ...)` | Static method |
| `new(type, ...)` | Construct an object or struct |
| `members(objOrType)` | List fields, properties, methods and constructors |
| `type(obj)`, `is_a(obj, type)` | Type name and type test |
| `enum_names(type)` | Names of an enum |
| `release(obj)` | Drop the handle so it stops resolving |
| `audit([n])` | Last n audit lines |

`obj` is a wrapped value (`RimPawn`, `RimThing`, `RimMap`, `RimFaction`, `RimObject`) or its handle.

## Values

Arguments are converted to the declared parameter type and results come back as Lua values.

| Game value | Lua value |
|------------|-----------|
| bool, numbers, string | same |
| enum | name string. Accepts a name or a number when passing in. |
| `Def` | `defName` string. Accepts a name when passing in. |
| `IntVec3`, `Vector3`, `Vector2` | table `{x=, y=, z=}` |
| `DamageInfo` | table with `def`, `amount`, `angle`, `instigator`, `weapon`, `hit_part` |
| `Pawn`, `Thing`, `Map`, `Faction` | `RimPawn`, `RimThing`, `RimMap`, `RimFaction` |
| other reference types | `RimObject` with `.handle` and `.type_name` |
| lists and arrays | Lua array (first 128 items) |
| `null` | `nil` |

Methods that have `out` parameters return a table `{ result = ..., out = { name = value } }`.

## Overloads

When several overloads share a name, the first one whose parameters accept your arguments is used, preferring the fewest unused optional parameters. Pass an explicit signature with `call_sig` or `static_call_sig` to choose, for example `R.static_call_sig("Verse.GenText", "CapitalizeFirst", {"string"}, "hello")`. Optional parameters may be left out.

## Errors

A failed call raises a Lua error whose message starts with an RK code.

| Code | Meaning |
|------|---------|
| RK1001 | Bad argument (missing, wrong type, not JSON) |
| RK2001 | Handle is stale or null |
| RK3001 | Type or member not found |
| RK3002 | No overload accepts the arguments. The message lists the candidates. |
| RK4001 | Blocked by policy |
| RK4002 | `developer_reflect` is off |
| RK5001 | The game method threw. The message carries the exception. |

## Policy

- Only types in `Verse`, `RimWorld` and `UnityEngine` (plus generic collections of them) are reachable. `System.*`, `HarmonyLib` and RimKit internals are not.
- Blocked: process, file, network, assembly, `Type` and delegate access, `UnityEngine.Application`, XML savers, mod loading, and members whose names suggest opening files, URLs or quitting the game.
- A method can never return a `Type`, `MemberInfo`, `Assembly` or delegate.
- Every call is recorded with an ISO 8601 UTC timestamp. Denied calls are written to the game log. Set `rimkit.audit_verbose` to log allowed calls too. Hook registration is audited the same way.

## Migration

The previous `rim.reflect` and `rim.cs` tables keep working unchanged (they now call the host ops `reflect_v1.*`). Their string arguments and integer results are kept for compatibility. New code should use `game.reflect`. The host op names `reflect.*` now belong to the typed API, so `rim.invoke("reflect.get", ...)` uses the new argument shape.
