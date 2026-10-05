# Jobs kit

`game.jobs` orders pawns to do any vanilla job. The pawn is the first argument. The kit is Experimental in the [stability tiers](stability.md). The older `make`, `start` and `register` (Lua-defined jobs) are unchanged, see [mod structure](../guide/mod-structure.md). Every function is in the [reference](reference.md#jobs).

| Function | What it does |
|----------|--------------|
| `give(pawn, def, opts)` | Orders a job by def name (`Wait`, `Goto`, `Clean`, `TendPatient`, ...) with `target`, `x`, `z`, `target2`, `count` and `queue` options. Returns false when the pawn cannot take it. |
| `current(pawn)`, `queue_list(pawn)` | What the pawn is doing, and what waits behind it. |
| `interrupt(pawn)`, `clear_queue(pawn)` | Stop the current job, or empty the queue. |
| `can_reserve(pawn, thing)`, `release_reservations(pawn)` | Reservation checks and cleanup. |
| `haul`, `tend`, `rescue`, `repair`, `clean`, `wait`, `go_to` | Ready-made helpers for the common vanilla jobs. Each takes `queue` as the last argument. |
| `defs()` | Every job def in the game. |

Jobs ordered here are forced, like a player order: the pawn drops what it is doing. Pass `queue = true` to run the job after the current one. Errors: an unknown def raises `RK3001`, a stale handle `RK2001`.

```lua
-- Send the best doctor to every downed colonist
for _, patient in ipairs(game.query.pawns({ map = game.maps.current(), faction = "player", downed = true })) do
  local doctor = game.query.pawns({ faction = "player", humanlike = true, downed = false })[1]
  if doctor then game.jobs.tend(doctor, patient) end
end
```
