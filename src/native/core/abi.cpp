#include "engine.hpp"
#include "rimlua_abi.h"

extern "C" {

RIMLUA_API int rimlua_init(const rimlua_callbacks* cb) {
    return rimlua::Engine::instance().init(cb);
}

RIMLUA_API void rimlua_shutdown(void) {
    rimlua::Engine::instance().shutdown();
}

RIMLUA_API int rimlua_load_script(const char* path) {
    if (!path) {
        return 1;
    }
    return rimlua::Engine::instance().load_script(path);
}

RIMLUA_API int rimlua_load_directory(const char* dir) {
    if (!dir) {
        return 1;
    }
    return rimlua::Engine::instance().load_directory(dir);
}

RIMLUA_API void rimlua_call_on_load(void) {
    rimlua::Engine::instance().call_on_load();
}

RIMLUA_API void rimlua_call_on_tick(void) {
    rimlua::Engine::instance().call_on_tick();
}

RIMLUA_API int rimlua_invoke_hook(int hook_id, int arg0_handle, int* out_continue) {
    return rimlua::Engine::instance().invoke_hook(hook_id, arg0_handle, out_continue);
}

RIMLUA_API void rimlua_emit_event(const char* name, int handle) {
    if (!name) {
        return;
    }
    rimlua::Engine::instance().emit_event(name, handle);
}

RIMLUA_API int rimlua_job_call(const char* name, const char* phase, int pawn_handle, int* out_result) {
    if (!name || !phase) {
        return 1;
    }
    return rimlua::Engine::instance().job_call(name, phase, pawn_handle, out_result);
}

RIMLUA_API void rimlua_ui_invoke(int callback_id) {
    rimlua::Engine::instance().ui_invoke(callback_id);
}

}  // extern "C"
