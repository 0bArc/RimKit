#include "engine.hpp"
#include "lua_file.hpp"
#include "stdlib_promise.hpp"
#include "stdlib_signal.hpp"
#include "handle_util.hpp"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <iostream>
#include <sstream>

namespace fs = std::filesystem;

namespace rimlua {

namespace rj = rimlua::json;

namespace {

std::string json_escape(const std::string& s) {
    std::string out;
    out.reserve(s.size() + 8);
    for (char c : s) {
        switch (c) {
            case '"':
                out += "\\\"";
                break;
            case '\\':
                out += "\\\\";
                break;
            case '\n':
                out += "\\n";
                break;
            case '\r':
                out += "\\r";
                break;
            case '\t':
                out += "\\t";
                break;
            default:
                if (static_cast<unsigned char>(c) < 0x20) {
                    char buf[8];
                    std::snprintf(buf, sizeof(buf), "\\u%04x", static_cast<unsigned>(static_cast<unsigned char>(c)));
                    out += buf;
                } else {
                    out += c;
                }
                break;
        }
    }
    return out;
}

// rim.invoke allowlist: api.list or reflect.<op>
static bool invoke_op_allowed(const std::string& op) {
    if (op.empty()) {
        return false;
    }
    for (unsigned char c : op) {
        if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.')) {
            return false;
        }
    }
    if (op == "api.list") {
        return true;
    }
    static const char kReflect[] = "reflect.";
    constexpr size_t n = sizeof(kReflect) - 1;
    if (op.size() <= n || op.compare(0, n, kReflect) != 0) {
        return false;
    }
    const std::string suffix = op.substr(n);
    if (suffix.empty() || suffix.front() == '.' || suffix.back() == '.') {
        return false;
    }
    if (suffix.find("..") != std::string::npos) {
        return false;
    }
    return true;
}

std::string extract_string_field(const std::string& json, const char* key) {
    std::string pat = std::string("\"") + key + "\":\"";
    auto pos = json.find(pat);
    if (pos == std::string::npos) {
        return {};
    }
    pos += pat.size();
    std::string out;
    while (pos < json.size() && json[pos] != '"') {
        if (json[pos] == '\\' && pos + 1 < json.size()) {
            out += json[pos + 1];
            pos += 2;
        } else {
            out += json[pos++];
        }
    }
    return out;
}

bool extract_bool_field(const std::string& json, const char* key, bool* found = nullptr) {
    std::string pat = std::string("\"") + key + "\":";
    auto pos = json.find(pat);
    if (pos == std::string::npos) {
        if (found) *found = false;
        return false;
    }
    if (found) *found = true;
    pos += pat.size();
    return json.compare(pos, 4, "true") == 0;
}

double extract_number_field(const std::string& json, const char* key) {
    std::string pat = std::string("\"") + key + "\":";
    auto pos = json.find(pat);
    if (pos == std::string::npos) {
        return 0;
    }
    pos += pat.size();
    return std::strtod(json.c_str() + pos, nullptr);
}

std::string extract_type_field(const std::string& json) {
    std::string pat = "\"t\":\"";
    auto pos = json.find(pat);
    if (pos == std::string::npos) {
        return {};
    }
    pos += pat.size();
    std::string out;
    while (pos < json.size() && json[pos] != '"') {
        out += json[pos++];
    }
    return out;
}

}  // namespace

Engine& Engine::instance() {
    static Engine eng;
    return eng;
}

