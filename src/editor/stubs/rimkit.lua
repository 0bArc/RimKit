--- RimKit Lua stubs (EmmyLua / LuaLS). Author: Team Stratware.win
--- Bound rim.* tables are the normal API. rim.invoke is escape-only (api.list / reflect.*).
--- Stability: docs/api/stability.md  Kits: docs/api/domains.md

--[[@stable]]
---@class RimFaction
---@field handle integer Integer handle of the faction
---@field name string Name of the faction
local RimFaction = {}
---@param other RimFaction
---@return boolean
function RimFaction:is_hostile(other) end
---@param other RimFaction
---@param kind string
function RimFaction:set_relation(other, kind) end

--[[@stable]]
---@class RimPawn
---@field handle integer Integer handle of the pawn
---@field name string The pawn's name
---@field health number Health from 0 to 1
---@field hunger number Food level from 0 (starving) to 1 (full)
---@field is_colonist boolean Whether the pawn is a colonist of the player
---@field is_humanlike boolean Whether the pawn is humanlike, not an animal or a mech
---@field map RimMap|nil The map the pawn is on, nil when not on a map
---@field faction RimFaction|nil The pawn's faction, nil when it has none
---@field position RimCell|nil The cell the pawn stands in, nil when not spawned
---@field is_moving boolean Whether the pawn is walking somewhere
---@field skills RimSkill[]|nil Skills with level, passion and xp. nil when the pawn has none.
---@field needs RimNeed[]|nil Needs with their level, nil when the pawn has none
---@field traits RimTrait[]|nil Traits with degree, nil when the pawn has none
---@field thoughts RimThought[]|nil Current mood thoughts.
---@field relations RimRelation[]|nil Direct relations with other pawns
---@field capacities RimCapacity[]|nil Capacity levels such as Moving and Consciousness
local RimPawn = {}
---@param def string
---@param stack integer|nil
function RimPawn:give_item(def, stack) end
---@param name string
function RimPawn:set_name(name) end
---@param trait string
---@param degree integer|nil Trait degree, for example Beauty 2. Default 0.
---@return boolean
function RimPawn:add_trait(trait, degree) end
---@param trait string
---@param degree integer|nil
---@return boolean
function RimPawn:has_trait(trait, degree) end
---@param skill string
---@return RimSkill
function RimPawn:skill_info(skill) end
---@param skill string
---@param passion "None"|"Minor"|"Major"
---@return boolean
function RimPawn:set_passion(skill, passion) end
---@param skill string
---@param amount number
---@return integer level
function RimPawn:add_xp(skill, amount) end
---@param def string
---@param other RimPawn|nil
---@return boolean
function RimPawn:add_thought(def, other) end
---@param def string
---@return boolean
function RimPawn:remove_thought(def) end
---@param other RimPawn
---@return integer
function RimPawn:opinion_of(other) end
function RimPawn:seek_medical() end
---@param trait string
function RimPawn:remove_trait(trait) end
---@param def string
---@param severity number|nil
function RimPawn:give_hediff(def, severity) end
function RimPawn:kill() end
---@param drafted boolean|nil
function RimPawn:draft(drafted) end
---@deprecated Use RimPawn:seek_medical()
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

--[[@experimental]]
---@class RimEntity
---@field handle integer Integer handle of the entity
---@field name string Name of the entity
---@field kind string Kind of entity, for example Revenant
local RimEntity = {}
---@return boolean
function RimEntity:is_entity() end
---@return boolean
function RimEntity:recruit() end
---@param severity number|nil
function RimEntity:knock_out(severity) end
---@return boolean
function RimEntity:release() end

--[[@experimental]]
---@class RimAnomalies
local RimAnomalies = {}
---@param name string
---@param map RimMap|nil
---@return RimEntity|nil
function RimAnomalies:get(name, map) end
---@param map RimMap|nil
---@return RimEntity[]
function RimAnomalies:list(map) end
---@param predicate fun(entity:RimEntity):boolean
---@param map RimMap|nil
---@return RimEntity|nil
function RimAnomalies:find(predicate, map) end

---@class RimCell
---@field x integer Cell x coordinate
---@field z integer Cell z coordinate

