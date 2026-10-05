local meta = require("host.metadata")
meta.name = "test"
meta.author = "Your Name"
meta.package_id = "yourname.test"
meta.version = "1.6"                -- the RimWorld version the mod is for
meta.mod_version = "0.1.0"          -- the mod's own version, bump it for every release
meta.description = "Replace this with one clear sentence about what the mod does for the player."
-- What the Lua may do. hooks: game.hooks and game.tweaks. reflect: game.reflect. files: write Defs and patches. dev: evaluate code.
meta.capabilities = {}
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