int Engine::init(const rimlua_callbacks* cb) {
    if (!cb || !cb->host_invoke || !cb->host_free) {
        return 1;
    }
    callbacks_ = *cb;
    lua_ = std::make_unique<sol::state>();
    // Strip io/os/debug/bit32.
    lua_->open_libraries(sol::lib::base, sol::lib::coroutine, sol::lib::string, sol::lib::table, sol::lib::math, sol::lib::utf8,
                         sol::lib::bit32);
    luaopen_buffer(lua_->lua_state());
    apply_sandbox();
    bind_rim_api();
    bind_oo_types();
    bind_events_and_timer();
    bind_jobs_and_faction();
    bind_ui_config_defs();
    bind_strong_api();
    bind_reflect_core();
    bind_pawns_kit();
    bind_version_helpers();
    bind_time_kit();
    bind_json_kits();
    bind_canonical_surface();
    bind_kit_helpers();
    bind_ecosystem();
    bind_rimkit_module();
    // One function per event, like on_load and on_tick: game.events.on_pawn_damaged(function(e) ... end, filter?).
    // The name is on_ plus the event name with the dot as an underscore. The editor types e from the function name,
    // which a string argument cannot do.
    lua_->safe_script(R"LUA(
local events = game.events
if type(events) == "table" and getmetatable(events) == nil then
  local multi = { "mental_state", "world_object" }
  setmetatable(events, { __index = function(t, k)
    if type(k) ~= "string" or k:sub(1, 3) ~= "on_" then return nil end
    local rest = k:sub(4)
    local domain, name
    for _, m in ipairs(multi) do
      if rest:sub(1, #m + 1) == m .. "_" then domain, name = m, rest:sub(#m + 2) break end
    end
    if not domain then domain, name = rest:match("^([a-z]+)_(.+)$") end
    if not domain then return nil end
    local event = domain .. "." .. name
    return function(fn, filter)
      -- The handler gets the subject of the event first (the pawn, else the thing) and the whole payload second.
      local function handler(e)
        local subject = e and (e.pawn or e.thing)
        if subject ~= nil then return fn(subject, e) end
        return fn(e)
      end
      if filter ~= nil then return t.on(event, filter, handler) end
      return t.on(event, handler)
    end
  end })
end
)LUA");
    // Built-in libraries: require("rimkit.signal") and require("rimkit.promise"), also reachable as rimkit.signal and rimkit.promise (the global rimkit is require("rimkit")).
    {
        sol::table preload = (*lua_)["package"]["preload"];
        const struct {
            const char* name;
            const char* source;
        } libs[] = {{"rimkit.signal", kSignalSource}, {"rimkit.promise", kPromiseSource}};
        for (const auto& lib : libs) {
            sol::load_result lr = lua_->load(lib.source, std::string("=") + lib.name);
            if (lr.valid()) {
                preload[lib.name] = lr.get<sol::protected_function>();
            } else if (callbacks_.log) {
                sol::error e = lr;
                callbacks_.log((std::string("[RimKit] built-in library ") + lib.name + " failed to load: " + e.what()).c_str());
            }
        }
        lua_->safe_script(R"LUA(
local rk = require("rimkit")
rimkit = rk
if type(rk) == "table" and getmetatable(rk) == nil then
  setmetatable(rk, { __index = function(t, k)
    if k == "signal" or k == "promise" then
      local module = require("rimkit." .. k)
      rawset(t, k, module)
      return module
    end
    return nil
  end })
end
)LUA");
    }
    ready_ = true;
    if (callbacks_.log) {
        callbacks_.log("[RimKit] C++ core initialized (sandboxed Lua)");
    }
    return 0;
}

void Engine::shutdown() {
    on_load_.clear();
    on_tick_.clear();
    hooks_.clear();
    event_handlers_.clear();
    lua_jobs_.clear();
    ui_callbacks_.clear();
    timers_.clear();
    allowed_lua_roots_.clear();
    lua_.reset();
    ready_ = false;
}

void Engine::apply_sandbox() {
    if (!lua_) {
        return;
    }
    // Strip loaders. Luau has no io, package, dofile or loadlib. loadstring, getfenv and setfenv exist and are removed.
    for (const char* name : {"dofile", "loadfile", "load", "loadstring", "getfenv", "setfenv", "io", "os", "debug", "package"}) {
        (*lua_)[name] = sol::lua_nil;
    }

    // A package table for loaded and preload, and a require that only reads files under the allowed Lua roots.
    sol::table package = lua_->create_table();
    package["loaded"] = lua_->create_table();
    package["preload"] = lua_->create_table();
    (*lua_)["package"] = package;

    sol::function find_module = sol::make_object(*lua_, [this](const std::string& modname) -> sol::object {
        if (!lua_) {
            return sol::make_object(*lua_, "RimKit sandbox: no state");
        }
        std::string dotted = modname;
        for (char& c : dotted) {
            if (c == '.') {
                c = '/';
            }
        }
        std::vector<std::string> candidates;
        candidates.push_back(dotted + ".luau");
        candidates.push_back(dotted + ".lua");
        candidates.push_back(dotted + "/init.luau");
        candidates.push_back(dotted + "/init.lua");
        for (const std::string& root : allowed_lua_roots_) {
            for (const std::string& rel : candidates) {
                fs::path full = fs::path(root) / rel;
                std::error_code ec;
                if (!fs::is_regular_file(full, ec)) {
                    continue;
                }
                std::string resolved = fs::weakly_canonical(full, ec).string();
                if (ec || !is_lua_path_allowed(resolved)) {
                    continue;
                }
                sol::load_result lr = load_lua_file(*lua_, resolved);
                if (!lr.valid()) {
                    sol::error e = lr;
                    return sol::make_object(*lua_, std::string(e.what()));
                }
                return lr.get<sol::object>();
            }
        }
        return sol::make_object(*lua_, "RimKit sandbox: module not in allowed Lua roots: " + modname);
    });
    {
        sol::load_result lr = lua_->load(R"LUA(
local find = ...
local loaded, preload = package.loaded, package.preload
return function(name)
  local v = loaded[name]
  if v ~= nil then return v end
  local loader = preload[name]
  if loader == nil then
    loader = find(name)
    if type(loader) ~= "function" then error(loader, 2) end
  end
  v = loader(name)
  if v == nil then v = true end
  loaded[name] = v
  return v
end
)LUA");
        sol::protected_function maker = lr;
        sol::protected_function_result require_fn = maker(find_module);
        if (require_fn.valid()) {
            (*lua_)["require"] = require_fn.get<sol::object>();
        }
    }

    // Freeze _G after stripping.
    try {
        lua_->script(R"LUA(
local frozen = {
  dofile=true, loadfile=true, load=true, loadstring=true,
  io=true, os=true, debug=true
}
local g = _G
local rawget, rawset, type = rawget, rawset, type
local mt = {
  __newindex = function(t, k, v)
    if frozen[k] then return end
    rawset(t, k, v)
  end,
  __index = function(t, k)
    return rawget(t, k)
  end
}
-- Preserve existing non-frozen entries; lock dangerous names to nil permanently.
for k,_ in pairs(frozen) do
  rawset(g, k, nil)
end
setmetatable(g, mt)
package.loadlib = nil
package.cpath = ""
)LUA");
    } catch (const sol::error& e) {
        if (callbacks_.log) {
            callbacks_.log((std::string("[RimKit] sandbox freeze warn: ") + e.what()).c_str());
        }
    }

    if (callbacks_.log) {
        callbacks_.log("[RimKit] Luau sandbox on (no io/os/dofile/loadstring/getfenv; require jailed; _G frozen)");
    }
}

void Engine::allow_lua_root(const std::string& dir) {
    std::error_code ec;
    fs::path p = fs::weakly_canonical(fs::path(dir), ec);
    if (ec) {
        p = fs::absolute(fs::path(dir), ec);
    }
    std::string root = p.string();
    while (!root.empty() && (root.back() == '/' || root.back() == '\\')) {
        root.pop_back();
    }
    for (const std::string& existing : allowed_lua_roots_) {
        if (existing == root) {
            return;
        }
    }
    allowed_lua_roots_.push_back(root);
    if (lua_) {
        sol::table package = (*lua_)["package"];
        std::string path = package["path"].get_or(std::string{});
        std::string add = root + "/?.lua;" + root + "/?/init.lua";
        if (path.empty()) {
            package["path"] = add;
        } else {
            package["path"] = path + ";" + add;
        }
    }
}

bool Engine::is_lua_path_allowed(const std::string& path) const {
    std::error_code ec;
    fs::path cand = fs::weakly_canonical(fs::path(path), ec);
    if (ec) {
        cand = fs::path(path);
    }
    std::string c = cand.string();
    for (char& ch : c) {
        if (ch == '\\') {
            ch = '/';
        }
    }
    for (const std::string& root : allowed_lua_roots_) {
        std::string r = root;
        for (char& ch : r) {
            if (ch == '\\') {
                ch = '/';
            }
        }
        if (c == r) {
            return true;
        }
        if (c.size() > r.size() && c.compare(0, r.size(), r) == 0 && c[r.size()] == '/') {
            return true;
        }
    }
    return false;
}

std::string Engine::table_to_json(const sol::table& args) {
    std::ostringstream ss;
    ss << '{';
    bool first = true;
    if (args.valid()) {
        for (const auto& kv : args) {
            sol::object key = kv.first;
            sol::object val = kv.second;
            if (key.get_type() != sol::type::string) {
                continue;
            }
            if (!first) {
                ss << ',';
            }
            first = false;
            ss << '"' << json_escape(key.as<std::string>()) << '"' << ':';
            switch (val.get_type()) {
                case sol::type::boolean:
                    ss << (val.as<bool>() ? "true" : "false");
                    break;
                case sol::type::number: {
                    double d = val.as<double>();
                    if (d == static_cast<int64_t>(d)) {
                        ss << static_cast<int64_t>(d);
                    } else {
                        ss << d;
                    }
                    break;
                }
                case sol::type::string:
                    ss << '"' << json_escape(val.as<std::string>()) << '"';
                    break;
                case sol::type::nil:
                    ss << "null";
                    break;
                default:
                    ss << '"' << json_escape(val.as<std::string>()) << '"';
                    break;
            }
        }
    }
    ss << '}';
    return ss.str();
}

sol::object Engine::decode_response(const std::string& json) {
    if (!lua_) {
        return sol::make_object(lua_->lua_state(), sol::lua_nil);
    }
    // Strict parse first. The legacy scanner below stays as a fallback for host replies that are not valid JSON
    // (for example a bare NaN) so older ops keep working.
    {
        rj::Value root;
        if (rj::parse(json, root) && root.is_object()) {
            if (!root.get_bool("ok", false)) {
                if (strict_now()) {
                    throw sol::error(root.get_string("e", "RK5001: host error"));
                }
                if (callbacks_.log) {
                    std::string msg = "[RimKit] host error: " + root.get_string("e");
                    callbacks_.log(msg.c_str());
                }
                return sol::make_object(*lua_, sol::lua_nil);
            }
            const std::string t = root.get_string("t");
            const rj::Value* v = root.find("v");
            if (t == "s" && v && v->is_string()) {
                return sol::make_object(*lua_, v->s);
            }
            if ((t == "i" || t == "h") && v && v->is_number()) {
                return sol::make_object(*lua_, static_cast<int>(v->as_double()));
            }
            if (t == "f" && v && v->is_number()) {
                return sol::make_object(*lua_, v->as_double());
            }
            if (t == "b" && v && v->type == rj::Value::Type::Bool) {
                return sol::make_object(*lua_, v->b);
            }
            if ((t == "a" || t == "sa") && v && v->is_array()) {
                sol::table arr = lua_->create_table();
                int idx = 1;
                for (const rj::Value& item : v->items) {
                    if (item.is_string()) {
                        arr[idx++] = item.s;
                    } else if (item.is_number()) {
                        arr[idx++] = static_cast<int>(item.as_double());
                    }
                }
                return sol::make_object(*lua_, arr);
            }
            if (t == "j" && v) {
                // Structured value: arbitrary JSON decoded into Lua tables.
                return json_to_lua(*v);
            }
        }
    }
    bool ok_found = false;
    bool ok = extract_bool_field(json, "ok", &ok_found);
    if (!ok) {
        std::string err = extract_string_field(json, "e");
        if (strict_now()) {
            throw sol::error(err.empty() ? std::string("RK5001: host error") : err);
        }
        if (callbacks_.log) {
            std::string msg = "[RimKit] host error: " + err;
            callbacks_.log(msg.c_str());
        }
        return sol::make_object(*lua_, sol::lua_nil);
    }
    std::string t = extract_type_field(json);
    if (t == "s") {
        return sol::make_object(*lua_, extract_string_field(json, "v"));
    }
    if (t == "i") {
        return sol::make_object(*lua_, static_cast<int>(extract_number_field(json, "v")));
    }
    if (t == "f") {
        return sol::make_object(*lua_, extract_number_field(json, "v"));
    }
    if (t == "b") {
        bool found = false;
        return sol::make_object(*lua_, extract_bool_field(json, "v", &found));
    }
    if (t == "h") {
        return sol::make_object(*lua_, static_cast<int>(extract_number_field(json, "v")));
    }
    if (t == "a" || t == "sa") {
        sol::table arr = lua_->create_table();
        auto pos = json.find("\"v\":[");
        if (pos == std::string::npos) {
            return sol::make_object(*lua_, arr);
        }
        pos += 5;
        int idx = 1;
        while (pos < json.size() && json[pos] != ']') {
            while (pos < json.size() && (json[pos] == ',' || json[pos] == ' ')) {
                ++pos;
            }
            if (pos >= json.size() || json[pos] == ']') {
                break;
            }
            if (json[pos] == '"') {
                ++pos;
                std::string s;
                while (pos < json.size() && json[pos] != '"') {
                    if (json[pos] == '\\' && pos + 1 < json.size()) {
                        s += json[pos + 1];
                        pos += 2;
                    } else {
                        s += json[pos++];
                    }
                }
                if (pos < json.size() && json[pos] == '"') {
                    ++pos;
                }
                arr[idx++] = s;
            } else {
                char* end = nullptr;
                long v = std::strtol(json.c_str() + pos, &end, 10);
                arr[idx++] = static_cast<int>(v);
                pos = static_cast<size_t>(end - json.c_str());
            }
        }
        return sol::make_object(*lua_, arr);
    }
    return sol::make_object(*lua_, sol::lua_nil);
}

// Ops that add something to the screen (gizmos, alerts, tabs, tools, status lines, settings pages).
static bool registers_ui(const std::string& op) {
    static const char* const kOps[] = {"gizmo.add", "gizmo.add_toggle", "gizmo.add_slider", "alert.add", "hud.status", "tabs.add_main",
                                       "tabs.add_inspect", "tabs.add_column", "designator.add", "options.page"};
    for (const char* o : kOps) {
        if (op == o) return true;
    }
    return false;
}

// The request the host receives. Ops that add something to the screen also carry the running mod's id as "_mod", so the host can
// remember who registered it and a hot reload can take it away again.
std::string Engine::request_json(const std::string& op, const sol::table& args) {
    std::string json = table_to_json(args);
    if (!current_mod_.empty() && registers_ui(op) && json.size() >= 2 && json.back() == '}') {
        json.insert(json.size() - 1, std::string(json.size() > 2 ? "," : "") + "\"_mod\":\"" + json_escape(current_mod_) + "\"");
    }
    return json;
}

sol::object Engine::host_call(const std::string& op, const sol::table& args) {
    if (mock_active_) {
        sol::object mocked;
        if (try_mock(op, args, mocked)) {
            return mocked;
        }
    }
    if (!ready_ || !callbacks_.host_invoke) {
        return sol::make_object(*lua_, sol::lua_nil);
    }
    try {
        check_capability_for_op(op);
    } catch (const sol::error& e) {
        if (callbacks_.log) {
            callbacks_.log((std::string("[RimKit] ") + e.what()).c_str());
        }
        return sol::make_object(*lua_, sol::lua_nil);
    }
    const std::string json = request_json(op, args);
    char* raw = callbacks_.host_invoke(op.c_str(), json.c_str());
    if (!raw) {
        return sol::make_object(*lua_, sol::lua_nil);
    }
    std::string resp(raw);
    callbacks_.host_free(raw);
    return decode_response(resp);
}

void Engine::bind_rim_api() {
    sol::table rim = lua_->create_named_table("rim");

    rim["log"] = [this](const std::string& msg) {
        if (callbacks_.log) {
            callbacks_.log(msg.c_str());
        }
    };
    rim["message"] = [this](const std::string& msg) {
        if (callbacks_.message) {
            callbacks_.message(msg.c_str());
        }
    };
    rim["on_load"] = [this](sol::protected_function fn) { on_load_.push_back(ModFn{std::move(fn), current_mod_}); };
    rim["on_tick"] = [this](sol::protected_function fn) { on_tick_.push_back(ModFn{std::move(fn), current_mod_}); };

    rim["invoke"] = [this](sol::object op_obj, sol::variadic_args va) {
        if (!op_obj.valid() || !op_obj.is<std::string>()) {
            if (callbacks_.log) {
                callbacks_.log("[RimKit] rim.invoke denied: op must be a non-empty string");
            }
            return sol::make_object(*lua_, sol::lua_nil);
        }
        const std::string op = op_obj.as<std::string>();
        if (!invoke_op_allowed(op)) {
            if (callbacks_.log) {
                std::string msg = "[RimKit] rim.invoke denied: " + op + " (use rim.<domain>.*; escape is api.list / reflect.* only)";
                callbacks_.log(msg.c_str());
            }
            return sol::make_object(*lua_, sol::lua_nil);
        }
        sol::table args = lua_->create_table();
        if (va.size() >= 1 && va[0].is<sol::table>()) {
            args = va[0].as<sol::table>();
        }
        return host_call(op, args);
    };

    // bind_op(table, "name", "host.op" [, {"key", ...}]): binds a host operation as a Lua function. Positional arguments
    // are sent under the listed keys, in order. The default is one argument, the subject handle "h". A single table
    // argument is sent as is. Nothing is guessed: an argument without a key is ignored.
    auto bind_op = [this](sol::table& tbl, const char* name, const char* op, std::vector<std::string> keys = {"h"}) {
        const std::string op_name = op;
        tbl[name] = [this, op_name, keys](sol::variadic_args va) {
            sol::table args = lua_->create_table();
            // A real Lua table is the argument table. A wrapped game object (userdata) is the subject, so it must not match here.
            if (va.size() >= 1 && va[0].get_type() == sol::type::table) {
                args = va[0].as<sol::table>();
            } else {
                size_t i = 0;
                for (auto v : va) {
                    if (i >= keys.size()) {
                        break;
                    }
                    sol::object value = v;
                    put_arg(args, keys[i].c_str(), value);
                    ++i;
                }
            }
            return host_call(op_name, args);
        };
    };

    sol::table hooks = lua_->create_table();
    // rim.hooks.prefix(type, method, fn [, opts]); opts: sig, priority, before, after.
    auto kind_binder = [this](int kind) {
        return [this, kind](const std::string& type_name, const std::string& method_name, sol::protected_function fn,
                            sol::optional<sol::table> opts) {
            const std::string opts_json = opts ? options_table_json(*opts) : std::string("{}");
            return register_hook_lua(kind, type_name, method_name, std::move(fn), opts_json);
        };
    };
    hooks["prefix"] = kind_binder(RIMLUA_HOOK_PREFIX);
    hooks["postfix"] = kind_binder(RIMLUA_HOOK_POSTFIX);
    hooks["finalizer"] = kind_binder(RIMLUA_HOOK_FINALIZER);
    // rim.hooks.patch{type=, method=, sig=, prefix=, postfix=, finalizer=, priority=, before=, after=}
    hooks["patch"] = [this](const sol::table& spec) { return hooks_patch_table(spec); };
    // rim.hooks.replace_call{type=, method=, sig=, call="Type.Method", call_sig=, nth=, fn=}
    hooks["replace_call"] = [this](const sol::table& spec) { return hooks_redirect_call_table(spec); };
    hooks["remove"] = [this](int hook_id) { unregister_hook_lua(hook_id); };
    // game.hooks.list(): one row per hook with its cost so far (calls, total_us, avg_us), measured by the host.
    hooks["list"] = [this]() {
        std::unordered_map<int, sol::table> stats;
        sol::object stats_obj = host_call("hooks.stats", lua_->create_table());
        if (stats_obj.is<sol::table>()) {
            sol::table st = stats_obj.as<sol::table>();
            for (size_t i = 1; i <= st.size(); ++i) {
                sol::object row_obj = st[i];
                if (row_obj.is<sol::table>()) {
                    sol::table row = row_obj.as<sol::table>();
                    stats[row.get_or("id", 0)] = row;
                }
            }
        }
        sol::table out = lua_->create_table();
        int n = 1;
        for (const auto& kv : hooks_) {
            sol::table row = lua_->create_table();
            row["id"] = kv.second.id;
            row["kind"] = kv.second.kind == RIMLUA_HOOK_PREFIX      ? "prefix"
                          : kv.second.kind == RIMLUA_HOOK_POSTFIX   ? "postfix"
                          : kv.second.kind == RIMLUA_HOOK_FINALIZER ? "finalizer"
                                                                    : "replace_call";
            row["type"] = kv.second.type_name;
            row["method"] = kv.second.method_name;
            auto st = stats.find(kv.second.id);
            row["calls"] = st != stats.end() ? st->second.get_or("calls", 0) : 0;
            row["total_us"] = st != stats.end() ? st->second.get_or("total_us", 0.0) : 0.0;
            row["avg_us"] = st != stats.end() ? st->second.get_or("avg_us", 0.0) : 0.0;
            row["fast"] = st != stats.end() ? st->second.get_or("fast", false) : false;
            out[n++] = row;
        }
        return out;
    };
    bind_op(hooks, "has_patch", "harmony.has_patch", {"type", "method"});
    bind_op(hooks, "type_exists", "harmony.type_exists", {"type"});
    // Game-update watch helpers. sig is a comma separated list of parameter type names.
    bind_op(hooks, "check_target", "hooks.check_target", {"type", "method", "sig"});
    bind_op(hooks, "check_events", "hooks.check_events", {});
    bind_op(hooks, "method_exists", "harmony.method_exists", {"type", "method"});
    rim["hooks"] = hooks;
    rim["harmony"] = hooks;

    // rim.prefix["RimWorld.JobGiver_GetFood"].TryGiveJob = function(pawn) ... end
    auto make_type_selector = [this](bool is_prefix) {
        sol::table selector = lua_->create_table();
        sol::table mt = lua_->create_table();
        mt["__index"] = [this, is_prefix](sol::object /*self*/, sol::object key) -> sol::object {
            if (!key.is<std::string>()) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            std::string type_name = key.as<std::string>();
            sol::table proxy = lua_->create_table();
            sol::table pmt = lua_->create_table();
            pmt["__newindex"] = [this, is_prefix, type_name](sol::object /*t*/, sol::object mkey, sol::object value) {
                if (!mkey.is<std::string>()) {
                    return;
                }
                const int kind = is_prefix ? RIMLUA_HOOK_PREFIX : RIMLUA_HOOK_POSTFIX;
                if (value.is<sol::protected_function>()) {
                    register_hook_lua(kind, type_name, mkey.as<std::string>(), value.as<sol::protected_function>());
                } else if (value.is<sol::table>()) {
                    // { fn = function(ctx) ... end, sig = {"System.Int32"}, priority = 400 }
                    sol::table spec = value.as<sol::table>();
                    sol::object fn = spec["fn"];
                    if (fn.is<sol::protected_function>()) {
                        register_hook_lua(kind, type_name, mkey.as<std::string>(), fn.as<sol::protected_function>(),
                                          options_table_json(spec));
                    }
                }
            };
            proxy[sol::metatable_key] = pmt;
            return sol::make_object(*lua_, proxy);
        };
        selector[sol::metatable_key] = mt;
        return selector;
    };
    rim["prefix"] = make_type_selector(true);
    rim["prefixes"] = rim["prefix"];
    rim["postfix"] = make_type_selector(false);
    rim["postfixes"] = rim["postfix"];

    // rim.events.prefix["RimWorld.JobGiver_GetFood.TryGiveJob"] = fn
    auto make_event_map = [this](bool is_prefix) {
        sol::table map = lua_->create_table();
        sol::table mt = lua_->create_table();
        mt["__newindex"] = [this, is_prefix](sol::object /*t*/, sol::object key, sol::object value) {
            if (!key.is<std::string>() || !value.is<sol::protected_function>()) {
                return;
            }
            std::string dotted = key.as<std::string>();
            auto pos = dotted.rfind('.');
            if (pos == std::string::npos || pos == 0 || pos + 1 >= dotted.size()) {
                if (callbacks_.log) {
                    callbacks_.log("[RimKit] events key need Type.Method");
                }
                return;
            }
            register_hook_lua(is_prefix ? RIMLUA_HOOK_PREFIX : RIMLUA_HOOK_POSTFIX, dotted.substr(0, pos),
                              dotted.substr(pos + 1), value.as<sol::protected_function>());
        };
        map[sol::metatable_key] = mt;
        return map;
    };
    sol::table events = lua_->create_table();
    events["prefix"] = make_event_map(true);
    events["postfix"] = make_event_map(false);
    rim["events"] = events;

    sol::table find = lua_->create_table();
    bind_op(find, "tick", "find.tick");
    bind_op(find, "current_map", "find.current_map");
    bind_op(find, "world", "find.world");
    bind_op(find, "selected", "find.selector_first");
    bind_op(find, "selected_things", "find.selected_things");
    bind_op(find, "maps", "find.maps");
    bind_op(find, "any_player_pawn", "find.any_player_pawn");
    rim["find"] = find;

    sol::table defs = lua_->create_table();
    defs["get"] = [this](const std::string& typeName, const std::string& name) {
        sol::table args = lua_->create_table();
        args["type"] = typeName;
        args["name"] = name;
        return host_call("defs.get", args);
    };
    defs["exists"] = [this](const std::string& typeName, const std::string& name) {
        sol::table args = lua_->create_table();
        args["type"] = typeName;
        args["name"] = name;
        return host_call("defs.exists", args);
    };
    defs["list"] = [this](const std::string& typeName) {
        sol::table args = lua_->create_table();
        args["type"] = typeName;
        return host_call("defs.list", args);
    };
    bind_op(defs, "label", "defs.label");
    rim["defs"] = defs;

    sol::table map = lua_->create_table();
    bind_op(map, "nutrition", "map.nutrition");
    bind_op(map, "total_human_edible_nutrition", "map.nutrition");  // alias
    bind_op(map, "width", "map.width");
    bind_op(map, "height", "map.height");
    bind_op(map, "pawns", "map.pawns");
    bind_op(map, "colonists", "map.colonists");
    bind_op(map, "prisoners", "map.prisoners");
    bind_op(map, "things", "map.things");
    map["things_of_def"] = [this](HandleArg h, const std::string& def) {
        sol::table args = lua_->create_table();
        args["h"] = h.v;
        args["def"] = def;
        return host_call("map.things_of_def", args);
    };
    map["spawn"] = [this](HandleArg h, const std::string& def, int x, int z, sol::optional<int> stack) {
        sol::table args = lua_->create_table();
        args["h"] = h.v;
        args["def"] = def;
        args["x"] = x;
        args["z"] = z;
        if (stack) {
            args["stack"] = *stack;
        }
        return host_call("map.spawn", args);
    };
    rim["map"] = map;

    sol::table thing = lua_->create_table();
    bind_op(thing, "def", "thing.def");
    bind_op(thing, "label", "thing.label");
    bind_op(thing, "label_short", "thing.label_short");
    bind_op(thing, "destroy", "thing.destroy");
    bind_op(thing, "despawn", "thing.despawn");
    bind_op(thing, "pos", "thing.pos");
    thing["set_pos"] = [this](HandleArg h, int x, int z) {
        sol::table args = lua_->create_table();
        args["h"] = h.v;
        args["x"] = x;
        args["z"] = z;
        return host_call("thing.set_pos", args);
    };
    bind_op(thing, "hp", "thing.hp");
    bind_op(thing, "max_hp", "thing.max_hp");
    thing["set_hp"] = [this](HandleArg h, int v) {
        sol::table args = lua_->create_table();
        args["h"] = h.v;
        args["v"] = v;
        return host_call("thing.set_hp", args);
    };
    bind_op(thing, "stack", "thing.stack");
    thing["set_stack"] = [this](HandleArg h, int v) {
        sol::table args = lua_->create_table();
        args["h"] = h.v;
        args["v"] = v;
        return host_call("thing.set_stack", args);
    };
    bind_op(thing, "faction", "thing.faction");
    thing["set_faction"] = [this](HandleArg h, HandleArg faction) {
        sol::table args = lua_->create_table();
        args["h"] = h.v;
        args["faction"] = faction.v;
        return host_call("thing.set_faction", args);
    };
    bind_op(thing, "map", "thing.map");
    bind_op(thing, "spawned", "thing.spawned");
    rim["thing"] = thing;

    sol::table pawn = lua_->create_table();
    bind_op(pawn, "is_humanlike", "pawn.is_humanlike");
    bind_op(pawn, "is_colonist", "pawn.is_colonist");
    bind_op(pawn, "is_prisoner", "pawn.is_prisoner");
    bind_op(pawn, "is_slave", "pawn.is_slave");
    bind_op(pawn, "is_downed", "pawn.is_downed");
    bind_op(pawn, "is_dead", "pawn.is_dead");
    bind_op(pawn, "faction_is_player", "pawn.faction_is_player");
    bind_op(pawn, "name", "pawn.name");
    bind_op(pawn, "gender", "pawn.gender");
    bind_op(pawn, "age", "pawn.age");
    bind_op(pawn, "kind", "pawn.kind");
    bind_op(pawn, "map", "pawn.map");
    bind_op(pawn, "hunger", "pawn.hunger");
    bind_op(pawn, "hunger_pct", "pawn.hunger");  // alias
    bind_op(pawn, "rest", "pawn.rest");
    bind_op(pawn, "recreation", "pawn.recreation");
    bind_op(pawn, "mood", "pawn.mood");
    pawn["set_hunger"] = [this](HandleArg h, double v) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["v"] = v;
        return host_call("pawn.set_hunger", a);
    };
    pawn["set_rest"] = [this](HandleArg h, double v) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["v"] = v;
        return host_call("pawn.set_rest", a);
    };
    bind_op(pawn, "health_pct", "pawn.health_pct");
    bind_op(pawn, "kill", "pawn.kill");
    bind_op(pawn, "drafted", "pawn.drafted");
    pawn["set_drafted"] = [this](HandleArg h, bool v) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["v"] = v;
        return host_call("pawn.set_drafted", a);
    };
    bind_op(pawn, "equipment", "pawn.equipment");
    bind_op(pawn, "apparel", "pawn.apparel");
    bind_op(pawn, "inventory", "pawn.inventory");
    bind_op(pawn, "carry", "pawn.carry");
    pawn["give_hediff"] = [this](HandleArg h, const std::string& def, sol::optional<double> sev) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["def"] = def;
        if (sev) a["severity"] = *sev;
        return host_call("pawn.give_hediff", a);
    };
    pawn["remove_hediff"] = [this](HandleArg h, const std::string& def) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["def"] = def;
        return host_call("pawn.remove_hediff", a);
    };
    bind_op(pawn, "hediffs", "pawn.hediffs");
    pawn["skill"] = [this](HandleArg h, const std::string& skill) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["skill"] = skill;
        return host_call("pawn.skill", a);
    };
    pawn["set_skill"] = [this](HandleArg h, const std::string& skill, int v) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["skill"] = skill;
        a["v"] = v;
        return host_call("pawn.set_skill", a);
    };
    pawn["give_thing"] = [this](HandleArg h, const std::string& def, sol::optional<int> stack) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["def"] = def;
        if (stack) a["stack"] = *stack;
        return host_call("pawn.give_thing", a);
    };
    bind_op(pawn, "strip", "pawn.strip");
    bind_op(pawn, "job_def", "pawn.job_def");
    bind_op(pawn, "end_job", "pawn.end_job");
    rim["pawn"] = pawn;

    sol::table faction = lua_->create_table();
    bind_op(faction, "player", "faction.player");
    bind_op(faction, "list", "faction.list");
    bind_op(faction, "name", "faction.name");
    faction["of_def"] = [this](const std::string& def) {
        sol::table a = lua_->create_table();
        a["def"] = def;
        return host_call("faction.of_def", a);
    };
    rim["faction"] = faction;

    sol::table job = lua_->create_table();
    job["make"] = [this](const std::string& def, sol::optional<int> target) {
        sol::table a = lua_->create_table();
        a["def"] = def;
        if (target) a["target"] = *target;
        return host_call("job.make", a);
    };
    job["start"] = [this](HandleArg pawn, HandleArg jobHandle) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.v;
        a["job"] = jobHandle.v;
        return host_call("job.start", a);
    };
    rim["job"] = job;

    sol::table ui = lua_->create_table();
    ui["message"] = [this](const std::string& text) {
        sol::table a = lua_->create_table();
        a["text"] = text;
        return host_call("ui.message", a);
    };
    ui["letter"] = [this](const std::string& label, const std::string& text) {
        sol::table a = lua_->create_table();
        a["label"] = label;
        a["text"] = text;
        return host_call("ui.letter", a);
    };
    rim["ui"] = ui;

    sol::table reflect = lua_->create_table();
    bind_op(reflect, "type", "reflect_v1.type");
    reflect["get"] = [this](HandleArg h, const std::string& member) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["member"] = member;
        return host_call("reflect_v1.get", a);
    };
    reflect["set"] = [this](HandleArg h, const std::string& member, sol::object value) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["member"] = member;
        if (value.is<bool>()) a["value"] = value.as<bool>() ? "true" : "false";
        else if (value.is<double>()) a["value"] = std::to_string(value.as<double>());
        else if (value.is<int>()) a["value"] = std::to_string(value.as<int>());
        else a["value"] = value.as<std::string>();
        return host_call("reflect_v1.set", a);
    };
    reflect["call"] = [this](HandleArg h, const std::string& method, sol::optional<std::string> args) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["method"] = method;
        if (args) a["args"] = *args;
        return host_call("reflect_v1.call", a);
    };
    reflect["static_get"] = [this](const std::string& typeName, const std::string& member) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["member"] = member;
        return host_call("reflect_v1.static_get", a);
    };
    reflect["static_call"] = [this](const std::string& typeName, const std::string& method, sol::optional<std::string> args) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["method"] = method;
        if (args) a["args"] = *args;
        return host_call("reflect_v1.static_call", a);
    };
    bind_op(reflect, "members", "reflect_v1.members");
    reflect["handle_of_static"] = [this](const std::string& typeName, const std::string& member) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["member"] = member;
        return host_call("reflect_v1.handle_of_static", a);
    };
    rim["reflect"] = reflect;
    rim["cs"] = reflect;  // alias
}