---@class RimMap
---@field handle integer Integer handle of the map
---@field nutrition number Total nutrition of food items on the map
---@field width integer Width in cells
---@field height integer Height in cells
local RimMap = {}
---@param def string
---@param x integer
---@param z integer
---@param stack integer|nil
---@return RimThing|nil
function RimMap:spawn(def, x, z, stack) end
---@deprecated Use RimMap:spawn
---@param def string
---@param x integer
---@param z integer
---@param stack integer|nil
---@return RimThing|nil
function RimMap:spawn_thing(def, x, z, stack) end
---@param kind string PawnKindDef name
---@param faction string|nil
---@param x integer|nil
---@param z integer|nil
---@return RimPawn|nil
function RimMap:spawn_pawn(kind, faction, x, z) end
---@param opts {terrain:string|nil, limit:integer|nil}|nil
---@return RimCell[]
function RimMap:find_cells(opts) end
---@return RimPawn[]
function RimMap:colonists() end

---@class RimThing
---@field handle integer Integer handle of the thing
---@field def string Def name of the thing
---@field label string Name shown to the player
local RimThing = {}
function RimThing:destroy() end

---@class RimObject
---@field handle integer Integer handle of the object
---@field type_name string C# type name of the object
---@field def string defName when the object has one (a hediff, job, quest), else an empty string

---@class RimDamageInfo
---@field def string Damage def name, for example Cut
---@field amount number Damage before armour
---@field angle number Direction of the hit in degrees
---@field instigator RimThing|RimPawn|nil Who or what caused it
---@field weapon string|nil Def name of the weapon
---@field hit_part string|nil Body part that was hit

---@class RimEventInfo
---@field name string Event name, for example pawn.died
---@field description string What the event means and what its payload holds
---@field hot boolean Whether it fires very often and costs time to listen to
---@field installed boolean Whether the game patch for it is installed

events = {}
--[[@experimental]]
---Canonical names ("pawn.died") receive one payload table and install their Harmony patch on first use.
---Legacy names ("pawn_died") receive a RimPawn.
---@param name string
---@param fn fun(payload:table|RimPawn)
function events.on(name, fn) end
---@param name string
function events.off(name) end
--[[@experimental]]
---@return RimEventInfo[]
function events.list() end

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
--[[@experimental]]
---@type RimAnomalies
game.anomalies = {}

--[[@stable]]
---@class RimKitModule
---@field version string RimKit version, for example 0.10.0
---@field api_level integer API level of the running RimKit
---@field stable table Names of the stable functions
---@field experimental table Names of the experimental functions
local RimKitModule = {}
---@param min_level integer
---@return boolean
function RimKitModule.assert_api(min_level) end

--- In-game: `local rk = require("rimkit")` (package.preload; api_level 0).
---@type RimKitModule
rimkit = nil

player = {}
---@param text string
---@deprecated Use game.ui.message (works until 1.0.0). Run: rimkit migrate --write
function player.send_message(text) end

timer = {}
---@param ticks integer
---@param fn fun()
function timer.after(ticks, fn) end

jobs = {}
---@param name string
---@param spec {can_do: (fun(pawn:RimPawn):boolean)|nil, execute: fun(pawn:RimPawn):boolean}
---@deprecated Use game.jobs.register (works until 1.0.0). Run: rimkit migrate --write
function jobs.register(name, spec) end
---@param pawn RimPawn
---@param name string
---@return boolean
---@deprecated Use game.jobs.start (works until 1.0.0). Run: rimkit migrate --write
function jobs.start(pawn, name) end

path = {}
---@param pawn RimPawn
---@param x integer
---@param z integer
---@return boolean
---@deprecated Use game.paths.can_reach (works until 1.0.0). Run: rimkit migrate --write
function path.can_reach(pawn, x, z) end
---@param pawn RimPawn
---@param x integer
---@param z integer
---@param sprint boolean|nil
---@return boolean
---@deprecated Use game.paths.walk (works until 1.0.0). Run: rimkit migrate --write
function path.walk(pawn, x, z, sprint) end
---@param pawn RimPawn
---@param radius integer|nil
---@return boolean
---@deprecated Use game.paths.wander (works until 1.0.0). Run: rimkit migrate --write
function path.wander(pawn, radius) end
---@param pawn RimPawn
---@param x integer
---@param z integer
---@return RimCell[]
---@deprecated Use game.paths.compute (works until 1.0.0). Run: rimkit migrate --write
function path.compute(pawn, x, z) end
---@param pawn RimPawn
---@deprecated Use game.paths.stop (works until 1.0.0). Run: rimkit migrate --write
function path.stop(pawn) end

