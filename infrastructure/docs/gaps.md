# Gaps and next steps

Working list after the second pass. It records what the first version of this page asked for, what was done, and what is still open. The full phase tables are in [What is missing](missing.md). IDs here match those tables.

State: 105 kits, 568 functions, 87 events, 13 tweak points, 31 Lua class families. Everything below that says "done" was checked by the native tests, the host build, the lints and the headless mod tests. **None of the work in this pass has been run in the game**, because the in-game smoke suite needs RimWorld to launch. Smoke checks for it are written (quest nodes, Ideology styles, the new families) and are the first thing to run.

Overall rating: a solid beta. Tweak, automation, UI and now content mods (quests, scenarios, rituals, world generation steps) have a Lua path. It is not ready to promise to outside authors until the in-game checks pass and someone outside has tried it.

## Done in this pass

| ID | What was done |
|----|---------------|
| A-01 | Every legacy function that took a pawn, thing, map or faction handle now also takes the wrapped object. A native test calls each form. |
| A-06 | The mock host is strict: a mock for an op that does not exist, a wrong argument type, a missing required argument or an extra argument raises `RK1001`. This found one stale test and is documented in [testing](guide/testing.md). |
| A-02 to A-05 | `meta.api_level = 1` opts a mod in to the level 1 rules now: errors raise, old names raise `RK1002`, only declared capabilities work. `rimkit mod create` sets it, `rimkit mod check` reports old names. See [stability](api/stability.md). The whole API still stays at level 0 until the owner decides to freeze it (G-01). |
| C-01 to C-04, C-06 | New Lua class families: quest node (with `game.quests.slate_get`, `slate_set`, `slate_set_object`), world gen step, scenario part, ritual outcome, conditional think node. [classes](api/classes.md) |
| D-01 | Ideology: add and remove memes with the game's own conflict checks, style categories, rename. |
| D-02 (part) | Odyssey: engine details (fuel, range, cooldown, substructure, components) and space maps. Launching, travel and orbital sites are still not exposed. |
| T-01 | Hot reload removes the gizmos, alerts, tabs, pawn columns, designators, status lines and settings pages the old version registered, and registering the same tab or column again replaces it instead of failing. |
| T-02 | A Reflect tab in the F11 window browses any game object. A headless test builds every tab. |
| Lua defs | Defs and strings can be written in Lua: `Defs/*.lua` and `Languages/*/Keyed/*.lua` become XML on `rimkit mod sync`, ship, or `rimkit mod defs <dir>`. The examples, the guide mods, the kit itself and the smoke mods use it. [defs](guide/defs-xml.md) |
| C-05 | `game.needs.define` creates a need from Lua with no XML: the def is made when the mod loads, the game draws the bar, saves the level and adds it to pawns. The class gets `interval(pawn, level)`, and `fall_per_day` is handled by the game. |
| C-07 | Whole-thing Lua classes for buildings: `building` (spawn, despawn, destroy, tick, damage, inspect text), `door` (who may open or pass) and `storage` (received and lost). |
| T-04 | Ten example mods with tests, in [src/examples](../../src/examples/README.md). CI runs all of them. |
| T-05 | The cookbook has one recipe for every mod idea (83), and `check-docs` fails when one is missing or when an example calls a function that does not exist. |
| T-06 | The guide scripts and finished mods match the modern template and no longer need a handle helper. |
| Events | Four new events: `ability.used`, `shot.fired`, `pawn.left_map`, `letter.opened`. |
| Bugs found | `rimkit migrate` rewrote op names inside string literals such as `t.mock("config.get")`. It no longer does. `T.run` left the mock host on after a test run. One native test had never compiled because its build output was hidden. |

## Still open

### Verify in a real game

Nothing here has been seen working by a person. Do this first, in one RimWorld session.

| ID | Item | Check |
|----|------|-------|
| V-00 | Run the smoke suite | It was 421 of 421 before this pass. The new checks are the quest node, the five new class families, Ideology styles and renaming. Odyssey stays excluded because it hangs the quick test |
| V-01 | F11 dev tools window | Opens, console runs, event recorder lists events, profiler shows numbers, the Reflect tab opens a member |
| V-02 | Mods tab in the RimKit hub | Shows capabilities per mod, deactivate works |
| V-03 | Mod failure letter | Break a mod on purpose, the letter names it and "disable" sticks |
| V-04 | Hot reload with UI | Edit a mod with a window, a gizmo and a hotkey, reload, nothing doubles and the old gizmo is gone |
| V-05 | TurboTime key presses | `=` and `-` change the speed |
| V-06 | Commander key presses | `G` sends the selection, `Y` drafts |
| V-07 | Multi-version folders | A mod with two `version_folders` loads the right Lua per game version |
| V-08 | `rimkit publish` | Run against Steam with `--visibility 2` (private) first, then delete the test item |
| V-09 | RimKitDemo and the new examples | LuckyIncident, GlowLamp and CheerfulMood define XML that follows the smoke mods but has not been played |
| V-10 | The four new events | They patch `Ability.Activate`, `Verb.TryCastNextBurstShot`, `Pawn.ExitMap` and `ChoiceLetter.OpenLetter`. Run the hook-target check in game |

### API and content

| ID | Item | Done when |
|----|------|-----------|
| G-01 | Freeze the API | Promote ready Experimental kits to Stable, set `api_level = 1` for everyone, remove deprecated aliases. Do this after the in-game checks, not before. Plain integer returns from older functions move to wrapped objects at the same time |
| D-02 | Odyssey launching, travel and orbital sites | A mod can start a gravship launch, read the travel cost and create an orbital site. The cause of the quick test hang with Odyssey must be found first |
| C-09 | Buildings defined without XML | A building def made from Lua. The class side is done (`building`, `door`, `storage` families), the def side still needs XML for graphics and stats |
| D-03 | DLC depth audit | List the calls the ten most popular mods per DLC need and add what is missing |

### Tooling

| ID | Item | Done when |
|----|------|-----------|
| T-03 | Scenario saves in the game | `rimkit mod test --in-game` launches RimWorld on a save, runs a Lua scenario, prints pass or fail. Needs a launcher that never touches the player's mod list |

### Release

| ID | Item | Done when |
|----|------|-----------|
| R-01 | Deploy 0.10.0 | The Workshop copy of RimKit is still the 0.9 build. Deploy the new host, native core and allowlist with `release.js`, verify hashes. Needs the owner's go ahead |
| R-02 | An outside author | One person with no project context makes a mod from the quickstart. Write down every stumble |
| R-03 | Hook-target check on every push | The host job runs on a self-hosted runner only. Decide whether to keep it manual |
| R-04 | Commit | Nothing from this pass is committed. The tree has `guide/`, `src/examples`, the new host files and the moved docs. Review and commit in logical pieces when asked |

### Events

[What is missing](missing.md) lists the event groups that still have no event. Add the ones a real mod asks for.

## Suggested order for next time

1. Run the smoke suite and the checklist above (V-00 to V-10) in one RimWorld session. Fix what breaks.
2. Have one outside author try the quickstart (R-02) and fix what they stumble on.
3. Deploy 0.10.0 (R-01) once the smoke suite is green.
4. Freeze the API (G-01) after that.
5. Odyssey launching and travel (D-02), then `LordJob` and Lua needs (C-08, C-05).