int Engine::register_hook_lua(int kind, const std::string& type_name, const std::string& method_name,
                              sol::protected_function fn, const std::string& opts_json) {
    const int id = next_hook_id_++;
    HookEntry entry;
    entry.id = id;
    require_capability("hooks", "game.hooks");
    entry.mod = current_mod_;
    entry.kind = kind;
    entry.type_name = type_name;
    entry.method_name = method_name;
    entry.fn = std::move(fn);
    hooks_[id] = std::move(entry);

    if (callbacks_.register_hook) {
        if (!callbacks_.register_hook(type_name.c_str(), method_name.c_str(), kind, id, opts_json.c_str())) {
            hooks_.erase(id);
            if (callbacks_.log) {
                callbacks_.log("[RimKit] Failed to register Harmony hook on host");
            }
            return 0;
        }
    }
    return id;
}

void Engine::unregister_hook_lua(int hook_id) {
    auto it = hooks_.find(hook_id);
    if (it == hooks_.end()) {
        return;
    }
    if (callbacks_.unregister_hook) {
        callbacks_.unregister_hook(hook_id);
    }
    hooks_.erase(it);
}

namespace {

void append_string_array(std::string& out, const sol::object& value) {
    out += '[';
    if (value.is<sol::table>()) {
        sol::table t = value.as<sol::table>();
        bool first = true;
        for (size_t i = 1; i <= t.size(); ++i) {
            sol::object item = t[i];
            if (!item.is<std::string>()) {
                continue;
            }
            if (!first) {
                out += ',';
            }
            first = false;
            rj::write_string(out, item.as<std::string>());
        }
    } else if (value.is<std::string>()) {
        rj::write_string(out, value.as<std::string>());
    }
    out += ']';
}

}  // namespace

