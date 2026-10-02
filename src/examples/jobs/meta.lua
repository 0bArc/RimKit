local meta = require("host.metadata")
meta.name = "Jobs Test"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.jobs"
meta.version = "1.6"
meta.description = "RimKit Phase 1-3 test mod: jobs, UI window, float menu, config, defs, logs."
meta.depends = {
  {
    id = "brrainz.harmony",
    name = "Harmony",
    steam = "steam://url/CommunityFilePage/2009463077",
    download = "https://github.com/pardeike/HarmonyRimWorld",
  },
  {
    id = "stratware.rimkit",
    name = "RimKit",
    download = "https://github.com/stratware/RimWorldModKit",
  },
}
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
