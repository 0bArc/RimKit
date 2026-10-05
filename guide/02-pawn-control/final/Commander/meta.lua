local meta = require("host.metadata")
meta.name = "Commander"
meta.author = "Your Name"
meta.package_id = "yourname.commander"
meta.version = "1.6"
meta.mod_version = "1.0.0"
meta.description = "Select colonists, point at the ground and press G: they walk there and stand in a loose group. Press Y to draft or undraft the selected colonists."
meta.api_level = 1
meta.capabilities = {  }
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