std::string Engine::options_table_json(const sol::table& spec) {
    std::string out = "{";
    bool first = true;
    auto sep = [&]() {
        if (!first) {
            out += ',';
        }
        first = false;
    };
    for (const char* key : {"sig", "before", "after", "call_sig"}) {
        sol::object v = spec[key];
        if (v.get_type() == sol::type::nil) {
            continue;
        }
        sep();
        rj::write_string(out, key);
        out += ':';
        append_string_array(out, v);
    }
    for (const char* key : {"priority", "nth"}) {
        sol::object v = spec[key];
        if (v.is<double>()) {
            sep();
            rj::write_string(out, key);
            out += ':';
            out += std::to_string(static_cast<long long>(v.as<double>()));
        }
    }
    sol::object call = spec["call"];
    if (call.is<std::string>()) {
        sep();
        out += "\"call\":";
        rj::write_string(out, call.as<std::string>());
    }
    out += '}';
    return out;
}

sol::table Engine::hooks_patch_table(const sol::table& spec) {
    sol::table ids = lua_->create_table();
    const std::string type_name = spec.get_or("type", std::string());
    const std::string method_name = spec.get_or("method", std::string());
    if (type_name.empty() || method_name.empty()) {
        if (callbacks_.log) {
            callbacks_.log("[RimKit] rim.hooks.patch needs type and method");
        }
        return ids;
    }
    std::string opts = options_table_json(spec);
    // Prefix, postfix and finalizer of one patch share a state slot. The group is the id of the first hook.
    const std::string group = "\"group\":" + std::to_string(next_hook_id_);
    opts = opts == "{}" ? "{" + group + "}" : opts.substr(0, opts.size() - 1) + "," + group + "}";
    struct Slot {
        const char* field;
        int kind;
    };
    for (const Slot& slot : {Slot{"prefix", RIMLUA_HOOK_PREFIX}, Slot{"postfix", RIMLUA_HOOK_POSTFIX},
                             Slot{"finalizer", RIMLUA_HOOK_FINALIZER}}) {
        sol::object fn = spec[slot.field];
        if (!fn.is<sol::protected_function>()) {
            continue;
        }
        ids[slot.field] = register_hook_lua(slot.kind, type_name, method_name, fn.as<sol::protected_function>(), opts);
    }
    return ids;
}

