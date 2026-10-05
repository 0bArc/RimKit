#include "aliases.hpp"
#include "engine.hpp"

#include <string>
#include <vector>

namespace rimlua {

namespace {

std::vector<std::string> split_path(const std::string& path) {
    std::vector<std::string> parts;
    size_t start = 0;
    while (start <= path.size()) {
        const size_t dot = path.find('.', start);
        if (dot == std::string::npos) {
            parts.push_back(path.substr(start));
            break;
        }
        parts.push_back(path.substr(start, dot - start));
        start = dot + 1;
    }
    return parts;
}

sol::object get_path(sol::state& L, const std::string& path) {
    sol::object cur = sol::make_object(L, L.globals());
    for (const std::string& part : split_path(path)) {
        if (!cur.is<sol::table>()) {
            return sol::make_object(L, sol::lua_nil);
        }
        sol::table t = cur.as<sol::table>();
        cur = t[part];
    }
    return cur;
}

// Sets a value at a dotted path, creating missing tables. Returns false when a parent is not a table.
bool set_path(sol::state& L, const std::string& path, const sol::object& value) {
    std::vector<std::string> parts = split_path(path);
    sol::table cur = L.globals();
    for (size_t i = 0; i + 1 < parts.size(); ++i) {
        sol::object next = cur[parts[i]];
        if (next.get_type() == sol::type::nil) {
            sol::table created = L.create_table();
            cur[parts[i]] = created;
            cur = created;
        } else if (next.is<sol::table>()) {
            cur = next.as<sol::table>();
        } else {
            return false;
        }
    }
    cur[parts.back()] = value;
    return true;
}

}  // namespace

int Engine::mod_api_level() const {
    auto it = mods_.find(current_mod_);
    return it == mods_.end() ? 0 : it->second.api_level;
}

void Engine::warn_deprecated(const std::string& old_name, const std::string& new_name) {
    if (mod_api_level() >= 1) {
        throw sol::error("RK1002: " + old_name + " was removed at api level 1. Use " + new_name + " (rimkit migrate rewrites it).");
    }
    if (!warned_aliases_.insert(old_name).second) {
        return;
    }
    if (callbacks_.log) {
        const std::string msg = "[RimKit] deprecated: " + old_name + " is now " + new_name +
                                " (the old name works until " + aliases::table().removed_in + ")";
        callbacks_.log(msg.c_str());
    }
}

std::string Engine::canonical_event_name(const std::string& name) {
    for (const aliases::EventAlias& e : aliases::table().events) {
        if (e.from == name) {
            warn_deprecated("events \"" + name + "\"", "\"" + e.to + "\"");
            return e.to;
        }
    }
    return name;
}

void Engine::add_event_handler(const std::string& name, sol::protected_function fn, bool legacy_signature) {
    std::string canonical = name;
    std::string payload_key;
    for (const aliases::EventAlias& e : aliases::table().events) {
        if (e.from == name) {
            warn_deprecated("events \"" + name + "\"",
                            "\"" + e.to + "\" (old handlers still get the " + e.payload_key +
                                "; new handlers get a payload table)");
            canonical = e.to;
            payload_key = e.payload_key;
            legacy_signature = true;
            break;
        }
        if (e.to == name && legacy_signature) {
            payload_key = e.payload_key;
        }
    }

    if (legacy_signature && !payload_key.empty()) {
        // Old handlers took the pawn. The canonical event passes a payload table, so unwrap it.
        if (!event_adapter_maker_.valid()) {
            sol::load_result lr = lua_->load("return function(fn, key) return function(p) return fn(p[key]) end end");
            sol::protected_function_result made = lr();
            event_adapter_maker_ = made.get<sol::protected_function>();
        }
        sol::protected_function_result adapted = event_adapter_maker_(fn, payload_key);
        if (adapted.valid()) {
            fn = adapted.get<sol::protected_function>();
        }
    }

    event_handlers_[canonical].push_back(ModFn{std::move(fn), current_mod_});
    subscribe_named_event(canonical);
}

// Builds game.<domain> from the alias table and wraps the old names so they warn once.
void Engine::bind_canonical_surface() {
    sol::state& L = *lua_;
    const aliases::Table& table = aliases::table();

    sol::load_result lr = L.load(
        "local warn = ...\n"
        "return function(f, old, new)\n"
        "  return function(...)\n"
        "    warn(old, new)\n"
        "    return f(...)\n"
        "  end\n"
        "end\n");
    sol::function warn_fn = sol::make_object(L, [this](const std::string& o, const std::string& n) { warn_deprecated(o, n); });
    sol::protected_function_result made = lr(warn_fn);
    sol::function make_wrapper = made.get<sol::function>();

    // Pass 1 reads every original before anything is wrapped. Two old names can point at one table.
    std::vector<sol::object> originals;
    originals.reserve(table.paths.size());
    for (const aliases::PathAlias& a : table.paths) {
        originals.push_back(get_path(L, a.old_path));
    }

    for (size_t i = 0; i < table.paths.size(); ++i) {
        const aliases::PathAlias& a = table.paths[i];
        const sol::object& original = originals[i];
        if (!original.is<sol::function>()) {
            continue;  // Not bound in this build (for example ui.panel without the UI plugin).
        }
        // First definition wins, so group order decides which function backs a canonical name.
        if (get_path(L, a.new_path).get_type() == sol::type::nil) {
            set_path(L, a.new_path, original);
        }
        if (a.deprecate) {
            sol::function wrapper = make_wrapper(original, a.old_path, a.new_path);
            set_path(L, a.old_path, sol::make_object(L, wrapper));
        }
    }

    for (const aliases::ManualAlias& m : table.manual) {
        // Documented only. These tables behave differently, so they are not rewritten at runtime.
        (void)m;
    }
}

}  // namespace rimlua
