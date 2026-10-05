#include "engine.hpp"
#include "handle_util.hpp"
#include "aliases.hpp"
#include "version_data.hpp"

#include <algorithm>
#include <stdexcept>

namespace rimlua {

sol::object Engine::host_call_h(const std::string& op, int h) {
    sol::table args = lua_->create_table();
    args["h"] = h;
    return host_call(op, args);
}

sol::object Engine::host_call_hv(const std::string& op, int h, const std::string& key, sol::object value) {
    sol::table args = lua_->create_table();
    args["h"] = h;
    if (value.is<bool>()) {
        args[key] = value.as<bool>();
    } else if (value.is<int>()) {
        args[key] = value.as<int>();
    } else if (value.is<double>()) {
        args[key] = value.as<double>();
    } else if (value.is<std::string>()) {
        args[key] = value.as<std::string>();
    }
    return host_call(op, args);
}

int Engine::current_tick() {
    sol::object o = host_call("find.tick", lua_->create_table());
    if (o.is<int>()) {
        return o.as<int>();
    }
    if (o.is<double>()) {
        return static_cast<int>(o.as<double>());
    }
    return 0;
}

void Engine::bind_oo_types() {
    lua_->new_usertype<RimFaction>(
        "RimFaction", sol::no_constructor, "handle", &RimFaction::h, "name",
        sol::property([this](RimFaction& f) -> std::string {
            sol::object o = host_call_h("faction.name", f.h);
            return o.is<std::string>() ? o.as<std::string>() : std::string{};
        }),
        "is_hostile",
        [this](RimFaction& a, RimFaction& b) {
            sol::table args = lua_->create_table();
            args["h"] = a.h;
            args["other"] = b.h;
            sol::object o = host_call("faction.is_hostile", args);
            return o.is<bool>() && o.as<bool>();
        },
        "set_relation",
        [this](RimFaction& a, RimFaction& b, const std::string& kind) {
            sol::table args = lua_->create_table();
            args["h"] = a.h;
            args["other"] = b.h;
            args["kind"] = kind;
            return host_call("faction.set_relation", args);
        });

    lua_->new_usertype<RimPawn>(
        "RimPawn", sol::no_constructor, "handle", &RimPawn::h, "name",
        sol::property([this](RimPawn& p) -> std::string {
            sol::object o = host_call_h("pawn.name", p.h);
            return o.is<std::string>() ? o.as<std::string>() : std::string{};
        }),
        "health",
        sol::property([this](RimPawn& p) -> double {
            sol::object o = host_call_h("pawn.health_pct", p.h);
            return o.is<double>() ? o.as<double>() : (o.is<int>() ? o.as<int>() : 1.0);
        }),
        "hunger",
        sol::property([this](RimPawn& p) -> double {
            sol::object o = host_call_h("pawn.hunger", p.h);
            return o.is<double>() ? o.as<double>() : 1.0;
        }),
        "is_colonist",
        sol::property([this](RimPawn& p) -> bool {
            sol::object o = host_call_h("pawn.is_colonist", p.h);
            return o.is<bool>() && o.as<bool>();
        }),
        "is_humanlike",
        sol::property([this](RimPawn& p) -> bool {
            sol::object o = host_call_h("pawn.is_humanlike", p.h);
            return o.is<bool>() && o.as<bool>();
        }),
        "map",
        sol::property([this](RimPawn& p) -> sol::object {
            sol::object o = host_call_h("pawn.map", p.h);
            int mh = o.is<int>() ? o.as<int>() : 0;
            if (mh == 0) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            return sol::make_object(*lua_, wrap_map(mh));
        }),
        "faction",
        sol::property([this](RimPawn& p) -> sol::object {
            sol::object o = host_call_h("pawn.faction", p.h);
            int fh = o.is<int>() ? o.as<int>() : 0;
            if (fh == 0) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            return sol::make_object(*lua_, wrap_faction(fh));
        }),
        "give_item",
        [this](RimPawn& p, const std::string& def, sol::optional<int> stack) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["def"] = def;
            if (stack) {
                a["stack"] = *stack;
            }
            return host_call("pawn.give_item", a);
        },
        "set_name",
        [this](RimPawn& p, const std::string& name) {
            return host_call_hv("pawn.set_name", p.h, "v", sol::make_object(*lua_, name));
        },
        "add_trait",
        [this](RimPawn& p, const std::string& trait, sol::optional<int> degree) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["def"] = trait;
            a["degree"] = degree.value_or(0);
            return host_call("pawn.add_trait", a);
        },
        "remove_trait",
        [this](RimPawn& p, const std::string& trait) {
            return host_call_hv("pawn.remove_trait", p.h, "def", sol::make_object(*lua_, trait));
        },
        // game.pawns kit (docs/api/pawns.md). Properties return nil when the pawn lacks the tracker.
        "skills", sol::property([this](RimPawn& p) { return host_call_h("pawn.skills", p.h); }),
        "needs", sol::property([this](RimPawn& p) { return host_call_h("pawn.needs", p.h); }),
        "traits", sol::property([this](RimPawn& p) { return host_call_h("pawn.traits", p.h); }),
        "thoughts", sol::property([this](RimPawn& p) { return host_call_h("pawn.thoughts", p.h); }),
        "relations", sol::property([this](RimPawn& p) { return host_call_h("pawn.relations", p.h); }),
        "capacities", sol::property([this](RimPawn& p) { return host_call_h("pawn.capacities", p.h); }),
        "skill_info",
        [this](RimPawn& p, const std::string& skill) {
            return host_call_hv("pawn.skill_info", p.h, "skill", sol::make_object(*lua_, skill));
        },
        "set_passion",
        [this](RimPawn& p, const std::string& skill, const std::string& passion) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["skill"] = skill;
            a["passion"] = passion;
            return host_call("pawn.set_passion", a);
        },
        "add_xp",
        [this](RimPawn& p, const std::string& skill, double amount) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["skill"] = skill;
            a["amount"] = amount;
            return host_call("pawn.add_skill_xp", a);
        },
        "has_trait",
        [this](RimPawn& p, const std::string& trait, sol::optional<int> degree) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["def"] = trait;
            if (degree) {
                a["degree"] = *degree;
            }
            return host_call("pawn.has_trait", a);
        },
        "add_thought",
        [this](RimPawn& p, const std::string& def, sol::optional<RimPawn> other) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["def"] = def;
            if (other) {
                a["other"] = other->h;
            }
            return host_call("pawn.add_thought", a);
        },
        "remove_thought",
        [this](RimPawn& p, const std::string& def) {
            return host_call_hv("pawn.remove_thought", p.h, "def", sol::make_object(*lua_, def));
        },
        "opinion_of",
        [this](RimPawn& p, RimPawn& other) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["other"] = other.h;
            return host_call("pawn.opinion_of", a);
        },
        "give_hediff",
        [this](RimPawn& p, const std::string& def, sol::optional<double> sev) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["def"] = def;
            if (sev) {
                a["severity"] = *sev;
            }
            return host_call("pawn.give_hediff", a);
        },
        "kill", [this](RimPawn& p) { return host_call_h("pawn.kill", p.h); },
        "draft",
        [this](RimPawn& p, sol::optional<bool> v) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["v"] = v.value_or(true);
            return host_call("pawn.set_drafted", a);
        },
        "seek_medical", [this](RimPawn& p) { return host_call_h("pawn.seek_medical", p.h); },
        "seek_medical_help",
        [this](RimPawn& p) {
            warn_deprecated("RimPawn:seek_medical_help", "RimPawn:seek_medical");
            return host_call_h("pawn.seek_medical", p.h);
        },
        "position",
        sol::property([this](RimPawn& p) -> sol::object {
            sol::object o = host_call_h("pawn.pos", p.h);
            if (!o.is<std::string>()) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            std::string s = o.as<std::string>();
            auto comma = s.find(',');
            if (comma == std::string::npos) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            sol::table cell = lua_->create_table();
            cell["x"] = std::stoi(s.substr(0, comma));
            cell["z"] = std::stoi(s.substr(comma + 1));
            return sol::make_object(*lua_, cell);
        }),
        "is_moving",
        sol::property([this](RimPawn& p) -> bool {
            sol::object o = host_call_h("pawn.is_moving", p.h);
            return o.is<bool>() && o.as<bool>();
        }),
        "can_reach",
        [this](RimPawn& p, int x, int z) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["x"] = x;
            a["z"] = z;
            sol::object o = host_call("pawn.can_reach", a);
            return o.is<bool>() && o.as<bool>();
        },
        "walk_to",
        [this](RimPawn& p, int x, int z, sol::optional<bool> sprint) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["x"] = x;
            a["z"] = z;
            if (sprint && *sprint) {
                a["sprint"] = true;
            }
            sol::object o = host_call("pawn.walk_to", a);
            return o.is<bool>() && o.as<bool>();
        },
        "wander",
        [this](RimPawn& p, sol::optional<int> radius) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["radius"] = radius.value_or(12);
            sol::object o = host_call("pawn.wander", a);
            return o.is<bool>() && o.as<bool>();
        },
        "stop", [this](RimPawn& p) { return host_call_h("pawn.stop", p.h); },
        "equip_weapon",
        [this](RimPawn& p, sol::optional<std::string> def) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            if (def) {
                a["def"] = *def;
            }
            sol::object o = host_call("pawn.equip_weapon", a);
            return o.is<bool>() && o.as<bool>();
        },
        "shoot_hostiles",
        [this](RimPawn& p, sol::optional<bool> instant) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            if (instant && *instant) {
                a["instant"] = true;
            }
            sol::object o = host_call("pawn.shoot_hostiles", a);
            return o.is<int>() ? o.as<int>() : 0;
        },
        "start_job",
        [this](RimPawn& p, const std::string& job_name) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["name"] = job_name;
            sol::object o = host_call("job.start_lua", a);
            return o.is<bool>() && o.as<bool>();
        },
        "has_skill",
        [this](RimPawn& p, const std::string& skill) {
            sol::table a = lua_->create_table();
            a["h"] = p.h;
            a["skill"] = skill;
            sol::object o = host_call("pawn.skill", a);
            int level = o.is<int>() ? o.as<int>() : -1;
            return level >= 0;
        });

    auto spawn_on_map = [this](RimMap& m, const std::string& def, int x, int z, sol::optional<int> stack) {
        sol::table a = lua_->create_table();
        a["h"] = m.h;
        a["def"] = def;
        a["x"] = x;
        a["z"] = z;
        if (stack) {
            a["stack"] = *stack;
        }
        sol::object o = host_call("map.spawn", a);
        int th = o.is<int>() ? o.as<int>() : 0;
        return th == 0 ? sol::make_object(*lua_, sol::lua_nil) : sol::make_object(*lua_, wrap_thing(th));
    };

    lua_->new_usertype<RimMap>(
        "RimMap", sol::no_constructor, "handle", &RimMap::h, "nutrition",
        sol::property([this](RimMap& m) -> double {
            sol::object o = host_call_h("map.nutrition", m.h);
            return o.is<double>() ? o.as<double>() : 0.0;
        }),
        "width",
        sol::property([this](RimMap& m) -> int {
            sol::object o = host_call_h("map.width", m.h);
            return o.is<int>() ? o.as<int>() : 0;
        }),
        "height",
        sol::property([this](RimMap& m) -> int {
            sol::object o = host_call_h("map.height", m.h);
            return o.is<int>() ? o.as<int>() : 0;
        }),
        "spawn", spawn_on_map,
        "spawn_thing",
        [this, spawn_on_map](RimMap& m, const std::string& def, int x, int z, sol::optional<int> stack) {
            warn_deprecated("RimMap:spawn_thing", "RimMap:spawn");
            return spawn_on_map(m, def, x, z, stack);
        },
        "spawn_pawn",
        [this](RimMap& m, const std::string& kind, sol::optional<std::string> faction, sol::optional<int> x,
               sol::optional<int> z) {
            sol::table a = lua_->create_table();
            a["h"] = m.h;
            a["kind"] = kind;
            if (faction) {
                a["faction"] = *faction;
            }
            if (x && z) {
                a["x"] = *x;
                a["z"] = *z;
            }
            sol::object o = host_call("map.spawn_pawn", a);
            int ph = o.is<int>() ? o.as<int>() : 0;
            return ph == 0 ? sol::make_object(*lua_, sol::lua_nil) : sol::make_object(*lua_, wrap_pawn(ph));
        },
        "find_cells",
        [this](RimMap& m, sol::optional<sol::table> opts) {
            sol::table a = lua_->create_table();
            a["h"] = m.h;
            if (opts) {
                sol::object terrain = (*opts)["terrain"];
                if (terrain.is<std::string>()) {
                    a["terrain"] = terrain.as<std::string>();
                }
                sol::object limit = (*opts)["limit"];
                if (limit.is<int>()) {
                    a["limit"] = limit.as<int>();
                } else if (limit.is<double>()) {
                    a["limit"] = static_cast<int>(limit.as<double>());
                }
            }
            sol::object o = host_call("map.find_cells", a);
            sol::table out = lua_->create_table();
            if (o.is<sol::table>()) {
                sol::table arr = o.as<sol::table>();
                int i = 1;
                for (const auto& kv : arr) {
                    if (!kv.second.is<std::string>()) {
                        continue;
                    }
                    std::string s = kv.second.as<std::string>();
                    auto comma = s.find(',');
                    if (comma == std::string::npos) {
                        continue;
                    }
                    sol::table cell = lua_->create_table();
                    cell["x"] = std::stoi(s.substr(0, comma));
                    cell["z"] = std::stoi(s.substr(comma + 1));
                    out[i++] = cell;
                }
            }
            return out;
        },
        "colonists",
        [this](RimMap& m) {
            sol::object o = host_call_h("map.colonists", m.h);
            sol::table out = lua_->create_table();
            if (o.is<sol::table>()) {
                sol::table arr = o.as<sol::table>();
                int i = 1;
                for (const auto& kv : arr) {
                    if (kv.second.is<int>()) {
                        out[i++] = wrap_pawn(kv.second.as<int>());
                    }
                }
            }
            return out;
        });

    lua_->new_usertype<RimThing>("RimThing", sol::no_constructor, "handle", &RimThing::h, "def",
                                sol::property([this](RimThing& t) -> std::string {
                                    sol::object o = host_call_h("thing.def", t.h);
                                    return o.is<std::string>() ? o.as<std::string>() : std::string{};
                                }),
                                "label", sol::property([this](RimThing& t) -> std::string {
                                    sol::object o = host_call_h("thing.label", t.h);
                                    return o.is<std::string>() ? o.as<std::string>() : std::string{};
                                }),
                                "destroy", [this](RimThing& t) { return host_call_h("thing.destroy", t.h); });

    lua_->new_usertype<RimEntity>(
        "RimEntity", sol::no_constructor, "handle", &RimEntity::h, "name",
        sol::property([this](RimEntity& e) -> std::string {
            sol::object o = host_call_h("pawn.name", e.h);
            return o.is<std::string>() ? o.as<std::string>() : std::string{};
        }),
        "kind",
        sol::property([this](RimEntity& e) -> std::string {
            sol::object o = host_call_h("pawn.kind", e.h);
            return o.is<std::string>() ? o.as<std::string>() : std::string{};
        }),
        "is_entity",
        [this](RimEntity& e) {
            sol::object o = host_call_h("anomaly.is_entity", e.h);
            return o.is<bool>() && o.as<bool>();
        },
        "recruit",
        [this](RimEntity& e) {
            sol::object o = host_call_h("anomaly.recruit", e.h);
            return o.is<bool>() && o.as<bool>();
        },
        "knock_out",
        [this](RimEntity& e, sol::optional<double> severity) {
            sol::table a = lua_->create_table();
            a["h"] = e.h;
            a["severity"] = severity.value_or(1.0);
            return host_call("anomaly.knock_out", a);
        },
        "release",
        [this](RimEntity& e) {
            sol::object o = host_call_h("anomaly.release_to_hostile", e.h);
            return o.is<bool>() && o.as<bool>();
        });

    lua_->new_usertype<RimObject>(
        "RimObject", sol::no_constructor, "handle", &RimObject::h, "type_name", &RimObject::type_name, "def", &RimObject::def,
        sol::meta_function::to_string, [](RimObject& o) { return "RimObject<" + o.type_name + "#" + std::to_string(o.h) + ">"; });

    (*lua_)["rim"]["wrap"] = [this](int h) { return wrap_pawn(h); };
    (*lua_)["rim"]["wrap_map"] = [this](int h) { return wrap_map(h); };
    (*lua_)["rim"]["wrap_thing"] = [this](int h) { return wrap_thing(h); };
    (*lua_)["rim"]["wrap_faction"] = [this](int h) { return wrap_faction(h); };
    (*lua_)["rim"]["wrap_entity"] = [this](int h) { return wrap_entity(h); };
}