sol::object Engine::hooks_redirect_call_table(const sol::table& spec) {
    const std::string type_name = spec.get_or("type", std::string());
    const std::string method_name = spec.get_or("method", std::string());
    const std::string call = spec.get_or("call", std::string());
    sol::object fn = spec["fn"];
    if (type_name.empty() || method_name.empty() || call.empty() || !fn.is<sol::protected_function>()) {
        if (callbacks_.log) {
            callbacks_.log("[RimKit] rim.hooks.replace_call needs type, method, call and fn");
        }
        return sol::make_object(*lua_, sol::lua_nil);
    }
    const int id = register_hook_lua(RIMLUA_HOOK_REDIRECT_CALL, type_name, method_name, fn.as<sol::protected_function>(),
                                     options_table_json(spec));
    return sol::make_object(*lua_, id);
}

void Engine::subscribe_named_event(const std::string& name) {
    // Legacy names without a dot are always on. Canonical names install a host patch on first use.
    if (name.find('.') == std::string::npos) {
        return;
    }
    if (named_event_subscriptions_[name]++ > 0) {
        return;
    }
    sol::table a = lua_->create_table();
    a["name"] = name;
    host_call("events.subscribe", a);
}

void Engine::unsubscribe_named_event(const std::string& name) {
    auto it = named_event_subscriptions_.find(name);
    if (it == named_event_subscriptions_.end()) {
        return;
    }
    if (--it->second > 0) {
        return;
    }
    named_event_subscriptions_.erase(it);
    sol::table a = lua_->create_table();
    a["name"] = name;
    host_call("events.unsubscribe", a);
}

