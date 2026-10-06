# Mods we can write

A planning list of Lua mods that the unified API makes possible. Each entry names the API it needs. "Today" means it can be built now with named events, `game.hooks` and `game.reflect`. "Kit" means it is cleaner once the domain kit exists (see [domains](api/domains.md) and the [inventory](concepts/game-inventory.md)).

Legend for effort: S is an afternoon, M is a few days, L is a few weeks.

## 1. Quality of life

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Smart notifications | Mutes or groups letters and messages by rule, with a per-type allowlist | `letter.received`, `message.shown`, hooks on `LetterStack.ReceiveLetter` | Today | S |
| Alert filter | Hides chosen alerts and adds custom ones (low steel, empty freezer) | `game.time`, `game.maps`, `game.ui` | Today | M |
| Better pause | Auto-pauses on raid, mental break, death, downed colonist, research finished | `incident.fired`, `pawn.died`, `mental_state.started`, `research.finished`, `game.time` | Today | S |
| Speed profiles | Sets game speed by situation (slow in fights, fast when idle) | `time.speed_changed`, `game.time`, `job.started` | Today | S |
| One-click loadouts | Saves and applies weapon and apparel sets to colonists | `game.pawns` equipment, apparel, inventory | Kit | M |
| Bulk work priorities | Applies a priority template to many pawns at once | `game.work`, selection | Today | S |
| Stack and haul tidy | Merges partial stacks and fixes stray items in stockpiles | `game.things`, `game.maps` | Kit | M |
| Colony stats panel | Live dashboard: wealth, food days, mood spread, skill gaps | `game.ui.panel`, `game.maps`, `game.pawns` | Today | M |
| Quick-bind hotkeys | Custom keys for draft all, select idle, jump to alert | `game.input`, `game.ui` | Today | S |
| Save-scum guard | Warns before overwriting an autosave, keeps rolling backups of the save list | `game.saving`, `game.util` | Today | S |

## 2. Colony management and automation

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Auto-draft on threat | Drafts and positions colonists when hostiles appear | `incident.fired`, `game.pawns` drafted, `game.paths` | Today | M |
| Shift scheduler | Generates timetables from a template (night owls, day crews) | `Pawn_TimetableTracker` through `game.reflect`, later `game.pawns` | Kit | M |
| Resource governor | Pauses crafting bills when stock is high, resumes when low | `game.buildings` bills, `game.things` counts | Kit | L |
| Smart doctor | Orders surgery and tending by triage rules | `game.pawns` health, `surgery`, `hediff.added` | Today | M |
| Immigration control | Accepts or rejects wanderers and refugees by traits and skills | `letter.received`, `quest.added`, `game.quests` | Kit | M |
| Prisoner pipeline | Auto-recruits, releases or exchanges prisoners by policy | `game.pawns` guest, `game.factions` | Kit | M |
| Trade assistant | Suggests best sells and keeps a shopping list across caravans | `game.economy`, `game.world` | Kit | L |
| Zone painter | Applies zone and area templates to new bases | `game.maps` zones, areas, designations | Kit | L |
| Power manager | Balances batteries and switches loads by priority | `game.buildings` power | Kit | M |
| Farm planner | Chooses crops by season, soil and nutrition need | `game.plants`, `game.maps`, `game.weather` | Kit | L |

## 3. Combat and threats

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Raid director | Rewrites raid points, arrival edges and strategy rules | hooks on `IncidentWorker.TryExecute`, `game.incidents` | Today | M |
| Damage numbers | Floating combat text for hits and heals | `thing.damaged`, `game.ui` | Today | S |
| Armor and stat inspector | Shows effective stats and hit chances in the inspect pane | `game.reflect`, `game.combat` | Kit | M |
| Kill feed | Chat-style log of kills and deaths with killer and weapon | `pawn.died`, `thing.damaged` | Today | S |
| Friendly fire rules | Per-faction and per-weapon damage rules | hooks on `Thing.TakeDamage`, `Pawn_HealthTracker.PreApplyDamage` | Today | M |
| Turret tuner | Edits turret range, cooldown and targeting rules | `game.reflect` on verb props, `game.defs` | Kit | M |
| Boss encounters | Scripted fights with phases, adds and rewards | `game.incidents`, `game.maps`, `thing.damaged`, `pawn.died` | Kit | L |
| Last stand mode | Buffs colonists when the colony is nearly dead | `pawn.died`, `hediff.added`, `game.pawns` | Today | S |

