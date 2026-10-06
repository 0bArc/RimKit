# Third-party

Vendored for offline builds:

- `luau/` - the Luau VM and compiler (MIT, `luau/LICENSE.txt`), taken from https://github.com/luau-lang/luau at commit `1eca9fd`. Only `Common`, `Ast`, `Bytecode`, `Compiler` and `VM` are included, built as one static library by `luau/CMakeLists.txt`. No native code generation, no analysis.
- `sol2/` - sol2 header library (MIT). It has no Luau support. `core/luau_compat/` supplies the Lua 5.4 API names it expects on top of Luau.

Do not edit unless upgrading versions. To upgrade Luau, copy the five folders from a newer checkout and rebuild.
