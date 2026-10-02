local meta = require("host.metadata")
meta.name = "Tame Anomalies (prototype)"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.tameanomalies"
meta.version = "1.6"
meta.description = "Prototype: claim anomaly entities to player faction and release on F7. Needs Anomaly DLC. Power-fail terminal is future work."
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
    steam = "steam://url/CommunityFilePage/3811629229",
    download = "https://steamcommunity.com/sharedfiles/filedetails/?id=3811629229",
  },
}
meta.load_after = { "brrainz.harmony", "stratware.rimkit" }
return meta