void Engine::bind_events_and_timer() {
    sol::table events = lua_->create_named_table("events");
    // Canonical event names contain a dot (pawn.died). The host installs the Harmony patch on first subscription.
    events["on"] = [this](const std::string& name, sol::protected_function fn) {
        add_event_handler(name, std::move(fn));
    };
    events["off"] = [this](const std::string& requested) {
        const std::string name = canonical_event_name(requested);
        auto it = event_handlers_.find(name);
        if (it == event_handlers_.end()) {
            return;
        }
        const size_t count = it->second.size();
        event_handlers_.erase(it);
        for (size_t i = 0; i < count; ++i) {
            unsubscribe_named_event(name);
        }
    };
    events["list"] = [this]() {
        sol::table out = lua_->create_table();
        sol::object o = host_call("events.list", lua_->create_table());
        return o.is<sol::table>() ? o.as<sol::table>() : out;
    };

    sol::table logt = lua_->create_named_table("log");
    logt["info"] = [this](const std::string& msg) {
        if (callbacks_.log) {
            callbacks_.log(msg.c_str());
        }
    };
    logt["error"] = [this](const std::string& msg) {
        if (callbacks_.log) {
            std::string m = "[error] " + msg;
            callbacks_.log(m.c_str());
        }
    };

    sol::table game = lua_->create_named_table("game");
    game["current_map"] = [this]() -> sol::object {
        sol::object o = host_call("find.current_map", lua_->create_table());
        int h = o.is<int>() ? o.as<int>() : 0;
        if (h == 0) {
            return sol::make_object(*lua_, sol::lua_nil);
        }
        return sol::make_object(*lua_, wrap_map(h));
    };
    game["tick"] = [this]() { return current_tick(); };
    game["player_faction"] = [this]() -> sol::object {
        sol::object o = host_call("faction.player", lua_->create_table());
        int h = o.is<int>() ? o.as<int>() : 0;
        if (h == 0) {
            return sol::make_object(*lua_, sol::lua_nil);
        }
        return sol::make_object(*lua_, wrap_faction(h));
    };

    sol::table player = lua_->create_table();
    player["send_message"] = [this](const std::string& msg) {
        if (callbacks_.message) {
            callbacks_.message(msg.c_str());
        }
    };
    (*lua_)["player"] = player;

    sol::table timer = lua_->create_named_table("timer");
    timer["after"] = [this](int ticks, sol::protected_function fn) {
        TimerEntry e;
        e.mod = current_mod_;
        e.fire_tick = current_tick() + std::max(0, ticks);
        e.fn = std::move(fn);
        timers_.push_back(std::move(e));
    };
}

