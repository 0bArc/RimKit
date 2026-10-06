# Events

Named events tell your Lua that something happened in the game. They are the safest way to react: no Harmony knowledge needed, a clear payload, and the patch behind each event is installed only while you are listening. Tier: Experimental.

```lua
game.events.on("thing.damaged", function(e)
  game.log.info(e.thing.label .. " took " .. e.damage.amount .. " " .. e.damage.def)
end)
```

Every event also has its own function, `game.events.on_pawn_damaged(fn, filter?)`, with the dot of the event name written as an underscore. Its handler gets the pawn (or the thing) first and the payload second: `function(pawn, e)`. The editor types both from that name, see [Luau](../guide/luau.md). The string form below passes only the payload table.

`events.on` and `events.off` also work without the `game.` prefix.

## Functions

| Function | Does |
|----------|------|
| `events.on(name, fn)` | Subscribes. A canonical name (it contains a dot) installs its patch on the first subscription |
| `events.on(name, filter, fn)` | Subscribes, and runs `fn` only when the payload matches the filter table. See [Filters](#filters) |
| `events.off(name)` | Removes every handler you registered for the name. The patch is removed when the last handler goes |
| `events.list()` | Every event as `{ name, description, hot, installed }` |

## Filters

A filter is a table checked before your function runs, so the checks do not have to be written in every handler. All entries must match.

| Key | Matches when |
|-----|--------------|
| `pawn = true` | The event's pawn (or its thing, when that is a pawn) is a pawn. |
| `humanlike = true` | That pawn is humanlike, not an animal, mech or entity. |
| `colonist = true` | That pawn is a colonist. `false` matches everyone else. |
| `def = "Wall"` or `{ "Wall", "Door" }` | The thing or pawn has one of these defs. |
| `min_dealt = 1` | The payload's `dealt` is at least this. |
| any other key | The payload field of that name equals the value. |

```lua
-- Damage numbers over people, not over walls and animals
game.events.on_pawn_damaged(function(pawn, e)
  game.effects.text(pawn, tostring(math.floor(e.dealt)), pawn.is_colonist and "red" or "orange")
end, { humanlike = true, min_dealt = 1 })
```

The filter runs in Lua after the game has built the payload, so it saves your own code, not the cost of the event. A hot event is still best listened to only while you need it.

## Payloads

A handler receives one table. Fields by type:

| Game value | In the payload |
|------------|----------------|
| Pawn, Thing, Map, Faction | `RimPawn`, `RimThing`, `RimMap`, `RimFaction` |
| Anything else (a hediff, job, quest) | `RimObject` with `.handle`, `.type_name` and `.def` |
| Def or enum | name string |
| `IntVec3` | `{ x, y, z }` |
| Damage | table: `def`, `amount`, `angle`, `instigator`, `weapon`, `hit_part` |

```lua
game.events.on("hediff.added", function(e)
  -- e.pawn is a RimPawn, e.hediff is a RimObject whose .def is the hediff's defName
  if e.hediff.def == "Plague" then game.ui.message(e.pawn.name .. " caught the plague") end
end)
```

## Catalog

"Hot" events fire very often (every tick, every hit). Subscribe to them only while you need them.

| Event | Payload | Hot | When |
|-------|---------|-----|------|
| `thing.spawned` | `thing`, `map`, `def` | no | A thing was spawned on a map after load. |
| `thing.despawned` | `thing`, `mode` | no | A thing is about to leave the map. |
| `thing.destroyed` | `thing`, `mode` | no | A thing is about to be destroyed. |
| `thing.damaged` | `thing`, `damage`, `dealt` | yes | A thing took damage. |
| `pawn.spawned` | `pawn`, `map` | no | A pawn was spawned after load. |
| `pawn.died` | `pawn`, `damage`, `culprit` | no | A pawn is dying. |
| `pawn.damaged` | `pawn`, `damage`, `dealt` | yes | A pawn took damage. Use a filter, for example { humanlike = true }. |
| `pawn.resurrected` | `pawn` | no | A pawn was resurrected. |
| `hediff.added` | `pawn`, `hediff`, `part` | no | A hediff was added to a pawn. |
| `hediff.removed` | `pawn`, `hediff` | no | A hediff is about to be removed. |
| `job.started` | `pawn`, `job`, `target` | no | A pawn started a job. |
| `job.ended` | `pawn`, `job`, `condition` | no | A pawn is ending its current job. |
| `mental_state.started` | `pawn`, `state`, `started` | no | A mental state was requested. |
| `time.speed_changed` | `speed` | no | The game speed changed. |
| `time.ticked` | `ticks` | yes | One game tick ran. |
| `game.new` | none | no | A new game was initialised. |
| `game.loaded` | none | no | A save was loaded. |
| `game.ready` | none | no | The game finished initialising and is playable. |
| `game.saving` | `file` | no | The game is about to save. |
| `map.ready` | `map` | no | A map finished initialising. |
| `map.removed` | `map` | no | A map is being removed. |
| `incident.fired` | `incident`, `success`, `points`, `map` | no | An incident was attempted. |
| `quest.added` | `quest`, `name`, `id` | no | A quest was added. |
| `quest.removed` | `quest`, `name`, `id` | no | A quest is being removed. |
| `research.finished` | `project`, `researcher` | no | A research project finished. |
| `research.progressed` | `amount`, `researcher` | yes | Research progress was made. |
| `letter.received` | `label`, `def` | no | A letter arrived. |
| `message.shown` | `text`, `def` | no | A message was shown. |
| `faction.added` | `faction`, `def` | no | A faction was added to the world. |
| `pawn.joined_colony` | `pawn`, `from` | no | A pawn joined the player faction (recruited, rescued, born, tamed). |
| `pawn.downed` | `pawn` | no | A pawn was downed. |
| `pawn.stood_up` | `pawn` | no | A pawn is no longer downed. |
| `pawn.drafted_changed` | `pawn`, `drafted` | no | A pawn was drafted or undrafted. |
| `pawn.born` | `pawn` | no | A pawn was born or hatched. |
| `pawn.birthday` | `pawn`, `age` | no | A pawn had a biological birthday. |
| `pawn.equipped` | `pawn`, `thing` | no | A pawn equipped a weapon or tool. |
| `pawn.wore` | `pawn`, `thing` | no | A pawn put on apparel. |
| `bill.completed` | `pawn`, `recipe` | no | A bill finished one repetition. |
| `plant.harvested` | `plant`, `pawn` | no | A plant was harvested. |
| `construction.finished` | `frame`, `def`, `pawn` | no | A building frame is about to complete. |
| `recipe.applied` | `pawn`, `recipe`, `doer` | no | A recipe (surgery, medical operation) was applied to a pawn. |
| `hediff.tended` | `pawn`, `hediff`, `quality` | no | A hediff was tended. |
| `mental_state.ended` | `pawn`, `state` | no | A mental state ended. |
| `inspiration.gained` | `pawn`, `started` | no | An inspiration was requested. |
| `thought.gained` | `pawn`, `thought` | no | A memory thought was gained. |
| `relation.formed` | `pawn`, `relation`, `other` | no | A direct relation formed (marriage, bond, rival). |
| `relation.ended` | `pawn`, `relation`, `other` | no | A direct relation is ending (breakup, divorce). |
| `interaction.done` | `pawn`, `recipient`, `interaction`, `success` | no | A pawn tried a social interaction. |
| `skill.learned` | `pawn`, `skill`, `xp`, `level` | yes | A pawn gained skill experience. |
| `projectile.hit` | `projectile`, `target`, `launcher` | no | A projectile is hitting something. |
| `explosion.occurred` | `map`, `x`, `z`, `radius`, `damage_def` | no | An explosion is starting. |
| `melee.hit` | `attacker`, `target` | no | A melee attack landed. |
| `map.generated` | `map` | no | A map was generated. |
| `world_object.added` | `object`, `def`, `tile` | no | A world object was added (settlement, site, caravan). |
| `world_object.removed` | `object`, `def`, `tile` | no | A world object is being removed. |
| `caravan.formed` | `caravan` | no | A caravan was formed. |
| `weather.changed` | `map`, `weather` | no | The weather is changing. |
| `condition.started` | `condition`, `label` | no | A game condition started. |
| `condition.ended` | `condition`, `label` | no | A game condition is ending. |
| `time.hour_changed` | `ticks` | no | A new game hour began. |
| `time.day_changed` | `ticks` | no | A new game day began. |
| `time.quadrum_changed` | `ticks` | no | A new quadrum began. |
| `time.year_changed` | `ticks` | no | A new year began. |
| `trade.completed` | `traded` | no | A trade was executed. |
| `quest.accepted` | `quest`, `name`, `id` | no | A quest was accepted. |
| `quest.ended` | `quest`, `name`, `id`, `outcome` | no | A quest ended. |
| `research.started` | `project` | no | A research project became the current one. |
| `power.changed` | `thing`, `on` | no | A building was powered on or off. |
| `designation.added` | `def`, `thing`, `x`, `z` | no | A designation was added. |
| `designation.removed` | `def`, `thing`, `x`, `z` | no | A designation is being removed. |
| `zone.created` | `id`, `label`, `type` | no | A zone was created. |
| `zone.removed` | `id`, `label`, `type` | no | A zone is being removed. |
| `faction.goodwill_changed` | `faction`, `other`, `goodwill` | no | Goodwill between two factions changed. |
| `pawn.ideo_changed` | `pawn`, `ideo` | no | A pawn converted to another ideoligion. |
| `ideo.precept_added` | `ideo`, `precept` | no | A precept was added to an ideoligion. |
| `royalty.title_changed` | `pawn`, `faction`, `title` | no | A pawn's royal title changed. |
| `anomaly.level_changed` | `level` | no | The monolith level changed. |
| `ability.used` | `pawn`, `ability`, `target` | no | A pawn used an ability (a psycast, a gene ability, a ritual power). |
| `shot.fired` | `shooter`, `target`, `verb` | yes | A verb fired one shot of a burst. |
| `pawn.left_map` | `pawn`, `map` | no | A pawn is leaving a map (caravan, exit grid, shuttle). |
| `letter.opened` | `label` | no | The player opened a letter. |
| `letter.choice` | `tag`, `index`, `label` | no | The player picked an answer on a letter made with game.hud.letter. |
| `mod.disabled` | `mod`, `where` | no | A mod was switched off after too many Lua errors. |
| `mod.over_budget` | `mod`, `avg_us` | no | A mod is using more time per tick than its budget. |
| `options.changed` | `package_id`, `key`, `value` | no | A value on a game.options page changed. |
| `selection.changed` | `count` | no | Something was selected. |
| `selection.cleared` | none | no | The selection was cleared. |
| `window.opened` | `class` | no | A window was opened. |

Naming: `<domain>.<past tense verb>`. See [naming](naming.md).

## Delivery

- Events are queued and delivered on the main thread once per frame, so your handler never runs in the middle of game code.
- The queue holds 8192 events. A frame delivers up to 256. Overflow is counted by the host.
- `game.loaded` and `game.new` invalidate every handle created before them. Do not keep handles across them.
- A Lua error in a handler is logged with the event name. Other handlers still run.

## Need an event that is not here?

Use a [hook](hooks.md) on the game method, or ask for a catalog entry. Adding one is a single row in `src/host/EventCatalog.cs` plus a patch body, see [what is missing](../missing.md).
