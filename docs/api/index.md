# Strong API domains

`rim.*` bound tables (and globals like `anomaly`, `data`, `ui`) are the **normal** Lua API.

`rim.invoke` is an **escape interface** for explicitly designated operations only (`api.list`, `reflect.*`). It is **not** a general mechanism for calling GameApi operations.

```lua
-- normal
rim.pawn.is_humanlike(h)
anomaly.recruit(h)

-- escape only
rim.invoke("api.list")
rim.invoke("reflect.get", { h = h, member = "..." })

-- denied (use bound tables)
-- rim.invoke("pawn.is_humanlike", { h = h })
```

Allowlisted ops live in `ApiRegistry` (tier `gameplay` or `util`). Prefer bound tables; the host chooses the op string inside `bind_op` / `oo_bind`.

## Threat tiers

| Tier | Meaning |
|------|---------|
| gameplay | RimWorld-only colony logic |
| util | Least-privilege OS helper (fixed folders / mod Export only) |
| denied | Keylog, free Process, free IO, silent net, `load` (not exposed) |

Request new ops: [info](info/index.md).

## Lua tables (Wave 0-3)

```lua
data.get(package_id, key)
data.set(package_id, key, value)
data.keys(package_id)

health.has_hediff(h, defName)
health.hediff_severity(h, defName)
health.set_hediff_severity(h, defName, severity)
health.tend(h, quality?)
surgery.queue_operation(h, recipeDefName)

-- General draftable control (animals, entities, ToolUsers, …)
rim.pawn.make_controllable(h)
rim.pawn.release_control(h)
rim.pawn.is_controllable(h)

anomaly.dlc_active()
anomaly.is_entity(h)
anomaly.list_on_map(mapHandle?)
anomaly.knock_out(h, severity?)
anomaly.find_platform(hauler_h, entity_h?)
anomaly.start_capture(hauler_h, entity_h, platform_h?)
anomaly.recruit(h)                     -- same as rim.pawn.make_controllable
anomaly.try_set_faction_player(h)      -- alias recruit
anomaly.release_to_hostile(h)

-- Lua-authored right-click menus (no C# mod required):
ui.on_map_float_menu(function(ctx)  -- ctx.clicked, ctx.hauler handles
  return { { label = "...", on_click = function() end, disabled = false } }
end)

util.open_folder("ModRoot"|"Saves"|"PlayerLog"|"RimKit", package_id?)
util.write_export(package_id, "file.txt", content)

building.power_on(h)
building.set_power(h, true)
building.flick(h, true)

work.get_priority(h, workType)
work.set_priority(h, workType, 0-4)
work.list_types()

world_api.weather(map?)
world_api.set_weather(def, map?)
incident.try_fire(def, map?)
incident.list()
audio.play(soundDef)

ui.panel({ title=, body=, checks={...}, list={...} })
```

Legacy find/map/pawn/job/ui APIs remain in [lua-api.md](lua-api.md).

## Examples

- `src/examples/logic_panel` : data + ui.panel (F8)
- `src/examples/tame_anomalies` : pure Lua float menu + recruit/capture (F6/F7). Holding platform for Capture; Recruit = army.