void Engine::bind_jobs_and_faction() {
    sol::table jobs = lua_->create_named_table("jobs");
    jobs["register"] = [this](const std::string& name, sol::table spec) {
        LuaJobDef def;
        sol::object can = spec["can_do"];
        if (can.is<sol::protected_function>()) {
            def.can_do = can.as<sol::protected_function>();
        }
        sol::object exec = spec["execute"];
        if (exec.is<sol::protected_function>()) {
            def.execute = exec.as<sol::protected_function>();
        }
        lua_jobs_[name] = std::move(def);
    };
    jobs["start"] = [this](RimPawn pawn, const std::string& name) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.h;
        a["name"] = name;
        sol::object o = host_call("job.start_lua", a);
        return o.is<bool>() && o.as<bool>();
    };

    sol::table path = lua_->create_named_table("path");
    path["can_reach"] = [this](RimPawn pawn, int x, int z) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.h;
        a["x"] = x;
        a["z"] = z;
        sol::object o = host_call("pawn.can_reach", a);
        return o.is<bool>() && o.as<bool>();
    };
    path["walk"] = [this](RimPawn pawn, int x, int z, sol::optional<bool> sprint) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.h;
        a["x"] = x;
        a["z"] = z;
        if (sprint && *sprint) {
            a["sprint"] = true;
        }
        sol::object o = host_call("pawn.walk_to", a);
        return o.is<bool>() && o.as<bool>();
    };
    path["wander"] = [this](RimPawn pawn, sol::optional<int> radius) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.h;
        a["radius"] = radius.value_or(12);
        sol::object o = host_call("pawn.wander", a);
        return o.is<bool>() && o.as<bool>();
    };
    path["compute"] = [this](RimPawn pawn, int x, int z) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.h;
        a["x"] = x;
        a["z"] = z;
        sol::object o = host_call("path.compute", a);
        sol::table out = lua_->create_table();
        if (o.is<sol::table>()) {
            sol::table arr = o.as<sol::table>();
            int i = 1;
            for (const auto& kv : arr) {
                if (!kv.second.is<std::string>()) {
                    continue;
                }
                std::string s = kv.second.as<std::string>();
                auto comma = s.find(',');
                if (comma == std::string::npos) {
                    continue;
                }
                sol::table cell = lua_->create_table();
                cell["x"] = std::stoi(s.substr(0, comma));
                cell["z"] = std::stoi(s.substr(comma + 1));
                out[i++] = cell;
            }
        }
        return out;
    };
    path["stop"] = [this](RimPawn pawn) { return host_call_h("pawn.stop", pawn.h); };

    sol::table control = lua_->create_named_table("control");
    control["claim"] = [this](RimPawn pawn, sol::optional<bool> click_walk) {
        sol::table a = lua_->create_table();
        a["h"] = pawn.h;
        a["click_walk"] = click_walk.value_or(true);
        sol::object o = host_call("control.set", a);
        return o.is<bool>() && o.as<bool>();
    };
    control["clear"] = [this]() { return host_call("control.clear", lua_->create_table()); };
    control["get"] = [this]() -> sol::object {
        sol::object o = host_call("control.get", lua_->create_table());
        int h = o.is<int>() ? o.as<int>() : 0;
        if (h == 0) {
            return sol::make_object(*lua_, sol::lua_nil);
        }
        return sol::make_object(*lua_, wrap_pawn(h));
    };
}