sol::object Engine::json_to_lua(const rj::Value& v) {
    switch (v.type) {
        case rj::Value::Type::Null:
            return sol::make_object(*lua_, sol::lua_nil);
        case rj::Value::Type::Bool:
            return sol::make_object(*lua_, v.b);
        case rj::Value::Type::Int:
            return sol::make_object(*lua_, static_cast<long long>(v.i));
        case rj::Value::Type::Float:
            return sol::make_object(*lua_, v.d);
        case rj::Value::Type::String:
            return sol::make_object(*lua_, v.s);
        case rj::Value::Type::Array: {
            sol::table t = lua_->create_table();
            int idx = 1;
            for (const rj::Value& item : v.items) {
                t[idx++] = json_to_lua(item);
            }
            return sol::make_object(*lua_, t);
        }
        case rj::Value::Type::Object: {
            const rj::Value* handle = v.find("$h");
            if (handle) {
                const int h = static_cast<int>(handle->as_double());
                if (h <= 0) {
                    return sol::make_object(*lua_, sol::lua_nil);
                }
                const std::string kind = v.get_string("$k");
                if (kind == "pawn") {
                    return sol::make_object(*lua_, wrap_pawn(h));
                }
                if (kind == "thing") {
                    return sol::make_object(*lua_, wrap_thing(h));
                }
                if (kind == "map") {
                    return sol::make_object(*lua_, wrap_map(h));
                }
                if (kind == "faction") {
                    return sol::make_object(*lua_, wrap_faction(h));
                }
                return sol::make_object(*lua_, RimObject{h, v.get_string("$t"), v.get_string("def")});
            }
            sol::table t = lua_->create_table();
            for (size_t i = 0; i < v.keys.size(); ++i) {
                t[v.keys[i]] = json_to_lua(v.items[i]);
            }
            return sol::make_object(*lua_, t);
        }
    }
    return sol::make_object(*lua_, sol::lua_nil);
}

void Engine::lua_to_json(const sol::object& o, std::string& out, int depth) {
    if (depth > 16) {
        out += "null";
        return;
    }
    switch (o.get_type()) {
        case sol::type::boolean:
            out += o.as<bool>() ? "true" : "false";
            return;
        case sol::type::number: {
            const double d = o.as<double>();
            if (d == std::floor(d) && std::fabs(d) < 9.0e15) {
                out += std::to_string(static_cast<long long>(d));
            } else {
                rj::write_double(out, d);
            }
            return;
        }
        case sol::type::string:
            rj::write_string(out, o.as<std::string>());
            return;
        case sol::type::table: {
            sol::table t = o.as<sol::table>();
            const size_t n = t.size();
            if (n > 0) {
                out += '[';
                for (size_t i = 1; i <= n; ++i) {
                    if (i > 1) {
                        out += ',';
                    }
                    sol::object item = t[i];
                    lua_to_json(item, out, depth + 1);
                }
                out += ']';
                return;
            }
            out += '{';
            bool first = true;
            for (const auto& kv : t) {
                sol::object key = kv.first;
                if (key.get_type() != sol::type::string) {
                    continue;
                }
                if (!first) {
                    out += ',';
                }
                first = false;
                rj::write_string(out, key.as<std::string>());
                out += ':';
                sol::object val = kv.second;
                lua_to_json(val, out, depth + 1);
            }
            out += '}';
            return;
        }
        case sol::type::userdata: {
            int h = 0;
            if (o.is<RimPawn>()) {
                h = o.as<RimPawn&>().h;
            } else if (o.is<RimThing>()) {
                h = o.as<RimThing&>().h;
            } else if (o.is<RimMap>()) {
                h = o.as<RimMap&>().h;
            } else if (o.is<RimFaction>()) {
                h = o.as<RimFaction&>().h;
            } else if (o.is<RimEntity>()) {
                h = o.as<RimEntity&>().h;
            } else if (o.is<RimObject>()) {
                h = o.as<RimObject&>().h;
            }
            if (h > 0) {
                out += "{\"$h\":" + std::to_string(h) + "}";
            } else {
                out += "null";
            }
            return;
        }
        default:
            out += "null";
            return;
    }
}

std::string Engine::lua_value_json(const sol::object& o) {
    std::string out;
    lua_to_json(o, out, 0);
    return out;
}

void Engine::log_lua_error(const std::string& context, const sol::protected_function_result& result) {
    std::string err = context + ": ";
    if (!result.valid()) {
        sol::error e = result;
        err += e.what();
    } else {
        err += "unknown error";
    }
    if (callbacks_.log) {
        callbacks_.log(err.c_str());
    } else {
        std::cerr << err << std::endl;
    }
}

int Engine::load_script(const std::string& path) {
    if (!ready_ || !lua_) {
        return 1;
    }
    std::error_code ec;
    std::string resolved = fs::weakly_canonical(fs::path(path), ec).string();
    if (ec) {
        resolved = path;
    }
    if (!allowed_lua_roots_.empty() && !is_lua_path_allowed(resolved)) {
        if (callbacks_.log) {
            std::string msg = "[RimKit] sandbox blocked load outside Lua roots: " + resolved;
            callbacks_.log(msg.c_str());
        }
        return 3;
    }
    ModScope scope(*this, current_mod_, PROF_LOAD);
    sol::protected_function_result result = run_lua_file(*lua_, resolved);
    if (!result.valid()) {
        log_lua_error(std::string("[RimKit] load ") + resolved, result);
        return 2;
    }
    return 0;
}

