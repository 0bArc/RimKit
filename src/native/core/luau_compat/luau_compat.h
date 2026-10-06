#pragma once
// Presents Luau's C API to sol2 as Lua 5.4. sol2 has no Luau support, so the 5.3 and 5.4 functions it calls are provided here.
// Differences that matter to the code that uses this header:
//   - numbers are doubles and lua_Integer is 32 bit
//   - userdata is never finalized: Luau has no __gc, so C++ destructors of userdata do not run
//   - source text is compiled with luau_compile, so luaL_loadbufferx loads source and never bytecode
#include <lua.h>
#include <lualib.h>
#include <luacode.h>

#include <cstdarg>
#include <cstddef>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>

#define LUA_VERSION_NUM 504
#define LUA_VERSION_MAJOR "5"
#define LUA_VERSION_MINOR "4"
#define LUA_VERSION "Lua 5.4"
#define LUA_RELEASE "Lua 5.4.0"
#define LUA_OK 0
#define LUA_ERRFILE (LUA_ERRERR + 1)
#define LUA_NUMTYPES LUA_T_COUNT
#define LUA_RIDX_GLOBALS 2
#define LUA_RIDX_MAINTHREAD 1
#define LUAI_MAXSTACK 1000000
#define LUA_OPEQ 0
#define LUA_OPLT 1
#define LUA_OPLE 2
#define LUAL_NUMSIZES 0
#define LUA_NOREF (-1)
#define LUA_REFNIL 0

// lua_error never returns in Luau and has no result. Code written for 5.4 does "return lua_error(L)".
inline int luau_error_compat(lua_State* L) {
    lua_error(L);
    return 0;
}
#define lua_error(L) luau_error_compat(L)

// luaL_error is void and noreturn in Luau. 5.4 code returns its result.
inline int luau_lerror_compat(lua_State* L, const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    lua_pushvfstring(L, fmt, args);
    va_end(args);
    return luau_error_compat(L);
}
#undef luaL_error
#define luaL_error(L, ...) luau_lerror_compat(L, __VA_ARGS__)

// lua_gc takes extra arguments in 5.4. Luau only has one.
inline int luau_gc_compat(lua_State* L, int what, int a = 0, int = 0, int = 0) { return lua_gc(L, what, a); }
#define lua_gc(L, what, ...) luau_gc_compat(L, what, __VA_ARGS__)

// 5.4 style closures: Luau's takes a debug name and a continuation.
#undef lua_pushcclosure
#define lua_pushcclosure(L, fn, n) lua_pushcclosurek(L, fn, nullptr, n, nullptr)
#undef lua_pushcfunction
#define lua_pushcfunction(L, fn) lua_pushcclosurek(L, fn, nullptr, 0, nullptr)

#define LUA_GCGEN 10
#define LUA_GCINC 11

inline lua_CFunction lua_atpanic(lua_State*, lua_CFunction f) { return f; }

// Stack inspection is not used by RimKit. sol2 only calls these to find the calling environment.
inline int lua_getstack(lua_State*, int, lua_Debug*) { return 0; }
inline int lua_getinfo(lua_State*, const char*, lua_Debug*) { return 0; }

typedef long long lua_KContext;
typedef int (*lua_KFunction)(lua_State* L, int status, lua_KContext ctx);
typedef const char* (*lua_Reader)(lua_State* L, void* ud, size_t* sz);
typedef int (*lua_Writer)(lua_State* L, const void* p, size_t sz, void* ud);

inline void lua_pushglobaltable(lua_State* L) { lua_pushvalue(L, LUA_GLOBALSINDEX); }

inline void lua_rotate(lua_State* L, int idx, int n) {
    // Rotates the elements between idx and the top by n positions toward the top (n > 0) or toward idx (n < 0).
    const int t = lua_gettop(L);
    const int p = lua_absindex(L, idx);
    if (n == 0 || p >= t) return;
    if (n > 0) {
        for (int i = 0; i < n; ++i) {
            lua_insert(L, p);
        }
    } else {
        for (int i = 0; i < -n; ++i) {
            lua_pushvalue(L, p);
            lua_remove(L, p);
        }
    }
}

inline void lua_copy(lua_State* L, int from, int to) {
    lua_pushvalue(L, from);
    lua_replace(L, to);
}

inline int lua_compare(lua_State* L, int a, int b, int op) {
    switch (op) {
        case LUA_OPEQ: return lua_equal(L, a, b);
        case LUA_OPLT: return lua_lessthan(L, a, b);
        default: return lua_lessthan(L, a, b) || lua_equal(L, a, b);
    }
}