int Engine::register_ui_callback(sol::protected_function fn) {
    int id = next_ui_id_++;
    ui_callbacks_[id] = std::move(fn);
    ui_callback_mod_[id] = current_mod_;
    return id;
}

void Engine::ui_invoke(int callback_id) {
    auto it = ui_callbacks_.find(callback_id);
    if (it == ui_callbacks_.end() || !ready_) {
        return;
    }
    ModScope scope(*this, ui_callback_mod_[callback_id], PROF_UI);
    sol::protected_function_result r = it->second();
    if (!r.valid()) {
        log_lua_error("[RimKit] ui callback " + std::to_string(callback_id), r);
    }
}

const char* Engine::ui_call(int callback_id, const char* arg_json) {
    ui_call_cache_.clear();
    auto it = ui_callbacks_.find(callback_id);
    if (it == ui_callbacks_.end() || !ready_ || !lua_) {
        return ui_call_cache_.c_str();
    }
    sol::object arg = sol::make_object(*lua_, sol::lua_nil);
    if (arg_json && *arg_json) {
        rimlua::json::Value v;
        if (rimlua::json::parse(arg_json, v)) {
            arg = json_to_lua(v);
        }
    }
    ModScope scope(*this, ui_callback_mod_[callback_id], PROF_UI);
    sol::protected_function_result r = it->second(arg);
    if (!r.valid()) {
        // A widget view fails every frame. Log the first few, then stay quiet for that callback.
        if (++ui_error_counts_[callback_id] <= 3) {
            log_lua_error("[RimKit] ui callback " + std::to_string(callback_id), r);
        }
        return ui_call_cache_.c_str();
    }
    sol::object ret = r;
    if (ret.get_type() != sol::type::lua_nil) {
        ui_call_cache_ = lua_value_json(ret);
    }
    return ui_call_cache_.c_str();
}

