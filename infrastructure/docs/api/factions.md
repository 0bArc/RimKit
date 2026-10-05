# Factions kit

`game.factions` reads and changes goodwill and relations, and lists members and definitions. The faction (a `RimFaction` or its handle) is the first argument. The kit is Experimental in the [stability tiers](stability.md).

| Function | Returns | Notes |
|----------|---------|-------|
| `game.factions.player()` | RimFaction | The player faction. |
| `game.factions.list()` | RimFaction[] | Every faction in the game. |
| `game.factions.of_def(def)` | RimFaction? | First faction of a definition. |
| `game.factions.name(faction)` | string | |
| `game.factions.info(faction)` | table | `name`, `def`, `label`, `is_player`, `hidden`, `defeated`, `temporary`, `permanent_enemy`, `tech_level`, and for other factions `goodwill` and `relation` toward the player, plus `leader` when there is one. |
| `game.factions.goodwill(faction[, other])` | integer | -100 to 100. `other` defaults to the player faction. |
| `game.factions.set_goodwill(faction, other, value)` | integer | Exact value on both sides, clamped to -100 to 100. Does not change the relation kind, use `set_relation` for that. No letters or messages are shown. |
| `game.factions.adjust_goodwill(faction, other, delta)` | integer | Relative, and scaled by the game's goodwill difficulty setting like any in-game goodwill change, so the result can differ from `goodwill + delta`. |
| `game.factions.relation(faction[, other])` | string | `Hostile`, `Neutral` or `Ally`. |
| `game.factions.set_relation(faction, other, kind)` | | Forces the relation, ignoring goodwill. |
| `game.factions.is_hostile(faction, other)` | boolean | |
| `game.factions.leader(faction)` | RimPawn? | |
| `game.factions.members(faction)` | RimPawn[] | Living pawns on maps and in the world. |
| `game.factions.hostiles(faction)` | RimFaction[] | |
| `game.factions.allies(faction)` | RimFaction[] | |
| `game.factions.defs()` | table[] | `def`, `label`, `hidden`, `permanent_enemy`, `tech_level` for every faction definition. |

Errors: a stale faction handle raises `RK2001`, asking for the goodwill of a faction with itself raises `RK1001`.

```lua
local player = game.factions.player()
for _, f in ipairs(game.factions.list()) do
  local i = game.factions.info(f)
  if not i.is_player and not i.hidden and i.relation == "Hostile" and i.goodwill > -50 then
    game.factions.adjust_goodwill(f, player, 10)
  end
end
```

Not yet in the kit: creating temporary factions and diplomacy actions (gifts, trade caravans, peace talks). They are tracked as P1-04 in the [roadmap](../missing.md).
