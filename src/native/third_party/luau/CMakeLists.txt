# Luau (MIT, see LICENSE.txt), the Lua dialect RimKit mods run on. Vendored from https://github.com/luau-lang/luau at 1eca9fd.
# Only the VM, the compiler and what they need are built, as one static library. No native code generation and no analysis.
# Errors are C++ exceptions (not longjmp), which is what sol2 expects.
cmake_minimum_required(VERSION 3.20)

file(GLOB LUAU_COMMON_SRC "${CMAKE_CURRENT_LIST_DIR}/Common/src/*.cpp")
file(GLOB LUAU_AST_SRC "${CMAKE_CURRENT_LIST_DIR}/Ast/src/*.cpp")
file(GLOB LUAU_BYTECODE_SRC "${CMAKE_CURRENT_LIST_DIR}/Bytecode/src/*.cpp")
file(GLOB LUAU_COMPILER_SRC "${CMAKE_CURRENT_LIST_DIR}/Compiler/src/*.cpp")
file(GLOB LUAU_VM_SRC "${CMAKE_CURRENT_LIST_DIR}/VM/src/*.cpp")

add_library(luau_static STATIC ${LUAU_COMMON_SRC} ${LUAU_AST_SRC} ${LUAU_BYTECODE_SRC} ${LUAU_COMPILER_SRC} ${LUAU_VM_SRC})
target_compile_features(luau_static PUBLIC cxx_std_17)
target_include_directories(luau_static PUBLIC
  "${CMAKE_CURRENT_LIST_DIR}/VM/include"
  "${CMAKE_CURRENT_LIST_DIR}/Compiler/include"
)
target_include_directories(luau_static PRIVATE
  "${CMAKE_CURRENT_LIST_DIR}/Common/include"
  "${CMAKE_CURRENT_LIST_DIR}/Ast/include"
  "${CMAKE_CURRENT_LIST_DIR}/Bytecode/include"
  "${CMAKE_CURRENT_LIST_DIR}/Compiler/include"
  "${CMAKE_CURRENT_LIST_DIR}/VM/src"
)
if(MSVC)
  target_compile_definitions(luau_static PRIVATE _CRT_SECURE_NO_WARNINGS)
  target_compile_options(luau_static PRIVATE /MP)
  # Keeps the interpreter loop fast on MSVC, same as upstream.
  set_source_files_properties("${CMAKE_CURRENT_LIST_DIR}/VM/src/lvmexecute.cpp" PROPERTIES COMPILE_FLAGS /d2ssa-pre-)
else()
  target_compile_options(luau_static PRIVATE -fno-math-errno)
endif()