ui = {}
---@param text string
---@deprecated Use game.ui.message (works until 1.0.0). Run: rimkit migrate --write
function ui.message(text) end
---@param label string
---@param text string
---@deprecated Use game.ui.letter (works until 1.0.0). Run: rimkit migrate --write
function ui.letter(label, text) end
---@param spec {title:string|nil, body:string|nil, buttons:table}
---@deprecated Use game.ui.window (works until 1.0.0). Run: rimkit migrate --write
function ui.window(spec) end
---@param options table
---@deprecated Use game.ui.float_menu (works until 1.0.0). Run: rimkit migrate --write
function ui.float_menu(options) end
---@param spec {title:string|nil, body:string|nil, checks:string[]|nil, list:string[]|nil}
---@deprecated Use game.ui.panel (works until 1.0.0). Run: rimkit migrate --write
function ui.panel(spec) end
--- Right-click map menus. Humans recruit/capture; entities are commandable only.
---@param fn fun(ctx:{clicked:integer, hauler:integer}): table[]|nil
---@deprecated Use game.ui.on_map_float_menu (works until 1.0.0). Run: rimkit migrate --write
function ui.on_map_float_menu(fn) end

config = {}
---@param key string
---@param spec table
---@deprecated Use game.config.register (works until 1.0.0). Run: rimkit migrate --write
function config.register(key, spec) end
---@param key string
---@return string
---@deprecated Use game.config.get (works until 1.0.0). Run: rimkit migrate --write
function config.get(key) end
---@param key string
---@return boolean
---@deprecated Use game.config.get_bool (works until 1.0.0). Run: rimkit migrate --write
function config.get_bool(key) end
---@param key string
---@param value any
---@deprecated Use game.config.set (works until 1.0.0). Run: rimkit migrate --write
function config.set(key, value) end

defs = {}
---@param spec table
---@deprecated Use game.defs.register_thing (works until 1.0.0). Run: rimkit migrate --write
function defs.register_thing(spec) end
---@param typeName string
---@param name string
---@return boolean
---@deprecated Use game.defs.exists (works until 1.0.0). Run: rimkit migrate --write
function defs.exists(typeName, name) end

input = {}
---@param def string
---@return boolean
---@deprecated Use game.input.binding_just_pressed (works until 1.0.0). Run: rimkit migrate --write
function input.binding_just_pressed(def) end

---@type RimPawn|nil
selected_pawn = nil

---@param pawn RimPawn
function on_pawn_spawned(pawn) end
---@param pawn RimPawn
function on_pawn_died(pawn) end

rim = {}
---@param msg string
---@deprecated Use game.log.info (works until 1.0.0). Run: rimkit migrate --write
function rim.log(msg) end
---@param msg string
---@deprecated Use game.ui.message (works until 1.0.0). Run: rimkit migrate --write
function rim.message(msg) end
---@param fn fun()
---@deprecated Use game.events.on_load (works until 1.0.0). Run: rimkit migrate --write
function rim.on_load(fn) end
---@param fn fun()
---@deprecated Use game.events.on_tick (works until 1.0.0). Run: rimkit migrate --write
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
---@param h integer
---@return RimEntity
function rim.wrap_entity(h) end

--[[@experimental]]
---@class RimHookCtx
---@field method string Full name of the patched method.
---@field phase "prefix"|"postfix"|"finalizer"|"replace_call" Which part of the call this is
---@field pawn RimPawn|nil The pawn the call is about, when there is one
---@field instance RimPawn|RimThing|RimObject|nil The object the method is called on
---@field args any[] Arguments, 1 based.
---@field named table<string, any> Arguments by parameter name.
---@field arg_names string[] Parameter names, in order
---@field arg_modes ("in"|"ref"|"out")[] For each parameter: in, ref or out
---@field has_result boolean Whether the method returns a value
---@field result any Return value of the method (absent for void).
---@field exception string|nil Finalizer only.
---@field state any Value stored with set_state in an earlier phase of this call.
local RimHookCtx = {}
---Sets the return value. In a prefix this also skips the original.
---@param value any
function RimHookCtx:set_result(value) end
---Prefix only. Skips the original, optionally with a return value.
---@param value any|nil
function RimHookCtx:skip(value) end
---Replaces argument i (1 based). Writes ref and out arguments in a postfix.
---@param index integer
---@param value any
function RimHookCtx:set_arg(index, value) end
---@param value any
function RimHookCtx:set_state(value) end
---Finalizer only. Swallows the exception.
function RimHookCtx:suppress() end