const char* Engine::collect_map_float_menu(int clicked_handle, int hauler_handle) {
    float_menu_blob_cache_.clear();
    if (!ready_ || !lua_ || map_float_menu_handlers_.empty()) {
        return float_menu_blob_cache_.c_str();
    }

    for (sol::protected_function& fn : map_float_menu_handlers_) {
        sol::table ctx = lua_->create_table();
        ctx["clicked"] = clicked_handle;
        ctx["hauler"] = hauler_handle;
        sol::protected_function_result r = fn(ctx);
        if (!r.valid()) {
            log_lua_error("[RimKit] on_map_float_menu", r);
            continue;
        }
        sol::object obj = r;
        if (!obj.is<sol::table>()) {
            continue;
        }
        sol::table opts = obj.as<sol::table>();
        for (const auto& kv : opts) {
            if (!kv.second.is<sol::table>()) {
                continue;
            }
            sol::table b = kv.second.as<sol::table>();
            std::string label = "Option";
            sol::object lo = b["label"];
            if (lo.is<std::string>()) {
                label = lo.as<std::string>();
            }
            bool disabled = false;
            sol::object dobj = b["disabled"];
            if (dobj.is<bool>()) {
                disabled = dobj.as<bool>();
            }
            int id = 0;
            if (!disabled) {
                sol::object click = b["on_click"];
                if (!click.valid()) {
                    click = b["action"];
                }
                if (click.is<sol::protected_function>()) {
                    id = register_ui_callback(click.as<sol::protected_function>());
                }
            }
            if (!float_menu_blob_cache_.empty()) {
                float_menu_blob_cache_ += "\n";
            }
            float_menu_blob_cache_ += label;
            float_menu_blob_cache_ += "\t";
            float_menu_blob_cache_ += std::to_string(id);
        }
    }
    return float_menu_blob_cache_.c_str();
}

void Engine::bind_ui_config_defs() {
    auto build_button_blob = [this](sol::object buttons_obj) -> std::string {
        std::string blob;
        if (!buttons_obj.is<sol::table>()) {
            return blob;
        }
        sol::table buttons = buttons_obj.as<sol::table>();
        for (const auto& kv : buttons) {
            if (!kv.second.is<sol::table>()) {
                continue;
            }
            sol::table b = kv.second.as<sol::table>();
            std::string label = "Button";
            sol::object lo = b["label"];
            if (lo.is<std::string>()) {
                label = lo.as<std::string>();
            }
            int id = 0;
            sol::object click = b["on_click"];
            if (!click.valid()) {
                click = b["action"];
            }
            if (click.is<sol::protected_function>()) {
                id = register_ui_callback(click.as<sol::protected_function>());
            }
            if (!blob.empty()) {
                blob += "\n";
            }
            blob += label;
            blob += "\t";
            blob += std::to_string(id);
        }
        return blob;
    };

    sol::table ui = lua_->create_named_table("ui");
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
    ui["window"] = [this, build_button_blob](sol::table spec) {
        sol::table a = lua_->create_table();
        sol::object title = spec["title"];
        a["title"] = title.is<std::string>() ? title.as<std::string>() : "RimLua";
        sol::object body = spec["body"];
        a["body"] = body.is<std::string>() ? body.as<std::string>() : "";
        a["buttons"] = build_button_blob(spec["buttons"]);
        return host_call("ui.open_window", a);
    };
    ui["float_menu"] = [this, build_button_blob](sol::table options) {
        sol::table a = lua_->create_table();
        a["buttons"] = build_button_blob(options);
        return host_call("ui.float_menu", a);
    };
    // Right-click map thing: fn(ctx) -> { {label=, on_click=, disabled?}, ... }
    ui["on_map_float_menu"] = [this](sol::protected_function fn) {
        map_float_menu_handlers_.push_back(std::move(fn));
        map_float_menu_mods_.push_back(current_mod_);
    };

    sol::table config = lua_->create_named_table("config");
    config["register"] = [this](const std::string& key, sol::table spec) {
        sol::table a = lua_->create_table();
        a["key"] = key;
        sol::object type = spec["type"];
        a["type"] = type.is<std::string>() ? type.as<std::string>() : "bool";
        sol::object label = spec["label"];
        a["label"] = label.is<std::string>() ? label.as<std::string>() : key;
        sol::object def = spec["default"];
        if (def.is<bool>()) {
            a["default"] = def.as<bool>() ? "true" : "false";
        } else if (def.is<double>()) {
            a["default"] = std::to_string(def.as<double>());
        } else if (def.is<std::string>()) {
            a["default"] = def.as<std::string>();
        } else {
            a["default"] = "false";
        }
        return host_call("config.register", a);
    };
    config["get"] = [this](const std::string& key) -> std::string {
        sol::table a = lua_->create_table();
        a["key"] = key;
        sol::object o = host_call("config.get", a);
        return o.is<std::string>() ? o.as<std::string>() : std::string{};
    };
    config["get_bool"] = [this](const std::string& key) {
        sol::table a = lua_->create_table();
        a["key"] = key;
        sol::object o = host_call("config.get", a);
        if (o.is<bool>()) {
            return o.as<bool>();
        }
        if (o.is<std::string>()) {
            std::string s = o.as<std::string>();
            return s == "1" || s == "true" || s == "True";
        }
        return false;
    };
    config["set"] = [this](const std::string& key, sol::object value) {
        sol::table a = lua_->create_table();
        a["key"] = key;
        if (value.is<bool>()) {
            a["value"] = value.as<bool>() ? "true" : "false";
        } else if (value.is<double>()) {
            a["value"] = std::to_string(value.as<double>());
        } else if (value.is<std::string>()) {
            a["value"] = value.as<std::string>();
        } else {
            a["value"] = "";
        }
        return host_call("config.set", a);
    };

    sol::table defs = lua_->create_named_table("defs");
    defs["register_thing"] = [this](sol::table spec) {
        sol::table a = lua_->create_table();
        auto copy_str = [&](const char* k) {
            sol::object o = spec[k];
            if (o.is<std::string>()) {
                a[k] = o.as<std::string>();
            }
        };
        copy_str("defName");
        copy_str("label");
        copy_str("description");
        copy_str("package_id");
        copy_str("texPath");
        sol::object stack = spec["stackLimit"];
        if (stack.is<int>()) {
            a["stackLimit"] = stack.as<int>();
        } else if (stack.is<double>()) {
            a["stackLimit"] = static_cast<int>(stack.as<double>());
        }
        sol::object o = host_call("defs.write_thing", a);
        if (callbacks_.log) {
            callbacks_.log("[RimKit] defs.register_thing wrote XML (restart RimWorld to load Def)");
        }
        return o;
    };
    defs["exists"] = [this](const std::string& typeName, const std::string& name) {
        sol::table a = lua_->create_table();
        a["type"] = typeName;
        a["name"] = name;
        sol::object o = host_call("defs.exists", a);
        return o.is<bool>() && o.as<bool>();
    };

    sol::table input = lua_->create_named_table("input");
    input["binding_just_pressed"] = [this](const std::string& def) {
        sol::table a = lua_->create_table();
        a["def"] = def;
        sol::object o = host_call("input.binding_just_pressed", a);
        return o.is<bool>() && o.as<bool>();
    };
}

