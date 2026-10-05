# mod/

Shippable RimWorld package for RimKit. This folder is what `rimkit mod ship .` copies into your game `Mods` folder.

| Path | Role |
|------|------|
| `About/` | Generated `About.xml` from `meta.lua` |
| `Assemblies/` | `RimLuaHost.dll` |
| `Native/` | `rimlua_core.dll` |
| `Auth/` | `allowlist.json` binary pins |
| `Lua/` | Kit bootstrap scripts |
| `Defs/` `Languages/` | Kit defs and keyed strings |
| `meta.lua` | Mod identity |

Do not put `src/`, `tests/`, or `infrastructure/` in here. Those stay in the repo and are not shipped.