---@class RimHookOptions
---@field sig string[]|nil Parameter type names selecting one overload ("int", "Verse.DamageInfo", "System.Int32&").
---@field priority integer|nil Harmony priority, higher runs first. Default 400.
---@field before string[]|nil Mod ids whose hooks this one must run before
---@field after string[]|nil Mod ids whose hooks this one must run after

---@class RimHookPatch : RimHookOptions
---@field type string Full name of the type to patch
---@field method string Name of the method to patch
---@field prefix fun(ctx:RimHookCtx):boolean|nil Runs before the method, return false to skip it
---@field postfix fun(ctx:RimHookCtx) Runs after the method
---@field finalizer fun(ctx:RimHookCtx) Runs after the method even when it threw

---@class RimHookIds
---@field prefix integer|nil Id of the prefix hook, to pass to game.hooks.remove
---@field postfix integer|nil Id of the postfix hook
---@field finalizer integer|nil Id of the finalizer hook

---@class RimHookCallReplace : RimHookOptions
---@field type string Method whose body is rewritten.
---@field method string Name of the method whose body is rewritten
---@field call string "Type.Method" of the call to replace.
---@field call_sig string[]|nil Parameter type names of the call to replace
---@field nth integer|nil Replace only the nth match. 0 or nil replaces all.
---@field fn fun(ctx:RimHookCtx):any Return nil to run the original call.

