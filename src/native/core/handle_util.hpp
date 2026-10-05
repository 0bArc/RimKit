#pragma once

// Shared helper: any wrapped game object (or a plain integer handle) to its handle.

#include "engine.hpp"

namespace rimlua {

inline int handle_of(const sol::object& o) {
    if (o.is<RimPawn>()) return o.as<RimPawn&>().h;
    if (o.is<RimThing>()) return o.as<RimThing&>().h;
    if (o.is<RimMap>()) return o.as<RimMap&>().h;
    if (o.is<RimFaction>()) return o.as<RimFaction&>().h;
    if (o.is<RimEntity>()) return o.as<RimEntity&>().h;
    if (o.is<RimObject>()) return o.as<RimObject&>().h;
    if (o.get_type() == sol::type::number) return static_cast<int>(o.as<double>());
    return 0;
}

// A lambda parameter that takes a wrapped game object or a plain integer handle, so legacy functions accept both.
struct HandleArg {
    int v = 0;
};

}  // namespace rimlua

namespace sol {
template <>
struct lua_type_of<rimlua::HandleArg> : std::integral_constant<type, type::poly> {};

namespace stack {
template <>
struct unqualified_checker<rimlua::HandleArg, type::poly> {
    template <typename Handler>
    static bool check(lua_State*, int, Handler&&, record& tracking) {
        tracking.use(1);
        return true;
    }
};

template <>
struct unqualified_getter<rimlua::HandleArg> {
    static rimlua::HandleArg get(lua_State* L, int index, record& tracking) {
        tracking.use(1);
        sol::object o = stack::get<sol::object>(L, index);
        return rimlua::HandleArg{rimlua::handle_of(o)};
    }
};
}  // namespace stack
}  // namespace sol

namespace rimlua {

// Lua value to a named host argument. Wrapped game objects become handles, nil leaves the key out so the host
// applies its default.
inline void put_arg(sol::table& args, const char* key, const sol::object& value) {
    switch (value.get_type()) {
        case sol::type::boolean:
            args[key] = value.as<bool>();
            return;
        case sol::type::number:
            args[key] = value.as<double>();
            return;
        case sol::type::string:
            args[key] = value.as<std::string>();
            return;
        case sol::type::userdata: {
            const int h = handle_of(value);
            if (h > 0) {
                args[key] = h;
            }
            return;
        }
        default:
            return;
    }
}

}  // namespace rimlua