void Engine::bind_strong_api() {
    auto call1 = [this](const char* op, const std::string& key, const std::string& val) {
        sol::table a = lua_->create_table();
        a[key] = val;
        return host_call(op, a);
    };

    sol::table data = lua_->create_named_table("data");
    data["get"] = [this](const std::string& package_id, const std::string& key) {
        sol::table a = lua_->create_table();
        a["package_id"] = package_id;
        a["key"] = key;
        return host_call("data.get", a);
    };
    data["set"] = [this](const std::string& package_id, const std::string& key, const std::string& value) {
        sol::table a = lua_->create_table();
        a["package_id"] = package_id;
        a["key"] = key;
        a["value"] = value;
        return host_call("data.set", a);
    };
    data["remove"] = [this](const std::string& package_id, const std::string& key) {
        sol::table a = lua_->create_table();
        a["package_id"] = package_id;
        a["key"] = key;
        return host_call("data.remove", a);
    };
    data["keys"] = [this](const std::string& package_id) {
        sol::table a = lua_->create_table();
        a["package_id"] = package_id;
        return host_call("data.keys", a);
    };

    sol::table health = lua_->create_named_table("health");
    health["has_hediff"] = [this](HandleArg h, const std::string& def) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["def"] = def;
        return host_call("health.has_hediff", a);
    };
    health["hediff_severity"] = [this](HandleArg h, const std::string& def) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["def"] = def;
        return host_call("health.hediff_severity", a);
    };
    health["set_hediff_severity"] = [this](HandleArg h, const std::string& def, double sev) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["def"] = def;
        a["severity"] = sev;
        return host_call("health.set_hediff_severity", a);
    };
    health["tend"] = [this](HandleArg h, sol::optional<double> quality) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["quality"] = quality.value_or(0.5);
        return host_call("health.tend", a);
    };

    sol::table surgery = lua_->create_named_table("surgery");
    surgery["queue_operation"] = [this](HandleArg h, const std::string& recipe) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["recipe"] = recipe;
        return host_call("surgery.queue_operation", a);
    };

    // General draftable control on rim.pawn (also mirrored as global pawn).
    sol::table rim_tbl = (*lua_)["rim"];
    sol::table pawn_tbl = rim_tbl.get<sol::table>("pawn");
    if (pawn_tbl.valid()) {
        pawn_tbl["make_controllable"] = [this](HandleArg h) {
            sol::table a = lua_->create_table();
            a["h"] = h.v;
            return host_call("pawn.make_controllable", a);
        };
        pawn_tbl["release_control"] = [this](HandleArg h) {
            sol::table a = lua_->create_table();
            a["h"] = h.v;
            return host_call("pawn.release_control", a);
        };
        pawn_tbl["is_controllable"] = [this](HandleArg h) {
            sol::table a = lua_->create_table();
            a["h"] = h.v;
            return host_call("pawn.is_controllable", a);
        };
        (*lua_)["pawn"] = pawn_tbl;
    }

    sol::table anomaly = lua_->create_named_table("anomaly");
    anomaly["dlc_active"] = [this]() { return host_call("anomaly.dlc_active", lua_->create_table()); };
    anomaly["is_entity"] = [this](HandleArg h) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        return host_call("anomaly.is_entity", a);
    };
    anomaly["try_set_faction_player"] = [this](HandleArg h) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        return host_call("anomaly.try_set_faction_player", a);
    };
    anomaly["release_to_hostile"] = [this](HandleArg h) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        return host_call("anomaly.release_to_hostile", a);
    };
    anomaly["list_on_map"] = [this](sol::optional<HandleArg> map_h) {
        sol::table a = lua_->create_table();
        if (map_h && map_h->v > 0) a["h"] = map_h->v;
        return host_call("anomaly.list_on_map", a);
    };
    anomaly["get_on_map"] = [this](const std::string& name, sol::optional<HandleArg> map_h) {
        sol::table a = lua_->create_table();
        a["name"] = name;
        if (map_h && map_h->v > 0) a["h"] = map_h->v;
        return host_call("anomaly.get_on_map", a);
    };
    anomaly["knock_out"] = [this](HandleArg h, sol::optional<double> severity) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["severity"] = severity.value_or(1.0);
        return host_call("anomaly.knock_out", a);
    };
    anomaly["find_platform"] = [this](HandleArg hauler_h, sol::optional<HandleArg> entity_h) {
        sol::table a = lua_->create_table();
        a["h"] = hauler_h.v;
        if (entity_h && entity_h->v > 0) a["entity"] = entity_h->v;
        return host_call("anomaly.find_platform", a);
    };
    anomaly["start_capture"] = [this](HandleArg hauler_h, HandleArg entity_h, sol::optional<HandleArg> platform_h) {
        sol::table a = lua_->create_table();
        a["h"] = hauler_h.v;
        a["entity"] = entity_h.v;
        if (platform_h && platform_h->v > 0) a["platform"] = platform_h->v;
        return host_call("anomaly.start_capture", a);
    };
    anomaly["recruit"] = [this](HandleArg h) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        return host_call("anomaly.recruit", a);
    };

    sol::table util = lua_->create_named_table("util");
    util["open_folder"] = [this](const std::string& target, sol::optional<std::string> package_id) {
        sol::table a = lua_->create_table();
        a["target"] = target;
        if (package_id) a["package_id"] = *package_id;
        return host_call("util.open_folder", a);
    };
    util["write_export"] = [this](const std::string& package_id, const std::string& file, const std::string& content) {
        sol::table a = lua_->create_table();
        a["package_id"] = package_id;
        a["file"] = file;
        a["content"] = content;
        return host_call("util.write_export", a);
    };

    sol::table building = lua_->create_named_table("building");
    building["power_on"] = [this](HandleArg h) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        return host_call("building.power_on", a);
    };
    building["set_power"] = [this](HandleArg h, bool v) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["v"] = v ? "true" : "false";
        return host_call("building.set_power", a);
    };
    building["flick"] = [this](HandleArg h, bool v) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["v"] = v ? "true" : "false";
        return host_call("building.flick", a);
    };

    sol::table work = lua_->create_named_table("work");
    work["get_priority"] = [this](HandleArg h, const std::string& wt) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["work"] = wt;
        return host_call("work.get_priority", a);
    };
    work["set_priority"] = [this](HandleArg h, const std::string& wt, int pri) {
        sol::table a = lua_->create_table();
        a["h"] = h.v;
        a["work"] = wt;
        a["priority"] = pri;
        return host_call("work.set_priority", a);
    };
    work["list_types"] = [this]() { return host_call("work.list_types", lua_->create_table()); };

    sol::table world = lua_->create_named_table("world_api");
    world["weather"] = [this](sol::optional<HandleArg> map_h) {
        sol::table a = lua_->create_table();
        if (map_h && map_h->v > 0) a["h"] = map_h->v;
        return host_call("world.weather", a);
    };
    world["set_weather"] = [this](const std::string& def, sol::optional<HandleArg> map_h) {
        sol::table a = lua_->create_table();
        a["def"] = def;
        if (map_h && map_h->v > 0) a["h"] = map_h->v;
        return host_call("world.set_weather", a);
    };
    (*lua_)["world_api"] = world;

    sol::table incident = lua_->create_named_table("incident");
    incident["try_fire"] = [this](const std::string& def, sol::optional<HandleArg> map_h) {
        sol::table a = lua_->create_table();
        a["def"] = def;
        if (map_h && map_h->v > 0) a["h"] = map_h->v;
        return host_call("incident.try_fire", a);
    };
    incident["list"] = [this]() { return host_call("incident.list", lua_->create_table()); };

    sol::table audio = lua_->create_named_table("audio");
    audio["play"] = [this](const std::string& def) {
        sol::table a = lua_->create_table();
        a["def"] = def;
        return host_call("audio.play", a);
    };

    sol::table ui = (*lua_)["ui"];
    if (ui.valid()) {
        ui["panel"] = [this](sol::table spec) {
            sol::table a = lua_->create_table();
            sol::object title = spec["title"];
            a["title"] = title.is<std::string>() ? title.as<std::string>() : "RimKit";
            sol::object body = spec["body"];
            a["body"] = body.is<std::string>() ? body.as<std::string>() : "";
            std::string checks;
            sol::object checks_obj = spec["checks"];
            if (checks_obj.is<sol::table>()) {
                sol::table checks_tbl = checks_obj.as<sol::table>();
                for (const auto& kv : checks_tbl) {
                    if (kv.second.is<std::string>()) {
                        if (!checks.empty()) checks += "\n";
                        checks += kv.second.as<std::string>();
                    }
                }
            }
            a["checks"] = checks;
            std::string list;
            sol::object list_obj = spec["list"];
            if (list_obj.is<sol::table>()) {
                sol::table list_tbl = list_obj.as<sol::table>();
                for (const auto& kv : list_tbl) {
                    if (kv.second.is<std::string>()) {
                        if (!list.empty()) list += "\n";
                        list += kv.second.as<std::string>();
                    }
                }
            }
            a["list"] = list;
            return host_call("ui.panel", a);
        };
    }

    sol::table game = (*lua_)["game"];
    if (game.valid()) {
        sol::table anomalies = lua_->create_table();
        anomalies["get"] = [this](sol::table /*self*/, const std::string& name, sol::optional<RimMap> map) -> sol::object {
            sol::table a = lua_->create_table();
            a["name"] = name;
            if (map) {
                a["h"] = map->h;
            }
            sol::object o = host_call("anomaly.get_on_map", a);
            int h = o.is<int>() ? o.as<int>() : 0;
            if (h <= 0) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            return sol::make_object(*lua_, wrap_entity(h));
        };
        anomalies["list"] = [this](sol::table /*self*/, sol::optional<RimMap> map) -> sol::object {
            sol::table a = lua_->create_table();
            if (map) {
                a["h"] = map->h;
            }
            sol::object o = host_call("anomaly.list_on_map", a);
            sol::table out = lua_->create_table();
            if (o.is<sol::table>()) {
                sol::table src = o.as<sol::table>();
                int i = 1;
                for (const auto& kv : src) {
                    if (kv.second.is<int>()) {
                        int h = kv.second.as<int>();
                        if (h > 0) {
                            out[i++] = wrap_entity(h);
                        }
                    }
                }
            }
            return sol::make_object(*lua_, out);
        };
        anomalies["find"] = [this](sol::table /*self*/, sol::protected_function predicate,
                                   sol::optional<RimMap> map) -> sol::object {
            sol::table a = lua_->create_table();
            if (map) {
                a["h"] = map->h;
            }
            sol::object o = host_call("anomaly.list_on_map", a);
            if (!o.is<sol::table>()) {
                return sol::make_object(*lua_, sol::lua_nil);
            }
            sol::table src = o.as<sol::table>();
            for (const auto& kv : src) {
                if (!kv.second.is<int>()) {
                    continue;
                }
                int h = kv.second.as<int>();
                if (h <= 0) {
                    continue;
                }
                RimEntity ent = wrap_entity(h);
                sol::protected_function_result r = predicate(ent);
                if (!r.valid()) {
                    log_lua_error("[RimKit] game.anomalies:find", r);
                    continue;
                }
                bool ok = false;
                if (r.return_count() > 0) {
                    if (r.get_type(0) == sol::type::boolean) {
                        ok = r.get<bool>(0);
                    } else if (r.get_type(0) != sol::type::nil) {
                        ok = true;
                    }
                }
                if (ok) {
                    return sol::make_object(*lua_, ent);
                }
            }
            return sol::make_object(*lua_, sol::lua_nil);
        };
        game["anomalies"] = anomalies;
    }

    if (callbacks_.log) {
        callbacks_.log("[RimKit] strong API domains bound (data/health/anomaly/util/building/work/...)");
    }
}

