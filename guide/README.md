# Video guide: make a RimWorld mod with RimKit

Two follow-along episodes you can record. Each one is a script: what is on screen, what you type, and what to say. The finished mods are in each episode's `final/` folder so you can check your work and do a dry take.

| Episode | Mod | Length | You teach |
|---------|-----|--------|-----------|
| [1. Speed up time](01-speed-mod/script.md) | Turbo Time | about 12 minutes | Create a mod, change a game number with a tweak, hotkeys, a setting, tests, ship |
| [2. Control your colonists](02-pawn-control/script.md) | Commander | about 12 minutes | Selection, mouse position, giving orders, drafting, tests, ship |

Do episode 1 first. Episode 2 assumes the viewer knows the `create`, `ship` and `test` loop.

## Before you record

1. **RimKit 0.10.0 or newer.** The episodes use `game.tweaks`, `game.interop` and `game.test`. Check the game log for `[RimKit] version 0.10.0`. The older Workshop copy lacks them: build the repo and enable the dev copy.
2. **`rimkit.exe`** is in `bin\` of the repo. Run it as `.\bin\rimkit.exe` from the repo folder, or put `bin` on your PATH and say `rimkit`.
3. **VS Code with the RimKit extension.** Install `src\editor\rimkit-0.10.0.vsix`, plus a Lua extension such as LuaLS, and accept the prompt to add the RimKit definitions. Type `game.t` in a Lua file and you should see completions.
4. **RimWorld with Harmony and RimKit enabled.** Turn on Development mode if you want the F11 dev tools window.
5. **A clean working folder** for the viewer's mod, for example `C:\Videos\mods`.
6. **Screen setup:** large fonts, terminal and editor on one side, the game on the other, windowed or borderless.
7. **Dry run each episode** with the `final/` mod first, then delete the shipped copy from your `Mods` folder before the real take.

Both finished mods pass their tests (`rimkit mod test`) and the content check. They have not been played through in the game by the authors, so the dry run matters, especially the key presses.

## The loop to say out loud

```text
edit the Lua  ->  rimkit mod ship  ->  restart the game  ->  try it  ->  repeat
rimkit mod test       runs your tests in a second, no game needed
```

## Things that can go wrong on camera

| What you see | Why | What to say and do |
|--------------|-----|--------------------|
| The mod is not in the mod list | Not shipped, or the game needs a restart | "The game only reads the Mods folder at start." |
| `RK4001` in the log | The Lua used something the mod did not declare | Add it to `capabilities` in `meta.lua`, run `rimkit mod sync`. |
| Nothing happens on the key | Another key uses it, or the mod is off | Rebind in Options, Key bindings. |
| Lua error in the log | A typo | Read the line number in the message and fix it on camera. |
| `rimkit` is not recognised | Not on PATH | Use `.\bin\rimkit.exe`. |

More: [save safety](../infrastructure/docs/guide/save-safety.md), [publishing](../infrastructure/docs/guide/publishing.md), the [cookbook](../infrastructure/docs/guide/cookbook.md), the [dev tools](../infrastructure/docs/guide/performance.md) (F11).
