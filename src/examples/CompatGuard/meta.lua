local meta = require("host.metadata")
meta.name = "Compat Guard"
meta.author = "RimKit"
meta.package_id = "rimkit.compatguard"
meta.version = "1.6"                -- the RimWorld version the mod is for
meta.mod_version = "0.1.0"          -- the mod's own version, bump it for every release
meta.description = "Shows how to cooperate with another mod only when it is installed: a guarded hook and a function offered to other mods."
meta.api_level = 1                 -- strict mode: errors raise, old API names are refused, only declared capabilities work
-- What the Lua may do. hooks: game.hooks and game.tweaks. reflect: game.reflect. files: write Defs and patches. dev: evaluate code.
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
