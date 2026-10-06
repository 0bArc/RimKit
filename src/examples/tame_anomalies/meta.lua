local meta = require("host.metadata")
meta.name = "Tame Anomalies"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.tameanomalies"
meta.version = "1.6"
meta.mod_version = "0.2.0"
meta.description = "Recruit anomaly entities into a commandable army (F6, or right-click with a colonist), capture them on holding platforms, and release them (F7). Needs the Anomaly DLC and RimKit."
-- dev: game.dev.watch reloads this mod's Lua whenever a file changes while Development mode is on.
meta.capabilities = { "dev" }
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
