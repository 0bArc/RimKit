cmake_minimum_required(VERSION 3.20)
project(RimLuaKitNative LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 99)

# UNC alias table: one JSON file feeds the runtime, the CLI migrate command and the VS Code extension.
set(RIMKIT_ALIASES_SOURCE "${CMAKE_CURRENT_SOURCE_DIR}/../api/aliases.json")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${RIMKIT_ALIASES_SOURCE}")
file(READ "${RIMKIT_ALIASES_SOURCE}" RIMKIT_ALIASES_JSON)
configure_file("${CMAKE_CURRENT_SOURCE_DIR}/core/aliases_data.hpp.in" "${CMAKE_CURRENT_BINARY_DIR}/generated/aliases_data.hpp" @ONLY)
configure_file("${RIMKIT_ALIASES_SOURCE}" "${CMAKE_CURRENT_SOURCE_DIR}/../editor/aliases.json" COPYONLY)

# Kit table: api/kits.json describes every kit function (domain, op, arguments, docs). It drives the Lua binding,
# the editor stubs, the API reference and the editor completions.
set(RIMKIT_KITS_SOURCE "${CMAKE_CURRENT_SOURCE_DIR}/../api/kits.json")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${RIMKIT_KITS_SOURCE}")
file(READ "${RIMKIT_KITS_SOURCE}" RIMKIT_KITS_HEX HEX)
string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1," RIMKIT_KITS_BYTES "${RIMKIT_KITS_HEX}")

# Tweak catalog: api/tweaks.json names the places a mod can change a value (game.tweaks).
set(RIMKIT_TWEAKS_SOURCE "${CMAKE_CURRENT_SOURCE_DIR}/../api/tweaks.json")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${RIMKIT_TWEAKS_SOURCE}")
file(READ "${RIMKIT_TWEAKS_SOURCE}" RIMKIT_TWEAKS_HEX HEX)
string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1," RIMKIT_TWEAKS_BYTES "${RIMKIT_TWEAKS_HEX}")
configure_file("${CMAKE_CURRENT_SOURCE_DIR}/core/tweaks_data.hpp.in" "${CMAKE_CURRENT_BINARY_DIR}/generated/tweaks_data.hpp" @ONLY)
configure_file("${RIMKIT_TWEAKS_SOURCE}" "${CMAKE_CURRENT_SOURCE_DIR}/../editor/tweaks.json" COPYONLY)
configure_file("${CMAKE_CURRENT_SOURCE_DIR}/core/kits_data.hpp.in" "${CMAKE_CURRENT_BINARY_DIR}/generated/kits_data.hpp" @ONLY)
configure_file("${RIMKIT_KITS_SOURCE}" "${CMAKE_CURRENT_SOURCE_DIR}/../editor/kits.json" COPYONLY)

# One version for everything: src/api/VERSION and src/api/API_LEVEL. Native, host and editor read the same files.
set(RIMKIT_VERSION_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../api/VERSION")
set(RIMKIT_API_LEVEL_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../api/API_LEVEL")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${RIMKIT_VERSION_FILE}" "${RIMKIT_API_LEVEL_FILE}")
file(STRINGS "${RIMKIT_VERSION_FILE}" RIMKIT_VERSION LIMIT_COUNT 1)
file(STRINGS "${RIMKIT_API_LEVEL_FILE}" RIMKIT_API_LEVEL LIMIT_COUNT 1)
configure_file("${CMAKE_CURRENT_SOURCE_DIR}/core/version_data.hpp.in" "${CMAKE_CURRENT_BINARY_DIR}/generated/version_data.hpp" @ONLY)

set(SOL2_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/sol2")
# Luau VM and compiler. sol2 has no Luau support, so core/luau_compat holds the Lua 5.4 names it expects.
add_subdirectory(third_party/luau)
set(LUAU_COMPAT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/core/luau_compat")

add_library(rimlua_core SHARED
  core/abi.cpp
  core/engine.cpp
  core/oo_bind.cpp
  core/reflect_bind.cpp
  core/pawns_bind.cpp
  core/kits_bind.cpp
  core/ecosystem.cpp
  core/canonical_bind.cpp
)
target_compile_definitions(rimlua_core PRIVATE RIMLUA_EXPORTS SOL_ALL_SAFETIES_ON=1 SOL_USING_CXX_LUA=1)
target_include_directories(rimlua_core
  PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/core"
    "${CMAKE_CURRENT_BINARY_DIR}/generated"
    "${LUAU_COMPAT_DIR}"
    "${SOL2_DIR}/include"
)
target_link_libraries(rimlua_core PRIVATE luau_static)
if(MSVC)
  target_compile_options(rimlua_core PRIVATE /EHsc /W3 /bigobj)
endif()

add_executable(rimkit cli/main.cpp cli/tools.cpp cli/tools_content.cpp)
target_include_directories(rimkit PRIVATE "${LUAU_COMPAT_DIR}" "${SOL2_DIR}/include" "${CMAKE_CURRENT_SOURCE_DIR}/core" "${CMAKE_CURRENT_BINARY_DIR}/generated")
target_compile_definitions(rimkit PRIVATE SOL_ALL_SAFETIES_ON=1 SOL_USING_CXX_LUA=1)
target_link_libraries(rimkit PRIVATE luau_static)
if(MSVC)
  target_compile_options(rimkit PRIVATE /EHsc /W3)
endif()

set(RIMLUA_NATIVE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../mod/Native")
set(RIMKIT_BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../bin")
add_custom_command(TARGET rimlua_core POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E make_directory "${RIMLUA_NATIVE_DIR}"
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    $<TARGET_FILE:rimlua_core>
    "${RIMLUA_NATIVE_DIR}/rimlua_core.dll"
  COMMENT "Copy rimlua_core.dll to mod/Native/ (not Assemblies)"
)
add_custom_command(TARGET rimkit POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E make_directory "${RIMKIT_BIN_DIR}"
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    $<TARGET_FILE:rimkit>
    "${RIMKIT_BIN_DIR}/rimkit.exe"
  COMMENT "Copy rimkit.exe to bin/"
)
