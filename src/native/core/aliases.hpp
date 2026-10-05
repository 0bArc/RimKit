#pragma once

// UNC alias table, loaded from the JSON embedded at build time (api/aliases.json).
// Shared by the Lua runtime (deprecation shims) and the rimkit CLI (migrate).

#include <string>
#include <vector>

#include "aliases_data.hpp"
#include "json_lite.hpp"

namespace rimlua {
namespace aliases {

struct PathAlias {
    std::string old_path;  // for example rim.pawn.name
    std::string new_path;  // for example game.pawns.name
    bool deprecate = true;
};

struct EventAlias {
    std::string from;
    std::string to;
    std::string payload_key;
};

struct GlobalAlias {
    std::string from;
    std::string event;
};

struct MethodAlias {
    std::string type;
    std::string from;
    std::string to;
};

struct ManualAlias {
    std::string from;
    std::string to;
    std::string note;
};

struct Table {
    std::string removed_in = "1.0.0";
    std::vector<PathAlias> paths;
    std::vector<EventAlias> events;
    std::vector<GlobalAlias> globals;
    std::vector<MethodAlias> methods;
    std::vector<ManualAlias> manual;
};

inline Table load(const std::string& text) {
    Table t;
    json::Value root;
    if (!json::parse(text, root) || !root.is_object()) {
        return t;
    }
    t.removed_in = root.get_string("removed_in", "1.0.0");

    if (const json::Value* groups = root.find("groups")) {
        for (const json::Value& g : groups->items) {
            const std::string from = g.get_string("from");
            const std::string to = g.get_string("to");
            const bool deprecate = g.get_bool("deprecate", true);
            const json::Value* names = g.find("names");
            if (from.empty() || to.empty() || !names) {
                continue;
            }
            for (const json::Value& n : names->items) {
                if (!n.is_string()) {
                    continue;
                }
                std::string old_name = n.s;
                std::string new_name = n.s;
                const size_t gt = n.s.find('>');
                if (gt != std::string::npos) {
                    old_name = n.s.substr(0, gt);
                    new_name = n.s.substr(gt + 1);
                }
                PathAlias a;
                a.old_path = from + "." + old_name;
                // A target containing a dot is a path under game. Otherwise it stays in the group's domain.
                a.new_path = new_name.find('.') != std::string::npos ? "game." + new_name : to + "." + new_name;
                a.deprecate = deprecate && a.old_path != a.new_path;
                t.paths.push_back(std::move(a));
            }
        }
    }
    if (const json::Value* events = root.find("events")) {
        for (const json::Value& e : events->items) {
            t.events.push_back({e.get_string("from"), e.get_string("to"), e.get_string("payload_key")});
        }
    }
    if (const json::Value* globals = root.find("globals")) {
        for (const json::Value& e : globals->items) {
            t.globals.push_back({e.get_string("from"), e.get_string("event")});
        }
    }
    if (const json::Value* methods = root.find("methods")) {
        for (const json::Value& e : methods->items) {
            t.methods.push_back({e.get_string("type"), e.get_string("from"), e.get_string("to")});
        }
    }
    if (const json::Value* manual = root.find("manual")) {
        for (const json::Value& e : manual->items) {
            t.manual.push_back({e.get_string("from"), e.get_string("to"), e.get_string("note")});
        }
    }
    return t;
}

inline const Table& table() {
    static const Table t = load(kAliasesJson);
    return t;
}

}  // namespace aliases
}  // namespace rimlua
