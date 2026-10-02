#include "engine.hpp"

#include <algorithm>
#include <cstdlib>
#include <filesystem>
#include <iostream>
#include <sstream>

namespace fs = std::filesystem;

namespace rimlua {

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
                out += c;
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
    lua_->open_libraries(sol::lib::base, sol::lib::package, sol::lib::coroutine, sol::lib::string, sol::lib::table,
                         sol::lib::math, sol::lib::utf8);
    apply_sandbox();
    bind_rim_api();
    bind_oo_types();
    bind_events_and_timer();
    bind_jobs_and_faction();
    bind_ui_config_defs();
    bind_strong_api();
    ready_ = true;
    if (callbacks_.log) {
        callbacks_.log("[RimLuaKit] C++ core initialized (sandboxed Lua)");
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
    // Strip loaders.
    (*lua_)["dofile"] = sol::lua_nil;
    (*lua_)["loadfile"] = sol::lua_nil;
    (*lua_)["load"] = sol::lua_nil;
    (*lua_)["loadstring"] = sol::lua_nil;  // alias on older Lua; harmless if absent
    (*lua_)["io"] = sol::lua_nil;
    (*lua_)["os"] = sol::lua_nil;
    (*lua_)["debug"] = sol::lua_nil;

    sol::table package = (*lua_)["package"];
    package["loadlib"] = sol::lua_nil;
    package["cpath"] = "";
    package["path"] = "";

    // Keep Lua searchers only.
    sol::object searchers_obj = package["searchers"];
    if (searchers_obj.is<sol::table>()) {
        sol::table searchers = searchers_obj.as<sol::table>();
        for (int i = 3; i <= 8; ++i) {
            searchers[i] = sol::lua_nil;
        }
    }

    // require only under allowed Lua roots.
    package["searchers"][2] = [this](const std::string& modname) -> sol::object {
        if (!lua_) {
            return sol::make_object(*lua_, "RimLuaKit sandbox: no state");
        }
        std::string dotted = modname;
        for (char& c : dotted) {
            if (c == '.') {
                c = '/';
            }
        }
        std::vector<std::string> candidates;
        candidates.push_back(dotted + ".lua");
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
                sol::load_result lr = lua_->load_file(resolved);
                if (!lr.valid()) {
                    sol::error e = lr;
                    return sol::make_object(*lua_, std::string(e.what()));
                }
                return lr.get<sol::object>();
            }
        }
        return sol::make_object(*lua_, "RimLuaKit sandbox: module not in allowed Lua roots: " + modname);
    };

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
            callbacks_.log((std::string("[RimLuaKit] sandbox freeze warn: ") + e.what()).c_str());
        }
    }

    if (callbacks_.log) {
        callbacks_.log("[RimLuaKit] Lua sandbox on (no io/os/dofile/loadlib; require jailed; _G frozen)");
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
    bool ok_found = false;
    bool ok = extract_bool_field(json, "ok", &ok_found);
    if (!ok) {
        std::string err = extract_string_field(json, "e");
        if (callbacks_.log) {
            std::string msg = "[RimLuaKit] host error: " + err;
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

sol::object Engine::host_call(const std::string& op, const sol::table& args) {
    if (!ready_ || !callbacks_.host_invoke) {
        return sol::make_object(*lua_, sol::lua_nil);
    }
    std::string json = table_to_json(args);
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
    rim["on_load"] = [this](sol::protected_function fn) { on_load_.push_back(std::move(fn)); };
    rim["on_tick"] = [this](sol::protected_function fn) { on_tick_.push_back(std::move(fn)); };

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

    auto bind_op = [this](sol::table& tbl, const char* name, const char* op, const char* handle_key = "h") {
        std::string opName = op;
        std::string hk = handle_key ? handle_key : "h";
        tbl[name] = [this, opName, hk](sol::variadic_args va) {
            sol::table args = lua_->create_table();
            if (va.size() >= 1) {
                if (va[0].is<sol::table>()) {
                    args = va[0].as<sol::table>();
                } else if (va[0].is<int>() || va[0].is<double>()) {
                    args[hk] = va[0].as<int>();
                }
            }
            // optional 2nd arg
            if (va.size() >= 2 && !va[0].is<sol::table>()) {
                if (va[1].is<std::string>()) {
                    args["def"] = va[1].as<std::string>();
                    args["v"] = va[1].as<std::string>();
                    args["member"] = va[1].as<std::string>();
                    args["name"] = va[1].as<std::string>();
                    args["skill"] = va[1].as<std::string>();
                    args["text"] = va[1].as<std::string>();
                    args["method"] = va[1].as<std::string>();
                    args["type"] = va[1].as<std::string>();
                } else if (va[1].is<int>() || va[1].is<double>()) {
                    args["v"] = va[1].as<int>();
                    args["x"] = va[1].as<int>();
                    args["faction"] = va[1].as<int>();
                    args["job"] = va[1].as<int>();
                } else if (va[1].is<bool>()) {
                    args["v"] = va[1].as<bool>();
                } else if (va[1].is<double>()) {
                    args["v"] = va[1].as<double>();
                }
            }
            if (va.size() >= 3 && !va[0].is<sol::table>()) {
                if (va[2].is<std::string>()) {
                    args["method"] = va[2].as<std::string>();
                    args["name"] = va[2].as<std::string>();
                    args["args"] = va[2].as<std::string>();
                    args["stuff"] = va[2].as<std::string>();
                } else if (va[2].is<int>() || va[2].is<double>()) {
                    args["z"] = va[2].as<int>();
                    args["v"] = va[2].as<int>();
                    args["severity"] = va[2].as<double>();
                    args["stack"] = va[2].as<int>();
                }
            }
            if (va.size() >= 4 && (va[3].is<int>() || va[3].is<double>())) {
                args["stack"] = va[3].as<int>();
            }
            return host_call(opName, args);
        };
    };

    sol::table hooks = lua_->create_table();
    hooks["prefix"] = [this](const std::string& type_name, const std::string& method_name, sol::protected_function fn) {
        return register_hook_lua(true, type_name, method_name, std::move(fn));
    };
    hooks["postfix"] = [this](const std::string& type_name, const std::string& method_name, sol::protected_function fn) {
        return register_hook_lua(false, type_name, method_name, std::move(fn));
    };
    hooks["remove"] = [this](int hook_id) { unregister_hook_lua(hook_id); };
    bind_op(hooks, "has_patch", "harmony.has_patch");
    bind_op(hooks, "type_exists", "harmony.type_exists");
    bind_op(hooks, "method_exists", "harmony.method_exists");
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
                if (!mkey.is<std::string>() || !value.is<sol::protected_function>()) {
                    return;
                }
                register_hook_lua(is_prefix, type_name, mkey.as<std::string>(), value.as<sol::protected_function>());
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
                    callbacks_.log("[RimLuaKit] events key need Type.Method");
                }
                return;
            }
            register_hook_lua(is_prefix, dotted.substr(0, pos), dotted.substr(pos + 1),
                              value.as<sol::protected_function>());
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
    map["things_of_def"] = [this](int h, const std::string& def) {
        sol::table args = lua_->create_table();
        args["h"] = h;
        args["def"] = def;
        return host_call("map.things_of_def", args);
    };
    map["spawn"] = [this](int h, const std::string& def, int x, int z, sol::optional<int> stack) {
        sol::table args = lua_->create_table();
        args["h"] = h;
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
    thing["set_pos"] = [this](int h, int x, int z) {
        sol::table args = lua_->create_table();
        args["h"] = h;
        args["x"] = x;
        args["z"] = z;
        return host_call("thing.set_pos", args);
    };
    bind_op(thing, "hp", "thing.hp");
    bind_op(thing, "max_hp", "thing.max_hp");
    thing["set_hp"] = [this](int h, int v) {
        sol::table args = lua_->create_table();
        args["h"] = h;
        args["v"] = v;
        return host_call("thing.set_hp", args);
    };
    bind_op(thing, "stack", "thing.stack");
    thing["set_stack"] = [this](int h, int v) {
        sol::table args = lua_->create_table();
        args["h"] = h;
        args["v"] = v;
        return host_call("thing.set_stack", args);
    };
    bind_op(thing, "faction", "thing.faction");
    thing["set_faction"] = [this](int h, int faction) {
        sol::table args = lua_->create_table();
        args["h"] = h;
        args["faction"] = faction;
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
    pawn["set_hunger"] = [this](int h, double v) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["v"] = v;
        return host_call("pawn.set_hunger", a);
    };
    pawn["set_rest"] = [this](int h, double v) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["v"] = v;
        return host_call("pawn.set_rest", a);
    };
    bind_op(pawn, "health_pct", "pawn.health_pct");
    bind_op(pawn, "kill", "pawn.kill");
    bind_op(pawn, "drafted", "pawn.drafted");
    pawn["set_drafted"] = [this](int h, bool v) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["v"] = v;
        return host_call("pawn.set_drafted", a);
    };
    bind_op(pawn, "equipment", "pawn.equipment");
    bind_op(pawn, "apparel", "pawn.apparel");
    bind_op(pawn, "inventory", "pawn.inventory");
    bind_op(pawn, "carry", "pawn.carry");
    pawn["give_hediff"] = [this](int h, const std::string& def, sol::optional<double> sev) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["def"] = def;
        if (sev) a["severity"] = *sev;
        return host_call("pawn.give_hediff", a);
    };
    pawn["remove_hediff"] = [this](int h, const std::string& def) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["def"] = def;
        return host_call("pawn.remove_hediff", a);
    };
    bind_op(pawn, "hediffs", "pawn.hediffs");
    pawn["skill"] = [this](int h, const std::string& skill) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["skill"] = skill;
        return host_call("pawn.skill", a);
    };
    pawn["set_skill"] = [this](int h, const std::string& skill, int v) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["skill"] = skill;
        a["v"] = v;
        return host_call("pawn.set_skill", a);
    };
    pawn["give_thing"] = [this](int h, const std::string& def, sol::optional<int> stack) {
        sol::table a = lua_->create_table();
        a["h"] = h;
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
    job["start"] = [this](int pawn, int jobHandle) {
        sol::table a = lua_->create_table();
        a["h"] = pawn;
        a["job"] = jobHandle;
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
    bind_op(reflect, "type", "reflect.type");
    reflect["get"] = [this](int h, const std::string& member) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["member"] = member;
        return host_call("reflect.get", a);
    };
    reflect["set"] = [this](int h, const std::string& member, sol::object value) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["member"] = member;
        if (value.is<bool>()) a["value"] = value.as<bool>() ? "true" : "false";
        else if (value.is<double>()) a["value"] = std::to_string(value.as<double>());
        else if (value.is<int>()) a["value"] = std::to_string(value.as<int>());
        else a["value"] = value.as<std::string>();
        return host_call("reflect.set", a);
    };
    reflect["call"] = [this](int h, const std::string& method, sol::optional<std::string> args) {
        sol::table a = lua_->create_table();
        a["h"] = h;
        a["method"] = method;
        if (args) a["args"] = *args;
        return host_call("reflect.call", a);
    };
    reflect["static_get"] = [this](const std::string& typeName, const std::string& member) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["member"] = member;
        return host_call("reflect.static_get", a);
    };
    reflect["static_call"] = [this](const std::string& typeName, const std::string& method, sol::optional<std::string> args) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["method"] = method;
        if (args) a["args"] = *args;
        return host_call("reflect.static_call", a);
    };
    bind_op(reflect, "members", "reflect.members");
    reflect["handle_of_static"] = [this](const std::string& typeName, const std::string& member) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["member"] = member;
        return host_call("reflect.handle_of_static", a);
    };
    rim["reflect"] = reflect;
    rim["cs"] = reflect;  // alias
}

