#pragma once

// Loading Lua files for the runtime and the CLI. Files are Luau: types, continue, += and the rest are part of the language,
// so the Luau compiler reads them as they are.

#include <string>

#include <sol/sol.hpp>

namespace rimlua {

inline sol::load_result load_lua_file(sol::state& L, const std::string& path) { return L.load_file(path); }

inline sol::protected_function_result run_lua_file(sol::state& L, const std::string& path) {
    return L.safe_script_file(path, sol::script_pass_on_error);
}

// Standard libraries for the CLI's own Lua states (meta.lua and Defs files). Luau has no package library, so this adds a package table
// with preload and a require that reads from it.
inline void open_cli_libs(sol::state& L) {
    L.open_libraries(sol::lib::base, sol::lib::string, sol::lib::table, sol::lib::math, sol::lib::utf8);
    L["package"] = L.create_table_with("loaded", L.create_table(), "preload", L.create_table());
    L.safe_script(R"LUA(
local loaded, preload = package.loaded, package.preload
function require(name)
  local v = loaded[name]
  if v ~= nil then return v end
  local loader = preload[name]
  if loader == nil then error("module not found: " .. tostring(name), 2) end
  v = loader(name)
  if v == nil then v = true end
  loaded[name] = v
  return v
end
)LUA");
}

}  // namespace rimlua
