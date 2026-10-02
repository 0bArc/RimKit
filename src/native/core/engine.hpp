#pragma once

#include "rimlua_abi.h"

#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>

namespace rimlua {

struct HookEntry {
    int id = 0;
    bool is_prefix = true;
    std::string type_name;
    std::string method_name;
    sol::protected_function fn;
};

struct TimerEntry {
    int fire_tick = 0;
    sol::protected_function fn;
};

struct RimPawn {
    int h = 0;
};

struct RimMap {
    int h = 0;
};

struct RimThing {
    int h = 0;
};

struct RimFaction {
    int h = 0;
};

struct LuaJobDef {
    sol::protected_function can_do;
    sol::protected_function execute;
};

class Engine {
public:
    static Engine& instance();

    int init(const rimlua_callbacks* cb);
    void shutdown();

    int load_script(const std::string& path);
    int load_directory(const std::string& dir);

    void call_on_load();
    void call_on_tick();
    int invoke_hook(int hook_id, int arg0_handle, int* out_continue);
    void emit_event(const std::string& name, int handle);
    int job_call(const std::string& name, const std::string& phase, int pawn_handle, int* out_result);
    void ui_invoke(int callback_id);
    /** UTF-8 "label\\tid\\n..." for map right-click options from Lua. Valid until next call. */
    const char* collect_map_float_menu(int clicked_handle, int hauler_handle);

    const rimlua_callbacks& callbacks() const { return callbacks_; }
    bool ready() const { return ready_; }

    sol::object host_call(const std::string& op, const sol::table& args);
    sol::object host_call_h(const std::string& op, int h);
    sol::object host_call_hv(const std::string& op, int h, const std::string& key, sol::object value);

    RimPawn wrap_pawn(int h) const { return RimPawn{h}; }
    RimMap wrap_map(int h) const { return RimMap{h}; }
    RimThing wrap_thing(int h) const { return RimThing{h}; }
    RimFaction wrap_faction(int h) const { return RimFaction{h}; }

private:
    Engine() = default;

    void bind_rim_api();
    void bind_oo_types();
    void bind_events_and_timer();
    void bind_jobs_and_faction();
    void bind_ui_config_defs();
    void bind_strong_api();
    void wire_global_handlers();
    void apply_sandbox();
    void allow_lua_root(const std::string& dir);
    bool is_lua_path_allowed(const std::string& path) const;
    int register_ui_callback(sol::protected_function fn);
    int register_hook_lua(bool is_prefix, const std::string& type_name, const std::string& method_name,
                          sol::protected_function fn);
    void unregister_hook_lua(int hook_id);
    void log_lua_error(const std::string& context, const sol::protected_function_result& result);
    std::string table_to_json(const sol::table& args);
    sol::object decode_response(const std::string& json);
    int current_tick();

    rimlua_callbacks callbacks_{};
    bool ready_ = false;
    std::unique_ptr<sol::state> lua_;
    std::vector<sol::protected_function> on_load_;
    std::vector<sol::protected_function> on_tick_;
    std::unordered_map<int, HookEntry> hooks_;
    std::unordered_map<std::string, std::vector<sol::protected_function>> event_handlers_;
    std::unordered_map<std::string, LuaJobDef> lua_jobs_;
    std::unordered_map<int, sol::protected_function> ui_callbacks_;
    std::vector<sol::protected_function> map_float_menu_handlers_;
    std::string float_menu_blob_cache_;
    std::vector<TimerEntry> timers_;
    std::vector<std::string> allowed_lua_roots_;
    int next_hook_id_ = 1;
    int next_ui_id_ = 1;
};

}  // namespace rimlua
