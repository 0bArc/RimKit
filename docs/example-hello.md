# Hello Lua

Folder: `src/examples/hello_lua/`

```
hello_lua/
  meta.lua
  Lua/main.lua
  About/About.xml
```

OO example: blocks player food jobs when map nutrition is under 10. Uses `RimPawn` userdata, `log.info`, `events.on`, and `on_pawn_spawned` sugar.

```text
bin\rimkit.exe mod sync src\examples\hello_lua
bin\rimkit.exe mod ship src\examples\hello_lua
```

Enable: Harmony, RimLuaKit, Hello Lua.
