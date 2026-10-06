local meta = require("host.metadata")
meta.name = "ReflectProbe"
meta.author = "RimKit examples"
meta.package_id = "rimkit.reflectprobe"
meta.version = "1.6"
meta.mod_version = "0.1.0"
meta.description = "Checks that game.reflect works in a running game: reads, writes, calls and builds game objects, and reports PASS or FAIL for each step."
meta.api_level = 1
-- reflect: game.reflect. It also needs the RimKit setting developer_reflect turned on in the mod options.
meta.capabilities = { "reflect" }
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
