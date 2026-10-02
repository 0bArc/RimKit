# RimLuaKit

[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen?style=for-the-badge&logo=steam&logoColor=white)](https://rimworldgame.com/)
[![Lua](https://img.shields.io/badge/Lua-5.4-blue?style=for-the-badge&logo=lua&logoColor=white)](https://www.lua.org/)
[![Harmony](https://img.shields.io/badge/Harmony-required-orange?style=for-the-badge)](https://github.com/pardeike/HarmonyRimWorld)
[![C++](https://img.shields.io/badge/core-C%2B%2B-00599C?style=for-the-badge&logo=cplusplus&logoColor=white)](https://isocpp.org/)
[![License](https://img.shields.io/badge/license-Apache%202.0-lightgrey?style=for-the-badge)](LICENSE)

**Write RimWorld mods in Lua.** Script behavior and patches without a C# project for every change.

`stratware.rimkit` · Team Stratware.win

---

## Why this exists

RimWorld modding usually means C#, Harmony, and a rebuild loop. RimLuaKit flips that:

- **Lua first** for behavior, events, jobs, UI hooks
- **C++ core** (Lua 5.4 + sol2) for the runtime
- **Thin C# host** so RimWorld can load the kit

```lua
function on_pawn_spawned(pawn)
  if pawn.is_colonist then
    pawn:give_item("Component", 1)
    log.info(pawn.name .. " got a component")
  end
end

events.on("pawn_died", function(pawn)
  log.info(pawn.name .. " died")
end)

rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(pawn)
  return true  -- false = skip original
end
```

Give items. Register jobs. Open debug windows. Patch with `rim.prefix`. One language for gameplay logic.

---

## Quick start

```powershell
# build kit
cmd /c src\native\build_release.bat
dotnet build .\src\host\RimLuaHost.csproj -c Release

# copy kit + a sample into RimWorld Mods/
.\bin\rimkit.exe mod ship .
.\bin\rimkit.exe mod ship .\src\examples\jobs
```

In-game load order: **Harmony, then RimLuaKit, then your mod**.

Close RimWorld before copying if `Native\rimlua_core.dll` is locked.

```powershell
rimkit mod create MyMod
rimkit mod sync
rimkit mod ship
```

---

## What you can do

| Surface | Examples |
|---------|----------|
| Pawn | `give_item`, traits, hediffs, draft, kill, `start_job` |
| Events | `on_pawn_spawned`, `pawn_died`, timers |
| Jobs | `jobs.register` + scripted `JobDriver_RimLua` |
| Map | spawn things, `find_cells` by terrain |
| UI | window, float menu, messages, letters |
| Config | Mod settings from Lua |
| Defs | `defs.lua` to XML on `rimkit mod sync` |
| Escape | `rim.reflect.*`, raw Harmony prefixes |

Samples: `src/examples/hello_lua`, `src/examples/jobs` (F8 debug UI).

Docs: [docs/](docs/index.md) · API: [docs/lua-api.md](docs/lua-api.md)

---

## Editor

Pack VSIX from `vscode-rimkit/` for Lua stubs and auto-copy to Mods on save.

```powershell
cursor --install-extension .\vscode-rimkit\rimkit-0.2.1.vsix
```

Workspace settings live under `.cursor/` (Lua stub library + CMake path).

---

## Layout

```text
src/host/          thin C# host (Harmony, GameApi, UI)
src/native/        C++ Lua core + rimkit CLI
src/examples/      hello_lua, jobs
vscode-rimkit/     editor stubs / extension
Native/            rimlua_core.dll (out of Assemblies/)
Assemblies/        RimLuaHost.dll only
meta.lua           mod identity (rimkit writes About.xml)
```

---

## License

Apache 2.0. See [LICENSE](LICENSE).
