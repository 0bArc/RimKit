# Create a RimKit Lua mod

Load order: Harmony → RimKit → your mod.

```powershell
bin\rimkit.exe mod create MyMod
bin\rimkit.exe mod ship MyMod
```

## Layout

```
MyMod/
  meta.lua
  About/About.xml
  Lua/main.lua
  Defs/
  Languages/English/Keyed/
```

`About/About.xml` comes from `rimkit mod sync`. Do not edit by hand.

## meta.lua

```lua
local meta = require("host.metadata")
meta.name = "My Mod"
meta.author = "Team Stratware.win"
meta.package_id = "yourname.mymod"
meta.version = "1.6"
meta.description = "One clear colony feature."
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
```

Deps: Harmony + RimKit only. Not `stratware.pauth`.

## Allowed example

```lua
rim.on_load(function()
  rim.log("[MyMod] loaded")
  rim.message("[MyMod] loaded")
end)

local map = rim.find.current_map()
local pawns = rim.find.selected()
rim.ui.message("Hello")

rim.events.prefix["SomeType.SomeMethod"] = function(...)
  return true
end
```

API: [lua-api.md](../lua-api.md). Optional KeyBindings under `Defs/KeyBindings/` (avoid F10).

## Require other files

`require` only loads `.lua` under **this mod's** `Lua/` folder (and other RimKit-allowed Lua roots). Dots become path segments. `require("os")` / `io` / `debug` are blocked.

```
MyMod/Lua/
  main.lua
  util.lua
  jobs/
    init.lua
```

`Lua/util.lua`:

```lua
local M = {}

function M.greet(name)
  rim.message("[MyMod] hello " .. tostring(name))
end

return M
```

`Lua/jobs/init.lua`:

```lua
local M = {}

function M.register()
  rim.log("[MyMod] jobs module ready")
end

return M
```

`Lua/main.lua`:

```lua
local util = require("util")
local jobs = require("jobs")

rim.on_load(function()
  util.greet("colonist")
  jobs.register()
end)
```

Same folder: `require("util")` → `Lua/util.lua`.  
Subfolder: `require("jobs")` → `Lua/jobs/init.lua` or `Lua/jobs.lua`.  
Nested: `require("jobs.haul")` → `Lua/jobs/haul.lua`.

`require("host.metadata")` is for **meta.lua** at sync time only (CLI). Do not use that inside runtime `Lua/` scripts.

## Mods you cannot make (blocked)

RimKit Lua is for **in-game colony logic** only. Scan + sandboxed VM + allowlisted API. Load fails or the call is dead at runtime.

| You cannot ship a Lua mod that… | Blocked capability |
|--------------------------------|--------------------|
| Runs shell / OS programs | `os.execute`, process start |
| Reads or writes arbitrary disk paths | `io.*`, `System.IO.File`, free `dofile`/`loadfile` |
| Downloads or phones home outside game APIs | no raw net/OS libs in the VM |
| Loads native DLLs into RimWorld | `package.loadlib`, `package.cpath` |
| Builds or injects code at runtime | `load`, `loadstring`, open `debug.*` |
| Reaches full C# reflection / System types | `rim.reflect` / `rim.cs` off for players |
| Writes ThingDefs outside your mod folder | path-jailed `defs` writes |
| `require`s files outside its own `Lua/` tree | jailed searcher |

If your idea needs those, ship a normal RimWorld C# mod (`Assemblies/*.dll`). That path is outside this gate.

### Real RimWorld ideas (not possible as RimKit Lua)

Normal colony features people ask for. Still **out of scope** in `Lua/`.

**1. Auto-backup saves**  
Copy the current `.rws` to a dated folder under Documents before a big raid or before quit. Needs free filesystem copy. Blocked.

**2. Export colony sheet to Desktop**  
Dump wealth, pawn skills, or drug policies to `Desktop/colony.csv` for spreadsheets. Needs arbitrary file write. Blocked.

**3. Rewrite ModsConfig / load order from in-game**  
A “smart sorter” that edits `ModsConfig.xml` or another mod’s files on disk. Needs paths outside your mod root. Blocked.

**4. Open log / Mods / save folder**  
Button that launches Explorer on `Player.log` or the Saves directory. Needs process start. Blocked.

**5. Shared data between two Lua mods on disk**  
Mod A writes a cache file; Mod B `require`s or reads it from Mod A’s folder. Jailed `require` + no free `io`. Blocked.

**6. Hot-reload scripts mid-save**  
Pull a new `.lua` from disk (or paste) and `load()` it without restart. Needs `load` / free file read. Blocked.

**7. Patch Core or another author’s Defs at runtime on disk**  
Write generated XML into `Data/Core` or someone else’s mod package. Path-jailed. Blocked.

**What to do instead:** jobs, AI hooks, UI letters, config, KeyBindings, defs **inside your mod**, APIs in [lua-api.md](../lua-api.md). For save backups, CSV export, folder openers, load-order tools: ship C#, or [request a reviewed allowlisted API](../info/index.md).

### Disallowed snippet (will trip the gate)

```lua
os.execute("echo pwned")
io.open("C:/Windows/win.ini", "r")
load("return 1")()
dofile("evil.lua")
package.loadlib("x", "y")
require("os")
debug.getinfo(1)
rim.reflect.static_call("System.Diagnostics.Process", "Start", "cmd")
rim.cs.static_call("System.IO.File", "WriteAllText", "x")
```

Any hit in any enabled mod `Lua/**/*.lua` blocks **all** Lua for that session.

## You can make

Colony jobs, UI messages/letters/panels, float menus, config toggles, Harmony hooks via `rim.events`, defs under your mod, KeyBindings, health/surgery helpers, work priorities, building power/flick, anomaly claim/release (DLC), save-scoped `data.*`, util open folder / export under mod. See [Strong API domains](../api/index.md).

## Gate test

Enable **Bad Probe** (`stratware.badprobe`) → expect `LUA BLOCKED` / letter. Disable it to play again.

## Ship

- [ ] Harmony + RimKit deps only  
- [ ] No disallowed calls under `Lua/`  
- [ ] `rimkit mod ship`  
- [ ] After kit rebuild: `rimkit build` (refreshes `Auth/allowlist.json`)  

More: [meta](../meta.md) · [structure](../mod-structure.md) · [hello](../example-hello.md) · [jobs](../example-jobs.md) · [build](../build.md)