## 4. Storytelling and events

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Custom storyteller | A Lua storyteller with its own pacing and rules | `game.storyteller`, `game.incidents`, hooks on `Storyteller.StorytellerTick` | Kit | L |
| Scripted quests | Quests written in Lua with objectives, rewards and branches | `game.quests`, `quest.added`, `game.ui` choice letters | Kit | L |
| Chain events | Event sequences where each outcome unlocks the next | `incident.fired`, `game.incidents`, `game.time` | Today | M |
| Colony chronicle | Auto-written diary of the colony in the history tab | all event hooks, `game.ui` | Today | M |
| Random encounter packs | Merchants, wanderers and mysteries with branching choices | `game.incidents`, `game.ui`, `game.world` | Kit | M |
| Seasonal festivals | Recurring gatherings with mood bonuses and rituals | `game.time`, `game.ideology`, thoughts | Kit | M |
| Difficulty scaler | Adapts threat to colony wealth, deaths and recent success | `game.storyteller`, `game.maps` wealth | Kit | M |

## 5. Pawns, social and mind

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Personality engine | New trait-like quirks driven by Lua rules | `game.pawns` traits, thoughts, mental states | Kit | L |
| Relationship drama | Rivalries, crushes and feuds with visible consequences | `game.pawns` relations, interactions, thoughts | Kit | L |
| Backstory editor | Pick or reroll backstories at pawn creation | `game.pawns` story, `game.defs` | Kit | M |
| Mood explainer | Plain-language reasons for each pawn's mood and how to fix them | `game.pawns` thoughts and needs | Kit | M |
| Skill coach | Suggests tasks that train weak skills without hurting output | `game.pawns` skills, `game.work` | Kit | M |
| Memorials | Graves, plaques and a memorial wall for the dead | `pawn.died`, `game.things`, `game.ui` | Today | M |
| Legacy system | Children inherit traits or a story from their parents | `game.biotech`, `game.pawns` | Kit | L |
| Mental break rules | Custom break types and recovery rules | `game.pawns` mental states, `mental_state.started` | Today | M |

## 6. World, factions and economy

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Living world | Settlements grow, trade and fight on their own | `game.world`, `game.factions`, `game.time` | Kit | L |
| Diplomacy overhaul | Treaties, tribute, alliances and betrayals | `game.factions` goodwill, relations, letters | Kit | L |
| Caravan manager | Routes, supplies and automatic rest stops | `game.world` caravans, `game.paths` | Kit | L |
| Dynamic prices | Market swings with events, season and faction mood | `game.economy`, `game.defs` stats | Kit | M |
| Contracts board | Repeatable delivery and defend contracts | `game.quests`, `game.economy` | Kit | L |
| Site and loot packs | New map sites with scripted layouts and rewards | `game.world`, `game.maps` generation hooks | Kit | L |
| Faction leaders | Persistent named leaders with goals | `game.factions`, `game.pawns` | Kit | M |
| Biome events | Weather and hazard packs per biome | `game.weather`, `game.incidents` | Today | M |

## 7. Rules, balance and sandbox

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Stat tweaker | Live edits to any StatDef, with presets | `game.defs`, `game.reflect` | Kit | M |
| Hardcore rules | Permadeath variants, no pause, scarcity modes | `game.time`, hooks, `game.saves` | Today | M |
| Challenge scenarios | Scripted start conditions and win goals | `game.incidents`, `game.maps`, `game.ui` | Kit | M |
| Cheat console | Safe in-game console for spawning, setting stats and teleporting | `game.reflect`, `game.maps`, `game.things` | Today | M |
| Map editor | Paint terrain, roofs and things in play | `game.maps`, `game.things` | Kit | L |
| Pawn forge | Create and edit pawns with a UI | `game.pawns`, `game.defs`, `game.ui` | Kit | L |
| Rule sets | Share a bundle of tweaks as one file | `game.data`, `game.config` | Today | S |
| Replay recorder | Records colony state per day for later timelapse | all events, `game.saves` | Kit | L |

