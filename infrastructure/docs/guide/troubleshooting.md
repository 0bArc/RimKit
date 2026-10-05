# Troubleshooting

Look in Player.log first. RimKit lines start with `[RimKit]` or `[RimLua]`. On Windows it is under `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`.

## My Lua does not run

| Symptom in the log | Cause | Fix |
|--------------------|-------|-----|
| `Quarantined Lua skipped: <id>` | The safety scan matched text in your Lua | The line above names the file and rule. Remove or rename it. A function called `load()` is a common cause. See [security](security.md) |
| `LUA BLOCKED by builtin pAuth` | RimKit's own files do not match the allowlist | Reinstall RimKit. If you built it yourself, run `rimkit build` so `mod/Auth/allowlist.json` is regenerated |
| `RimKit mod (stratware.rimkit) not enabled` or `Auth/allowlist.json missing` | An older RimKit build that does not recognise the Steam package id `stratware.rimkit_steam`, or a copy whose files do not match | Update RimKit. If you built it, run `node infrastructure/tools/release.js --deploy <mod folder>` and restart the game. The game must be closed because it locks the host DLL |
| `mod stratware.x was switched off after 20 Lua errors` | One mod raised 20 Lua errors in 60 seconds, so RimKit stopped its hooks, events and timers. Other mods keep running | Fix the errors listed above the message, then restart the game |
| You want to start the game without any Lua | A mod is crashing the game or you want to compare | Start with `-rimkit-safe`, set the environment variable `RIMKIT_SAFE=1`, or create `Config/rimkit_safe.txt`. The log says Lua is off |
| No RimKit lines at all | RimKit is not enabled, or loads before Harmony | Order: Harmony, RimKit, your mod |
| `Loading Lua from <id>` appears but nothing happens | The script ran but registered nothing, or errored | Look for a `lua:` error line right after |
| `attempt to index a nil value` | Calling a function that does not exist or is spelled differently | Check the [reference](../api/reference.md) |

## API errors

Errors from the typed kits start with a code. See the table in [RKS](../standard/rks.md).

| Code | Meaning |
|------|---------|
| RK1001 | Bad argument. The message says which |
| RK2001 | Stale or null handle. The object is gone. Handles are cleared when a game loads |
| RK3001 | Unknown def, or the pawn has no such tracker (animals have no skills) |
| RK3002 | No overload accepts those arguments. The message lists the candidates |
| RK3003 | The DLC is not active |
| RK4001 | Blocked by policy |
| RK4002 | `developer_reflect` is off |
| RK5001 | The game method threw. The message carries the exception |

## Deprecation lines

`deprecated: rim.pawn.name is now game.pawns.name` is a warning, not an error. The old name works until 1.0. Fix all of them at once with `rimkit migrate` ([migration](../api/migration.md)).

## Hooks

- `Ambiguous method ... Pass sig` means the method has several overloads. Add `sig = { "type", ... }` and the log shows the candidates.
- `Type not found` or `Method not found` means the name is wrong for this game version. Check spelling and namespace.
- A hook that does nothing: confirm the method really runs (add a log line), and that you are using `ctx:set_result` in a postfix.
- Hooks on very hot methods slow the game. Prefer a named [event](../api/events.md). `game.hooks.list()` shows calls and time per hook, and the log warns about a hook that averages over 100 microseconds.
- A hook that only reads scalar arguments and returns a number or boolean can pass `fast = true` to skip JSON. It falls back with a warning when the hook is not eligible.

## Key bindings

`Key binding conflict: X and Y are both bound to F6` means two mods use the same default key. Rebind one in the game's options. Defaults only apply to new bindings.

## Defs and saves

- `Could not load reference to ... named X` and `had a null def` usually mean a Def that a save refers to is not loaded. Make sure every mod the save used is enabled, and that Defs live in `Defs/` XML (not created at runtime) when saves depend on them. RimKit's own marker hediff was fixed this way.
- Runtime Def writes (`game.defs.register_thing`) need a restart to take effect.

## Settings

RimKit settings are in the mod options. `developer_reflect` enables `game.reflect`. `audit_verbose` logs every reflection call.

## Still stuck

Open an issue with the Player.log lines around the problem and your `meta.lua`. For a missing capability, see [request a feature](request-a-feature.md).

## A mod stopped working

If a mod's Lua fails 20 times in a minute, RimKit switches its scripts off for the session and shows a letter naming the mod. You can turn it off for good from the letter. The errors are in the game log. `rimkit diag` zips the log and the mod list for a bug report without using the network, see [performance and diagnostics](performance.md).

## RK4001: capability

The mod used something it did not declare. Add the named capability to `meta.capabilities` and run `rimkit mod sync`, see [capabilities](capabilities.md).
