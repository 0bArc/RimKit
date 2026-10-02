local meta = require("host.metadata")
meta.name = "Bad Probe (expect block)"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.badprobe"
meta.version = "1.6"
meta.description = "INTENTIONAL hostile Lua patterns to test RimKit quarantine. Enabling this skips ONLY this pack; other Lua mods still load."
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
