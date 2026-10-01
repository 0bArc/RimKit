local meta = require("host.metadata")
meta.name = "Hello Lua"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.hellolua"
meta.version = "1.6"
meta.description = "RimLuaKit sample: block food jobs when colony nutrition is low."
meta.depends = { "brrainz.harmony", "stratware.rimkit" }
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
