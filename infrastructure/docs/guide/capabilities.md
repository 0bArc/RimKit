# Capabilities

A capability is a permission a mod declares in `meta.lua`. RimKit shows the list to the player and refuses the call when a mod uses something it did not declare.

```lua
meta.capabilities = { "hooks" }
```

| Capability | Lets the Lua use |
|------------|------------------|
| `reflect` | `game.reflect` and `rim.reflect`: read and call any member of any game object |
| `hooks` | `game.hooks`, `game.tweaks` and the `game.hooks.before` and `game.hooks.after` sugar: change what game methods do |
| `files` | `game.defs.write_xml`, `game.patch.write`, `game.dev.export_defs` and the diagnostics bundle: write files next to the mod |
| `dev` | `game.dev.eval` and `game.dev.reload`: run code from a string and reload another mod's Lua |

Everything else (reading and changing the colony through the kits, UI, saving, events) needs no capability.

## What happens

- A call to something undeclared raises `RK4001` with the capability name, so the mod author sees what to add.
- `meta.capabilities = {}` means the mod declared that it needs none, and any of the calls above fail.
- A mod that does not declare the field at all keeps full access. The RimKit hub (`Tab Mods`) marks them "not declared: everything is allowed". A mod that sets `meta.api_level = 1` is different: it gets exactly what it declares, and nothing when it declares nothing. `rimkit mod create` sets it.
- `rimkit mod check` finds the usage the Lua makes and fails when a capability it needs is not listed.
- The list is written to `About/RimKit.json` by `rimkit mod sync`. RimKit reads it when the mod loads.

## What it does not do

Capabilities are an honesty and safety check inside RimKit, not a sandbox against hostile code. A mod that ships its own C# assembly runs with full trust, like any RimWorld mod. The Lua sandbox, the threat scanner and the binary allowlist (see [security](security.md)) are what stop hostile Lua. Capabilities answer a different question: what does this mod say it will do, and does it stay inside that.

## Evaluating code

`game.dev.eval` and the dev tools console run Lua from a string, which the threat scanner cannot read in advance. They need Development mode on and either the RimKit tools themselves or a mod that declared `dev`. Do not ship a mod that asks for `dev` unless it is a developer tool.
