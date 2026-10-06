#pragma once

#include "json_lite.hpp"
#include "rimlua_abi.h"

#include <chrono>
#include <set>
#include <memory>
#include <mutex>
#include <utility>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>

namespace rimlua {

// A Lua callback and the mod whose script registered it, for the per-mod error budget.
struct ModFn {
    sol::protected_function fn;
    std::string mod;
};

struct HookEntry {
    int id = 0;
    std::string mod;
    int kind = RIMLUA_HOOK_PREFIX;
    std::string type_name;
    std::string method_name;
    sol::protected_function fn;
    // Set by set_hook_info for hooks called through the fast path.
    std::string method;
    std::string phase;
    std::vector<std::string> names;
};

// Values a Lua hook asked the host to apply after it returned.
struct HookResponse {
    bool cont = true;
    bool has_result = false;
    std::string result_json;
    std::vector<std::pair<int, std::string>> arg_json;
    bool has_state = false;
    std::string state_json;
    bool suppress = false;
};

struct TimerEntry {
    std::string mod;
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

struct RimEntity {
    int h = 0;
};

// Any other game object, passed by handle. Use rim.reflect with .handle to read or call members.
struct RimObject {
    int h = 0;
    std::string type_name;
    std::string def;  // defName when the object has one (a Hediff, Job, Quest ...), else empty
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
    int invoke_hook(int hook_id, int arg0_handle, const char* ctx_json, int* out_continue);
    /** JSON response string, valid until the next hook call on this thread. Empty when nothing to apply. */
    const char* invoke_hook_ex(int hook_id, const char* ctx_json);
    /** Hook call without JSON for scalar-only methods. See rimlua_abi.h. */
    int invoke_hook_fast(int hook_id, int pawn_handle, int nargs, const double* args, const unsigned char* types,
                         int has_result, double result, unsigned char result_type, double* out_result, int* out_flags);
    void set_hook_info(int hook_id, const std::string& method, const std::string& phase, const std::string& names_json);
    /** Names the mod whose scripts load next. Callbacks they register are charged to it. Empty clears. */
    void set_mod_context(const std::string& package_id) { current_mod_ = package_id; }
    // Ecosystem (P6): per-mod profiling, capabilities, hot reload.
    std::string swap_mod(const std::string& mod) { std::string prev = current_mod_; current_mod_ = mod; return prev; }
    void prof_add(const std::string& mod, int kind, double us);
    void load_mod_info(const std::string& package_id, const std::string& lua_dir);
    int reload_mod(const std::string& package_id);
    void watch_tick();
    void set_test_mode(bool on) { test_mode_ = on; if (!on) mock_active_ = false; }
    bool test_mode() const { return test_mode_; }
    /** Lets a mod's callbacks run again, after the author fixed the script (used by tests and hot reload). */
    void reset_mod_budget(const std::string& package_id);
    bool is_mod_disabled(const std::string& package_id) const { return !mod_enabled(package_id); }
    void emit_event(const std::string& name, int handle);
    void emit_event_ex(const std::string& name, const std::string& payload_json);
    int job_call(const std::string& name, const std::string& phase, int pawn_handle, int* out_result);
    void ui_invoke(int callback_id);
    // Calls a registered Lua function with a JSON argument and returns its result as JSON (empty when nil or on error).
    const char* ui_call(int callback_id, const char* arg_json);
    /** UTF-8 "label\\tid\\n..." for map right-click options from Lua. Valid until next call. */
    const char* collect_map_float_menu(int clicked_handle, int hauler_handle);

    /** Lua is single threaded. Every exported entry point takes this lock (it is recursive). */
    std::recursive_mutex& mutex() { return mutex_; }
    const rimlua_callbacks& callbacks() const { return callbacks_; }
    bool ready() const { return ready_; }

    sol::object host_call(const std::string& op, const sol::table& args);
    sol::object host_call_h(const std::string& op, int h);
    sol::object host_call_hv(const std::string& op, int h, const std::string& key, sol::object value);

    RimPawn wrap_pawn(int h) const { return RimPawn{h}; }
    RimMap wrap_map(int h) const { return RimMap{h}; }
    RimThing wrap_thing(int h) const { return RimThing{h}; }
    RimFaction wrap_faction(int h) const { return RimFaction{h}; }
    RimEntity wrap_entity(int h) const { return RimEntity{h}; }

private:
    Engine() = default;

