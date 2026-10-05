# RimKit Standard (RKS)

Status: Draft 1. Applies to every public name, operation, event, error and document that RimKit ships.

The key words MUST, MUST NOT, SHOULD, SHOULD NOT and MAY are used as defined in RFC 2119.

## 1. Purpose

RimKit lets Lua control RimWorld through a C# host and Harmony. The API will grow to cover most of the game, so it needs rules that keep it predictable for mod authors and cheap to maintain. RKS is that rule set. Each rule has a reason, stated with it, so a contributor can apply the rule to a case the text does not list.

## 2. Principles

1. **One source of truth per fact.** A name, version, tier or argument shape is defined once and generated everywhere else. Reason: hand copied facts drift (today the version string exists in three places).
2. **Explicit over implicit.** An argument has one documented key and one type. A unit is in the name when the value is not a plain count. Reason: positional guessing and per-op key names are the main source of ambiguity today.
3. **Fail loudly.** Every failure returns a stable error code. Silent nulls and wrong-type no-ops MUST NOT occur. Reason: a mod that silently does nothing is the most expensive bug to diagnose.
4. **Additive by default.** A released name MUST NOT change meaning. It may be deprecated, then removed at a major version. Reason: clients ship mods that outlive RimKit releases.
5. **Least power.** Each op declares a tier and a capability. Reflection and raw Harmony are the highest tier and are audited. Reason: Lua mods run as the player.
6. **Traceable.** Every op can be traced from Lua name to C# handler to game call to doc entry to test. Reason: the game updates, and a gap must be findable.

## 3. Naming (UNC)

The Unified Naming Convention is defined in [naming](../api/naming.md). RKS requires conformance. A name that does not match UNC MUST NOT be registered, except as a deprecated alias.

## 4. Stability tiers

| Tier | Meaning | Change policy |
| --- | --- | --- |
| Stable | Covered by tests and docs. | Changes only at a major version, after one minor release of deprecation. |
| Experimental | Works, may change. | May change at any minor version. Changes are listed in the changelog. |
| Advanced | Can reach arbitrary game code (`reflect`, raw Harmony). | No stability promise. Gated by settings. Every call is audit logged. |

Rules:

1. Every op MUST be registered with a tier and a `since` version. An op without them is rejected at registration.
2. A new op starts Experimental. Promotion to Stable needs a changelog entry, docs, a stub, an example and a passing test.
3. `ApiRegistry` MUST enforce the tier. Advanced ops are denied unless the matching setting is on (`rimkit.developer_reflect` for reflection).
4. The Lua module `require("rimkit")` exposes `stable`, `experimental` and `advanced` tables generated from the registry, not written by hand.

## 5. Versioning

1. RimKit uses Semantic Versioning for the package (`MAJOR.MINOR.PATCH`).
2. A single `RimKitVersion` value is the only place the version is written. The build copies it to the C# constant, the native define, the Lua module and the VS Code extension. A mismatch fails the build.
3. `api_level` is an integer that increases when the Stable or Experimental surface gains an op. `assert_api(n)` fails when `n` is greater than the running level.
4. Release dates in changelogs and manifests use ISO 8601 calendar dates (`YYYY-MM-DD`). Durations in docs use ISO 8601 or an explicit unit name.
5. Deprecated names MUST be removed no earlier than the next major version. Until then each logs one warning per session, naming the canonical replacement.
6. Game versions. RimKit supports the current stable RimWorld release and the one before it. A kit function that exists only in newer games MUST be guarded with `game.version.at_least`, and a hook whose target differs between game versions MUST be installed with `game.version.hook`. A function that cannot work on the running game version raises `RK3003`.
7. Persisted type names MUST NOT change. Saves and settings refer to the original `RimLuaKit` namespace for `GameComponent_PawnControl`, `RimLuaModDataComponent` and `RimLuaSettings`. The rest of the host uses the `RimKit` namespace.
8. Typed results. A function declared to return a game object returns the wrapped object (`RimPawn`, `RimThing`, `RimMap`, `RimFaction`), and a list of them returns a table of wrapped objects. Every function accepts the wrapped object or its integer handle where it takes a game object. Functions from before api level 1 may still return plain integer handles, and that is the only difference between the two. They move to wrapped objects at api level 1.