void Engine::bind_rimkit_module() {
    if (!lua_) {
        return;
    }
    sol::table mod = lua_->create_table();
    mod["version"] = RIMKIT_VERSION;
    mod["api_level"] = RIMKIT_API_LEVEL;

    sol::table stable = lua_->create_table();
    stable["rim_pawn"] = (*lua_)["rim"]["pawn"];
    stable["rim_map"] = (*lua_)["rim"]["map"];
    stable["rim_find"] = (*lua_)["rim"]["find"];
    stable["rim_thing"] = (*lua_)["rim"]["thing"];
    stable["rim_faction"] = (*lua_)["rim"]["faction"];
    stable["events"] = (*lua_)["events"];
    stable["data"] = (*lua_)["data"];
    stable["ui"] = (*lua_)["ui"];
    stable["log"] = (*lua_)["log"];
    stable["game"] = (*lua_)["game"];
    stable["timer"] = (*lua_)["timer"];
    mod["stable"] = stable;

    sol::table experimental = lua_->create_table();
    experimental["anomaly"] = (*lua_)["anomaly"];
    experimental["game_anomalies"] = (*lua_)["game"]["anomalies"];
    experimental["rim_prefix"] = (*lua_)["rim"]["prefix"];
    experimental["rim_postfix"] = (*lua_)["rim"]["postfix"];
    experimental["rim_harmony"] = (*lua_)["rim"]["harmony"];
    experimental["jobs"] = (*lua_)["jobs"];
    experimental["path"] = (*lua_)["path"];
    experimental["config"] = (*lua_)["config"];
    experimental["health"] = (*lua_)["health"];
    experimental["surgery"] = (*lua_)["surgery"];
    experimental["building"] = (*lua_)["building"];
    experimental["work"] = (*lua_)["work"];
    experimental["world_api"] = (*lua_)["world_api"];
    experimental["incident"] = (*lua_)["incident"];
    experimental["audio"] = (*lua_)["audio"];
    experimental["control"] = (*lua_)["control"];
    mod["experimental"] = experimental;

    // rk.strict_errors(true): every function raises a Lua error on failure (starting with its RK code) instead of
    // logging and returning nil. Off by default for older mods. The newer kits always raise.
    mod["strict_errors"] = [this](sol::optional<bool> on) {
        if (on) {
            strict_errors_ = *on;
        }
        return strict_errors_;
    };
    mod["assert_api"] = [this](int min_level) {
        const int level = RIMKIT_API_LEVEL;
        if (min_level > level) {
            std::string msg = "RimKit api_level " + std::to_string(level) + " < required " + std::to_string(min_level);
            if (callbacks_.log) {
                callbacks_.log(("[error] " + msg).c_str());
            }
            throw std::runtime_error(msg);
        }
        return true;
    };

    sol::table package = (*lua_)["package"];
    sol::table preload = package["preload"];
    if (!preload.valid()) {
        preload = lua_->create_table();
        package["preload"] = preload;
    }
    preload["rimkit"] = [mod]() { return mod; };

    if (callbacks_.log) {
        const std::string msg = std::string("[RimKit] require(\"rimkit\") ready (version ") + RIMKIT_VERSION +
                                ", api_level " + std::to_string(RIMKIT_API_LEVEL) + ")";
        callbacks_.log(msg.c_str());
    }
}

