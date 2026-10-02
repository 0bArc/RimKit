# RimKit

Lua mods for RimWorld.

Stack: Lua scripts, C++ core (`src/native`), thin C# host (`src/host`) that RimWorld can load. Builtin pAuth: binary allowlist + per-mod Lua quarantine.

Mod identity comes from `meta.lua`. Run `rimkit mod sync` (or `rimkit mod ship`) so RimWorld gets About.xml.

## Start here

- **[Create a Lua mod](mod-create/index.md)**: allowed APIs and what you cannot ship  
- **[Strong API domains](api/index.md)**: data, health, anomaly, util, building, work  
- **[Request a blocked feature](info/index.md)**: ask for a reviewed, allowlisted API  
- [meta.lua](meta.md)  
- [Lua API](lua-api.md)  
- [Mod structure](mod-structure.md)  
- [Hello Lua](example-hello.md)  
- [Jobs Test](example-jobs.md)  
- [Showcase snippets](showcase.lua) (copy-paste demos)  
- [Build / ship](build.md)  
- [Hooks](hooks.md)  
