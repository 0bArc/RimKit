# Operate the game with Helm

[Helm](../../../helm/README.md) is a separate tool that starts the game, keeps it running and lets you operate it from a terminal or a script, with or without a window. RimKit ships the adapter that connects it to RimWorld. This page is how to use it with RimKit. Windows only for now.

```text
helm launch profiles/rimworld.hlm            # RimWorld with no window and no GPU, waits until it is playing
helm launch profiles/rimworld.hlm --gui      # with the game window; each action Helm runs shows as a message on screen
helm step 600                                        # runs 600 game ticks now
helm eval "game.time.now().year"                     # runs Lua inside the game
helm call time.now                                   # runs one RimKit operation
helm events pawn.damaged --count 1                   # prints events as they happen
helm shutdown                                        # closes the game and removes its work folder
```

## What you get

| Command | Does |
|---------|------|
| `helm launch <profile>` | Starts RimWorld on its own throwaway settings and saves, with its own mod list. Your `ModsConfig.xml`, options and saves are not used. `--set mods=a.b,c.d` adds mods |
| `helm info`, `helm ops` | What the game is, its tick, and every operation it offers (about 630) with its tier |
| `helm call <op> [json]` | Any RimKit operation. The same ones `game.*` calls |
| `helm eval <lua>` | Lua in the game's own sandbox, as no mod. Needs nothing declared |
| `helm step <n>` | Runs `n` game ticks now, up to 20000 per call, with queued events and mods' tick callbacks after each tick. The game keeps its speed and pause state |
| `helm events <names>` | Event frames as they happen: `pawn.*`, a name, or `*` |
| `helm run script.json` | Launch, run steps with expectations, shut down. Exit code 0 only when every step passed |

`rimkit mod test --in-game --headless` runs your `Tests/Game` tests in the same kind of window-less game.

## Typed reflection

The RimWorld profile turns the `developer_reflect` setting on in the run's own throwaway settings, so `helm eval "game.reflect..."` works. Pass `--set reflect=False` to leave it off. The setting in your own game options is never changed.

## A scripted check

`scripts/rimworld.hlm` (script `smoke`) is a script that launches the game, checks it is playing, steps time, runs Lua, spawns a colonist, hurts it, waits for the `pawn.damaged` event and shuts down. Run it with `helm run scripts/rimworld.hlm --script smoke`.

## How it is switched on

Helm talks to the game only when the launcher sets `HELM_ENDPOINT` and `HELM_TOKEN`. A normal game has neither, so nothing opens. RimKit's adapter then also needs two things before it starts:

1. pAuth must authorize the RimKit build (the same check that gates Lua).
2. `mod/Native/helm.dll` must have its SHA-256 in `mod/Auth/allowlist.json` under `helm`. `rimkit update --release` writes it.

Every command is logged as `[RimKit/helm]`. The pipe belongs to your user only, and every connection must present the run's random token. Details: [Helm security](../../../helm/docs/security.md) and [security](security.md).

## Your own game or mod

A mod does not use Helm directly. To make Helm drive another game, write an adapter and a profile: [writing an adapter](../../../helm/docs/adapters.md).
