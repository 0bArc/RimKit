local meta = require("host.metadata")
meta.name = "Hello Lua"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.hellolua"
meta.version = "1.6"
meta.description = "RimKit sample: block food jobs when colony nutrition is low."
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
