#include "engine.hpp"
#include "handle_util.hpp"
#include "json_lite.hpp"
#include "kits_data.hpp"

#include <string>
#include <unordered_set>
#include <vector>

namespace rimlua {

namespace {

// One argument of a kit function: "name", "name?", "name:type" or "name?:type". A table-typed argument is sent to the
// host as a JSON string, wrapped game objects inside it become {"$h": handle}.
struct KitArg {
    std::string name;
    bool is_table = false;
    bool is_function = false;  // a Lua function: registered as a callback, the host receives its id
    std::string type;          // declared type from the kit table, used to check arguments under rimkit mod test
    bool optional = false;
};

struct KitFn {
    std::string name;
    std::string ret_kind;      // pawn, thing, map, faction when the function returns a game object (or an array of them)
    bool ret_array = false;
    std::string op;
    bool subject = true;
    std::vector<KitArg> args;
};

struct KitDomain {
    std::string domain;
    std::vector<KitFn> fns;
};

KitArg parse_arg(const std::string& spec) {
    KitArg a;
    std::string name = spec;
    std::string type;
    const size_t colon = spec.find(':');
    if (colon != std::string::npos) {
        name = spec.substr(0, colon);
        type = spec.substr(colon + 1);
    }
    if (!name.empty() && name.back() == '?') {
        name.pop_back();
    }
    a.name = name;
    a.is_function = type == "function";
    a.is_table = type == "table" || type == "table[]" || type.rfind("{", 0) == 0;
    return a;
}

std::unordered_set<std::string>& known_ops() {
    static std::unordered_set<std::string> ops;
    return ops;
}

std::vector<KitDomain> load_kits() {
    std::vector<KitDomain> out;
    json::Value root;
    if (!json::parse(kits::kitsJson(), root) || !root.is_object()) {
        return out;
    }
    if (const json::Value* ops = root.find("ops")) {
        for (const json::Value& o : ops->items) {
            if (o.is_string()) {
                known_ops().insert(o.s);
            }
        }
    }
    const json::Value* list = root.find("kits");
    if (!list) {
        return out;
    }
    for (const json::Value& k : list->items) {
        KitDomain d;
        d.domain = k.get_string("domain");
        const std::string prefix = k.get_string("op");
        const bool default_subject = k.get_bool("subject", true);
        const json::Value* fns = k.find("fns");
        if (d.domain.empty() || prefix.empty() || !fns) {
            continue;
        }
        for (const json::Value& f : fns->items) {
            KitFn fn;
            fn.name = f.get_string("name");
            fn.op = f.get_string("op", prefix + "." + fn.name);
            fn.subject = f.get_bool("subject", default_subject);
            {
                std::string r = f.get_string("returns");
                if (r.size() > 1 && r.back() == '?') r.pop_back();
                if (r.size() > 2 && r.compare(r.size() - 2, 2, "[]") == 0) {
                    fn.ret_array = true;
                    r.resize(r.size() - 2);
                }
                if (r == "RimPawn") fn.ret_kind = "pawn";
                else if (r == "RimThing") fn.ret_kind = "thing";
                else if (r == "RimMap") fn.ret_kind = "map";
                else if (r == "RimFaction") fn.ret_kind = "faction";
                else fn.ret_array = false;
            }
            if (const json::Value* keys = f.find("keys")) {
                for (const json::Value& key : keys->items) {
                    if (key.is_string()) {
                        fn.args.push_back(parse_arg(key.s));
                    }
                }
            }
            if (const json::Value* params = f.find("params")) {
                size_t pi = fn.subject ? 1 : 0;
                for (size_t i = 0; i < fn.args.size() && pi + i < params->items.size(); ++i) {
                    const json::Value& p = params->items[pi + i];
                    fn.args[i].type = p.get_string("type");
                    fn.args[i].optional = p.get_bool("optional", false);
                }
            }
            if (!fn.name.empty()) {
                d.fns.push_back(std::move(fn));
            }
        }
        out.push_back(std::move(d));
    }
    return out;
}

// Under rimkit mod test a wrong argument type is an error, so a passing test cannot hide a call the game would reject.
const std::vector<KitDomain>& kit_table() {
    static const std::vector<KitDomain> table = load_kits();
    return table;
}

std::string check_arg_type(const KitArg& a, const sol::object& v) {
    if (v.get_type() == sol::type::lua_nil) {
        return a.optional ? std::string() : "argument " + a.name + " is required";
    }
    const std::string& t = a.type;
    const sol::type got = v.get_type();
    auto bad = [&](const char* want) { return std::string("argument ") + a.name + " must be " + want; };
    if ((t == "integer" || t == "number") && got != sol::type::number) return bad("a number");
    if (t == "string" && got != sol::type::string) return bad("a string");
    if (t == "boolean" && got != sol::type::boolean) return bad("a boolean");
    return std::string();
}

}  // namespace

bool Engine::is_known_op(const std::string& op) const {
    kit_table();
    return known_ops().empty() || known_ops().count(op) > 0;
}

// Binds every function in api/kits.json. The first Lua argument is the subject (pawn, thing, map, faction, ...) when
// the entry says so, the rest map to the named keys in order. Errors raise a Lua error with the RK code.
void Engine::bind_json_kits() {
    const std::vector<KitDomain>& table = kit_table();
    sol::table game = (*lua_)["game"];
    if (!thing_methods_.valid()) {
        thing_methods_ = lua_->create_table();
    }
    for (const KitDomain& d : table) {
        sol::object existing = game[d.domain];
        sol::table domain = existing.is<sol::table>() ? existing.as<sol::table>() : lua_->create_table();
        for (const KitFn& fn : d.fns) {
            const KitFn* spec = &fn;  // table is static, so the pointer stays valid
            // Things are objects: their subject functions are methods (thing:damage(5)), not game.things.damage(thing, 5).
            sol::table& target = (d.domain == "things" && fn.subject) ? thing_methods_ : domain;
            target[fn.name] = [this, spec](sol::variadic_args va) {
                sol::table a = lua_->create_table();
                size_t index = 0;
                size_t arg = 0;
                for (auto v : va) {
                    sol::object value = v;
                    if (spec->subject && index == 0) {
                        const int h = handle_of(value);
                        if (h <= 0) {
                            throw sol::error("RK2001: " + spec->op + " needs a game object or handle as its first argument");
                        }
                        a["h"] = h;
                    } else if (arg < spec->args.size()) {
                        const KitArg& ka = spec->args[arg];
                        if (test_mode_) {
                            const std::string why = check_arg_type(ka, value);
                            if (!why.empty()) throw sol::error("RK1001: " + spec->op + ": " + why);
                        }
                        if (ka.is_function) {
                            if (value.get_type() == sol::type::function) {
                                a[ka.name] = register_ui_callback(value.as<sol::protected_function>());
                            }
                        } else if (ka.is_table && value.get_type() == sol::type::table) {
                            a[ka.name] = lua_value_json(value);
                        } else {
                            put_arg(a, ka.name.c_str(), value);
                        }
                        ++arg;
                    } else if (test_mode_) {
                        throw sol::error("RK1001: " + spec->op + " takes " + std::to_string(spec->args.size()) + " argument(s) after the subject, got more");
                    }
                    ++index;
                }
                if (spec->subject && index == 0) {
                    throw sol::error("RK2001: " + spec->op + " needs a game object or handle as its first argument");
                }
                if (test_mode_) {
                    for (size_t missing = arg; missing < spec->args.size(); ++missing) {
                        if (!spec->args[missing].optional) {
                            throw sol::error("RK1001: " + spec->op + ": argument " + spec->args[missing].name + " is required");
                        }
                    }
                }
                sol::object result = host_call_checked(spec->op, a);
                if (spec->ret_kind.empty()) {
                    return result;
                }
                // Typed results: the host sends plain handles for these, the declared return type says what they are.
                auto wrap = [this, spec](const sol::object& o) -> sol::object {
                    if (o.get_type() != sol::type::number) return o;
                    const int h = static_cast<int>(o.as<double>());
                    if (h <= 0) return sol::make_object(*lua_, sol::lua_nil);
                    if (spec->ret_kind == "pawn") return sol::make_object(*lua_, wrap_pawn(h));
                    if (spec->ret_kind == "thing") return sol::make_object(*lua_, wrap_thing(h));
                    if (spec->ret_kind == "map") return sol::make_object(*lua_, wrap_map(h));
                    return sol::make_object(*lua_, wrap_faction(h));
                };
                if (spec->ret_array && result.is<sol::table>()) {
                    sol::table in = result.as<sol::table>();
                    sol::table out = lua_->create_table();
                    for (size_t i = 1; i <= in.size(); ++i) {
                        sol::object item = in[i];
                        out[i] = wrap(item);
                    }
                    return sol::make_object(*lua_, out);
                }
                return wrap(result);
            };
        }
        game[d.domain] = domain;
    }

    // game.effects also takes a thing or pawn as its anchor, and colour names: text(pawn, "12", "red").
    // fleck(thing, def, opts) and effecter(thing, def) work the same way. The (map, x, z, ...) form keeps working.
    lua_->safe_script(R"LUA(
local effects = game.effects
if type(effects) ~= "table" then return end
local colors = { red = "#ff4d4d", orange = "#ffa040", yellow = "#ffd84d", green = "#5fd35f", blue = "#5aa0ff",
  white = "#ffffff", black = "#000000", gray = "#999999", purple = "#b07cff", cyan = "#4de1e1" }
local function anchored(a)
  if type(a) ~= "userdata" then return false end
  local ok, flag = pcall(function() return a.is_pawn end)
  return ok and flag ~= nil
end
local function place(a)
  local m, p = a.map, a.position
  if m == nil or p == nil then return nil end
  return m, p.x, p.z
end
local text, fleck, effecter = effects.text, effects.fleck, effects.effecter
if text then
  effects.text = function(a, ...)
    if anchored(a) then
      local m, x, z = place(a)
      if not m then return false end
      local s, c = ...
      return text(m, x, z, s, c and colors[c] or c)
    end
    local x, z, s, c = ...
    if type(s) == "string" and type(c) == "string" and colors[c] then c = colors[c] end
    return text(a, x, z, s, c)
  end
end
if fleck then
  effects.fleck = function(a, ...)
    if anchored(a) then
      local def, opts = ...
      local m, x, z = place(a)
      if not m then return false end
      return fleck(m, def, x, z, opts)
    end
    return fleck(a, ...)
  end
end
if effecter then
  effects.effecter = function(a, ...)
    if anchored(a) then
      local def = ...
      local m, x, z = place(a)
      if not m then return false end
      return effecter(m, def, x, z)
    end
    return effecter(a, ...)
  end
end
)LUA");
}

}  // namespace rimlua
