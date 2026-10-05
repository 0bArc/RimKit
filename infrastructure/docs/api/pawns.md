# game.pawns

Experimental. Skills, needs, traits, thoughts, relations, backstory, capacities, timetable and genes for any pawn, without `game.reflect`. See [API stability](stability.md) and [naming](naming.md).

```lua
local pawn = rim.wrap(game.maps.colonists(game.current_map().handle)[1])

game.pawns.set_passion(pawn, "Shooting", "Major")
game.pawns.add_trait(pawn, "Beauty", 2)
game.pawns.add_thought(pawn, "Catharsis")

for _, skill in ipairs(game.pawns.skills(pawn)) do
  print(skill.def, skill.level, skill.passion)
end
```

Without the kit, setting a passion needs `game.reflect` and knowledge of `Pawn_SkillTracker` and `SkillRecord`. The same calls are also members of the wrapped object: `pawn.skills`, `pawn:set_passion("Shooting", "Major")`.

## Conventions

- The first argument is the pawn: a `RimPawn`, a `RimEntity`, or a handle.
- Defs are passed by `defName` and returned the same way.
- Levels of needs are ratios from 0 to 1.
- A failed call raises a Lua error that starts with an RK code. The properties on `RimPawn` (`pawn.skills` and others) return `nil` instead.

| Code | Meaning |
|------|---------|
| RK1001 | Bad argument (for example an invalid passion or trait degree) |
| RK2001 | Pawn handle is stale or null |
| RK3001 | Unknown def, or the pawn has no such tracker (animals have no skills) |
| RK3003 | The DLC is not active (genes need Biotech) |

## Skills

| Function | Returns |
|----------|---------|
| `skills(pawn)` | Array of `{ def, label, level, passion, xp, xp_required, disabled }` |
| `skill_info(pawn, skill)` | One skill table |
| `set_passion(pawn, skill, passion)` | `true`. `passion` is `"None"`, `"Minor"` or `"Major"` |
| `add_skill_xp(pawn, skill, amount [, direct])` | The level after the XP |

The older `skill(pawn, name)` (level only) and `set_skill(pawn, name, level)` keep working.

## Needs

| Function | Returns |
|----------|---------|
| `needs(pawn)` | Array of `{ def, label, level }` |
| `need(pawn, def)` | Level, 0 to 1 |
| `set_need(pawn, def, level)` | `true`. The level is clamped to 0 to 1 |

## Traits

| Function | Returns |
|----------|---------|
| `traits(pawn)` | Array of `{ def, degree, label }` |
| `has_trait(pawn, def [, degree])` | boolean |
| `add_trait(pawn, def [, degree])` | `true`, or `false` if the pawn already has it. A degree the trait does not have raises RK1001 and lists the valid ones |
| `remove_trait(pawn, def)` | boolean |

Degrees matter: `Beauty` has -2 to 2, `Nerves` has -2 to 2, and so on. The older `pawn:add_trait(def)` always used degree 0.

## Thoughts and opinions

| Function | Returns |
|----------|---------|
| `thoughts(pawn)` | Current mood thoughts as `{ def, label, mood_offset }` |
| `memories(pawn)` | Memories as `{ def, label, age, mood_offset, other }` |
| `add_thought(pawn, def [, other])` | `true`. `other` is the pawn the thought is about |
| `remove_thought(pawn, def)` | `true`. Removes every memory of that def |
| `opinion_of(pawn, other)` | Integer opinion |

## Relations

| Function | Returns |
|----------|---------|
| `relations(pawn)` | Array of `{ def, other }` (direct relations only) |
| `has_relation(pawn, def, other)` | boolean |
| `add_relation(pawn, def, other)` | `true`, or `false` if it exists |
| `remove_relation(pawn, def, other)` | boolean |

## Backstory

| Function | Returns |
|----------|---------|
| `backstory(pawn)` | `{ childhood, childhood_title, adulthood, adulthood_title }` |
| `set_backstory(pawn, slot, def)` | `true`. `slot` is `"childhood"` or `"adulthood"` |

## Capacities

| Function | Returns |
|----------|---------|
| `capacities(pawn)` | Array of `{ def, level, capable }` for every `PawnCapacityDef` |
| `capacity(pawn, def)` | Level (1.0 is normal) |

## Timetable

| Function | Returns |
|----------|---------|
| `timetable(pawn)` | Array of 24 def names, hour 0 first. Colonists only |
| `set_assignment(pawn, hour, def)` | `true`. `def` is `Anything`, `Work`, `Joy`, `Sleep` or `Meditate` |

## Genes (Biotech)

| Function | Returns |
|----------|---------|
| `genes(pawn)` | Array of `{ def, label, active, xenogene }` |
| `has_gene(pawn, def)` | boolean |
| `add_gene(pawn, def [, xenogene])` | `true`, or `false` if present |
| `remove_gene(pawn, def)` | boolean |
| `xenotype(pawn)` | `{ def, label }` |

## Health

`hediff_list`, `injuries`, `body_parts`, `damage_part`, `heal_injuries`, `restore_part`, `remove_part`, `add_hediff_on_part` (prosthetics), `immunity`, `health_summary` and `surgeries`. Parts are addressed by the index that `body_parts` returns.

```lua
-- Replace a missing leg with a prosthetic
for _, part in ipairs(game.pawns.body_parts(pawn)) do
  if part.def == "Leg" and part.missing then
    game.pawns.add_hediff_on_part(pawn, "SimpleProstheticLeg", part.index)
  end
end
```

## Gear and policies

`gear` (weapon, apparel and inventory in one call), `equip`, `unequip`, `wear`, `remove_apparel`, `add_to_inventory`, `drop`, `outfit`/`set_outfit`/`outfits`, `drug_policy`/`set_drug_policy`/`drug_policies`, `food_policy`/`set_food_policy`/`food_policies`, `work_priorities`, `bed`/`assign_bed`/`unassign_bed` and `set_assignments` (one timetable hour for a group).

## Social, animals, prisoners

`interact`, `interaction_defs`, `opinion_reasons`, `partner`, `marry`, `break_up`, `animal_info`, `set_master`, `train`, `tame`, `guest_info`, `set_guest_status`, `set_interaction_mode`, `interaction_modes` and `recruit`.

## Mind

`mental_info`, `start_mental_state`, `stop_mental_state`, `mental_state_defs`, `break_thresholds`, `mind_summary`, `inspiration`, `give_inspiration`, `end_inspiration` and `use_drug`.

## Generation

`game.pawns.generate(opts)` makes a pawn with the game's own generator and spawns it when `map`, `x` and `z` are given. `game.pawns.kinds()` lists the pawn kinds.

Every function above is in the [reference](reference.md#pawns) with its parameters and return type.

## Strings for your mod

`game.ui.translate(key, ...)` looks up a Keyed language string from `Languages/<language>/Keyed/*.xml`. `{0}`, `{1}` and so on take the extra arguments. An unknown key returns the key, so a missing translation shows up instead of failing.

```xml
<TameAnomalies_Recruited>[TameAnomalies] Recruited {0}.</TameAnomalies_Recruited>
```

```lua
game.ui.message(game.ui.translate("TameAnomalies_Recruited", "Revenant"))
```

## Notes

- These functions change live game state. Test on a throwaway colony.
- Mood changes made through `set_need` and `add_thought` are normal game state and persist in the save.
- Each function is covered by the in-game smoke test in `tests/smoke`.
