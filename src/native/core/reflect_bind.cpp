#include "engine.hpp"
#include "handle_util.hpp"

#include <string>

namespace rimlua {

namespace rj = rimlua::json;

// game.reflect: typed reflection over the host. Arguments and results are real Lua values.
// Failures raise a Lua error whose message starts with an RK code (see docs/standard/rks.md).
void Engine::bind_reflect_core() {
    sol::table game = (*lua_)["game"];
    sol::table reflect = lua_->create_table();

    auto call_op = [this](const std::string& op, const sol::table& args) -> sol::object {
        if (mock_active_) {
            sol::object mocked;
            if (try_mock(op, args, mocked)) {
                return mocked;
            }
        }
        check_capability_for_op(op);
        const std::string json = table_to_json(args);
        char* raw = callbacks_.host_invoke(op.c_str(), json.c_str());
        if (!raw) {
            throw sol::error("RK5001: host returned nothing for " + op);
        }
        std::string resp(raw);
        callbacks_.host_free(raw);
        rj::Value root;
        if (!rj::parse(resp, root) || !root.is_object()) {
            throw sol::error("RK5001: unreadable host response for " + op);
        }
        if (!root.get_bool("ok", false)) {
            throw sol::error(root.get_string("e", "RK5001: host error in " + op));
        }
        return decode_response(resp);
    };

    auto args_array = [this](sol::variadic_args va) {
        std::string out = "[";
        bool first = true;
        for (auto v : va) {
            if (!first) {
                out += ',';
            }
            first = false;
            sol::object o = v;
            lua_to_json(o, out, 0);
        }
        out += ']';
        return out;
    };

    auto instance_call = [this, call_op, args_array](const char* op, sol::object target, const std::string& method,
                                                     sol::variadic_args va, const std::string& sig_json) {
        sol::table a = lua_->create_table();
        a["h"] = handle_of(target);
        a["method"] = method;
        a["args_json"] = args_array(va);
        if (!sig_json.empty()) {
            a["sig_json"] = sig_json;
        }
        return call_op(op, a);
    };
    auto static_call = [this, call_op, args_array](const char* op, const std::string& type, const std::string& method,
                                                   sol::variadic_args va, const std::string& sig_json) {
        sol::table a = lua_->create_table();
        a["type"] = type;
        a["method"] = method;
        a["args_json"] = args_array(va);
        if (!sig_json.empty()) {
            a["sig_json"] = sig_json;
        }
        return call_op(op, a);
    };

    reflect["get"] = [this, call_op](sol::object target, const std::string& member) {
        sol::table a = lua_->create_table();
        a["h"] = handle_of(target);
        a["member"] = member;
        return call_op("reflect.get", a);
    };
    reflect["set"] = [this, call_op](sol::object target, const std::string& member, sol::object value) {
        sol::table a = lua_->create_table();
        a["h"] = handle_of(target);
        a["member"] = member;
        a["value_json"] = lua_value_json(value);
        return call_op("reflect.set", a);
    };
    reflect["call"] = [instance_call](sol::object target, const std::string& method, sol::variadic_args va) {
        return instance_call("reflect.call", target, method, va, std::string());
    };
    reflect["call_sig"] = [this, instance_call](sol::object target, const std::string& method, sol::table sig,
                                                sol::variadic_args va) {
        return instance_call("reflect.call", target, method, va, lua_value_json(sol::make_object(*lua_, sig)));
    };
    reflect["static_get"] = [this, call_op](const std::string& type, const std::string& member) {
        sol::table a = lua_->create_table();
        a["type"] = type;
        a["member"] = member;
        return call_op("reflect.static_get", a);
    };
    reflect["static_set"] = [this, call_op](const std::string& type, const std::string& member, sol::object value) {
        sol::table a = lua_->create_table();
        a["type"] = type;
        a["member"] = member;
        a["value_json"] = lua_value_json(value);
        return call_op("reflect.static_set", a);
    };
    reflect["static_call"] = [static_call](const std::string& type, const std::string& method, sol::variadic_args va) {
        return static_call("reflect.static_call", type, method, va, std::string());
    };
    reflect["static_call_sig"] = [this, static_call](const std::string& type, const std::string& method, sol::table sig,
                                                     sol::variadic_args va) {
        return static_call("reflect.static_call", type, method, va, lua_value_json(sol::make_object(*lua_, sig)));
    };
    reflect["new"] = [this, call_op, args_array](const std::string& type, sol::variadic_args va) {
        sol::table a = lua_->create_table();
        a["type"] = type;
        a["args_json"] = args_array(va);
        return call_op("reflect.new", a);
    };
    // Accepts a wrapped object, a handle, or a type name.
    reflect["members"] = [this, call_op](sol::object target) {
        sol::table a = lua_->create_table();
        if (target.is<std::string>()) {
            a["type"] = target.as<std::string>();
        } else {
            a["h"] = handle_of(target);
        }
        return call_op("reflect.members", a);
    };
    reflect["type"] = [this, call_op](sol::object target) {
        sol::table a = lua_->create_table();
        a["h"] = handle_of(target);
        return call_op("reflect.type", a);
    };
    reflect["is_a"] = [this, call_op](sol::object target, const std::string& type) {
        sol::table a = lua_->create_table();
        a["h"] = handle_of(target);
        a["type"] = type;
        return call_op("reflect.is_a", a);
    };
    reflect["release"] = [this, call_op](sol::object target) {
        sol::table a = lua_->create_table();
        a["h"] = handle_of(target);
        return call_op("reflect.release", a);
    };
    reflect["enum_names"] = [this, call_op](const std::string& type) {
        sol::table a = lua_->create_table();
        a["type"] = type;
        return call_op("reflect.enum_names", a);
    };
    reflect["audit"] = [this, call_op](sol::optional<int> n) {
        sol::table a = lua_->create_table();
        a["n"] = n.value_or(50);
        return call_op("reflect.audit", a);
    };

    game["reflect"] = reflect;
}

}  // namespace rimlua
