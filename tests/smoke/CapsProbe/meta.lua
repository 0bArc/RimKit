local meta = require("host.metadata")
meta.name = "RimKit Capabilities Probe"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.rimkit_capsprobe"
meta.version = "1.6"
meta.mod_version = "9.9.9"
meta.description = "Smoke test mod that declares no capabilities and checks that RimKit refuses what it did not declare."
meta.capabilities = { "files" }
meta.depends = { "brrainz.harmony", "stratware.rimkit" }
meta.load_after = { "ludeon.rimworld", "brrainz.harmony", "stratware.rimkit" }
return meta
