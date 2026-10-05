# RimKit

[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen?style=for-the-badge&logo=steam&logoColor=white)](https://rimworldgame.com/)
[![Lua](https://img.shields.io/badge/Lua-5.4-blue?style=for-the-badge&logo=lua&logoColor=white)](https://www.lua.org/)
[![Harmony](https://img.shields.io/badge/Harmony-required-orange?style=for-the-badge)](https://github.com/pardeike/HarmonyRimWorld)
[![C++](https://img.shields.io/badge/core-C%2B%2B-00599C?style=for-the-badge&logo=cplusplus&logoColor=white)](https://isocpp.org/)
[![License](https://img.shields.io/badge/license-Apache%202.0-lightgrey?style=for-the-badge)](LICENSE)

**Write RimWorld mods in Lua.** Script behavior, react to events and patch game methods without a C# project for every change.

`stratware.rimkit` · Team Stratware.win

---

## What it looks like

```lua
-- Colonists never lose accuracy with distance.
game.hooks.postfix("Verse.ShotReport", "HitFactorFromShooter", function(ctx)
  ctx:set_result(1)
end)

-- Say something when a pawn dies.
game.events.on("pawn.died", function(e)
  game.ui.message(e.pawn.name .. " died")
end)

-- Edit a pawn without knowing RimWorld internals.
game.pawns.set_passion(pawn, "Shooting", "Major")
game.pawns.add_trait(pawn, "Beauty", 2)
```

How it fits together: Lua, a C++ core (Lua 5.4 and sol2) with a sandbox, a thin C# host, then Harmony and RimWorld. See [architecture](infrastructure/docs/concepts/architecture.md).

---

## Quick start

```powershell
# build the kit
rimkit build

# install it and a sample into RimWorld Mods
.\bin\rimkit.exe mod ship .
.\bin\rimkit.exe mod ship .\src\examples\more_speed
```

`rimkit mod ship .` from the repo root ships the `mod/` package. Load order: **Harmony, then RimKit, then your mod.** Close RimWorld before shipping if `mod\Native\rimlua_core.dll` is locked.

Make your own:

```powershell
rimkit mod create MyMod
rimkit mod ship MyMod
```

Full walkthrough: [quickstart](infrastructure/docs/guide/quickstart.md).

---

## What you can do

| Layer | Use it for |
|-------|-----------|
| Kits (`game.pawns`, `game.anomaly`, `game.things`, ...) | The normal, documented API |
| Events (`events.on("pawn.died", fn)`) | Reacting to what happens. 28 named events with payloads |
| Hooks (`game.hooks.*`) | Changing how a game method behaves: prefix, postfix, finalizer, call replacement |
| Reflection (`game.reflect.*`) | Reading or calling anything in Verse, RimWorld and UnityEngine. Off by default, audited |

Plus settings, saved data, key bindings, Lua jobs, windows and panels, right-click menus, translated strings, and XML Defs shipped next to your Lua.

What is not possible yet, in phases: [what is missing](infrastructure/docs/missing.md).

---

## Docs

| Start with | |
|------------|--|
| [Documentation home](infrastructure/docs/index.md) | Map of everything |
| [Quickstart](infrastructure/docs/guide/quickstart.md) | First mod in ten minutes |
| [API overview](infrastructure/docs/api/overview.md) and [reference](infrastructure/docs/api/reference.md) | What exists |
| [Security](infrastructure/docs/guide/security.md) | Sandbox, scanner, what mods cannot do |
| [Migration](infrastructure/docs/api/migration.md) | Moving an older mod with one command |
| [What is missing](infrastructure/docs/missing.md) | Roadmap |

Docs (custom Stratware theme): `cd infrastructure/docs-site` then `npm install` and `npm run serve`.

---

## Editor

The extension in `src/editor/` gives completions, warnings for deprecated names with quick fixes, and API definitions for the Lua language server.

```powershell
code --install-extension .\src\editor\rimkit-0.10.0.vsix
```

Workspace settings live under `.cursor/` (Lua stub library and CMake path).

---

## Layout

```text
mod/               RimWorld ship package (About, Assemblies, Auth, Defs,
                   Languages, Lua, Native, meta.lua)

src/
  api/             aliases.json
  host/            C# host
  native/          C++ core + rimkit CLI
  editor/          VS Code / Cursor extension
  mods/            internal probe mods
  templates/       mod template

infrastructure/    docs-site/, docs/, tools/
tests/             native tests + in-game smoke
bin/               rimkit.exe
```

---

## License

Apache 2.0. See [LICENSE](LICENSE).