    void bind_rim_api();
    void bind_oo_types();
    void bind_events_and_timer();
    void bind_jobs_and_faction();
    void bind_ui_config_defs();
    void bind_strong_api();
    void bind_reflect_core();
    void bind_pawns_kit();
    void bind_version_helpers();
    void bind_time_kit();
    void bind_json_kits();
    // True when the host has an op with this name (the kit table lists them). Used to reject a mistyped mock.
    bool is_known_op(const std::string& op) const;
    void bind_kit_helpers();
    void bind_ecosystem();
    void check_capability_for_op(const std::string& op);
    bool try_mock(const std::string& op, const sol::table& args, sol::object& out);
    void require_capability(const std::string& cap, const std::string& what);
    /** Host call that raises a Lua error (message starts with an RK code) when the host reports a failure. */
    sol::object host_call_checked(const std::string& op, const sol::table& args);
    std::string request_json(const std::string& op, const sol::table& args);
    /** Builds game.<domain> from api/aliases.json and installs deprecation shims on the old names. */
    void bind_canonical_surface();
    void warn_deprecated(const std::string& old_name, const std::string& new_name);
    // The api level the running mod asked for in meta.api_level (0 when it did not).
    int mod_api_level() const;
    bool strict_now() const { return strict_errors_ || mod_api_level() >= 1; }
    /** Legacy event names are mapped to canonical ones. Returns the name unchanged when it is not legacy. */
    std::string canonical_event_name(const std::string& name);
    /** Registers an event handler. legacy_signature passes only the payload field the old API passed. */
    void add_event_handler(const std::string& name, sol::protected_function fn, bool legacy_signature = false);
    void bind_rimkit_module();
    void wire_global_handlers();
    void apply_sandbox();
    void allow_lua_root(const std::string& dir);
    bool is_lua_path_allowed(const std::string& path) const;
    int register_ui_callback(sol::protected_function fn);
    int register_hook_lua(int kind, const std::string& type_name, const std::string& method_name,
                          sol::protected_function fn, const std::string& opts_json = "{}");
    /** rim.hooks.patch{...} and rim.hooks.replace_call{...} option tables. Returns a table of ids. */
    sol::table hooks_patch_table(const sol::table& spec);
    sol::object hooks_redirect_call_table(const sol::table& spec);
    void unregister_hook_lua(int hook_id);
    void subscribe_named_event(const std::string& name);
    void unsubscribe_named_event(const std::string& name);
    void log_lua_error(const std::string& context, const sol::protected_function_result& result);
    std::string table_to_json(const sol::table& args);
    sol::object decode_response(const std::string& json);
    sol::table build_hook_context(int arg0_handle, const char* ctx_json);
    void attach_hook_mutators(sol::table& ctx);
    bool mod_enabled(const std::string& mod) const;
    void record_mod_error(const std::string& mod, const std::string& where);
    /** Hook value codec: host JSON to Lua and back. Handles are {"$h":n,"$k":"pawn|thing|map|faction|object"}. */
    sol::object json_to_lua(const rimlua::json::Value& v);
    void lua_to_json(const sol::object& o, std::string& out, int depth);
    std::string lua_value_json(const sol::object& o);
    static std::string options_table_json(const sol::table& spec);
    int current_tick();

    std::recursive_mutex mutex_;
    rimlua_callbacks callbacks_{};
    bool ready_ = false;
    std::unique_ptr<sol::state> lua_;
    std::vector<ModFn> on_load_;
    std::vector<ModFn> on_tick_;
    std::unordered_map<int, HookEntry> hooks_;
    std::unordered_map<std::string, std::vector<ModFn>> event_handlers_;
    std::unordered_map<std::string, int> named_event_subscriptions_;
    std::unordered_set<std::string> warned_aliases_;
    // One error model: older functions log and return nil. With strict errors on they raise like the newer kits.
    bool strict_errors_ = false;

    // Error budget: a mod whose callbacks keep failing is switched off so it cannot flood the log or slow the game.
    struct ModBudget {
        int errors = 0;
        std::chrono::steady_clock::time_point window_start{};
        bool disabled = false;
    };
    std::string current_mod_;  // package id of the mod whose scripts are loading, empty otherwise
    std::unordered_map<std::string, ModBudget> mod_budgets_;
    sol::protected_function event_adapter_maker_;
    sol::table thing_methods_;  // subject functions of the things kit, reached as thing:method() through the RimThing and RimPawn index fallback
    sol::protected_function event_filter_maker_;
    HookResponse* active_hook_response_ = nullptr;
    std::unordered_map<std::string, LuaJobDef> lua_jobs_;
    std::unordered_map<int, sol::protected_function> ui_callbacks_;
    std::vector<sol::protected_function> map_float_menu_handlers_;
    std::string float_menu_blob_cache_;
    std::string ui_call_cache_;
    std::unordered_map<int, int> ui_error_counts_;
    std::vector<TimerEntry> timers_;
    // Ecosystem state.
    struct ModProf {
        double us[6] = {0, 0, 0, 0, 0, 0};
        long long calls[6] = {0, 0, 0, 0, 0, 0};
        double window_base_us = 0;
        int window_base_tick = 0;
        double avg_tick_us = 0;
        bool over_budget = false;
    };
    struct ModInfo {
        std::string root;
        std::string lua_dir;
        std::vector<std::string> lua_dirs;
        std::string version;
        std::string name;
        bool declared = false;
        int api_level = 0;  // meta.api_level: 1 opts in to the api level 1 rules (errors raise, old names removed, capabilities enforced)
        std::set<std::string> caps;
        double budget_us = 300.0;
        long long newest_mtime = 0;
    };
    std::unordered_map<std::string, ModProf> mod_prof_;
    std::unordered_map<std::string, ModInfo> mods_;
    std::unordered_map<int, std::string> ui_callback_mod_;
    std::vector<std::string> map_float_menu_mods_;
    bool mock_active_ = false;
    bool test_mode_ = false;
    int test_errors_ = 0;  // handler and callback errors since the last reset, read by game.test.errors()
    bool dev_watch_ = false;
    int watch_counter_ = 0;
    bool in_reload_ = false;
    std::vector<std::string> allowed_lua_roots_;
    int next_hook_id_ = 1;
    int next_ui_id_ = 1;
};

enum ProfKind { PROF_TICK = 0, PROF_EVENT = 1, PROF_HOOK = 2, PROF_TIMER = 3, PROF_UI = 4, PROF_LOAD = 5 };

// Makes a mod the current mod while its callback runs (so capabilities and registrations are attributed) and times it.
struct ModScope {
    Engine& e;
    std::string prev;
    std::string mod;
    int kind;
    std::chrono::steady_clock::time_point t0;
    ModScope(Engine& en, const std::string& m, int k) : e(en), prev(en.swap_mod(m)), mod(m), kind(k), t0(std::chrono::steady_clock::now()) {}
    ~ModScope() {
        const double us = std::chrono::duration<double, std::micro>(std::chrono::steady_clock::now() - t0).count();
        e.prof_add(mod, kind, us);
        e.swap_mod(prev);
    }
};

}  // namespace rimlua
