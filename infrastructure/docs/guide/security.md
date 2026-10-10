# Security

Lua mods run inside a sandbox, and RimKit checks itself and every mod before running anything. This page explains what that means for you as an author and as a player.

## The four checks

1. **Binary allowlist (AuthGate).** RimKit's own files must match known SHA256 hashes: the Harmony assembly, the RimKit host and the native core. If they do not, no Lua loads at all. `rimkit build` regenerates `mod/Auth/allowlist.json` for builds you make yourself.
2. **Lua quarantine.** Every enabled mod's `Lua/**/*.lua` is scanned for dangerous patterns before it runs. A hostile mod is skipped. Clean mods still load.
3. **Sandboxed Lua.** No `io`, `os`, `debug`, `dofile`, `load` or `loadlib`. `require` works only for files under that mod's own `Lua/` folder. Writing to Defs and exports is jailed to the mod's folders.
4. **API allowlist.** Lua can only call operations the host explicitly registers. Unknown operations fail. `rim.invoke` is limited to a short list of escape operations.

## What the scanner rejects

The scan matches source text, so avoid these even in comments and strings:

| Pattern | Why |
|---------|-----|
| `os.execute`, `os.remove`, `os.exit` and other `os.*` | process and file control |
| `io.open`, `io.popen` and other `io.*` | disk access |
| `dofile(`, `loadfile(`, `load(`, `loadstring(` | runtime code loading |
| `package.loadlib`, `package.cpath`, `require("os")` and similar | native loading |
| `debug.*` | VM internals |
| `ffi.` | native calls |
| `System.Diagnostics.Process`, `System.IO.File/Directory/Path` | process and disk access |
| `reflect.static_call("System...` | system types through reflection |
| `Assembly.Load...` | loading code |

A common surprise: a function you name `load()` is flagged, because the scanner cannot tell it from the forbidden one. Name it `restore`, `read_state`, anything else.

## What mods can never do

Keyloggers, free process launching, arbitrary file access, silent network access, loading code at runtime. Least-privilege helpers exist instead: `game.util.open_folder` opens a few fixed folders, `game.util.write_export` writes under the mod's own export folder.

## Reflection and hooks are audited

`game.reflect` is off by default and limited to the `Verse`, `RimWorld` and `UnityEngine` namespaces ([reflect](../api/reflect.md)). Hooks that skip game code or write results are Advanced tier ([hooks](../api/hooks.md)). Every reflection call and every hook registration is written to an audit ring. Denied calls always appear in the game log. Turn on the setting `rimkit.audit_verbose` to log allowed calls too. Entries carry an ISO 8601 UTC timestamp.

## If your mod is quarantined

The log shows `Quarantined Lua skipped: <package id>` and, above it, the file and the rule that matched with a short excerpt. Remove or rename the matching text and ship again. See [troubleshooting](troubleshooting.md).

## If you need something blocked

Do not look for a bypass. Ask for a named, narrow API: [request a feature](request-a-feature.md). If it cannot be done safely, the answer is a normal C# mod.

## For players

- Only install Lua mods you would install any other mod from.
- A clean scan is a safety net, not a guarantee of good behavior. Mods can still change the game in ways you dislike.
- RimKit shows its authenticity proof in the mod options. A mismatch means the RimKit files were modified.

## Helm, the control channel

[Helm](helm.md) can operate a running game from outside. It is off unless a launcher sets `HELM_ENDPOINT` and `HELM_TOKEN`, it needs pAuth to authorize RimKit and the Helm library to be on the allowlist, it uses a named pipe that only your user can open, and every connection needs the run's random token. It is a host feature, not a Lua capability: mods cannot start it, reach it or use it to get around the sandbox. Every command is logged.
