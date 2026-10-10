# Gaps and next steps

What is still open, with a check for each item. The full phase tables are in [Roadmap and status](missing.md). IDs here match those tables.

State: 105 kits, 571 functions, 116 events, 13 tweak points, 31 Lua class families. RimKit runs on [Luau](guide/luau.md). The in-game smoke suite passed 434 of 434 before the second event pass and the hot reload work, and has not run since. The `damage` in-game tests (3) passed in the game on 2026-10-08. The native tests, host tests (45, including every event against the game assemblies), the reload and in-game runner fixtures, lints and example mod tests pass without the game.

Overall rating: a solid beta. Tweak, automation, UI and content mods (quests, scenarios, rituals, world generation steps) have a Lua path. It is ready to promise to outside authors once the checks below pass and someone outside has tried the quickstart.

## Still open

### Verify in a real game

These parts need a person looking at the game. The smoke suite already covers registration, state, errors and that callbacks run.

| ID | Item | Check |
|----|------|-------|
| V-01 | F11 dev tools window | Opens, console runs, event recorder lists events, profiler shows numbers, the Reflect tab opens a member |
| V-02 | Mods tab in the RimKit hub | Shows capabilities per mod, deactivate works |
| V-03 | Mod failure letter | Break a mod on purpose, the letter names it and "disable" sticks |
| V-04 | Hot reload with UI | Edit a mod with a window, a gizmo and a hotkey, reload, nothing doubles and the old gizmo is gone. Also a mod with `game.stats.add_factor`: after three reloads the factor applies once |
| V-05 | TurboTime key presses | `=` and `-` change the speed |
| V-06 | Commander key presses | `G` sends the selection, `Y` drafts |
| V-07 | Multi-version folders | A mod with two `version_folders` loads the right Lua per game version |
| V-08 | `rimkit publish` | Run against Steam with `--visibility 2` (private) first, then delete the test item |
| V-09 | Example mod Defs | The example mods that define XML follow the smoke mods but have not been played |
| V-10 | Damage numbers | `damage` shows a number above a hurt humanlike pawn, red for colonists |
| V-12 | New event patches | Run the smoke suite once, it resolves all 116 event targets. Then play ten minutes with the event recorder on and look for events that never fire: `thing.hauled`, `thing.repaired`, `building.deconstructed`, `immunity.gained`, `room.changed` |
| V-13 | Hot reload in the game | The handler part passed in the `damage` in-game test. Still to see: a window and a gizmo are gone after a reload |

### API and content

| ID | Item | Done when |
|----|------|-----------|
| G-01 | Freeze the API | Promote ready Experimental kits to Stable, set `api_level = 1` for everyone. Do this after the in-game checks. Plain integer returns from handle functions move to wrapped objects at the same time |
| D-02 | Odyssey launching, travel and orbital sites | A mod can start a gravship launch, read the travel cost and create an orbital site. The cause of the quick test hang with Odyssey must be found first |
| C-09 | Buildings defined without XML | A building def made from Lua. The class side is done (`building`, `door`, `storage` families), the def side still needs XML for graphics and stats |
| D-03 | DLC depth audit | List the calls the ten most popular mods per DLC need and add what is missing |

### Tooling

| ID | Item | Done when |
|----|------|-----------|
| T-03 | Run in-game tests on a chosen save | `rimkit mod test --in-game --save <name>` loads a given save instead of the quicktest map. The launcher and runner exist (P6-11), they always use the quicktest map today |

### Release

| ID | Item | Done when |
|----|------|-----------|
| R-01 | Workshop release | Deploy the host, native core and allowlist with `release.js`, verify the hashes, publish |
| R-02 | An outside author | One person with no project context makes a mod from the quickstart. Write down every stumble |
| R-03 | Hook-target check on every push | The host job runs on a self-hosted runner only. Decide whether to keep it manual |

### Events

[Roadmap and status](missing.md) lists the three wishes that cannot be a single event and what to use instead. Add others when a real mod asks for them: one catalog row, one patch body, and the host test checks it.

## Suggested order

1. Walk through the checklist above (V-01 to V-10) in one RimWorld session. Fix what breaks.
2. Have one outside author try the quickstart (R-02) and fix what they stumble on.
3. Publish (R-01).
4. Freeze the API (G-01).
5. Odyssey launching and travel (D-02), then `LordJob` and Lua needs.
