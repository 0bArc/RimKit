local meta = require("host.metadata")
meta.name = "RimLuaKit"
meta.author = "Team Stratware.win"
meta.package_id = "stratware.rimkit"
meta.version = "1.6"
meta.description = "Thin C# host plus C++ Lua runtime for authoring RimWorld mods in Lua. Load before Lua mods that depend on it. Requires Harmony."
meta.depends = {
  {
    id = "brrainz.harmony",
    name = "Harmony",
    steam = "steam://url/CommunityFilePage/2009463077",
    download = "https://github.com/pardeike/HarmonyRimWorld",
  },
}
meta.load_after = { "brrainz.harmony" }
return meta
