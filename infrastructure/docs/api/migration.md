# Migrating to the unified names

RimKit now has one naming convention (UNC, see [naming](naming.md)). Every function lives under `game.<domain>`. The old names still work and print one deprecation line per name per session. They are removed no earlier than 1.0.0.

## Check what needs changing

```bat
rimkit migrate path\to\your\mod
```

This is a dry run. It lists the files and the number of names it would change, and prints notes for things that need a decision.

## Apply

```bat
rimkit migrate path\to\your\mod --write
```

Use version control so you can review the diff. Use `--check` in a script: it exits with code 1 while deprecated names remain.

## What the tool rewrites

- Function paths from the alias table, for example `rim.pawn.name` to `game.pawns.name`, `ui.message` to `game.ui.message`, `rim.find.tick` to `game.time.ticks`.
- Method names on wrapped objects, for example `pawn:seek_medical_help()` to `pawn:seek_medical()` and `map:spawn_thing(...)` to `map:spawn(...)`.

Matches must be whole names. `x.rim.pawn.name` and `rim.pawn.hunger_pct` are not confused with `rim.pawn.name` or `rim.pawn.hunger`.

## What it only reports

- **Legacy events.** `events.on("pawn_died", function(pawn) ... end)` keeps working. The canonical form passes a payload table, so the handler changes shape. Rewrite by hand:

  ```lua
  events.on("pawn.died", function(e)
    local pawn = e.pawn
  end)
  ```

  The globals `on_pawn_spawned` and `on_pawn_died` follow the same rule: use `events.on`.

- **`rim.reflect`, `rim.cs`, `rim.harmony`.** Their arguments and results differ from `game.reflect`, or they are another name for `rim.hooks`. Move to [game.reflect](reflect.md) and `game.hooks` by hand.

## Caveats

- A local variable that shares a name with an old table, such as `local data = {}` followed by `data.get(...)`, can be rewritten by mistake. Review the diff.
- Strings that contain an old name are rewritten too.

## Editor support

The RimKit VS Code extension (0.4.0 and later) underlines deprecated names with a quick fix, offers "replace all in this file", and has the command "RimKit: Migrate deprecated API names". It reads the same `aliases.json` as the tool and the runtime.
