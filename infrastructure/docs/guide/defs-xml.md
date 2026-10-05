# Defs

Content that RimWorld loads at startup (things, recipes, research, hediffs, thoughts, key bindings, translations) stays as ordinary XML under `Defs/`. Lua handles runtime behavior. XML handles data.

```
MyMod/Defs/KeyBindings/MyKeys.xml
MyMod/Defs/HediffDefs/MyMarker.xml
```

## Rules

- Prefix every `defName` with your mod, for example `MyMod_Marker`, so mods do not collide.
- **Anything a save refers to must be an XML Def.** If a Def is created only at runtime, a save that contains it fails to load it next time (`Could not load reference to ...`, `had a null def`). RimKit's own marker hediff had this bug and now lives in `Defs/HediffDefs/RimLua_Controllable.xml`.
- Runtime writes with `game.defs.register_thing` generate XML, and the game must restart to load it.
- A key binding is a Def: define a `KeyBindingDef`, then read it in Lua with `game.input.binding_just_pressed("DefName")`.

## Key binding example

```xml
<Defs>
  <KeyBindingDef>
    <defName>MyMod_Toggle</defName>
    <label>My Mod: toggle</label>
    <category>Game</category>
    <defaultKeyCodeA>Backslash</defaultKeyCodeA>
  </KeyBindingDef>
</Defs>
```

Pick a default that the game does not use. A clash shows as `Key binding conflict` in the log. The game's own defaults include the digits 1 to 4, F6 and F7, the brackets (dev mode), comma, period and slash.

## Strings

Put text in `Languages/English/Keyed/MyMod.xml` and read it with `game.ui.translate("MyMod_Key", arg0, arg1)`. `{0}`, `{1}` in the text take the arguments. Other languages add their own `Languages/<name>/Keyed` file. An unknown key returns the key, so a missing translation is visible.

```xml
<LanguageData>
  <MyMod_Hello>Hello {0}</MyMod_Hello>
</LanguageData>
```

## Defs in Lua

You do not have to write the XML by hand. Put a Lua file in `Defs/` and call `def` for each thing you want to add. `rimkit mod sync` (and `rimkit mod ship`, which syncs first) turns every `Defs/*.lua` into a `Defs/*.xml` next to it. The game reads that XML like any other Def, and the Lua file is the one you edit.

```lua
-- Defs/chairs.lua
def("ThingDef", "MyMod_Stool", {
  label = "stool",
  description = "A plain wooden stool.",
  graphicData = { texPath = "Things/Building/Furniture/MyMod_Stool", graphicClass = "Graphic_Single", drawSize = vec(1, 1) },
  statBases = { MaxHitPoints = 60, WorkToBuild = 1500, Comfort = 0.45, Beauty = 1 },
  stuffCategories = { "Woody" },
  costStuffCount = 20,
  building = { isSittable = true },
}, { parent = "FurnitureWithQualityBase" })
```

| Part | Meaning |
|------|---------|
| `def(kind, defName, fields, opts)` | One Def. `kind` is the Def type (`ThingDef`, `ThoughtDef`, `RecipeDef`, ...) |
| `fields` | A plain table. Nested tables become nested elements and a list becomes `<li>` items |
| `_class` inside a table | Sets the `Class` attribute, for example `{ _class = "CompProperties_Forbiddable" }` |
| `_attrs` inside a table | Extra attributes on that element |
| `opts.parent`, `opts.name`, `opts.abstract` | `ParentName`, `Name` and `Abstract="True"` |
| `vec(x, z)` and `rgb(r, g, b)` | Give the `(x,z)` and `(r,g,b)` text the game expects |

The script runs in a bare Lua state, so you can use `local`, functions, loops and `string`, `table` and `math` to generate many Defs from one helper. It cannot see the game. A picture for an item or building goes in `Textures/` and is named by `texPath`, without the extension. `rimkit mod check` fails when a `texPath` has no picture.

Behavior goes in `Lua/`: a Def that needs code names a Lua class (see [Lua classes](../api/classes.md)), and everything else, such as events, settings and thoughts you hand out, is ordinary `Lua/main.lua`. [MoreChairs](../../../src/examples/README.md) is a whole mod written this way.

`rimkit mod sync` refuses to overwrite an XML file it did not generate, so a hand-written `chairs.xml` next to `chairs.lua` is safe: it stops with a message. The older `defs.lua` in the mod root, which only makes simple items, still works.

## Strings in Lua

Translation files can be Lua too. `Languages/English/Keyed/MyMod.lua` returns a table of key and text, and `rimkit mod sync` writes the `LanguageData` XML next to it:

```lua
-- Languages/English/Keyed/MyMod.lua
return {
  MyMod_Loaded = "My mod is loaded.",
  MyMod_Paused = "Paused: {0}",
}
```

Use them with `game.ui.translate("MyMod_Paused", reason)`. Keys are sorted in the generated file, and a key that is not a plain name (letters, digits, underscore, dot) stops the sync with a message. Another language is the same file under `Languages/German/Keyed/`. `rimkit mod create` writes its first string file this way.
