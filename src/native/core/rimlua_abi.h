#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#ifdef RIMLUA_EXPORTS
#define RIMLUA_API __declspec(dllexport)
#else
#define RIMLUA_API __declspec(dllimport)
#endif

typedef void (*rimlua_log_fn)(const char* msg);
typedef void (*rimlua_message_fn)(const char* msg);
typedef int (*rimlua_register_hook_fn)(const char* type_name, const char* method_name, int is_prefix, int hook_id);
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

RIMLUA_API int rimlua_init(const rimlua_callbacks* cb);
RIMLUA_API void rimlua_shutdown(void);
RIMLUA_API int rimlua_load_script(const char* path);
RIMLUA_API int rimlua_load_directory(const char* dir);
RIMLUA_API void rimlua_call_on_load(void);
RIMLUA_API void rimlua_call_on_tick(void);
RIMLUA_API int rimlua_invoke_hook(int hook_id, int arg0_handle, int* out_continue);
RIMLUA_API void rimlua_emit_event(const char* name, int handle);
/* phase: "can_do" | "execute". out_result: 1=true/done, 0=false/continue. returns 0 on ok. */
RIMLUA_API int rimlua_job_call(const char* name, const char* phase, int pawn_handle, int* out_result);
RIMLUA_API void rimlua_ui_invoke(int callback_id);

#ifdef __cplusplus
}
#endif