int Engine::job_call(const std::string& name, const std::string& phase, int pawn_handle, int* out_result) {
    if (out_result) {
        *out_result = 0;
    }
    if (!ready_) {
        return 1;
    }
    auto it = lua_jobs_.find(name);
    if (it == lua_jobs_.end()) {
        return 1;
    }
    RimPawn pawn = wrap_pawn(pawn_handle);
    if (phase == "can_do") {
        if (!it->second.can_do.valid()) {
            if (out_result) {
                *out_result = 1;
            }
            return 0;
        }
        sol::protected_function_result r = it->second.can_do(pawn);
        if (!r.valid()) {
            log_lua_error("[RimKit] job can_do " + name, r);
            return 2;
        }
        bool ok = true;
        if (r.return_count() > 0 && r.get_type(0) == sol::type::boolean) {
            ok = r.get<bool>(0);
        }
        if (out_result) {
            *out_result = ok ? 1 : 0;
        }
        return 0;
    }
    if (phase == "execute") {
        if (!it->second.execute.valid()) {
            if (out_result) {
                *out_result = 1;
            }
            return 0;
        }
        sol::protected_function_result r = it->second.execute(pawn);
        if (!r.valid()) {
            log_lua_error("[RimKit] job execute " + name, r);
            return 2;
        }
        bool done = false;
        if (r.return_count() > 0 && r.get_type(0) == sol::type::boolean) {
            done = r.get<bool>(0);
        }
        if (out_result) {
            *out_result = done ? 1 : 0;
        }
        return 0;
    }
    return 1;
}

void Engine::wire_global_handlers() {
    if (!lua_) {
        return;
    }
    // Legacy file level handlers (on_pawn_died) are served through the canonical event with the old signature.
    for (const aliases::GlobalAlias& g : aliases::table().globals) {
        sol::object o = (*lua_)[g.from];
        if (!o.is<sol::protected_function>()) {
            continue;
        }
        warn_deprecated(g.from, "events.on(\"" + g.event + "\", fn)");
        add_event_handler(g.event, o.as<sol::protected_function>(), /*legacy_signature=*/true);
    }
}

void Engine::emit_event_ex(const std::string& name, const std::string& payload_json) {
    auto it = event_handlers_.find(name);
    if (it == event_handlers_.end() || !ready_) {
        return;
    }
    rimlua::json::Value root;
    sol::object payload = sol::make_object(*lua_, sol::lua_nil);
    if (rimlua::json::parse(payload_json, root)) {
        payload = json_to_lua(root);
    }
    // Copy: a handler may call events.off while running.
    const std::vector<ModFn> handlers = it->second;
    for (const auto& cb : handlers) {
        if (!mod_enabled(cb.mod)) {
            continue;
        }
        ModScope scope(*this, cb.mod, PROF_EVENT);
        sol::protected_function_result r = cb.fn(payload);
        if (!r.valid()) {
            log_lua_error("[RimKit] event " + name, r);
            record_mod_error(cb.mod, "event " + name);
        }
    }
}

void Engine::emit_event(const std::string& name, int handle) {
    auto it = event_handlers_.find(name);
    if (it == event_handlers_.end() || !ready_) {
        return;
    }
    RimPawn pawn = wrap_pawn(handle);
    const std::vector<ModFn> handlers = it->second;
    for (const auto& cb : handlers) {
        if (!mod_enabled(cb.mod)) {
            continue;
        }
        ModScope scope(*this, cb.mod, PROF_EVENT);
        sol::protected_function_result r = cb.fn(pawn);
        if (!r.valid()) {
            log_lua_error("[RimKit] event " + name, r);
            record_mod_error(cb.mod, "event " + name);
        }
    }
}

}  // namespace rimlua
