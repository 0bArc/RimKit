#include "engine.hpp"
#include "rimlua_abi.h"
#include "version_data.hpp"

// Lua state is single threaded, but Harmony hooks can fire on worker threads (map generation, loading).
// Every entry point takes the engine lock. The lock is recursive, so Lua calling the host, which triggers
// a hook on the same thread, does not deadlock.
#define RIMLUA_LOCK() std::lock_guard<std::recursive_mutex> rimlua_guard(rimlua::Engine::instance().mutex())

extern "C" {

RIMLUA_API const char* rimlua_version(void) {
    return RIMKIT_VERSION;
}

RIMLUA_API int rimlua_init(const rimlua_callbacks* cb) {
    return rimlua::Engine::instance().init(cb);
}

RIMLUA_API void rimlua_shutdown(void) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().shutdown();
}

RIMLUA_API int rimlua_load_script(const char* path) {
    if (!path) {
        return 1;
    }
    RIMLUA_LOCK();
    return rimlua::Engine::instance().load_script(path);
}

RIMLUA_API int rimlua_load_directory(const char* dir) {
    if (!dir) {
        return 1;
    }
    RIMLUA_LOCK();
    return rimlua::Engine::instance().load_directory(dir);
}

RIMLUA_API void rimlua_call_on_load(void) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().call_on_load();
}

RIMLUA_API void rimlua_call_on_tick(void) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().call_on_tick();
}

RIMLUA_API int rimlua_invoke_hook(int hook_id, int arg0_handle, const char* ctx_json, int* out_continue) {
    RIMLUA_LOCK();
    return rimlua::Engine::instance().invoke_hook(hook_id, arg0_handle, ctx_json, out_continue);
}

// The returned buffer is thread local, so it stays valid after the lock is released.
RIMLUA_API const char* rimlua_invoke_hook_ex(int hook_id, const char* ctx_json) {
    RIMLUA_LOCK();
    return rimlua::Engine::instance().invoke_hook_ex(hook_id, ctx_json);
}

RIMLUA_API int rimlua_invoke_hook_fast(int hook_id, int pawn_handle, int nargs, const double* args,
                                       const unsigned char* types, int has_result, double result,
                                       unsigned char result_type, double* out_result, int* out_flags) {
    RIMLUA_LOCK();
    return rimlua::Engine::instance().invoke_hook_fast(hook_id, pawn_handle, nargs, args, types, has_result, result,
                                                       result_type, out_result, out_flags);
}

RIMLUA_API void rimlua_set_hook_info(int hook_id, const char* method, const char* phase, const char* arg_names_json) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().set_hook_info(hook_id, method ? method : "", phase ? phase : "",
                                             arg_names_json ? arg_names_json : "[]");
}

RIMLUA_API void rimlua_set_mod_context(const char* package_id) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().set_mod_context(package_id ? package_id : "");
}

RIMLUA_API void rimlua_set_test_mode(int on) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().set_test_mode(on != 0);
}

RIMLUA_API int rimlua_mod_disabled(const char* package_id) {
    RIMLUA_LOCK();
    return package_id && rimlua::Engine::instance().is_mod_disabled(package_id) ? 1 : 0;
}

RIMLUA_API void rimlua_reset_mod_budget(const char* package_id) {
    RIMLUA_LOCK();
    if (package_id) {
        rimlua::Engine::instance().reset_mod_budget(package_id);
    }
}

RIMLUA_API void rimlua_emit_event_ex(const char* name, const char* payload_json) {
    if (!name) {
        return;
    }
    RIMLUA_LOCK();
    rimlua::Engine::instance().emit_event_ex(name, payload_json ? payload_json : "{}");
}

RIMLUA_API void rimlua_emit_event(const char* name, int handle) {
    if (!name) {
        return;
    }
    RIMLUA_LOCK();
    rimlua::Engine::instance().emit_event(name, handle);
}

RIMLUA_API int rimlua_job_call(const char* name, const char* phase, int pawn_handle, int* out_result) {
    if (!name || !phase) {
        return 1;
    }
    RIMLUA_LOCK();
    return rimlua::Engine::instance().job_call(name, phase, pawn_handle, out_result);
}

RIMLUA_API void rimlua_ui_invoke(int callback_id) {
    RIMLUA_LOCK();
    rimlua::Engine::instance().ui_invoke(callback_id);
}

RIMLUA_API const char* rimlua_ui_call(int callback_id, const char* arg_json) {
    RIMLUA_LOCK();
    return rimlua::Engine::instance().ui_call(callback_id, arg_json);
}

RIMLUA_API const char* rimlua_collect_map_float_menu(int clicked_handle, int hauler_handle) {
    RIMLUA_LOCK();
    return rimlua::Engine::instance().collect_map_float_menu(clicked_handle, hauler_handle);
}

}  // extern "C"
