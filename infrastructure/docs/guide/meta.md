# meta.lua

Your mod's identity lives in `meta.lua`. `rimkit mod sync` (and `ship`) turn it into `About/About.xml`. Do not edit About.xml by hand, it is overwritten.

```lua
local meta = require("host.metadata")
meta.name = "My Mod"
meta.author = "Your Name"
meta.package_id = "yourname.mymod"
meta.version = "1.6"
meta.mod_version = "1.0.0"
meta.description = "One or two sentences players will read in the mod list."
meta.capabilities = { "hooks" }
meta.depends = {
  {
    id = "brrainz.harmony",
    name = "Harmony",
    steam = "steam://url/CommunityFilePage/2009463077",
    download = "https://github.com/pardeike/HarmonyRimWorld",
  },
  {
    id = "stratware.rimkit",
    name = "RimKit",
    steam = "steam://url/CommunityFilePage/3811629229",
    download = "https://steamcommunity.com/sharedfiles/filedetails/?id=3811629229",
  },
}
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
```

| Field | Meaning |
|-------|---------|
| `name` | Display name |
| `author` | Shown in the mod list |
| `package_id` | Unique id, lowercase, `author.modname`. Never change it after release, saves refer to it |
| `version` | Game version the mod targets (`"1.6"`) |
| `supported_versions` | Several game versions, for example `{ "1.5", "1.6" }`. Defaults to `version` |
| `mod_version` | The mod's own semantic version (`"1.2.0"`). Shown in the mod list, checked by `release-check`, readable with `game.mods.version` |
| `description` | Text in the mod list. Keep it plain |
| `url` | Link shown in the mod list |
| `api_level` | `1` opts in to strict mode: errors raise instead of returning `nil`, old API names are refused with `RK1002`, and only declared capabilities work. `rimkit mod create` sets it. See [stability](../api/stability.md) |
| `capabilities` | What the Lua may do: `reflect`, `hooks`, `files`, `dev`. See [capabilities](capabilities.md) |
| `perf_budget_us` | Microseconds per tick the mod may use on average before RimKit warns. Default 300. See [performance](performance.md) |
| `depends` | Dependencies. Each needs `id`, `name` and a `steam` or `download` link, or the game warns |
| `load_after`, `load_before` | Package ids that must load before or after this mod |
| `incompatible_with` | Package ids the mod cannot run with. The game warns the player |
| `version_folders` | Per game version folders, see below |
| `tags`, `license`, `workshop_id` | Used by `rimkit publish`. See [publishing](publishing.md) |

## Several game versions

`version_folders` writes `LoadFolders.xml` and fills `supportedVersions`. Each version lists the folders the game loads, `"/"` being the mod root. A `Lua/`, `Defs/` or `Textures/` folder inside a listed folder is loaded for that version only, so one mod can ship different hook targets for 1.5 and 1.6.

```lua
meta.version_folders = {
  ["1.5"] = { "/", "v1.5" },
  ["1.6"] = { "/", "v1.6" },
}
```

RimKit loads the `Lua/` folder of every listed folder, root first. For code that differs by game version inside one file, use `game.version.at_least("1.6")` and `game.version.hook`.

## Gotchas

- `meta.lua` is Lua. A string with a lone backslash stops the build with `invalid escape sequence`. Write "the Backslash key" instead of the character.
- Keep `package_id` free of spaces and capitals. The game warns `is not in valid format` otherwise.

## Commands

```text
rimkit mod create MyMod     new mod folder with a template
rimkit mod sync [path]      meta.lua to About/About.xml
rimkit mod ship [path]      sync, then copy into RimWorld Mods
rimkit mod check [path]     validate meta.lua, Lua, Defs, patches, textures and translation keys
rimkit mod test [path]      run Tests/*.lua against a mock host, no game needed
rimkit mod assets [path]    check the Workshop preview and the mod icon (--fix makes placeholders)
rimkit mod i18n ...         extract, missing, export and import translation strings
rimkit mod release-check    the checklist before publishing
rimkit publish [path]       upload to the Steam Workshop
rimkit diag                 zip the log and mod list for a bug report
```

See [testing](testing.md), [publishing](publishing.md) and [troubleshooting](troubleshooting.md).