rim.prefix = {}
rim.prefixes = rim.prefix
rim.postfix = {}
rim.postfixes = rim.postfix
rim.events = { prefix = {}, postfix = {} }
rim.hooks = {}
rim.harmony = rim.hooks
---@param typeName string
---@param methodName string
---@param fn fun(ctx:RimHookCtx):boolean|nil
---@param opts RimHookOptions|nil
---@return integer hookId
---@deprecated Use game.hooks.prefix (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.prefix(typeName, methodName, fn, opts) end
---@param typeName string
---@param methodName string
---@param fn fun(ctx:RimHookCtx)
---@param opts RimHookOptions|nil
---@return integer hookId
---@deprecated Use game.hooks.postfix (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.postfix(typeName, methodName, fn, opts) end
---@param typeName string
---@param methodName string
---@param fn fun(ctx:RimHookCtx)
---@param opts RimHookOptions|nil
---@return integer hookId
---@deprecated Use game.hooks.finalizer (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.finalizer(typeName, methodName, fn, opts) end
---@param spec RimHookPatch
---@return RimHookIds
---@deprecated Use game.hooks.patch (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.patch(spec) end
---@param spec RimHookCallReplace
---@return integer|nil hookId
---@deprecated Use game.hooks.replace_call (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.replace_call(spec) end
---@param hookId integer
---@deprecated Use game.hooks.remove (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.remove(hookId) end
---@return { id: integer, kind: string, type: string, method: string }[]
---@deprecated Use game.hooks.list (works until 1.0.0). Run: rimkit migrate --write
function rim.hooks.list() end

rim.pawn = {}
---@param h integer
---@return boolean
---@deprecated Use game.pawns.is_humanlike (works until 1.0.0). Run: rimkit migrate --write
function rim.pawn.is_humanlike(h) end
---@param h integer
---@return boolean
---@deprecated Use game.pawns.faction_is_player (works until 1.0.0). Run: rimkit migrate --write
function rim.pawn.faction_is_player(h) end
---@param h integer
---@return number
---@deprecated Use game.pawns.hunger (works until 1.0.0). Run: rimkit migrate --write
function rim.pawn.hunger(h) end
---@param h integer
---@return integer
---@deprecated Use game.pawns.map (works until 1.0.0). Run: rimkit migrate --write
function rim.pawn.map(h) end
---@param h integer
---@return string
---@deprecated Use game.pawns.name (works until 1.0.0). Run: rimkit migrate --write
function rim.pawn.name(h) end

rim.map = {}
---@param h integer
---@return number
---@deprecated Use game.maps.nutrition (works until 1.0.0). Run: rimkit migrate --write
function rim.map.nutrition(h) end

rim.find = {}
---@return integer
---@deprecated Use game.maps.current (works until 1.0.0). Run: rimkit migrate --write
function rim.find.current_map() end
---@return integer
---@deprecated Use game.selection.first (works until 1.0.0). Run: rimkit migrate --write
function rim.find.selected() end
---@return integer
---@deprecated Use game.time.ticks (works until 1.0.0). Run: rimkit migrate --write
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
---@deprecated Use game.data.get (works until 1.0.0). Run: rimkit migrate --write
function data.get(package_id, key) end
---@param package_id string
---@param key string
---@param value string
---@deprecated Use game.data.set (works until 1.0.0). Run: rimkit migrate --write
function data.set(package_id, key, value) end
---@param package_id string
---@param key string
---@deprecated Use game.data.remove (works until 1.0.0). Run: rimkit migrate --write
function data.remove(package_id, key) end
---@param package_id string
---@return string[]
---@deprecated Use game.data.keys (works until 1.0.0). Run: rimkit migrate --write
function data.keys(package_id) end

health = {}
---@param h integer
---@param defName string
---@return boolean
---@deprecated Use game.pawns.has_hediff (works until 1.0.0). Run: rimkit migrate --write
function health.has_hediff(h, defName) end
---@param h integer
---@param defName string
---@return number
---@deprecated Use game.pawns.hediff_severity (works until 1.0.0). Run: rimkit migrate --write
function health.hediff_severity(h, defName) end
---@param h integer
---@param defName string
---@param severity number
---@deprecated Use game.pawns.set_hediff_severity (works until 1.0.0). Run: rimkit migrate --write
function health.set_hediff_severity(h, defName, severity) end
---@param h integer
---@param quality number|nil
---@deprecated Use game.pawns.tend (works until 1.0.0). Run: rimkit migrate --write
function health.tend(h, quality) end

surgery = {}
---@param h integer
---@param recipeDefName string
---@return boolean
---@deprecated Use game.pawns.queue_surgery (works until 1.0.0). Run: rimkit migrate --write
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
---@deprecated Use game.anomaly.dlc_active (works until 1.0.0). Run: rimkit migrate --write
function anomaly.dlc_active() end
---@param h integer
---@return boolean
---@deprecated Use game.anomaly.is_entity (works until 1.0.0). Run: rimkit migrate --write
function anomaly.is_entity(h) end
---@param mapHandle integer|nil
---@return integer[]
---@deprecated Use game.anomaly.list_on_map (works until 1.0.0). Run: rimkit migrate --write
function anomaly.list_on_map(mapHandle) end
---@param name string
---@param mapHandle integer|nil
---@return integer
---@deprecated Use game.anomaly.get_on_map (works until 1.0.0). Run: rimkit migrate --write
function anomaly.get_on_map(name, mapHandle) end
---@param h integer
---@param severity number|nil
---@deprecated Use game.anomaly.knock_out (works until 1.0.0). Run: rimkit migrate --write
function anomaly.knock_out(h, severity) end
---@param hauler_h integer
---@param entity_h integer|nil
---@return integer
---@deprecated Use game.anomaly.find_platform (works until 1.0.0). Run: rimkit migrate --write
function anomaly.find_platform(hauler_h, entity_h) end
---@param hauler_h integer
---@param entity_h integer
---@param platform_h integer|nil
---@return boolean
---@deprecated Use game.anomaly.start_capture (works until 1.0.0). Run: rimkit migrate --write
function anomaly.start_capture(hauler_h, entity_h, platform_h) end
--- Same as pawn.make_controllable. Draft/move like an animal.
---@param h integer
---@return boolean
---@deprecated Use game.anomaly.recruit (works until 1.0.0). Run: rimkit migrate --write
function anomaly.recruit(h) end
---@param h integer
---@return boolean
---@deprecated Use game.anomaly.try_set_faction_player (works until 1.0.0). Run: rimkit migrate --write
function anomaly.try_set_faction_player(h) end
---@param h integer
---@return boolean
---@deprecated Use game.anomaly.release_to_hostile (works until 1.0.0). Run: rimkit migrate --write
function anomaly.release_to_hostile(h) end

util = {}
---@param target "ModRoot"|"Saves"|"PlayerLog"|"RimKit"
---@param package_id string|nil
---@deprecated Use game.util.open_folder (works until 1.0.0). Run: rimkit migrate --write
function util.open_folder(target, package_id) end
---@param package_id string
---@param file string
---@param content string
---@deprecated Use game.util.write_export (works until 1.0.0). Run: rimkit migrate --write
function util.write_export(package_id, file, content) end

building = {}
---@param h integer
---@return boolean
---@deprecated Use game.buildings.power_on (works until 1.0.0). Run: rimkit migrate --write
function building.power_on(h) end
---@param h integer
---@param on boolean
---@deprecated Use game.buildings.set_power (works until 1.0.0). Run: rimkit migrate --write
function building.set_power(h, on) end
---@param h integer
---@param on boolean
---@deprecated Use game.buildings.flick (works until 1.0.0). Run: rimkit migrate --write
function building.flick(h, on) end

work = {}
---@param h integer
---@param workType string
---@return integer
---@deprecated Use game.work.get_priority (works until 1.0.0). Run: rimkit migrate --write
function work.get_priority(h, workType) end
---@param h integer
---@param workType string
---@param priority integer
---@deprecated Use game.work.set_priority (works until 1.0.0). Run: rimkit migrate --write
function work.set_priority(h, workType, priority) end
---@return string[]
---@deprecated Use game.work.list_types (works until 1.0.0). Run: rimkit migrate --write
function work.list_types() end

world_api = {}
---@param map integer|nil
---@return string
---@deprecated Use game.weather.current (works until 1.0.0). Run: rimkit migrate --write
function world_api.weather(map) end
---@param def string
---@param map integer|nil
---@deprecated Use game.weather.set (works until 1.0.0). Run: rimkit migrate --write
function world_api.set_weather(def, map) end

incident = {}
---@param def string
---@param map integer|nil
---@return boolean
---@deprecated Use game.incidents.try_fire (works until 1.0.0). Run: rimkit migrate --write
function incident.try_fire(def, map) end
---@return string[]
---@deprecated Use game.incidents.list (works until 1.0.0). Run: rimkit migrate --write
function incident.list() end

audio = {}
---@param soundDef string
---@deprecated Use game.audio.play (works until 1.0.0). Run: rimkit migrate --write
function audio.play(soundDef) end

--[[@experimental]]
---Typed reflection. Advanced tier, needs the RimKit setting developer_reflect. See docs/api/reflect.md.
---@class RimReflect
game = game or {}
game.reflect = {}
---@param obj RimPawn|RimThing|RimMap|RimFaction|RimObject|integer
---@param member string
---@return any
function game.reflect.get(obj, member) end
---@param obj RimPawn|RimThing|RimMap|RimFaction|RimObject|integer
---@param member string
---@param value any
function game.reflect.set(obj, member, value) end
---@param obj RimPawn|RimThing|RimMap|RimFaction|RimObject|integer
---@param method string
---@return any
function game.reflect.call(obj, method, ...) end
---@param obj RimPawn|RimThing|RimMap|RimFaction|RimObject|integer
---@param method string
---@param sig string[] Parameter type names selecting one overload.
---@return any
function game.reflect.call_sig(obj, method, sig, ...) end
---@param type string
---@param member string
---@return any
function game.reflect.static_get(type, member) end
---@param type string
---@param member string
---@param value any
function game.reflect.static_set(type, member, value) end
---@param type string
---@param method string
---@return any
function game.reflect.static_call(type, method, ...) end
---@param type string
---@param method string
---@param sig string[]
---@return any
function game.reflect.static_call_sig(type, method, sig, ...) end
---@param type string
---@return any
function game.reflect.new(type, ...) end
---@param target RimObject|RimPawn|RimThing|RimMap|RimFaction|integer|string Object, handle or type name.
---@return { name: string, kind: string, type: string, static: boolean, declaring: string }[]
function game.reflect.members(target) end
---@param obj RimObject|RimPawn|RimThing|RimMap|RimFaction|integer
---@return string
function game.reflect.type(obj) end
---@param obj RimObject|RimPawn|RimThing|RimMap|RimFaction|integer
---@param type string
---@return boolean
function game.reflect.is_a(obj, type) end
---@param obj RimObject|RimPawn|RimThing|RimMap|RimFaction|integer
function game.reflect.release(obj) end
---@param type string
---@return string[]
function game.reflect.enum_names(type) end
---@param n integer|nil
---@return string[]
function game.reflect.audit(n) end


---Keyed language string from Languages/<lang>/Keyed. {0}, {1}, ... take the extra arguments. An unknown key returns the key.
---@param key string
---@return string
function game.ui.translate(key, ...) end

---@class RimWorldVersion
---@field major integer Major version, for example 1
---@field minor integer Minor version, for example 6
---@field build integer Build number
---@field text string Version as text, for example 1.6.4871

game.version = game.version or {}

--- The running RimWorld version.
---@return RimWorldVersion
function game.version.rimworld() end

--- True when the running game is at least this version, for example at_least("1.6").
---@param spec string
---@return boolean
function game.version.at_least(spec) end

--- The RimKit version string.
---@return string
function game.version.rimkit() end

--- The RimKit API level (see the Standard).
---@return integer
function game.version.api_level() end

--- Game-update watch: does a Harmony target still resolve in this game build?
---@param type string
---@param method string
---@param sig? string Comma separated parameter type names
---@return { found: boolean, problem?: string }
function game.hooks.check_target(type, method, sig) end

--- Game-update watch: every catalog event with whether its patch target still resolves.
---@return { event: string, ok: boolean, problem?: string }[]
function game.hooks.check_events() end

---@type integer
game.time.ticks_per_hour = 2500
---@type integer
game.time.ticks_per_day = 60000
---@type integer
game.time.ticks_per_quadrum = 900000
---@type integer
game.time.ticks_per_year = 3600000

---@class RimVersionedTarget
---@field type string Full name of the type
---@field method string Name of the method
---@field sig? string[] Parameter type names that pick one overload

--- Installs a hook for the game version that is running. targets maps a minimum game version to a target. The entry with
--- the highest minimum that the running game satisfies is used, nothing is installed (nil) when none does.
---@param kind "prefix"|"postfix"|"finalizer"
---@param targets table<string, RimVersionedTarget>
---@param fn function
---@param opts? table
---@return integer?
function game.version.hook(kind, targets, fn, opts) end

game.json = game.json or {}

--- Encodes a Lua value as JSON text. Game objects become handles.
---@param value any
---@return string
function game.json.encode(value) end

--- Decodes JSON text into Lua values. Raises RK1001 on invalid JSON.
---@param text string
---@return any
function game.json.decode(text) end

--- Stores any Lua table, string, number or boolean in the save. Scope is "game", "map" or "world". Uses JSON, so functions
--- and game objects cannot be stored.
---@param package_id string
---@param scope "game"|"map"|"world"
---@param key string
---@param value any
---@param map? RimMapRef Map scope only, defaults to the current map
---@return boolean
function game.save.put(package_id, scope, key, value, map) end

--- Reads a value stored with put. Returns default when nothing is stored.
---@param package_id string
---@param scope "game"|"map"|"world"
---@param key string
---@param map? RimMapRef
---@param default? any
---@return any
function game.save.fetch(package_id, scope, key, map, default) end

--- Registers a Lua class: every function in methods becomes one override of the generated proxy for the family
--- (see game.classes.families). The functions receive the game objects as positional arguments.
---@param family string
---@param name string
---@param methods table<string, function>
---@return integer count Functions registered
function game.classes.define(family, name, methods) end

--- Hooks a method of another mod. Returns nil and does nothing when that mod is not active or the type is not in its assemblies.
---@param package_id string
---@param kind "prefix"|"postfix"|"finalizer"
---@param type_name string
---@param method string
---@param fn function
---@param opts? table
---@return integer?
function game.hooks.in_mod(package_id, kind, type_name, method, fn, opts) end

-- ---- Phase 6: ecosystem

game.interop = game.interop or {}

--- Publishes a table of functions other mods can call. The name is global, the version is semantic.
---@param name string
---@param version string Like 1.2.0
---@param api table<string, function>
---@return boolean
function game.interop.publish(name, version, api) end

--- Returns another mod's published API when its version satisfies the requirement ("1.2.0", ">=1.2", "^1.2", "~1.2"). Returns nil and a reason otherwise.
---@param name string
---@param requirement? string
---@return table? api
---@return string? version_or_reason
function game.interop.get(name, requirement) end

---@param name string
---@param requirement? string
---@return boolean
function game.interop.has(name, requirement) end

--- Runs fn(api, version) when the API is published, now if it already is. Works with any load order.
---@param name string
---@param requirement string?
---@param fn fun(api: table, version: string)
function game.interop.when(name, requirement, fn) end

--- Calls a published function. A missing mod or function returns false, never raises.
---@param name string
---@param fn string
---@return boolean ok
---@return any result
function game.interop.call(name, fn, ...) end

---@return table[]
function game.interop.list() end

---@param have string
---@param requirement string
---@return boolean
function game.interop.satisfies(have, requirement) end

game.profiler = game.profiler or {}

--- Time each mod spent in ticks, events, hooks, timers, UI callbacks and loading, in microseconds, and its budget.
---@return table[]
function game.profiler.report() end

function game.profiler.reset() end

game.dev = game.dev or {}

--- Package id of the mod whose Lua is running.
---@return string
function game.dev.current_mod() end

--- Reload a mod's Lua when its files change.
---@param on boolean
---@return boolean
function game.dev.watch(on) end

--- Reloads a mod's Lua now. Needs the "dev" capability.
---@param package_id string
---@return boolean
function game.dev.reload(package_id) end

--- Registers a named function the dev tools window can run.
---@param name string
---@param fn function
---@param description? string
---@return boolean
function game.dev.action(name, fn, description) end

---@return table[]
function game.dev.actions() end

---@param name string
---@return boolean ok
---@return string? error
function game.dev.run(name) end

--- Evaluates a Lua expression or statement. Needs Development mode and the "dev" capability. Returns ok and the result as text.
---@param code string
---@return boolean ok
---@return string text
function game.dev.eval(code) end

---@param value any
---@return string
function game.dev.show(value) end

--- Starts recording every named event with its tick. filter keeps only events whose name contains it.
---@param filter? string
---@return boolean
function game.dev.record_start(filter) end

---@return integer count
function game.dev.record_stop() end

---@return table[]
function game.dev.record_log() end

---@return boolean
function game.dev.record_clear() end

---@return boolean
function game.dev.recording() end

--- Opens the dev tools window (Development mode only).
---@return boolean
function game.dev.open_tools() end

--- Permissions each mod declared in meta.capabilities.
---@return table[]
function game.mods.capabilities() end

--- The mod version a mod declared in meta.mod_version.
---@param package_id string
---@return string
function game.mods.version(package_id) end

--- Runs the migration steps after the data version stored in the save, in order. steps[2] upgrades version 1 to 2.
---@param package_id string
---@param steps function[]
---@return integer version
function game.save.migrate(package_id, steps) end

--- Records the mod version that wrote this save and returns the previous one.
---@param package_id string
---@param version? string
---@return string? previous
function game.save.stamp(package_id, version) end

--- Removes everything a mod stored in the game and world scopes. Returns how many keys.
---@param package_id string
---@return integer
function game.save.purge(package_id) end

game.test = game.test or {}

---@param name string
---@param body function
function game.test.describe(name, body) end

---@param name string
---@param fn function
function game.test.it(name, fn) end

---@param fn function
function game.test.before_each(fn) end

---@class RimExpect
---@field to_be fun(value: any)
---@field to_equal fun(value: any)
---@field to_be_nil fun() Passes when the value is nil
---@field to_be_truthy fun() Passes when the value is neither nil nor false
---@field to_be_falsy fun() Passes when the value is nil or false
---@field to_be_close fun(value: number, epsilon?: number)
---@field to_contain fun(value: any)
---@field to_have_length fun(n: integer)
---@field to_error fun(part?: string)

---@param value any
---@return RimExpect
function game.test.expect(value) end

--- Replaces what the host answers for an op. A function gets the argument table, any other value is returned as is.
---@param op string
---@param answer any
function game.test.mock(op, answer) end

--- Calls made to the mock host, optionally for one op.
---@param op? string
---@return table[]
function game.test.calls(op) end

--- The functions of a Lua class the mod defined with game.classes.define.
---@param family string
---@param name string
---@return table<string, function>?
function game.test.class(family, name) end

--- The function a mod gave to game.tweaks.on.
---@param name string
---@return function?
function game.test.tweak(name) end

---@param name string
---@param payload? table
function game.test.emit(name, payload) end

---@param n? integer
function game.test.tick(n) end

--- Runs the mod's on_load handlers.
function game.test.start() end

--- Messages the mod logged or showed during the current test.
---@return string[]
function game.test.logs() end

---@param part string
---@return boolean
function game.test.logged(part) end

function game.test.reset_mocks() end
function game.test.capture() end

---@return integer failures
function game.test.run() end

-- Defs written in Lua (Defs/*.lua). rimkit mod sync turns the calls into Def XML, see docs/guide/defs-xml.md.
---@param kind string The Def type, for example "ThingDef"
---@param def_name string
---@param fields? table Plain table: nested tables are elements, lists are li items, _class sets Class, _attrs sets attributes
---@param opts? { parent?: string, name?: string, abstract?: boolean }
function def(kind, def_name, fields, opts) end

---@return string The "(x,z)" text a Def expects for a vector
function vec(x, z) end

---@return string The "(r,g,b)" text a Def expects for a colour
function rgb(r, g, b) end
