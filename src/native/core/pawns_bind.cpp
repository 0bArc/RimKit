#include "engine.hpp"
#include "version_data.hpp"
#include "tweaks_data.hpp"
#include "handle_util.hpp"

#include <string>
#include <vector>

namespace rimlua {

namespace rj = rimlua::json;

// Host call that raises a Lua error on failure. The message starts with an RK code (docs/standard/rks.md).
sol::object Engine::host_call_checked(const std::string& op, const sol::table& args) {
    if (mock_active_) {
        sol::object mocked;
        if (try_mock(op, args, mocked)) {
            return mocked;
        }
    }
    check_capability_for_op(op);
    const std::string json = request_json(op, args);
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
}


// Lua helpers that sit on top of kit functions.
void Engine::bind_kit_helpers() {
    (*lua_)["__rk_tweaks_json"] = std::string(tweaks::tweaksJson());
    sol::load_result lr = lua_->load(R"LUA(
local save = game.save
if type(save) == "table" and type(game.json) == "table" then
  -- game.save.put(package_id, scope, key, value [, map]): stores any Lua table, string, number or boolean.
  function save.put(package_id, scope, key, value, map)
    return save.set(package_id, scope, key, game.json.encode(value), map)
  end
  -- game.save.fetch(package_id, scope, key [, map [, default]]): the stored value, or default.
  function save.fetch(package_id, scope, key, map, default)
    local raw = save.get(package_id, scope, key, map)
    if raw == nil or raw == "" then return default end
    local ok, value = pcall(game.json.decode, raw)
    if ok then return value end
    return default
  end
end

-- game.classes.define(family, name, methods): registers a Lua class. Every function in methods becomes one override of the
-- generated proxy for the family (see game.classes.families). The functions get the game objects as positional arguments.
local classes = game.classes
if type(classes) == "table" and type(classes.register_fn) == "function" then
  function classes.define(family, name, methods)
    if type(methods) ~= "table" then error("RK1001: game.classes.define needs a table of functions") end
    local n = 0
    for fname, fn in pairs(methods) do
      if type(fn) == "function" then
        -- The host sends { n = count, a = { args } } so nil arguments keep their place.
        classes.register_fn(family, name, fname, function(packed)
          return fn(table.unpack(packed.a, 1, packed.n))
        end)
        n = n + 1
      end
    end
    return n
  end
end

-- game.hooks.in_mod(package_id, kind, type, method, fn [, opts]): hooks a method of another mod, and does nothing (returns nil)
-- when that mod is not active or the type is not in its assemblies.
local mods = game.mods
if type(mods) == "table" and type(game.hooks) == "table" then
  function game.hooks.in_mod(package_id, kind, type_name, method, fn, opts)
    if not mods.active(package_id) then return nil end
    if not mods.type_exists(package_id, type_name) then return nil end
    return game.hooks[kind](type_name, method, fn, opts)
  end
end

-- game.tweaks: named places to change a value, from the tweak catalog (src/api/tweaks.json).
if type(game.json) == "table" and type(game.hooks) == "table" then
  local ok, catalog = pcall(game.json.decode, __rk_tweaks_json or "{}")
  __rk_tweaks_json = nil
  local byName = {}
  local rows = ok and catalog.tweaks or {}
  for _, row in ipairs(rows) do byName[row.name] = row end
  local tweaks = { _installed = {} }
  game.tweaks = tweaks
  -- game.tweaks.list(): every tweak point as { name, type, method, result, doc }.
  function tweaks.list()
    local out = {}
    for _, row in ipairs(rows) do out[#out + 1] = { name = row.name, type = row.type, method = row.method, result = row.result, doc = row.doc } end
    return out
  end
  -- game.tweaks.on(name, fn [, opts]): fn({ value, instance, args }) returns the new value, or nil to keep it. Returns a hook id.
  function tweaks.on(name, fn, opts)
    local row = byName[name]
    if not row then error("RK3001: unknown tweak " .. tostring(name) .. ", see game.tweaks.list()") end
    if type(fn) ~= "function" then error("RK1001: game.tweaks.on needs a function") end
    local id = game.hooks.postfix(row.type, row.method, function(ctx)
      local r = fn({ value = ctx.result, instance = ctx.instance, args = ctx.args })
      if r ~= nil then ctx:set_result(r) end
    end, opts)
    tweaks._installed[#tweaks._installed + 1] = { name = name, id = id }
    return id
  end
  -- game.tweaks.off(id): removes a tweak.
  function tweaks.off(id) return game.hooks.remove(id) end
end
)LUA");
    if (lr.valid()) {
        sol::protected_function fn = lr;
        fn();
    }
}

// game.time constants. The functions (now, speed, set_speed, ...) come from api/kits.json.
void Engine::bind_time_kit() {
    sol::table game = (*lua_)["game"];
    sol::object existing = game["time"];
    sol::table time = existing.is<sol::table>() ? existing.as<sol::table>() : lua_->create_table();
    time["ticks_per_hour"] = 2500;
    time["ticks_per_day"] = 60000;
    time["ticks_per_quadrum"] = 900000;
    time["ticks_per_year"] = 3600000;
    game["time"] = time;
}

// game.version: which RimWorld and which RimKit a mod runs on, for version-aware code.
//   game.version.rimworld()         -> { major, minor, build, text }
//   game.version.at_least("1.6")    -> true when the running game is 1.6 or newer
//   game.version.rimkit(), game.version.api_level()
void Engine::bind_version_helpers() {
    sol::table game = (*lua_)["game"];
    sol::table ver = lua_->create_table();
    ver["rimworld"] = [this]() { return host_call("util.game_version", lua_->create_table()); };
    ver["rimkit"] = []() { return std::string(RIMKIT_VERSION); };
    ver["api_level"] = []() { return RIMKIT_API_LEVEL; };
    game["version"] = ver;
    sol::load_result lr = lua_->load(R"LUA(
local ver = ...
function ver.at_least(spec)
  local major, minor = tostring(spec):match("^(%d+)%.?(%d*)")
  if not major then error("RK1001: version must look like 1.6") end
  local now = ver.rimworld()
  if type(now) ~= "table" then return false end
  major, minor = tonumber(major), tonumber(minor) or 0
  return now.major > major or (now.major == major and now.minor >= minor)
end

-- game.version.hook(kind, targets, fn[, opts]): installs the hook for the game version that is running.
-- targets maps a minimum game version to a { type, method, sig } target. The entry with the highest minimum
-- that the running game satisfies is used, and nothing is installed (nil) when none does.
function ver.hook(kind, targets, fn, opts)
  local function key(spec)
    local major, minor = tostring(spec):match("^(%d+)%.?(%d*)")
    return (tonumber(major) or 0) * 1000 + (tonumber(minor) or 0)
  end
  local best, bestKey
  for spec, target in pairs(targets or {}) do
    local k = key(spec)
    if ver.at_least(spec) and (bestKey == nil or k > bestKey) then best, bestKey = target, k end
  end
  if not best then return nil end
  local merged = {}
  for k, v in pairs(best) do if k ~= "type" and k ~= "method" then merged[k] = v end end
  for k, v in pairs(opts or {}) do merged[k] = v end
  if next(merged) == nil then merged = nil end
  return game.hooks[kind](best.type, best.method, fn, merged)
end
)LUA");
    if (lr.valid()) {
        sol::protected_function fn = lr;
        fn(ver);
    }
}

// game.ui.translate. The pawn kit functions (skills, needs, traits, ...) come from api/kits.json.
void Engine::bind_pawns_kit() {
    sol::table game = (*lua_)["game"];
    // game.ui.translate(key, ...): Keyed language strings with {0}, {1}, ... arguments. Unknown keys return the key.
    sol::object ui_existing = game["ui"];
    sol::table ui = ui_existing.is<sol::table>() ? ui_existing.as<sol::table>() : lua_->create_table();
    ui["translate"] = [this](const std::string& key, sol::variadic_args va) {
        sol::table a = lua_->create_table();
        a["key"] = key;
        size_t i = 0;
        for (auto v : va) {
            if (i >= 4) {
                break;
            }
            sol::object value = v;
            std::string name = "a" + std::to_string(i);
            if (value.get_type() == sol::type::number) {
                a[name] = std::to_string(value.as<double>());
            } else if (value.is<std::string>()) {
                a[name] = value.as<std::string>();
            } else {
                a[name] = std::string();
            }
            ++i;
        }
        sol::object r = host_call("ui.translate", a);
        return r.is<std::string>() ? r.as<std::string>() : key;
    };
    game["ui"] = ui;
    // game.ui.say(key, ...): translate a Keyed string, then show it as a message. A text that is not a key shows as it is.
    lua_->safe_script("game.ui.say = function(key, ...) return game.ui.message(game.ui.translate(key, ...)) end");

    // game.json.encode(value) and game.json.decode(text). Tables, strings, numbers and booleans only. Game objects encode as handles.
    sol::table json = lua_->create_table();
    json["encode"] = [this](sol::object value) { return lua_value_json(value); };
    json["decode"] = [this](const std::string& text) -> sol::object {
        rj::Value v;
        if (!rj::parse(text, v)) {
            throw sol::error("RK1001: game.json.decode needs valid JSON");
        }
        return json_to_lua(v);
    };
    game["json"] = json;
}

}  // namespace rimlua
