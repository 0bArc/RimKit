#include "engine.hpp"

#include <algorithm>

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
        [this](RimPawn& p, const std::string& trait) {
            return host_call_hv("pawn.add_trait", p.h, "def", sol::make_object(*lua_, trait));
        },
        "remove_trait",
        [this](RimPawn& p, const std::string& trait) {
            return host_call_hv("pawn.remove_trait", p.h, "def", sol::make_object(*lua_, trait));
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
        "seek_medical_help", [this](RimPawn& p) { return host_call_h("pawn.seek_medical", p.h); },
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
        "spawn_thing",
        [this](RimMap& m, const std::string& def, int x, int z, sol::optional<int> stack) {
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

    (*lua_)["rim"]["wrap"] = [this](int h) { return wrap_pawn(h); };
    (*lua_)["rim"]["wrap_map"] = [this](int h) { return wrap_map(h); };
    (*lua_)["rim"]["wrap_thing"] = [this](int h) { return wrap_thing(h); };
    (*lua_)["rim"]["wrap_faction"] = [this](int h) { return wrap_faction(h); };
}

void Engine::bind_events_and_timer() {
    sol::table events = lua_->create_named_table("events");
    events["on"] = [this](const std::string& name, sol::protected_function fn) {
        event_handlers_[name].push_back(std::move(fn));
    };
    events["off"] = [this](const std::string& name) { event_handlers_.erase(name); };

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
}

int Engine::register_ui_callback(sol::protected_function fn) {
    int id = next_ui_id_++;
    ui_callbacks_[id] = std::move(fn);
    return id;
}

void Engine::ui_invoke(int callback_id) {
    auto it = ui_callbacks_.find(callback_id);
    if (it == ui_callbacks_.end() || !ready_) {
        return;
    }
    sol::protected_function_result r = it->second();
    if (!r.valid()) {
        log_lua_error("[RimLuaKit] ui callback " + std::to_string(callback_id), r);
    }
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
            callbacks_.log("[RimLuaKit] defs.register_thing wrote XML (restart RimWorld to load Def)");
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
            log_lua_error("[RimLuaKit] job can_do " + name, r);
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
            log_lua_error("[RimLuaKit] job execute " + name, r);
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
    auto wire = [this](const char* global_name, const char* event_name) {
        sol::object o = (*lua_)[global_name];
        if (o.is<sol::protected_function>()) {
            event_handlers_[event_name].push_back(o.as<sol::protected_function>());
        }
    };
    wire("on_pawn_spawned", "pawn_spawned");
    wire("on_pawn_died", "pawn_died");
}

void Engine::emit_event(const std::string& name, int handle) {
    auto it = event_handlers_.find(name);
    if (it == event_handlers_.end() || !ready_) {
        return;
    }
    RimPawn pawn = wrap_pawn(handle);
    for (auto& fn : it->second) {
        sol::protected_function_result r = fn(pawn);
        if (!r.valid()) {
            log_lua_error("[RimLuaKit] event " + name, r);
        }
    }
}

}  // namespace rimlua
