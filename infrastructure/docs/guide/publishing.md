# Publishing to the Steam Workshop

Everything between "it works on my machine" and "strangers can install it" is a command. Run them in this order.

| Step | Command | What it covers |
|------|---------|----------------|
| 1 | `rimkit mod check` | Defs, patches, textures, translation keys, declared permissions |
| 2 | `rimkit mod test` | Your `Tests/*.luau` |
| 3 | `rimkit mod assets --fix` | Preview image and mod icon |
| 4 | `rimkit mod release-check` | Full release checklist |
| 5 | `rimkit publish --user <steam> --note "..."` | Sync, stage, upload via SteamCMD |

!!! note "Order matters"
    Fix check and test failures before you publish. `release-check` is the gate; `publish` runs it again.

## What each step checks

**`rimkit mod check`** reads the mod's files, not the game.

- `About/About.xml` fields come from `meta.lua`; `package_id` must look like `author.modname` and `mod_version` must be a semantic version.
- Every file in `Defs/` and `Patches/` is well formed XML, and a `defName` is not defined twice.
- A `texPath` that points into the mod's own `Textures/` folder must exist. Paths into the game's textures are left alone.
- Every `game.ui.translate("Key")` in your Lua has an English string. Other languages are compared to English and the missing keys are listed.
- If `meta.capabilities` is declared, the Lua must not use more than it lists, see [capabilities](capabilities.md).

**`rimkit mod assets`** checks `About/Preview.png` and `About/ModIcon.png`. The Workshop wants a PNG of at least 640x360, 16:9, under 1 MB. The icon should be square, between 64 and 512 pixels. With `--fix` missing files are created as plain placeholders, never overwritten. Replace the placeholder before you publish.

**`rimkit mod release-check`** adds the things that decide whether players trust the mod:

- `mod_version` is set and `CHANGELOG.md` has an entry for that version.
- A `LICENSE` file exists, and a `CREDITS.md` if the mod ships textures or sounds.
- The description does not claim to be "official" and says what game it is for.
- Preview and icon are valid, and the content check passes.

It also prints a reminder of Ludeon's rules: do not ship game files or logos, and get permission before you use other people's art or sound. RimKit cannot tell who made an image, so this part stays your responsibility.

## Versions and the changelog

Give every release a semantic version in `meta.mod_version` (`1.2.0`) and a heading in `CHANGELOG.md` with the same number. The version is written to `About.xml`, so players see it in the mod list, and your Lua can read it with `game.mods.version`. `game.save.stamp(package_id)` records it in the save so you can tell which version last wrote a game, see [save safety](save-safety.md).

## Dependencies and conflicts

`meta.depends` lists mods you need, each with a Workshop link so the game can offer a download. `meta.load_after` and `meta.load_before` order your mod against others, and `meta.incompatible_with` makes the game warn when a clashing mod is active. All of them go into `About.xml` when you run `rimkit mod sync`. To use another mod's code only when it is present, see `game.hooks.in_mod` and [interop](../api/interop.md).

## Several game versions

`meta.version_folders` writes `LoadFolders.xml` so one Workshop item can ship folders for several game versions, see [meta.lua](meta.md#several-game-versions).

## Translations

```text
rimkit mod i18n extract            add TODO strings for translate("Key") calls that have no English text
rimkit mod i18n missing German     list keys German does not have yet
rimkit mod i18n export German      German.csv with key, english and German columns
rimkit mod i18n import German German.csv
```

Send translators the csv. Import writes `Languages/German/Keyed/Imported.xml`.

## Publishing

`rimkit publish` syncs the mod, runs the release check, copies only the folders that ship (About, Defs, Patches, Lua, Languages, Textures, Sounds, Assemblies, LoadFolders.xml, the licence, credits and changelog) to a staging folder and writes the item file that SteamCMD reads: title, description (`Workshop.md` if you have one), change note, tags (the game versions plus `meta.tags`) and the preview.

```text
rimkit publish --user mysteamname --note "Fixes the hunger setting" [--steamcmd C:\steamcmd\steamcmd.exe] [--dry-run]
```

- It needs [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD). Pass `--steamcmd`, set the `STEAMCMD` environment variable, or put it in `C:\steamcmd`.
- With `--dry-run`, or when SteamCMD or `--user` is missing, nothing is uploaded and the exact command is printed.
- The first publish creates the item. The new id is written to `About/PublishedFileId.txt`: commit that file, later publishes update the same item. You can also set `meta.workshop_id`.
- SteamCMD asks for your Steam Guard code on the first run.

The upload step has been checked to produce the item file and the command. It has not been run against Steam by the RimKit authors, so try it with `--visibility 2` (private) first. `--visibility` takes 0 public, 1 friends only, 2 private, 3 unlisted.

## Templates

`rimkit mod create` adds `LICENSE`, `CREDITS.md`, `CHANGELOG.md`, `Workshop.md` (a description with the sections players look for) and a first test, so the release check passes from the start. Edit them; do not leave the placeholder text in.