inline int lua_isinteger(lua_State* L, int idx) {
    if (lua_type(L, idx) != LUA_TNUMBER) return 0;
    const double d = lua_tonumber(L, idx);
    return d == static_cast<double>(static_cast<long long>(d));
}

inline size_t lua_rawlen(lua_State* L, int idx) { return static_cast<size_t>(lua_objlen(L, idx)); }

inline void lua_len(lua_State* L, int idx) { lua_pushinteger(L, lua_objlen(L, idx)); }

inline lua_Integer luaL_len(lua_State* L, int idx) { return lua_objlen(L, idx); }

inline int lua_geti(lua_State* L, int idx, lua_Integer n) {
    idx = lua_absindex(L, idx);
    lua_pushinteger(L, n);
    lua_gettable(L, idx);
    return lua_type(L, -1);
}

inline void lua_seti(lua_State* L, int idx, lua_Integer n) {
    idx = lua_absindex(L, idx);
    lua_pushinteger(L, n);
    lua_insert(L, -2);
    lua_settable(L, idx);
}

inline void* lua_newuserdatauv(lua_State* L, size_t sz, int /*nuvalue*/) { return lua_newuserdata(L, sz); }

inline int lua_getiuservalue(lua_State* L, int /*idx*/, int /*n*/) {
    lua_pushnil(L);
    return LUA_TNONE;
}

inline int lua_setiuservalue(lua_State* L, int /*idx*/, int /*n*/) {
    lua_pop(L, 1);
    return 0;
}

inline int lua_getuservalue(lua_State* L, int idx) { return lua_getiuservalue(L, idx, 1); }
inline int lua_setuservalue(lua_State* L, int idx) { return lua_setiuservalue(L, idx, 1); }

inline int lua_callk(lua_State* L, int nargs, int nresults, lua_KContext, lua_KFunction) {
    lua_call(L, nargs, nresults);
    return 0;
}
inline int lua_pcallk(lua_State* L, int nargs, int nresults, int errfunc, lua_KContext, lua_KFunction) {
    return lua_pcall(L, nargs, nresults, errfunc);
}
inline int lua_yieldk(lua_State* L, int nresults, lua_KContext, lua_KFunction) { return lua_yield(L, nresults); }

inline int luau_resume_compat(lua_State* L, lua_State* from, int nargs, int* nres) {
    const int r = lua_resume(L, from, nargs);
    if (nres) *nres = lua_gettop(L);
    return r;
}
#define lua_resume(L, from, nargs, nres) luau_resume_compat(L, from, nargs, nres)

inline int lua_rawgetp_compat(lua_State* L, int idx, const void* p) {
    lua_rawgetp(L, idx, const_cast<void*>(p));
    return lua_type(L, -1);
}

inline int luaL_ref(lua_State* L, int /*t*/) {
    const int r = lua_ref(L, -1);
    lua_pop(L, 1);
    return r;
}
inline void luaL_unref(lua_State* L, int /*t*/, int ref) { lua_unref(L, ref); }

inline int luaL_getsubtable(lua_State* L, int idx, const char* name) {
    idx = lua_absindex(L, idx);
    lua_getfield(L, idx, name);
    if (lua_istable(L, -1)) return 1;
    lua_pop(L, 1);
    lua_newtable(L);
    lua_pushvalue(L, -1);
    lua_setfield(L, idx, name);
    return 0;
}

inline void luaL_setmetatable(lua_State* L, const char* tname) {
    luaL_getmetatable(L, tname);
    lua_setmetatable(L, -2);
}

inline void* luaL_testudata(lua_State* L, int ud, const char* tname) {
    void* p = lua_touserdata(L, ud);
    if (p != nullptr && lua_getmetatable(L, ud)) {
        luaL_getmetatable(L, tname);
        if (!lua_rawequal(L, -1, -2)) p = nullptr;
        lua_pop(L, 2);
        return p;
    }
    return nullptr;
}

inline void luaL_setfuncs(lua_State* L, const luaL_Reg* l, int nup) {
    for (; l->name != nullptr; ++l) {
        for (int i = 0; i < nup; ++i) lua_pushvalue(L, -nup);
        lua_pushcclosurek(L, l->func, l->name, nup, nullptr);
        lua_setfield(L, -(nup + 2), l->name);
    }
    lua_pop(L, nup);
}

