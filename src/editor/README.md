# RimKit VS Code extension

Publisher: **Team Stratware.win** (`stratware`)

RimKit mods are written in Luau. This extension gives the editor the RimKit API: completions, types, checks and one-key shipping.

## Features

- Completions and hover for the whole `game.<domain>` API.
- Luau types through the Luau Language Server (`JohnnyMorganz.luau-lsp`): `stubs/rimkit.d.luau` types every function, every `RimThing` and `RimPawn` member and every event payload. `game.events.on_pawn_damaged(function(pawn, e) ... end)` types both arguments from the function name.
- Command **RimKit: Set up Luau types** points luau-lsp at the RimKit definitions for the workspace. The extension offers it when it finds a `meta.lua`.
- Typed `rimkit.signal` and `rimkit.promise`, the built-in event and promise libraries.
- Checks for unknown `game.*` calls, with a did-you-mean suggestion.
- **Auto-ship** on save of `*.lua` or `meta.lua` (setting `rimkit.autoShipOnSave`).
- Commands: `RimKit: Ship Mod`, `Sync meta.lua`, `Run mod tests`, `Check mod content`, `Release check`, `Check or make preview and icon`, `Make a diagnostics zip`, `New mod from a template`.

Needs `rimkit` on PATH.

## Install (dev)

```powershell
cd src/editor
code --install-extension . --force
```

Or package a VSIX:

```powershell
cd src/editor
npx --yes @vscode/vsce package --no-dependencies
cursor --install-extension .\rimkit-<version>.vsix
```

## Example

```lua
-- Floating damage numbers above people.
game.events.on_pawn_damaged(function(pawn, e)
  game.effects.text(pawn, tostring(math.floor(e.dealt)), pawn.is_colonist and "red" or "orange")
end, { humanlike = true, min_dealt = 1 })

-- Change the result of a game method.
game.hooks.postfix("Verse.ShotReport", "HitFactorFromShooter", function(ctx)
  ctx:set_result(1)
end)
```

## Maintainers

`node tools/gen-luau-defs.js` regenerates the Luau definitions and the per-event stubs. `npm run package` runs the generators first.
