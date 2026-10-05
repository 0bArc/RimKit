# RimKit VS Code extension

Publisher: **Team Stratware.win** (`stratware`)

Version: **0.5.0**

Features:

- Completions for the canonical `game.<domain>` API and the older `rim.*` snippets.
- Deprecation warnings for old names, with a quick fix and "replace all in this file". The data comes from `aliases.json`, the same table the runtime and `rimkit migrate` use.
- Command **RimKit: Migrate deprecated API names** (runs `rimkit migrate` on the mod root, dry run first).
- Lua API stubs for the Lua language server (`stubs/`). The extension offers to add them to `Lua.workspace.library` for a RimKit mod workspace.
- Auto-ship on save.

Maintainers: `npm run gen` regenerates `stubs/rimkit_unc.lua` and marks old names `@deprecated` in `stubs/rimkit.lua`. `npm run package` runs it first.

## Install (dev)

```powershell
cd src/editor
code --install-extension . --force
```

Or package VSIX:

```powershell
cd src/editor
npx --yes @vscode/vsce package --no-dependencies
cursor --install-extension .\rimkit-0.10.0.vsix
```

## Features

- Completions for `rim.*`, `anomaly.*`, `ui.on_map_float_menu`, Strong API tables
- EmmyLua stubs in `stubs/rimkit.lua`
- **Auto-ship** on save of `*.lua` or `meta.lua` (setting `rimkit.autoShipOnSave`)
- Commands: `RimKit: Ship Mod`, `RimKit: Sync meta.lua`

Needs `rimkit` on PATH.

## New hook syntax

```lua
rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(pawn)
  return true
end

rim.events.prefix["RimWorld.JobGiver_GetFood.TryGiveJob"] = function(pawn)
  return true
end
```

## Anomaly float menu

```lua
ui.on_map_float_menu(function(ctx)
  if anomaly.is_entity(ctx.clicked) ~= true then return nil end
  return { { label = "Recruit", on_click = function() anomaly.recruit(ctx.clicked) end } }
end)
```
