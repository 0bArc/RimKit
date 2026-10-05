# Save safety

Players add your mod to a game in progress, update it, and sometimes remove it. A mod that breaks saves loses trust fast. This is how RimKit mods stay safe, and what to check.

## Removing a mod

Everything RimKit stores for a mod (`game.save`, `game.data`, Lua comp data) is saved as plain text inside neutral containers: `RimKit.MapComponent_LuaData`, `RimKit.WorldComponent_LuaData` and the game component. No type that belongs to your mod is written into the save. If a player removes your mod, the game loads without errors and the stored text stays unused.

Two things can still leave traces:

- **Defs you added.** A thing, hediff or thought def that is in a save when its mod is removed produces a warning and the object disappears. Keep defs in the save only as long as they are useful, and tell players in the description what happens on removal.
- **Lua classes in XML.** `RimKit.CompProperties_Lua` and the other proxy classes belong to RimKit, so the save stays loadable while RimKit is active. If RimKit itself is removed, the game cannot build those comps.

To give players a clean exit, offer a button that calls `game.save.purge(package_id)`. It removes everything the mod stored in the game and world scopes.

## Updating a mod

Keep a data version and migrate old data on load. `game.save.migrate` runs the steps that have not run yet, in order, and keeps the version of the last step that worked, so a failed migration is tried again at the next load:

```lua
local PKG = "my.mod"

game.events.on("game.loaded", function()
  game.save.migrate(PKG, {
    function()                              -- 1: first stored shape
      game.save.put(PKG, "game", "settings", { speed = 1 })
    end,
    function()                              -- 2: speed became a table
      local s = game.save.fetch(PKG, "game", "settings", nil, {})
      game.save.put(PKG, "game", "settings", { speed = { value = s.speed or 1 } })
    end,
  })
end)
```

Never reuse or reorder steps that have shipped: add a new step at the end.

`game.save.stamp(PKG)` records the mod's `mod_version` in the save and returns the version that wrote it before. Compare the two to notice a downgrade (a newer save loaded by an older mod) and refuse to touch data you do not understand.

## Renaming and changing fields

- **`package_id` never changes.** Saves refer to it. A new id is a new mod.
- **Def names never change** once a save may contain them. Add a new def and let the old one stay, or ship a def `Patch` that keeps the old name.
- **Stored keys:** treat them like a database column. Add keys freely, rename only inside a migration step that copies the old value.
- **Lua class names** (`luaClass` in XML) are looked up by name when a def loads. Renaming one in Lua without changing the XML silently runs the game's own behaviour, so keep them stable.

## A test for each

For every migration step, write a test with `rimkit mod test` that mocks `save.version` and `save.fetch` with the old data and checks what `save.put` receives.

```lua
t.it("step 2 wraps speed in a table", function()
  t.mock("save.version", 1)
  t.mock("save.set_version", true)
  t.mock("save.fetch", { speed = 3 })
  t.mock("save.put", true)
  t.emit("game.loaded", {})
  t.expect(t.calls("save.put")[1].args.value).to_contain("value")
end)
```