## 6. Errors

1. An op failure returns an error object with `code`, `op` and `message`. Lua raises it as an error whose message starts with the code.
2. Codes are `RK` plus four digits. The first digit is the class:

| Class | Range | Meaning |
| --- | --- | --- |
| 1 | RK1000 to RK1999 | Argument errors (missing, wrong type, out of range) |
| 2 | RK2000 to RK2999 | Handle errors (stale, wrong kind, null) |
| 3 | RK3000 to RK3999 | Game state errors (no map, not spawned, DLC inactive) |
| 4 | RK4000 to RK4999 | Permission errors (tier denied, sandbox, capability) |
| 5 | RK5000 to RK5999 | Host errors (unexpected exception, version mismatch) |

3. Codes are never reused. A retired code stays reserved. `RK1002` means a name that was removed at api level 1 was used by a mod that opted in with `meta.api_level = 1`.
4. The `message` is for people and MAY change. The `code` is for programs and MUST NOT.

## 7. Arguments and returns

1. The subject of an op is the key `h`. A second game object is `target` or `other`. A def is `def` (a `defName` string). A scalar is `value` or `amount`. No other per-op names for these roles.
2. Ratios are floats from 0 to 1 and the name ends in `_ratio` when ambiguous. Percent values MUST NOT be used.
3. Time values end in `_ticks`, `_seconds` or `_days`. Distances end in `_cells`. Rotation values state `degrees` or use `Rot4` names.
4. Floats follow IEEE 754 binary32 as the game does. Hosts MUST NOT round values before returning them.
5. Identifiers are `defName` strings. Display text is `label`. A name MUST NOT mix the two roles.
6. A returned game object is a typed handle (kind plus id plus generation). Collections return Lua arrays of handles. Absent values return `nil`, never an empty string.
7. Handles from a removed map or a previous game MUST fail with an RK2xxx error, never resolve to another object.

## 8. Events

1. Event names are `<domain>.<past_tense_verb>` (see UNC).
2. An event delivers a payload table with named fields. A single bare handle is not a payload.
3. A hot event (called many times per tick) is opt in. Its Harmony patch installs on first subscription and is removed on last unsubscribe.
4. Handler errors are caught, logged with mod id and event name, and do not stop other handlers or the game.
5. The queue has a documented per tick budget. Dropped events increment a counter that `game.diagnostics` exposes.

## 9. Harmony and reflection use

1. Hooks MUST name the target by type, method and, when overloaded, parameter types.
2. A hook declares priority when order matters. Default priority is the Harmony default.
3. A hook that can change gameplay (prefix that skips, result writes) is Advanced.
4. Reflection stays gated by `rimkit.developer_reflect`, limited to the allowed game namespaces, and every call is written to the audit log with mod id, target and outcome.
5. The host MUST NOT expose file, process, network or assembly loading through any tier.

## 10. Documentation

1. Every domain has a page listing each op with name, tier, `since`, arguments, returns, errors and the Harmony targets it relies on.
2. Docs use short formal sentences and contain no em dash or en dash, per the repository markup rule.
3. Every Experimental or Stable op has a stub and at least one example.

## 11. Conformance and tests

An automated lint checks, on every build:

1. Every registered op id matches UNC and has a tier and `since`.
2. Every op has a doc entry and a stub.
3. Every deprecated alias maps to a registered canonical name.
4. Argument keys follow section 7.
5. The version is identical in all generated locations.

A change that breaks the lint MUST NOT be merged.

## 12. Revision

RKS changes through a pull request that states the problem and the reason for the new rule. The draft number increases with each accepted change.
