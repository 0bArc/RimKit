# Example mods

Ten small mods you can read, copy and change. Each one has tests that run without the game, passes `rimkit mod check`, and uses `meta.api_level = 1` (strict mode). Run any of them with:

```text
rimkit mod test src/examples/SmartPause
rimkit mod check src/examples/SmartPause
rimkit mod ship src/examples/SmartPause
```

| Mod | Category | What it teaches |
|-----|----------|-----------------|
| [RimKitDemo](RimKitDemo) | A tour | A window, a tweak with a setting, an event, another mod's functions through `game.interop` |
| [TurboTime](../../guide/01-speed-mod/final/TurboTime) | Stat and balance tweaks | A tweak on game speed, hotkeys, a setting, translation, tests (video guide episode 1) |
| [Commander](../../guide/02-pawn-control/final/Commander) | Pawn behavior | Selection, mouse cell, giving orders, drafting (video guide episode 2) |
| [SmartPause](SmartPause) | Quality of life | Named events, settings, pausing, a function offered to other mods |
| [ColonyStats](ColonyStats) | Numbers and info panels | A live widget table in a window, a rebindable key, reading the colony |
| [LuckyIncident](LuckyIncident) | New content with behavior | An incident whose worker is a Lua class, a Def in XML, a letter |
| [GlowLamp](GlowLamp) | New buildings with behavior | A building with a Lua component, data saved with the object |
| [CheerfulMood](CheerfulMood) | Mood and mind | A thought whose condition is Lua, reading the pawn's map and weather |
| [ComfortNeed](ComfortNeed) | Needs and mood | A new need made entirely in Lua with no XML: `game.needs.define` and a need class |
| [MoreChairs](MoreChairs) | New items and textures | A whole mod in Lua: Defs written in Lua (`Defs/chairs.lua`, turned into XML by sync), PNG pictures in Textures, sitting logic and a mood boost in Lua |
| [CompatGuard](CompatGuard) | Compatibility | Cooperating with another mod only when it is installed |
| [RuleSet](RuleSet) | Rules and sandbox | Changing Defs from one table, undoing them, an options page |

## What has and has not been checked

The tests run against a mock host: they prove the logic and that every call has the right arguments. The Defs in LuckyIncident, GlowLamp and CheerfulMood follow the shapes of the smoke test mods, which run in the real game, but these mods themselves have not been played. Treat the first run in the game as part of reading them, and check the log for lines starting with `[RimKit]`.
