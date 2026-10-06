# Luau

RimKit mods run on [Luau](https://luau.org), a typed dialect of Lua. It is Lua with types, a fast VM and a set of conveniences. Annotate a parameter and the editor completes it. A wrong field or a wrong argument type is an error before you start the game.

```lua
game.events.on_pawn_damaged(function(pawn, e)
  game.effects.text(pawn, tostring(math.floor(e.dealt)), pawn.is_colonist and "red" or "orange")
end, { humanlike = true, min_dealt = 1 })
```

The handler gets the pawn first and the whole payload second. Neither needs an annotation: the event is named in the function (`on_pawn_damaged`), so the editor knows the first argument is a `RimPawn` and the second a `PawnDamagedEvent` with `pawn`, `damage` and `dealt`. The string form `game.events.on("pawn.damaged", fn)` passes only the payload table, and you can annotate that yourself:

```lua
game.events.on("pawn.damaged", function(e: PawnDamagedEvent)
  print(e.pawn.name)
end)
```

Name your script files `.luau`. Files ending in `.lua` run the same way. Types are checked in the editor, not at runtime. The game compiles your file and ignores them.

## What you get

| Feature | Example |
|---------|---------|
| Types on parameters, returns and locals | `function(a: number, b: string?): boolean` |
| Type aliases and generics | `type Cell = { x: number, z: number }`, `local function first<T>(list: {T}): T?` |
| Casts | `local p = game.pawns.first() :: RimPawn` |
| Compound assignment | `hp += 5`, `name ..= "!"` |
| `continue` | `if skip then continue end` |
| If expressions | `local label = if hp > 50 then "ok" else "hurt"` |
| String interpolation | `` `{pawn.name} is down` `` |
| Iterate a table directly | `for key, value in settings do ... end` |
| Libraries | `table.find`, `table.clone`, `table.create`, `string.split`, `math.clamp`, `math.round`, `bit32`, `buffer`, `utf8` |

## What is different from standard Lua 5.4

- Numbers are doubles. There is no separate integer type, no `//` on integers and no bitwise operators. Use `math.floor(a / b)` and the `bit32` library.
- No `goto`, no `<const>` or `<close>` attributes, no `string.pack`, no `math.tointeger`.
- No `load`, `loadstring`, `dofile`, `getfenv` or `setfenv`. RimKit also removes `io`, `os` and `debug`, as before.
- `require("rimkit")` and `require("module")` for files under your mod's `Lua/` folder work.
- Userdata is not finalized by the garbage collector. This does not affect mods, only how RimKit's own objects are freed.

## Editor setup

1. Install the Luau Language Server extension (`JohnnyMorganz.luau-lsp`). RimKit offers to do it.
2. Run `RimKit: Set up Luau types` in VS Code. It points luau-lsp at `stubs/rimkit.d.luau` in the RimKit extension and turns the Lua language server (sumneko) down for the workspace.
3. Put `--!strict` on the first line of a file to get errors for wrong fields and argument types. Without it the editor still completes, but reports less.

The definition file is generated from the same sources as the reference, so every `game.*` function, every event payload and every `RimThing` and `RimPawn` member is typed.

## One function per event

`game.events.on_<event>` is the typed way to subscribe. The name is `on_` plus the event name with the dot written as an underscore: `pawn.damaged` is `on_pawn_damaged`, `thing.spawned` is `on_thing_spawned`. The handler receives the pawn (or the thing, when the event has no pawn) first and the payload second, and an event with neither passes just the payload. The second argument of `on_<event>` is an optional [filter](../api/events.md#filters). `game.events.on("pawn.damaged", fn)` still works and takes the same filter.
