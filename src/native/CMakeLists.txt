cmake_minimum_required(VERSION 3.20)
project(RimLuaKitNative LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 99)

set(LUA_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/lua")
set(SOL2_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/sol2")

file(GLOB LUA_SOURCES "${LUA_DIR}/*.c")
list(FILTER LUA_SOURCES EXCLUDE REGEX ".*/lua\\.c$")
list(FILTER LUA_SOURCES EXCLUDE REGEX ".*/luac\\.c$")
list(FILTER LUA_SOURCES EXCLUDE REGEX ".*/onelua\\.c$")

add_library(lua_static STATIC ${LUA_SOURCES})
target_include_directories(lua_static PUBLIC "${LUA_DIR}")
if(MSVC)
  target_compile_definitions(lua_static PRIVATE _CRT_SECURE_NO_WARNINGS)
endif()

add_library(rimlua_core SHARED
  core/abi.cpp
  core/engine.cpp
  core/oo_bind.cpp
)
target_compile_definitions(rimlua_core PRIVATE RIMLUA_EXPORTS SOL_ALL_SAFETIES_ON=1)
target_include_directories(rimlua_core
  PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/core"
    "${SOL2_DIR}/include"
)
target_link_libraries(rimlua_core PRIVATE lua_static)
if(MSVC)
  target_compile_options(rimlua_core PRIVATE /EHsc /W3)
endif()

add_executable(rimkit cli/main.cpp)
target_include_directories(rimkit PRIVATE "${SOL2_DIR}/include" "${LUA_DIR}")
target_compile_definitions(rimkit PRIVATE SOL_ALL_SAFETIES_ON=1)
target_link_libraries(rimkit PRIVATE lua_static)
if(MSVC)
  target_compile_options(rimkit PRIVATE /EHsc /W3)
endif()

set(RIMLUA_NATIVE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../Native")
set(RIMKIT_BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../bin")
add_custom_command(TARGET rimlua_core POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E make_directory "${RIMLUA_NATIVE_DIR}"
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    $<TARGET_FILE:rimlua_core>
    "${RIMLUA_NATIVE_DIR}/rimlua_core.dll"
  COMMENT "Copy rimlua_core.dll to Native/ (not Assemblies)"
)
add_custom_command(TARGET rimkit POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E make_directory "${RIMKIT_BIN_DIR}"
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    $<TARGET_FILE:rimkit>
    "${RIMKIT_BIN_DIR}/rimkit.exe"
  COMMENT "Copy rimkit.exe to bin/"
)
