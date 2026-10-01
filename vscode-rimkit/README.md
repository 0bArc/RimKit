# RimKit VS Code extension

Publisher: **Team Stratware.win** (`stratware`)

## Install (dev)

```powershell
cd vscode-rimkit
code --install-extension . --force
```

Or package VSIX:

```powershell
cd vscode-rimkit
npm install -g @vscode/vsce
npx vsce package --no-dependencies
code --install-extension rimkit-0.1.0.vsix
```

## Features

- Completions for `rim.prefix` / `rim.events` / `rim.pawn` / …
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
