local meta = require("host.metadata")
meta.name = "LuaModTemplate"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.luamodtemplate"
meta.version = "1.6"
meta.description = "Template Lua mod for RimLuaKit."
meta.depends = { "brrainz.harmony", "stratware.rimkit" }
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
