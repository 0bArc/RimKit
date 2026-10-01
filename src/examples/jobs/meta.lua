local meta = require("host.metadata")
meta.name = "Jobs Test"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.jobs"
meta.version = "1.6"
meta.description = "RimLuaKit Phase 1-3 test mod: jobs, UI window, float menu, config, defs, logs."
meta.depends = { "brrainz.harmony", "stratware.rimkit" }
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