## 8. UI and presentation

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Custom tabs and windows | New main tabs and inspector tabs written in Lua | `game.ui` windows, inspect tabs | Kit | M |
| Gizmo packs | Extra buttons on pawns and buildings | `game.ui` gizmos | Kit | M |
| Overlays | Heat maps for beauty, wealth, threat, nutrition | `game.maps`, `game.ui` draw | Kit | L |
| HUD themes | Colours, fonts and layout presets | `game.ui`, `game.config` | Kit | M |
| Tutorials and onboarding | In-game guided steps for a mod pack | `game.ui`, events | Today | S |
| Accessibility pack | Larger text, colour-blind palettes, audio cues | `game.ui`, `game.audio`, `game.config` | Kit | M |
| Mod settings hub | One place to tune every Lua mod | `game.config`, `game.ui` | Today | S |
| Stream overlay | Exposes colony state to a local file for streaming tools | `game.util` export, all events | Today | S |

## 9. Content and DLC kits

| Mod | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Ideology rules | New precepts, rituals and memes written in Lua | `game.ideology` | Kit | L |
| Royalty extras | New titles, permits and psycasts | `game.royalty`, `game.defs` | Kit | L |
| Biotech lab | Custom genes, xenotypes and mech types | `game.biotech`, `game.defs` | Kit | L |
| Anomaly expansions | New entities, containment rules and rituals | `game.anomaly`, existing `game.anomalies` | Today | M |
| Odyssey travel | Gravship routes, orbital sites and new missions | `game.odyssey`, `game.world` | Kit | L |
| Entity tamer | Claim and command anomaly entities (the tame_anomalies prototype grown up) | `game.anomaly`, `game.pawns` control | Today | M |
| New factions and cultures | Full faction packs defined in Lua | `game.defs`, `game.factions` | Kit | L |
| Animal husbandry | Breeding lines, traits and farm management | `game.pawns` training, bonds, `game.plants` | Kit | L |

## 10. Tools for modders

| Tool | What it does | API | Ready | Effort |
| --- | --- | --- | --- | --- |
| Event recorder and replay | Records events with payloads and replays them to test a mod | event catalog, `game.data` | Today | M |
| Hook inspector | Lists live hooks, their cost and who owns them | `game.hooks.list`, audit log | Today | S |
| Live reflection browser | Browse any game object in an in-game window | `game.reflect.members`, `game.ui` | Today | M |
| Def explorer | Search and diff Defs across mods | `game.defs`, `game.reflect` | Kit | M |
| Performance profiler | Per-mod time spent in hooks and ticks | hooks, `game.time` | Kit | M |
| Test harness | Runs scripted scenarios and asserts outcomes (the smoke test, generalised) | all kits | Today | M |
| Mod pack manager | Enables, orders and configures mod groups from Lua profiles | `game.config`, `game.util` | Kit | L |

## Suggested first wave

These reuse what is already built and prove the platform to other modders:

1. **Better pause** and **Kill feed**: only events and `game.ui`. Show off the payload model.
2. **Cheat console**: shows `game.reflect` and is useful to every modder.
3. **Raid director**: shows hooks that change results.
4. **Colony chronicle**: shows persistence through `game.data` plus many events.
5. **Entity tamer**: extends the existing prototype and the Anomaly kit.
6. **Live reflection browser** and **Hook inspector**: grow the developer audience.

## What unlocks the rest

Most "Kit" rows depend on these domains, in this order of impact: `game.pawns` (skills, needs, thoughts, relations), `game.things` and `game.maps` (comps, cells, zones), `game.time`, `game.incidents` with `game.storyteller` and `game.quests`, `game.ui` (gizmos, tabs, overlays), then `game.world` and the DLC domains.