int Engine::register_hook_lua(bool is_prefix, const std::string& type_name, const std::string& method_name,
                              sol::protected_function fn) {
    const int id = next_hook_id_++;
    HookEntry entry;
    entry.id = id;
    entry.is_prefix = is_prefix;
    entry.type_name = type_name;
    entry.method_name = method_name;
    entry.fn = std::move(fn);
    hooks_[id] = std::move(entry);

    if (callbacks_.register_hook) {
        if (!callbacks_.register_hook(type_name.c_str(), method_name.c_str(), is_prefix ? 1 : 0, id)) {
            hooks_.erase(id);
            if (callbacks_.log) {
                callbacks_.log("[RimLuaKit] Failed to register Harmony hook on host");
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
            std::string msg = "[RimLuaKit] sandbox blocked load outside Lua roots: " + resolved;
            callbacks_.log(msg.c_str());
        }
        return 3;
    }
    sol::protected_function_result result = lua_->safe_script_file(resolved, sol::script_pass_on_error);
    if (!result.valid()) {
        log_lua_error(std::string("[RimLuaKit] load ") + resolved, result);
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
    std::vector<fs::path> files;
    for (auto it = fs::recursive_directory_iterator(dir, ec); !ec && it != fs::recursive_directory_iterator(); ++it) {
        if (!it->is_regular_file()) {
            continue;
        }
        if (it->path().extension() == ".lua") {
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
    for (auto& fn : on_load_) {
        sol::protected_function_result r = fn();
        if (!r.valid()) {
            log_lua_error("[RimLuaKit] on_load", r);
        }
    }
}

void Engine::call_on_tick() {
    int tick = current_tick();
    for (auto it = timers_.begin(); it != timers_.end();) {
        if (it->fire_tick <= tick) {
            sol::protected_function_result r = it->fn();
            if (!r.valid()) {
                log_lua_error("[RimLuaKit] timer", r);
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

    for (auto& fn : on_tick_) {
        sol::protected_function_result r = fn();
        if (!r.valid()) {
            log_lua_error("[RimLuaKit] on_tick", r);
        }
    }
}

int Engine::invoke_hook(int hook_id, int arg0_handle, int* out_continue) {
    if (out_continue) {
        *out_continue = 1;
    }
    auto it = hooks_.find(hook_id);
    if (it == hooks_.end() || !ready_) {
        return 1;
    }
    RimPawn pawn = wrap_pawn(arg0_handle);
    sol::protected_function_result r = it->second.fn(pawn);
    if (!r.valid()) {
        log_lua_error("[RimLuaKit] hook " + std::to_string(hook_id), r);
        return 2;
    }
    if (out_continue && r.return_count() > 0 && r.get_type(0) == sol::type::boolean) {
        *out_continue = r.get<bool>(0) ? 1 : 0;
    }
    return 0;
}

}  // namespace rimlua