int Engine::load_directory(const std::string& dir) {
    if (!ready_) {
        return 1;
    }
    std::error_code ec;
    if (!fs::exists(dir, ec)) {
        return 1;
    }
    allow_lua_root(dir);
    load_mod_info(current_mod_, dir);
    std::vector<fs::path> files;
    for (auto it = fs::recursive_directory_iterator(dir, ec); !ec && it != fs::recursive_directory_iterator(); ++it) {
        if (!it->is_regular_file()) {
            continue;
        }
        if (it->path().extension() == ".lua" || it->path().extension() == ".luau") {
            files.push_back(it->path());
        }
    }
    std::sort(files.begin(), files.end());
    int errors = 0;
    for (const auto& f : files) {
        if (load_script(f.string()) != 0) {
            ++errors;
        }
    }
    return errors == 0 ? 0 : 2;
}

void Engine::call_on_load() {
    wire_global_handlers();
    for (auto& cb : on_load_) {
        if (!mod_enabled(cb.mod)) {
            continue;
        }
        ModScope scope(*this, cb.mod, PROF_LOAD);
        sol::protected_function_result r = cb.fn();
        if (!r.valid()) {
            log_lua_error("[RimKit] on_load", r);
            record_mod_error(cb.mod, "on_load");
        }
    }
}

void Engine::call_on_tick() {
    int tick = current_tick();
    for (auto it = timers_.begin(); it != timers_.end();) {
        if (it->fire_tick <= tick) {
            if (mod_enabled(it->mod)) {
                ModScope scope(*this, it->mod, PROF_TIMER);
                sol::protected_function_result r = it->fn();
                if (!r.valid()) {
                    log_lua_error("[RimKit] timer", r);
                    record_mod_error(it->mod, "timer");
                }
            }
            it = timers_.erase(it);
        } else {
            ++it;
        }
    }

    sol::object sel = host_call("find.selector_first", lua_->create_table());
    int sh = sel.is<int>() ? sel.as<int>() : 0;
    if (sh != 0) {
        (*lua_)["selected_pawn"] = wrap_pawn(sh);
    } else {
        (*lua_)["selected_pawn"] = sol::lua_nil;
    }

    watch_tick();
    for (auto& cb : on_tick_) {
        if (!mod_enabled(cb.mod)) {
            continue;
        }
        ModScope scope(*this, cb.mod, PROF_TICK);
        sol::protected_function_result r = cb.fn();
        if (!r.valid()) {
            log_lua_error("[RimKit] on_tick", r);
            record_mod_error(cb.mod, "on_tick");
        }
    }
}

int Engine::invoke_hook(int hook_id, int /*arg0_handle*/, const char* ctx_json, int* out_continue) {
    if (out_continue) {
        *out_continue = 1;
    }
    auto it = hooks_.find(hook_id);
    if (it == hooks_.end() || !ready_) {
        return 1;
    }
    const char* raw = invoke_hook_ex(hook_id, ctx_json);
    if (!raw || !*raw) {
        return 2;
    }
    rj::Value resp;
    if (rj::parse(raw, resp) && out_continue) {
        *out_continue = resp.get_bool("cont", true) ? 1 : 0;
    }
    return 0;
}

const char* Engine::invoke_hook_ex(int hook_id, const char* ctx_json) {
    // Hooks can nest (a hook triggers game code that triggers another hook), so the buffer is per thread
    // and the host copies it before the next call.
    thread_local std::string out;
    out.clear();
    auto it = hooks_.find(hook_id);
    if (it == hooks_.end() || !ready_) {
        return out.c_str();
    }
    // Copy: the Lua function may remove its own hook while it runs.
    sol::protected_function fn = it->second.fn;
    const int kind = it->second.kind;
    const std::string hook_mod = it->second.mod;
    if (!mod_enabled(hook_mod)) {
        return out.c_str();
    }

    HookResponse resp;
    HookResponse* previous = active_hook_response_;
    active_hook_response_ = &resp;
    sol::table ctx = build_hook_context(0, ctx_json);
    sol::protected_function_result r;
    {
        ModScope scope(*this, hook_mod, PROF_HOOK);
        r = fn(ctx);
    }
    active_hook_response_ = previous;
    if (!r.valid()) {
        log_lua_error("[RimKit] hook " + std::to_string(hook_id), r);
        record_mod_error(hook_mod, "hook " + std::to_string(hook_id));
        return out.c_str();
    }

    if (r.return_count() > 0) {
        const sol::type t = r.get_type(0);
        if (kind == RIMLUA_HOOK_PREFIX && t == sol::type::boolean) {
            // Compatibility: `return false` from a prefix skips the original.
            if (!r.get<bool>(0)) {
                resp.cont = false;
            }
        } else if (kind == RIMLUA_HOOK_REDIRECT_CALL && t != sol::type::nil) {
            // A call replacement returns the value the original call would have produced.
            sol::object value = r.get<sol::object>(0);
            resp.has_result = true;
            resp.result_json = lua_value_json(value);
        }
    }

    out += "{\"cont\":";
    out += resp.cont ? "true" : "false";
    out += ",\"has_result\":";
    out += resp.has_result ? "true" : "false";
    if (resp.has_result) {
        out += ",\"result\":" + resp.result_json;
    }
    if (!resp.arg_json.empty()) {
        out += ",\"args\":{";
        bool first = true;
        for (const auto& kv : resp.arg_json) {
            if (!first) {
                out += ',';
            }
            first = false;
            out += "\"" + std::to_string(kv.first) + "\":" + kv.second;
        }
        out += '}';
    }
    if (resp.has_state) {
        out += ",\"state\":" + resp.state_json;
    }
    out += ",\"suppress\":";
    out += resp.suppress ? "true" : "false";
    out += '}';
    return out.c_str();
}

sol::table Engine::build_hook_context(int arg0_handle, const char* ctx_json) {
    sol::table ctx = lua_->create_table();
    sol::table args = lua_->create_table();
    sol::table named = lua_->create_table();
    sol::table arg_names = lua_->create_table();
    sol::table arg_modes = lua_->create_table();
    int pawn_h = arg0_handle;
    sol::object instance = sol::make_object(*lua_, sol::lua_nil);

    rj::Value root;
    const bool ok = ctx_json && *ctx_json && rj::parse(ctx_json, root) && root.is_object();
    if (ok) {
        const long long p = root.get_int("pawn", 0);
        if (p > 0) {
            pawn_h = static_cast<int>(p);
        }
        if (const rj::Value* inst = root.find("instance")) {
            instance = json_to_lua(*inst);
        }
        const rj::Value* names = root.find("arg_names");
        const rj::Value* modes = root.find("arg_modes");
        if (const rj::Value* list = root.find("args")) {
            int idx = 1;
            for (const rj::Value& item : list->items) {
                sol::object value = json_to_lua(item);
                args[idx] = value;
                if (names && static_cast<size_t>(idx - 1) < names->items.size() && names->items[idx - 1].is_string()) {
                    const std::string& n = names->items[idx - 1].s;
                    arg_names[idx] = n;
                    named[n] = value;
                }
                if (modes && static_cast<size_t>(idx - 1) < modes->items.size() && modes->items[idx - 1].is_string()) {
                    arg_modes[idx] = modes->items[idx - 1].s;
                }
                ++idx;
            }
        }
        ctx["method"] = root.get_string("method");
        ctx["phase"] = root.get_string("phase");
        const bool has_result = root.get_bool("has_result", false);
        ctx["has_result"] = has_result;
        if (has_result) {
            if (const rj::Value* res = root.find("result")) {
                ctx["result"] = json_to_lua(*res);
            }
        }
        if (const rj::Value* ex = root.find("exception")) {
            if (ex->is_string()) {
                ctx["exception"] = ex->s;
            }
        }
        if (const rj::Value* st = root.find("state")) {
            ctx["state"] = json_to_lua(*st);
        }
    } else {
        ctx["has_result"] = false;
    }
    ctx["args"] = args;
    ctx["named"] = named;
    ctx["arg_names"] = arg_names;
    ctx["arg_modes"] = arg_modes;

    attach_hook_mutators(ctx);

    if (pawn_h > 0) {
        RimPawn pawn = wrap_pawn(pawn_h);
        ctx["pawn"] = pawn;
        sol::table mt = lua_->create_table();
        mt["__index"] = sol::make_object(*lua_, pawn);
        ctx[sol::metatable_key] = mt;
    } else {
        ctx["pawn"] = sol::lua_nil;
    }

    if (instance.get_type() != sol::type::nil) {
        ctx["instance"] = instance;
    } else if (pawn_h > 0) {
        ctx["instance"] = wrap_pawn(pawn_h);
    } else {
        ctx["instance"] = sol::lua_nil;
    }
    return ctx;
}

