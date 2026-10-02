# RimKit VS Code extension

Publisher: **Team Stratware.win** (`stratware`)

Version: **0.2.1** (bound API first; `rim.invoke` escape = `api.list` / `reflect.*` only).

## Install (dev)

```powershell
cd vscode-rimkit
code --install-extension . --force
```

Or package VSIX:

```powershell
cd vscode-rimkit
npx --yes @vscode/vsce package --no-dependencies
cursor --install-extension .\rimkit-0.2.1.vsix
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
