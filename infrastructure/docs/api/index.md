# API

Everything Lua mods call lives under one table: `game`. Names are lowercase `snake_case`.

## Where to look

| Need | Page |
|------|------|
| Full function list | [Reference](reference.md) |
| Every returned table and its fields | [Types](types.md) |
| What each function sends to the game | [Host operations](ops.md) |
| Every command line command | [CLI reference](../guide/cli.md) |
| What exists and which layer to use | [Overview](overview.md) |
| React to game moments | [Events](events.md) |
| Change method behavior | [Hooks](hooks.md) |
| Which kit owns which ops | [Kits and domains](domains.md) |
| Stability before 1.0 | [Stability](stability.md) |

## Sections in this tab

- **Catalog**: reference, types, host operations, events, hooks, domains, tweaks, classes
- **Game**: Actors, Things and map, Action, Query and gen
- **Systems**: time, world, economy, defs, DLC
- **Presentation**: widgets, gizmos, HUD, graphics, audio
- **Advanced**: reflect, interop, naming, stability

!!! tip "Prefer kits"
    Use a kit first. Fall back to events, then hooks, then [typed reflection](reflect.md) only when nothing else fits.
