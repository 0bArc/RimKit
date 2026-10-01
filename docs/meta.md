# meta.lua

Mod identity lives in `meta.lua`. Do not edit About.xml by hand.

Author: **Team Stratware.win**. Kit package id: **stratware.rimkit**.

```lua
local meta = require("host.metadata")
meta.name = "Hello Lua"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.hellolua"
meta.version = "1.6"
meta.description = "Sample RimLuaKit mod"
meta.depends = { "brrainz.harmony", "stratware.rimkit" }
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
```

```text
rimkit mod sync
rimkit mod create MyMod
rimkit mod ship
```

| Field | Meaning |
|-------|---------|
| `name` | Display name |
| `author` | Team Stratware.win |
| `package_id` | Unique id (`stratware.mymod`) |
| `version` | Supported version shortcut |
| `depends` | Package ids |
| `load_after` | Load order |
