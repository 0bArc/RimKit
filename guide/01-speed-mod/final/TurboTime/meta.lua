local meta = require("host.metadata")
meta.name = "Turbo Time"
meta.author = "Your Name"
meta.package_id = "yourname.turbotime"
meta.version = "1.6"
meta.mod_version = "1.0.0"
meta.description = "Press = for a faster game and - for a slower one. The boost multiplies the normal Normal, Fast, Superfast and Ultrafast speeds, and pausing still stops the game. A cap in the mod options stops a big boost from freezing the game."
meta.api_level = 1
meta.capabilities = { "hooks" }
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
meta.load_after = { "ludeon.rimworld", "brrainz.harmony", "stratware.rimkit" }
return meta
