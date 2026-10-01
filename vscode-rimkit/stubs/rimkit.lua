--- RimKit Lua stubs (EmmyLua / LuaLS). Author: Team Stratware.win
--- High-level OO API (Phase 1–3) + escape hatch rim.invoke / rim.prefix / rim.reflect

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
