local meta = require("host.metadata")
meta.name = "In-game test fixture"
meta.author = "RimKit"
meta.package_id = "rimkit.fixture.itest"
meta.version = "1.6"
meta.mod_version = "0.1.0"
meta.description = "A mod with in-game tests, used to test the in-game test runner without a game."
meta.capabilities = { "dev" }
return meta