namespace {
constexpr int kErrorBudget = 20;          // errors allowed per window
constexpr int kErrorWindowSeconds = 60;   // window length
}  // namespace

bool Engine::mod_enabled(const std::string& mod) const {
    if (mod.empty()) {
        return true;
    }
    auto it = mod_budgets_.find(mod);
    return it == mod_budgets_.end() || !it->second.disabled;
}

void Engine::reset_mod_budget(const std::string& package_id) {
    mod_budgets_.erase(package_id);
}

// Counts one callback failure against the mod. Too many in a minute disables the mod's callbacks and its hooks.
void Engine::record_mod_error(const std::string& mod, const std::string& where) {
    if (test_mode_) {
        ++test_errors_;
    }
    if (mod.empty()) {
        return;
    }
    ModBudget& b = mod_budgets_[mod];
    if (b.disabled) {
        return;
    }
    const auto now = std::chrono::steady_clock::now();
    if (b.errors == 0 || now - b.window_start > std::chrono::seconds(kErrorWindowSeconds)) {
        b.errors = 0;
        b.window_start = now;
    }
    if (++b.errors < kErrorBudget) {
        return;
    }
    b.disabled = true;
    std::vector<int> mine;
    for (const auto& kv : hooks_) {
        if (kv.second.mod == mod) {
            mine.push_back(kv.first);
        }
    }
    for (int id : mine) {
        unregister_hook_lua(id);
    }
    const std::string msg = "[RimKit] mod " + mod + " was switched off after " + std::to_string(kErrorBudget) +
                            " Lua errors in " + std::to_string(kErrorWindowSeconds) + " seconds (last in " + where +
                            "). Its hooks and callbacks stop until you restart. See the errors above in the log.";
    if (callbacks_.log) {
        callbacks_.log(msg.c_str());
    }
    if (callbacks_.message) {
        callbacks_.message(msg.c_str());
    }
    emit_event_ex("mod.disabled", "{\"mod\":\"" + mod + "\",\"where\":\"" + where + "\"}");
}

void Engine::attach_hook_mutators(sol::table& ctx) {
    // Mutators. Call with a colon: ctx:set_result(5), ctx:skip(), ctx:set_arg(1, x), ctx:set_state(v), ctx:suppress().
    ctx["set_result"] = [this](sol::table self, sol::object value) {
        if (!active_hook_response_) {
            return;
        }
        active_hook_response_->has_result = true;
        active_hook_response_->result_json = lua_value_json(value);
        self["result"] = value;
        self["has_result"] = true;
    };
    ctx["skip"] = [this](sol::table self, sol::optional<sol::object> value) {
        if (!active_hook_response_) {
            return;
        }
        active_hook_response_->cont = false;
        if (value && value->get_type() != sol::type::nil) {
            active_hook_response_->has_result = true;
            active_hook_response_->result_json = lua_value_json(*value);
            self["result"] = *value;
            self["has_result"] = true;
        }
    };
    ctx["set_arg"] = [this](sol::table self, int index, sol::object value) {
        if (!active_hook_response_ || index < 1) {
            return;
        }
        const std::string json = lua_value_json(value);
        for (auto& kv : active_hook_response_->arg_json) {
            if (kv.first == index) {
                kv.second = json;
                sol::table a = self["args"];
                a[index] = value;
                return;
            }
        }
        active_hook_response_->arg_json.emplace_back(index, json);
        sol::table a = self["args"];
        a[index] = value;
    };
    ctx["set_state"] = [this](sol::table self, sol::object value) {
        if (!active_hook_response_) {
            return;
        }
        active_hook_response_->has_state = true;
        active_hook_response_->state_json = lua_value_json(value);
        self["state"] = value;
    };
    ctx["suppress"] = [this](sol::table /*self*/) {
        if (active_hook_response_) {
            active_hook_response_->suppress = true;
        }
    };
}

void Engine::set_hook_info(int hook_id, const std::string& method, const std::string& phase, const std::string& names_json) {
    auto it = hooks_.find(hook_id);
    if (it == hooks_.end()) {
        return;
    }
    it->second.method = method;
    it->second.phase = phase;
    it->second.names.clear();
    rj::Value names;
    if (rj::parse(names_json, names) && names.is_array()) {
        for (const rj::Value& n : names.items) {
            it->second.names.push_back(n.is_string() ? n.s : std::string());
        }
    }
}

// Hook call without JSON, for methods whose arguments and result are plain numbers or booleans.
// types: 0 float, 1 bool, 2 integer. out_flags: bit 1 result set, bit 2 skip the original.
int Engine::invoke_hook_fast(int hook_id, int pawn_handle, int nargs, const double* args, const unsigned char* types,
                             int has_result, double result, unsigned char result_type, double* out_result,
                             int* out_flags) {
    if (out_flags) {
        *out_flags = 0;
    }
    auto it = hooks_.find(hook_id);
    if (it == hooks_.end() || !ready_) {
        return 1;
    }
    sol::protected_function fn = it->second.fn;
    const int kind = it->second.kind;
    const std::string hook_mod = it->second.mod;
    if (!mod_enabled(hook_mod)) {
        return 1;
    }

    auto make_number = [this](double v, unsigned char type) -> sol::object {
        if (type == 1) {
            return sol::make_object(*lua_, v != 0.0);
        }
        if (type == 2) {
            return sol::make_object(*lua_, static_cast<long long>(v));
        }
        return sol::make_object(*lua_, v);
    };

    HookResponse resp;
    HookResponse* previous = active_hook_response_;
    active_hook_response_ = &resp;

    sol::table ctx = lua_->create_table();
    sol::table arg_table = lua_->create_table();
    sol::table named = lua_->create_table();
    for (int i = 0; i < nargs; ++i) {
        sol::object value = make_number(args[i], types ? types[i] : 0);
        arg_table[i + 1] = value;
        if (static_cast<size_t>(i) < it->second.names.size() && !it->second.names[i].empty()) {
            named[it->second.names[i]] = value;
        }
    }
    ctx["args"] = arg_table;
    ctx["named"] = named;
    ctx["method"] = it->second.method;
    ctx["phase"] = it->second.phase;
    ctx["has_result"] = has_result != 0;
    if (has_result) {
        ctx["result"] = make_number(result, result_type);
    }
    if (pawn_handle > 0) {
        ctx["pawn"] = wrap_pawn(pawn_handle);
        ctx["instance"] = wrap_pawn(pawn_handle);
    }
    attach_hook_mutators(ctx);

    sol::protected_function_result r;
    {
        ModScope scope(*this, hook_mod, PROF_HOOK);
        r = fn(ctx);
    }
    active_hook_response_ = previous;
    if (!r.valid()) {
        log_lua_error("[RimKit] hook " + std::to_string(hook_id), r);
        record_mod_error(hook_mod, "hook " + std::to_string(hook_id));
        return 2;
    }
    if (kind == RIMLUA_HOOK_PREFIX && r.return_count() > 0 && r.get_type(0) == sol::type::boolean && !r.get<bool>(0)) {
        resp.cont = false;
    }
    int flags = 0;
    if (resp.has_result) {
        flags |= 1;
        rj::Value v;
        if (rj::parse(resp.result_json, v)) {
            if (v.type == rj::Value::Type::Bool) {
                *out_result = v.b ? 1.0 : 0.0;
            } else if (v.is_number()) {
                *out_result = v.as_double();
            } else {
                flags &= ~1;
            }
        } else {
            flags &= ~1;
        }
    }
    if (!resp.cont) {
        flags |= 2;
    }
    if (out_flags) {
        *out_flags = flags;
    }
    return 0;
}

}  // namespace rimlua
