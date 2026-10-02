local meta = require("host.metadata")
meta.name = "Logic Panel Demo"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.logicpanel"
meta.version = "1.6"
meta.description = "Demo of data store + ui.panel + health hediff severity (Wave 1 API)."
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
