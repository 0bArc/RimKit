--- RimKit Lua stubs (EmmyLua / LuaLS). Author: Team Stratware.win
--- Bound rim.* tables are the normal API. rim.invoke is escape-only (api.list / reflect.*).

---@class RimFaction
---@field handle integer
---@field name string
local RimFaction = {}
---@param other RimFaction
---@return boolean
function RimFaction:is_hostile(other) end
---@param other RimFaction
---@param kind string
function RimFaction:set_relation(other, kind) end

---@class RimPawn
---@field handle integer
---@field name string
---@field health number
---@field hunger number
---@field is_colonist boolean
---@field is_humanlike boolean
---@field map RimMap|nil
---@field faction RimFaction|nil
---@field position RimCell|nil
---@field is_moving boolean
local RimPawn = {}
---@param def string
---@param stack integer|nil
function RimPawn:give_item(def, stack) end
---@param name string
function RimPawn:set_name(name) end
---@param trait string
function RimPawn:add_trait(trait) end
---@param trait string
function RimPawn:remove_trait(trait) end
---@param def string
---@param severity number|nil
function RimPawn:give_hediff(def, severity) end
function RimPawn:kill() end
---@param drafted boolean|nil
function RimPawn:draft(drafted) end
function RimPawn:seek_medical_help() end
---@param x integer
---@param z integer
---@return boolean
function RimPawn:can_reach(x, z) end
---@param x integer
---@param z integer
---@param sprint boolean|nil
---@return boolean
function RimPawn:walk_to(x, z, sprint) end
---@param radius integer|nil
---@return boolean
function RimPawn:wander(radius) end
function RimPawn:stop() end
---@param job_name string
---@return boolean
function RimPawn:start_job(job_name) end
---@param skill string
---@return boolean
function RimPawn:has_skill(skill) end

---@class RimCell
---@field x integer
---@field z integer

---@class RimMap
---@field handle integer
---@field nutrition number
---@field width integer
---@field height integer
local RimMap = {}
---@param def string
---@param x integer
---@param z integer
---@param stack integer|nil
---@return RimThing|nil
function RimMap:spawn_thing(def, x, z, stack) end
---@param opts {terrain:string|nil, limit:integer|nil}|nil
---@return RimCell[]
function RimMap:find_cells(opts) end
---@return RimPawn[]
function RimMap:colonists() end

---@class RimThing
---@field handle integer
---@field def string
---@field label string
local RimThing = {}
function RimThing:destroy() end

events = {}
---@param name string
---@param fn fun(pawn:RimPawn)
function events.on(name, fn) end
---@param name string
function events.off(name) end

log = {}
---@param msg string
function log.info(msg) end
---@param msg string
function log.error(msg) end

game = {}
---@return RimMap|nil
function game.current_map() end
---@return integer
function game.tick() end
---@return RimFaction|nil
function game.player_faction() end

player = {}
---@param text string
function player.send_message(text) end

timer = {}
---@param ticks integer
---@param fn fun()
function timer.after(ticks, fn) end

jobs = {}
---@param name string
---@param spec {can_do: (fun(pawn:RimPawn):boolean)|nil, execute: fun(pawn:RimPawn):boolean}
function jobs.register(name, spec) end
---@param pawn RimPawn
---@param name string
---@return boolean
function jobs.start(pawn, name) end

path = {}
---@param pawn RimPawn
---@param x integer
---@param z integer
---@return boolean
function path.can_reach(pawn, x, z) end
---@param pawn RimPawn
---@param x integer
---@param z integer
---@param sprint boolean|nil
---@return boolean
function path.walk(pawn, x, z, sprint) end
---@param pawn RimPawn
---@param radius integer|nil
---@return boolean
function path.wander(pawn, radius) end
---@param pawn RimPawn
---@param x integer
---@param z integer
---@return RimCell[]
function path.compute(pawn, x, z) end
---@param pawn RimPawn
function path.stop(pawn) end

ui = {}
---@param text string
function ui.message(text) end
---@param label string
---@param text string
function ui.letter(label, text) end
---@param spec {title:string|nil, body:string|nil, buttons:table}
function ui.window(spec) end
---@param options table
function ui.float_menu(options) end
---@param spec {title:string|nil, body:string|nil, checks:string[]|nil, list:string[]|nil}
function ui.panel(spec) end
--- Right-click map menus. Humans recruit/capture; entities are commandable only.
---@param fn fun(ctx:{clicked:integer, hauler:integer}): table[]|nil
function ui.on_map_float_menu(fn) end

config = {}
---@param key string
---@param spec table
function config.register(key, spec) end
---@param key string
---@return string
function config.get(key) end
---@param key string
---@return boolean
function config.get_bool(key) end
---@param key string
---@param value any
function config.set(key, value) end

defs = {}
---@param spec table
function defs.register_thing(spec) end
---@param typeName string
---@param name string
---@return boolean
function defs.exists(typeName, name) end

input = {}
---@param def string
---@return boolean
function input.binding_just_pressed(def) end

---@type RimPawn|nil
selected_pawn = nil

---@param pawn RimPawn
function on_pawn_spawned(pawn) end
---@param pawn RimPawn
function on_pawn_died(pawn) end

