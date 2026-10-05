local meta = require("host.metadata")
meta.name = "RimKit Demo"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.rimkitdemo"
meta.version = "1.6"
meta.mod_version = "1.0.0"
meta.description = "A small tour of what RimKit mods can do, in one Lua file. Press F9 for a window with your colonists' best skills and a hunger setting, hear about deaths, and see another mod's functions used through game.interop. Meant to be read and copied."
-- hooks: the hunger setting is a tweak, and tweaks are hooks underneath.
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