#define luaL_newlibtable(L, l) lua_createtable(L, 0, sizeof(l) / sizeof((l)[0]) - 1)
#define luaL_newlib(L, l) (luaL_newlibtable(L, l), luaL_setfuncs(L, l, 0))

inline void luaL_requiref(lua_State* L, const char* modname, lua_CFunction openf, int glb) {
    lua_pushcclosurek(L, openf, modname, 0, nullptr);
    lua_pushstring(L, modname);
    lua_call(L, 1, 1);
    if (glb) {
        lua_pushvalue(L, -1);
        lua_setglobal(L, modname);
    }
}

inline void luaL_checkversion(lua_State*) {}

// Loads source text. Luau compiles to bytecode first. A leading '=' or '@' in the chunk name is kept as Luau expects.
inline int luaL_loadbufferx(lua_State* L, const char* buff, size_t sz, const char* name, const char* /*mode*/) {
    size_t outsize = 0;
    lua_CompileOptions opts = {};
    opts.optimizationLevel = 1;
    opts.debugLevel = 1;
    char* bytecode = luau_compile(buff, sz, &opts, &outsize);
    if (!bytecode) {
        lua_pushstring(L, "luau_compile failed");
        return LUA_ERRSYNTAX;
    }
    const int result = luau_load(L, name ? name : "=?", bytecode, outsize, 0);
    std::free(bytecode);
    return result == 0 ? LUA_OK : LUA_ERRSYNTAX;
}
inline int luaL_loadbuffer(lua_State* L, const char* buff, size_t sz, const char* name) { return luaL_loadbufferx(L, buff, sz, name, nullptr); }
inline int luaL_loadstring(lua_State* L, const char* s) { return luaL_loadbuffer(L, s, std::strlen(s), s); }
inline int lua_load(lua_State* L, lua_Reader reader, void* data, const char* chunkname, const char* mode) {
    std::string src;
    size_t sz = 0;
    const char* piece;
    while ((piece = reader(L, data, &sz)) != nullptr && sz > 0) src.append(piece, sz);
    return luaL_loadbufferx(L, src.data(), src.size(), chunkname, mode);
}
inline int luaL_loadfilex(lua_State* L, const char* filename, const char* mode) {
    std::FILE* f = std::fopen(filename, "rb");
    if (!f) {
        lua_pushfstring(L, "cannot open %s", filename);
        return LUA_ERRFILE;
    }
    std::string src;
    char tmp[4096];
    size_t n;
    while ((n = std::fread(tmp, 1, sizeof tmp, f)) > 0) src.append(tmp, n);
    std::fclose(f);
    const std::string chunk = std::string("@") + filename;
    return luaL_loadbufferx(L, src.data(), src.size(), chunk.c_str(), mode);
}
#define luaL_loadfile(L, f) luaL_loadfilex(L, f, nullptr)

inline void lua_setallocf(lua_State*, lua_Alloc, void*) {}

inline int lua_dump(lua_State*, lua_Writer, void*, int) { return 1; }

inline int lua_stringtonumber(lua_State* L, const char* s) {
    char* end = nullptr;
    const double d = std::strtod(s, &end);
    if (end == s || *end != '\0') return 0;
    lua_pushnumber(L, d);
    return static_cast<int>(std::strlen(s)) + 1;
}

inline int lua_isyieldable_compat(lua_State* L) { return lua_isyieldable(L); }

inline int lua_arith_unsupported(lua_State* L) {
    lua_pushnil(L);
    return 0;
}

// Luau has no package or io library. RimKit provides its own require.
inline int luaopen_package(lua_State* L) {
    lua_newtable(L);
    return 1;
}
inline int luaopen_io(lua_State* L) {
    lua_newtable(L);
    return 1;
}

#define LUA_FILEHANDLE "FILE*"
struct luaL_Stream {
    std::FILE* f;
    lua_CFunction closef;
};

// Numbers are doubles, so integers wider than 32 bit travel as exact doubles (exact up to 2^53) instead of being truncated.
inline void luau_pushinteger_compat(lua_State* L, long long n) { lua_pushnumber(L, static_cast<double>(n)); }
inline long long luau_tointegerx_compat(lua_State* L, int idx, int* isnum) {
    int ok = 0;
    const double d = lua_tonumberx(L, idx, &ok);
    if (isnum) *isnum = ok;
    return ok ? static_cast<long long>(d) : 0;
}
#undef lua_pushinteger
#define lua_pushinteger(L, n) luau_pushinteger_compat(L, n)
#undef lua_tointegerx
#define lua_tointegerx(L, idx, isnum) luau_tointegerx_compat(L, idx, isnum)