rim = {}
---@param msg string
function rim.log(msg) end
---@param msg string
function rim.message(msg) end
---@param fn fun()
function rim.on_load(fn) end
---@param fn fun()
function rim.on_tick(fn) end
---@param h integer
---@return RimPawn
function rim.wrap(h) end
---@param h integer
---@return RimMap
function rim.wrap_map(h) end
---@param h integer
---@return RimThing
function rim.wrap_thing(h) end
---@param h integer
---@return RimFaction
function rim.wrap_faction(h) end

rim.prefix = {}
rim.prefixes = rim.prefix
rim.postfix = {}
rim.postfixes = rim.postfix
rim.events = { prefix = {}, postfix = {} }
rim.hooks = {}
---@param typeName string
---@param methodName string
---@param fn fun(pawn:RimPawn):boolean
function rim.hooks.prefix(typeName, methodName, fn) end

rim.pawn = {}
---@param h integer
---@return boolean
function rim.pawn.is_humanlike(h) end
---@param h integer
---@return boolean
function rim.pawn.faction_is_player(h) end
---@param h integer
---@return number
function rim.pawn.hunger(h) end
---@param h integer
---@return integer
function rim.pawn.map(h) end
---@param h integer
---@return string
function rim.pawn.name(h) end

rim.map = {}
---@param h integer
---@return number
function rim.map.nutrition(h) end

rim.find = {}
---@return integer
function rim.find.current_map() end
---@return integer
function rim.find.selected() end
---@return integer
function rim.find.tick() end

---@param op string Escape only: "api.list" or "reflect.<suffix>" (case-sensitive; no whitespace)
---@param args table|nil
---@return any
function rim.invoke(op, args) end

rim.reflect = {}
---@param h integer
---@param member string
---@return any
function rim.reflect.get(h, member) end
---@param h integer
---@param method string
---@param args string|nil
---@return any
function rim.reflect.call(h, method, args) end

-- Strong API domains (Wave 0-3). Prefer these tables; do not rim.invoke gameplay ops.

data = {}
---@param package_id string
---@param key string
---@return string|nil
function data.get(package_id, key) end
---@param package_id string
---@param key string
---@param value string
function data.set(package_id, key, value) end
---@param package_id string
---@param key string
function data.remove(package_id, key) end
---@param package_id string
---@return string[]
function data.keys(package_id) end

health = {}
---@param h integer
---@param defName string
---@return boolean
function health.has_hediff(h, defName) end
---@param h integer
---@param defName string
---@return number
function health.hediff_severity(h, defName) end
---@param h integer
---@param defName string
---@param severity number
function health.set_hediff_severity(h, defName, severity) end
---@param h integer
---@param quality number|nil
function health.tend(h, quality) end

surgery = {}
---@param h integer
---@param recipeDefName string
---@return boolean
function surgery.queue_operation(h, recipeDefName) end

---@param h integer
---@return boolean
function rim.pawn.make_controllable(h) end
---@param h integer
---@return boolean
function rim.pawn.release_control(h) end
---@param h integer
---@return boolean
function rim.pawn.is_controllable(h) end

--- Free-function mirror of rim.pawn (handle APIs).
pawn = rim.pawn

anomaly = {}
---@return boolean
function anomaly.dlc_active() end
---@param h integer
---@return boolean
function anomaly.is_entity(h) end
---@param mapHandle integer|nil
---@return integer[]
function anomaly.list_on_map(mapHandle) end
---@param h integer
---@param severity number|nil
function anomaly.knock_out(h, severity) end
---@param hauler_h integer
---@param entity_h integer|nil
---@return integer
function anomaly.find_platform(hauler_h, entity_h) end
---@param hauler_h integer
---@param entity_h integer
---@param platform_h integer|nil
---@return boolean
function anomaly.start_capture(hauler_h, entity_h, platform_h) end
--- Same as pawn.make_controllable. Draft/move like an animal.
---@param h integer
---@return boolean
function anomaly.recruit(h) end
---@param h integer
---@return boolean
function anomaly.try_set_faction_player(h) end
---@param h integer
---@return boolean
function anomaly.release_to_hostile(h) end

util = {}
---@param target "ModRoot"|"Saves"|"PlayerLog"|"RimKit"
---@param package_id string|nil
function util.open_folder(target, package_id) end
---@param package_id string
---@param file string
---@param content string
function util.write_export(package_id, file, content) end

building = {}
---@param h integer
---@return boolean
function building.power_on(h) end
---@param h integer
---@param on boolean
function building.set_power(h, on) end
---@param h integer
---@param on boolean
function building.flick(h, on) end

work = {}
---@param h integer
---@param workType string
---@return integer
function work.get_priority(h, workType) end
---@param h integer
---@param workType string
---@param priority integer
function work.set_priority(h, workType, priority) end
---@return string[]
function work.list_types() end

world_api = {}
---@param map integer|nil
---@return string
function world_api.weather(map) end
---@param def string
---@param map integer|nil
function world_api.set_weather(def, map) end

incident = {}
---@param def string
---@param map integer|nil
---@return boolean
function incident.try_fire(def, map) end
---@return string[]
function incident.list() end

audio = {}
---@param soundDef string
function audio.play(soundDef) end
