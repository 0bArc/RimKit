# Quickstart

Make a mod, ship it, and see it run. About ten minutes.

## 1. Install

You need RimWorld 1.6, the Harmony mod, and RimKit.

- Harmony: Steam Workshop id 2009463077.
- RimKit: Steam Workshop id 3811629229, or build from source ([build](build.md)).

Mod order in the game's mod list: Harmony, then RimKit, then your mods.

You also need the `rimkit` command line tool (`bin\rimkit.exe` in the kit repo).

## 2. Create a mod

```text
set RIMKIT_AUTHOR=Your Name
rimkit mod create MyMod
cd MyMod
```

This makes:

```
MyMod/
  meta.lua                         name, author, package id, version, dependencies, capabilities. Edit this.
  Lua/main.luau                     your script, with a first game.events.on_load handler
  Languages/English/Keyed/*.xml    the mod's text, read with game.ui.translate
  Tests/main_test.luau              a first test that runs without the game: rimkit mod test
  LICENSE, CREDITS.md, CHANGELOG.md, Workshop.md   what you need to publish
  About/About.xml                  generated, do not edit
```

The package id is made from `RIMKIT_AUTHOR` and the mod name (`yourname.mymod` when it is not set). It must be unique and must never change after release: check it in `meta.lua`. See [meta.lua](meta.md).

## 3. Write some Lua

Edit `Lua/main.luau`:

```lua
local rk = require("rimkit")
rk.assert_api(0)

-- Runs when a colonist is hurt badly enough to go down.
game.events.on("hediff.added", function(e)
  if e.pawn.is_colonist and e.hediff.def == "Anesthetic" then
    game.ui.message(e.pawn.name .. " is out cold")
  end
end)

-- Right now, once at startup.
game.log.info("MyMod ready")
```

What is available is in the [API overview](../api/overview.md). The editor extension (see below) completes names and checks types.

## 4. Ship it

```text
rimkit mod ship
```

This writes `About/About.xml` from `meta.lua` and copies the mod into your RimWorld `Mods` folder. Set the `RIMWORLD_MODS` environment variable if your game is in a non-standard place.

## 5. Run it

1. Start RimWorld and enable Harmony, RimKit, then MyMod. Restart when the game asks.
2. Load or start a colony.
3. Open the log (Player.log, or the in-game dev log) and look for `MyMod ready`.

If your Lua does not load, see [troubleshooting](troubleshooting.md). The usual cause is the safety scan ([security](security.md)).

## Editor support

Install the RimKit VS Code extension (`src/editor/rimkit-<version>.vsix`):

- completions for the whole API
- API definitions for the Lua language server

## Next

- React to the game: [events](../api/events.md)
- Change game behavior: [hooks](../api/hooks.md)
- Edit pawns: [pawns kit](../api/pawns.md)
- Settings and saved data: [reference, config and data](../api/reference.md)
- Ship translated text: Keyed language files and `game.ui.translate`
