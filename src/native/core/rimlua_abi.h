#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#ifdef RIMLUA_EXPORTS
#define RIMLUA_API __declspec(dllexport)
#else
#define RIMLUA_API __declspec(dllimport)
#endif

/* Hook kinds passed to register_hook. */
#define RIMLUA_HOOK_POSTFIX 0
#define RIMLUA_HOOK_PREFIX 1
#define RIMLUA_HOOK_FINALIZER 2
#define RIMLUA_HOOK_REDIRECT_CALL 3

typedef void (*rimlua_log_fn)(const char* msg);
typedef void (*rimlua_message_fn)(const char* msg);
/* opts_json is a UTF-8 JSON object, never NULL. Keys: sig (array of type names), priority (int),
   before (array of harmony ids), after (array of harmony ids), call (string "Type.Method" for REDIRECT_CALL),
   call_sig (array of type names), nth (int, 0 means every match). Returns 1 on success. */
typedef int (*rimlua_register_hook_fn)(const char* type_name, const char* method_name, int kind, int hook_id,
                                       const char* opts_json);
typedef void (*rimlua_unregister_hook_fn)(int hook_id);
typedef char* (*rimlua_host_invoke_fn)(const char* op, const char* args_json);
typedef void (*rimlua_host_free_fn)(char* p);

typedef struct rimlua_callbacks {
    rimlua_log_fn log;
    rimlua_message_fn message;
    rimlua_register_hook_fn register_hook;
    rimlua_unregister_hook_fn unregister_hook;
    rimlua_host_invoke_fn host_invoke;
    rimlua_host_free_fn host_free;
} rimlua_callbacks;

/* Native core version string (from src/api/VERSION). The host compares it with its own. */
RIMLUA_API const char* rimlua_version(void);
RIMLUA_API int rimlua_init(const rimlua_callbacks* cb);
RIMLUA_API void rimlua_shutdown(void);
RIMLUA_API int rimlua_load_script(const char* path);
RIMLUA_API int rimlua_load_directory(const char* dir);
RIMLUA_API void rimlua_call_on_load(void);
RIMLUA_API void rimlua_call_on_tick(void);
/* Legacy entry point. ctx_json is the UTF-8 hook context. The response is discarded. */
RIMLUA_API int rimlua_invoke_hook(int hook_id, int arg0_handle, const char* ctx_json, int* out_continue);
/* Hook call with a response. ctx_json is the UTF-8 hook context (see docs/hooks.md).
   Returns a pointer to a thread local UTF-8 JSON response object, valid until the next call on the same thread.
   Response keys: cont (bool), has_result (bool), result (value), args (object of 1 based index to value),
   state (value), suppress (bool, finalizer only). Returns an empty string when the hook failed or is unknown. */
RIMLUA_API const char* rimlua_invoke_hook_ex(int hook_id, const char* ctx_json);
/* Fast hook path, no JSON. Only for methods whose arguments and result are numbers or booleans.
   types[i]: 0 float, 1 bool, 2 integer. result_type uses the same codes. out_flags: bit 1 result set (out_result),
   bit 2 skip the original. Returns 0 on success. Call rimlua_set_hook_info once after registering the hook. */
RIMLUA_API int rimlua_invoke_hook_fast(int hook_id, int pawn_handle, int nargs, const double* args,
                                       const unsigned char* types, int has_result, double result,
                                       unsigned char result_type, double* out_result, int* out_flags);
RIMLUA_API void rimlua_set_hook_info(int hook_id, const char* method, const char* phase, const char* arg_names_json);
/* Names the mod whose Lua loads next (its package id). Hooks, events, timers and tick callbacks it registers are charged to
   it: after 20 Lua errors in 60 seconds they stop. Pass "" or NULL when loading is done. */
RIMLUA_API void rimlua_set_mod_context(const char* package_id);
/* Turns on the mock host used by rimkit mod test. Only the CLI calls this; in the game the mock functions refuse to work. */
RIMLUA_API void rimlua_set_test_mode(int on);
RIMLUA_API int rimlua_mod_disabled(const char* package_id);
RIMLUA_API void rimlua_reset_mod_budget(const char* package_id);
RIMLUA_API void rimlua_emit_event(const char* name, int handle);
/* Named event with a UTF-8 JSON object payload. */
RIMLUA_API void rimlua_emit_event_ex(const char* name, const char* payload_json);
/* phase: "can_do" | "execute". out_result: 1=true/done, 0=false/continue. returns 0 on ok. */
RIMLUA_API int rimlua_job_call(const char* name, const char* phase, int pawn_handle, int* out_result);
RIMLUA_API void rimlua_ui_invoke(int callback_id);
/* Calls a Lua function registered through a kit function argument, with a JSON argument. Returns its result as JSON,
   or an empty string. The pointer is valid until the next call. */
RIMLUA_API const char* rimlua_ui_call(int callback_id, const char* arg_json);
/* Returns pointer to static UTF-8 blob "label\\tid\\n..." from Lua ui.on_map_float_menu. Empty string if none. */
RIMLUA_API const char* rimlua_collect_map_float_menu(int clicked_handle, int hauler_handle);
/* Runs Lua text for the host and returns the result as text ("error: ..." on failure). Valid until the next call. Used by Helm. */
RIMLUA_API const char* rimlua_eval(const char* code);

#ifdef __cplusplus
}
#endif
