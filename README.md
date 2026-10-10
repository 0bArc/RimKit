# RimKit

[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen?style=for-the-badge&logo=steam&logoColor=white)](https://rimworldgame.com/)
[![Luau](https://img.shields.io/badge/Luau-typed-blue?style=for-the-badge)](https://luau.org/)
[![Harmony](https://img.shields.io/badge/Harmony-required-orange?style=for-the-badge)](https://github.com/pardeike/HarmonyRimWorld)
[![C++](https://img.shields.io/badge/core-C%2B%2B-00599C?style=for-the-badge&logo=cplusplus&logoColor=white)](https://isocpp.org/)
[![License](https://img.shields.io/badge/license-RimKit%20License-lightgrey?style=for-the-badge)](LICENSE)

Write RimWorld mods in Luau. RimKit loads your scripts, gives them a documented API to the game, and lets them patch game methods when the API does not cover something.

`stratware.rimkit` by Stratware.win

```lua
-- Floating damage numbers above people. Red for colonists, orange for everyone else.
game.events.on_pawn_damaged(function(pawn, e)
  game.effects.text(pawn, tostring(math.floor(e.dealt)), pawn.is_colonist and "red" or "orange")
end, { humanlike = true, min_dealt = 1 })

-- Colonists never lose accuracy with distance.
game.hooks.postfix("Verse.ShotReport", "HitFactorFromShooter", function(ctx)
  ctx:set_result(1)
end)
```

## How it works

Your Luau mod runs in a C++ core (the Luau VM, a sandbox and the `game.*` bindings). The core talks to a C# host, which calls RimWorld through Harmony. The pictures are in [architecture](infrastructure/docs/concepts/architecture.md) and [how RimKit loads your mod](infrastructure/docs/guide/how-mods-load.md).

## Install and make a mod

```powershell
rimkit build                                   # build the core and the host
.\bin\rimkit.exe mod ship .                    # install RimKit into RimWorld's Mods folder
.\bin\rimkit.exe mod ship .\src\examples\damage

rimkit mod create MyMod
rimkit mod test MyMod
rimkit mod ship MyMod
```

Load order is Harmony, then RimKit, then your mod. Close RimWorld before shipping if `mod\Native\rimlua_core.dll` is locked. The first mod, step by step: [quickstart](infrastructure/docs/guide/quickstart.md).

## What is in it

| | |
|-|-|
| Kits | `game.pawns`, `game.things`, `game.maps` and about a hundred more, typed and documented |
| Events | 116 named events with typed payloads, filters, and one `on_<event>` function each |
| Hooks | Prefix, postfix, finalizer and call replacement on any game method |
| Reflection | Read or call anything in Verse, RimWorld and UnityEngine. Off by default and audited |
| Libraries | `rimkit.signal` and `rimkit.promise` |
| Tooling | `rimkit` CLI, mock-host tests with `mod gen-tests`, tests in the real game (`mod test --in-game`), hot reload, `mod conform` for the RimKit Standard, a VS Code extension with types |

What changed and how each change was checked is in [CHANGES.md](CHANGES.md). What is not possible yet is in [what is missing](infrastructure/docs/missing.md). See [contributing](CONTRIBUTING.md) for the official ways to report bugs, request capabilities, improve docs, add examples and submit code.

## Docs

[Home](infrastructure/docs/index.md), [quickstart](infrastructure/docs/guide/quickstart.md), [API reference](infrastructure/docs/api/reference.md), [security](infrastructure/docs/guide/security.md). To read them as a site: `cd infrastructure/docs-site`, then `npm install` and `npm run serve`.

## Editor

The extension in `src/editor` adds completions, Luau types for the whole API and for every event payload, and commands to ship and test. It needs the Luau Language Server (`JohnnyMorganz.luau-lsp`).

```powershell
cd src/editor
npx --yes @vscode/vsce package --no-dependencies
code --install-extension .\rimkit-<version>.vsix
```

## Layout

```text
mod/             the RimWorld package (About, Assemblies, Auth, Defs, Lua, Native)
src/api/         kit tables and the API data the tools read
src/host/        C# host
src/native/      C++ core and the rimkit CLI
src/editor/      VS Code and Cursor extension
src/examples/    example mods
src/templates/   mod template
infrastructure/  docs, docs site, tools
tests/           native tests, host tests, in-game smoke mod
```

## License

RimKit is source available under the [RimKit License](LICENSE): you may use it to make and sell mods, and read, build and audit it, but not modify or redistribute it, apart from changes you prepare to send back to us. The example mods, templates and documentation samples are free to copy. Third-party parts keep their own licenses, see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
