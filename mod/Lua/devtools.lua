-- RimKit developer tools and player notices. Loaded with the RimKit mod itself.
--   F11 (rebindable) opens the dev tools window when Development mode is on: console, hooks, events, profiler, actions, mods.
--   A mod that was switched off for too many errors gets a letter that names it and offers to turn it off for good.
local game = game
if not (game and game.dev and game.input) then return end

local DISABLE_TAG = "rimkit.disable:"

game.events.on("mod.disabled", function(p)
  local mod = p and p.mod or "?"
  game.hud.letter(
    game.ui.translate("RimKit_ModFailed_Label"),
    game.ui.translate("RimKit_ModFailed_Text", mod, p and p.where or "?"),
    { game.ui.translate("RimKit_ModFailed_Disable"), game.ui.translate("RimKit_ModFailed_Keep") },
    DISABLE_TAG .. mod)
end)

game.events.on("letter.choice", function(p)
  if not p or type(p.tag) ~= "string" or p.tag:sub(1, #DISABLE_TAG) ~= DISABLE_TAG then return end
  if p.index ~= 1 then return end
  local mod = p.tag:sub(#DISABLE_TAG + 1)
  local ok, err = pcall(game.mods.deactivate, mod)
  if ok then
    game.ui.message(game.ui.translate("RimKit_ModDisabledRestart", mod))
  else
    game.ui.message(tostring(err))
  end
end)

game.events.on("mod.over_budget", function(p)
  game.log.info("[RimKit] " .. tostring(p.mod) .. " is over its time budget (" .. math.floor(p.avg_us or 0) .. " us per tick)")
end)

game.dev.action("RimKit: export def names for the editor", function()
  game.ui.message(game.dev.export_defs())
end, "Writes every def name to the config folder so the VS Code extension can complete them.")

game.dev.action("RimKit: write a diagnostics bundle", function()
  game.ui.message(game.dev.bundle(game.json.encode(game.profiler.report())))
end, "Writes the log, mod list and profiler numbers to a folder. Nothing is sent anywhere.")

game.dev.action("RimKit: clear profiler numbers", function() game.profiler.reset() end)

-- ---- window

local win
local history, output = {}, {}
local function say(line)
  output[#output + 1] = line
  if #output > 200 then table.remove(output, 1) end
end

local function rows_of(list, columns, limit)
  local rows = {}
  for i = 1, math.min(#list, limit or 60) do
    local r = {}
    for _, c in ipairs(columns) do r[#r + 1] = tostring(c.get(list[i])) end
    rows[#rows + 1] = r
  end
  return rows
end

local function fmt(n) return string.format("%.1f", n or 0) end

local function console_tab(v)
  local lines = {}
  for i = math.max(1, #output - 14), #output do lines[#lines + 1] = { type = "label", text = output[i], font = "tiny" } end
  return { type = "column", gap = 4, children = {
    { type = "label", text = "Lua console. Expressions print their value.", color = "muted" },
    { type = "scroll", height = 230, child = { type = "column", children = lines } },
    { type = "row", gap = 6, children = {
      { type = "text", id = "code", value = v.state.code or "", w = 380 },
      { type = "button", id = "run", text = "Run", w = 60 },
    } },
  } }
end

local function hooks_tab()
  local list = game.hooks.list()
  table.sort(list, function(a, b) return (a.total_us or 0) > (b.total_us or 0) end)
  return { type = "table", columns = { { label = "Target" }, { label = "Kind", w = 70 }, { label = "Calls", w = 70 }, { label = "Total us", w = 80 }, { label = "Avg us", w = 60 } },
    rows = rows_of(list, {
      { get = function(h) return h.type .. "." .. h.method end }, { get = function(h) return h.kind end },
      { get = function(h) return h.calls end }, { get = function(h) return fmt(h.total_us) end }, { get = function(h) return fmt(h.avg_us) end },
    }) }
end

local function events_tab()
  local recording = game.dev.recording()
  local log = game.dev.record_log()
  local rows = {}
  for i = #log, math.max(1, #log - 39), -1 do rows[#rows + 1] = { tostring(log[i].tick), log[i].name, log[i].payload } end
  return { type = "column", gap = 4, children = {
    { type = "row", gap = 6, children = {
      { type = "button", id = recording and "rec_stop" or "rec_start", text = recording and "Stop recording" or "Record events", w = 150 },
      { type = "button", id = "rec_clear", text = "Clear", w = 70 },
    } },
    { type = "table", columns = { { label = "Tick", w = 70 }, { label = "Event", w = 160 }, { label = "Payload" } }, rows = rows },
  } }
end

local function profiler_tab()
  local rows = {}
  for _, r in ipairs(game.profiler.report()) do
    rows[#rows + 1] = { r.mod, fmt(r.avg_tick_us), fmt(r.tick_us / 1000), fmt(r.event_us / 1000), fmt(r.hook_us / 1000), fmt(r.ui_us / 1000), r.over_budget and "over" or "" }
  end
  return { type = "table", columns = { { label = "Mod" }, { label = "us/tick", w = 70 }, { label = "Tick ms", w = 70 }, { label = "Event ms", w = 70 }, { label = "Hook ms", w = 70 }, { label = "UI ms", w = 60 }, { label = "Budget", w = 55 } }, rows = rows }
end

local function actions_tab()
  local kids = {}
  for _, a in ipairs(game.dev.actions()) do
    kids[#kids + 1] = { type = "button", id = "act:" .. a.name, text = a.name, tip = a.description }
  end
  if #kids == 0 then kids[1] = { type = "label", text = "No debug actions. Register one with game.dev.action(name, fn).", color = "muted" } end
  return { type = "scroll", height = 300, child = { type = "column", gap = 4, children = kids } }
end

local function mods_tab()
  local rows = {}
  for _, m in ipairs(game.mods.capabilities()) do
    local caps = m.declared and table.concat(m.capabilities, ", ") or "not declared (everything allowed)"
    rows[#rows + 1] = { m.mod, m.version ~= "" and m.version or "?", caps, m.disabled and "switched off" or "" }
  end
  return { type = "table", columns = { { label = "Mod" }, { label = "Version", w = 70 }, { label = "Permissions" }, { label = "State", w = 90 } }, rows = rows }
end

-- ---- reflection browser: pick a root, open members that hold objects, watch values live.
-- Needs the RimKit setting developer_reflect. Reads are guarded, and only the first rows are read each refresh.

local ref = { stack = {} }
local REF_ROWS = 28

local function ref_top() return ref.stack[#ref.stack] end

local function ref_push(label, target, is_type)
  ref.stack[#ref.stack + 1] = { label = label, target = target, is_type = is_type or false }
end

local function ref_describe(value)
  local t = type(value)
  if value == nil then return "nil", false end
  if t == "userdata" then
    local ok, name = pcall(game.reflect.type, value)
    return "<" .. (ok and name or "object") .. ">", true
  end
  if t == "table" then
    local ok, text = pcall(game.json.encode, value)
    return (ok and text or "{...}"):sub(1, 90), false
  end
  return tostring(value):sub(1, 90), false
end

local function ref_members(top)
  local ok, list = pcall(game.reflect.members, top.target)
  if not ok then return nil, tostring(list) end
  return list, nil
end

local function ref_read(top, m)
  if m.kind ~= "property" and m.kind ~= "field" then return nil, false end
  if top.is_type and not m.static then return nil, false end
  local ok, value
  if top.is_type then ok, value = pcall(game.reflect.static_get, top.target, m.name)
  else ok, value = pcall(game.reflect.get, top.target, m.name) end
  if not ok then return "(" .. tostring(value):match("RK%d+") .. ")", false end
  return ref_describe(value)
end

local function reflect_tab(v)
  local roots = { type = "row", gap = 4, children = {
    { type = "button", id = "ref_root:find", text = "Find", w = 60, tip = "The static Verse.Find class: maps, tick manager, world." },
    { type = "button", id = "ref_root:selected", text = "Selected", w = 70 },
    { type = "button", id = "ref_root:map", text = "Map", w = 50 },
    { type = "button", id = "ref_root:faction", text = "Player", w = 60 },
    { type = "text", id = "ref_type", value = v.state.ref_type or "", w = 190 },
    { type = "button", id = "ref_goto", text = "Type", w = 50, tip = "Browse the static members of a type, for example Verse.Find" },
  } }
  local top = ref_top()
  if not top then
    return { type = "column", gap = 4, children = { roots, { type = "label", text = "Pick a root above. Open a member that holds an object to look inside it.", color = "muted" } } }
  end
  local crumbs = {}
  for _, s in ipairs(ref.stack) do crumbs[#crumbs + 1] = s.label end
  local list, err = ref_members(top)
  if not list then
    local why = err:match("RK4002") and "Typed reflection is off. Turn on the RimKit setting developer_reflect." or err
    return { type = "column", gap = 4, children = { roots, { type = "label", text = why, color = "muted" } } }
  end
  local filter = (v.state.ref_filter or ""):lower()
  local rows, shown = {}, 0
  for _, m in ipairs(list) do
    if m.kind ~= "ctor" and (filter == "" or m.name:lower():find(filter, 1, true)) then
      if shown >= REF_ROWS then break end
      shown = shown + 1
      local text, is_object = ref_read(top, m)
      local children = {
        { type = "label", text = m.name, w = 170 },
        { type = "label", text = m.kind .. " " .. (m.type or ""), w = 150, color = "muted", font = "tiny" },
        { type = "label", text = text or "", w = 190 },
      }
      if is_object then children[#children + 1] = { type = "button", id = "ref_open:" .. m.name, text = "open", w = 50 } end
      rows[#rows + 1] = { type = "row", gap = 4, children = children }
    end
  end
  return { type = "column", gap = 4, children = {
    roots,
    { type = "row", gap = 6, children = {
      { type = "button", id = "ref_back", text = "Back", w = 50 },
      { type = "label", text = table.concat(crumbs, " > "), color = "muted" },
    } },
    { type = "text", id = "ref_filter", value = v.state.ref_filter or "", w = 250, tip = "Filter members by name" },
    { type = "scroll", height = 230, child = { type = "column", gap = 2, children = rows } },
  } }
end

local function ref_open_member(name)
  local top = ref_top()
  if not top then return end
  local ok, value
  if top.is_type then ok, value = pcall(game.reflect.static_get, top.target, name)
  else ok, value = pcall(game.reflect.get, top.target, name) end
  if ok and type(value) == "userdata" then ref_push(name, value, false) end
end

local function ref_root(kind, v)
  ref.stack = {}
  if kind == "find" then ref_push("Verse.Find", "Verse.Find", true)
  elseif kind == "selected" then
    local ok, sel = pcall(game.selection.inspected)
    if ok and sel and sel.thing then ref_push(sel.label or "selected", sel.thing, false)
    else say("reflect: nothing is selected") end
  elseif kind == "map" then
    local ok, map = pcall(game.current_map)
    if ok and map then ref_push("map", map, false) end
  elseif kind == "faction" then
    local ok, f = pcall(game.player_faction)
    if ok and f then ref_push("player faction", f, false) end
  end
end

local function view(v)
  return { type = "tabs", id = "tab", tabs = {
    { id = "console", label = "Console", content = console_tab(v) },
    { id = "hooks", label = "Hooks", content = hooks_tab() },
    { id = "events", label = "Events", content = events_tab() },
    { id = "profiler", label = "Profiler", content = profiler_tab() },
    { id = "actions", label = "Actions", content = actions_tab() },
    { id = "mods", label = "Mods", content = mods_tab() },
    { id = "reflect", label = "Reflect", content = reflect_tab(v) },
  } }
end

local function run_code(code)
  if code == nil or code == "" then return end
  say("> " .. code)
  local called, ok, text = pcall(game.dev.eval, code)
  if not called then say("error: " .. tostring(ok)) return end
  say(ok and text or ("error: " .. tostring(text)))
end

local function on_event(e)
  if e.id == "run" then
    local code = game.widgets.state(e.window).code
    run_code(code)
    game.widgets.set_state(e.window, "code", "")
  elseif type(e.id) == "string" and e.id:sub(1, 9) == "ref_root:" then ref_root(e.id:sub(10))
  elseif e.id == "ref_goto" then
    local name = game.widgets.state(e.window).ref_type
    if name and name ~= "" then ref.stack = {} ref_push(name, name, true) end
  elseif e.id == "ref_back" then ref.stack[#ref.stack] = nil
  elseif type(e.id) == "string" and e.id:sub(1, 9) == "ref_open:" then ref_open_member(e.id:sub(10))
  elseif e.id == "rec_start" then game.dev.record_start()
  elseif e.id == "rec_stop" then game.dev.record_stop()
  elseif e.id == "rec_clear" then game.dev.record_clear()
  elseif type(e.id) == "string" and e.id:sub(1, 4) == "act:" then
    local ok, err = game.dev.run(e.id:sub(5))
    if not ok then say("action failed: " .. tostring(err)) end
  end
  game.widgets.invalidate(e.window)
end

function game.dev.open_tools()
  if not game.dev.mode() then
    game.ui.message(game.ui.translate("RimKit_DevToolsNeedDevMode"))
    return false
  end
  if win then game.widgets.close(win) end
  win = game.widgets.open("RimKit dev tools", view, on_event, { width = 640, height = 420, refresh = 30 })
  return true
end

game.input.register_key("RimKit_DevTools", "Open the RimKit dev tools", "F11")
game.events.on_tick(function()
  if game.input.binding_just_pressed("RimKit_DevTools") then game.dev.open_tools() end
end)

-- For tests: the window's view and event handler, so `rimkit mod test` can build every tab without opening a window.
if game.interop and game.interop.publish then
  game.interop.publish("rimkit.devtools", "1.0.0", { view = view, on_event = on_event })
end
