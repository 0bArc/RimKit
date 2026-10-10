#!/usr/bin/env node
// Generates Luau type definitions for luau-lsp from the LuaLS stubs and the event catalog.
// Run: node src/editor/tools/gen-luau-defs.js [--check]
//
// Input:  stubs/rimkit.lua, stubs/rimkit_kits.lua, stubs/rimkit_unc.lua, src/host/EventCatalog*.cs
// Output: stubs/rimkit.d.luau  (generated, do not edit)
//
// With the definitions loaded a mod can be written with Luau types, and the editor completes the payload of every event:
//   game.events.on_pawn_damaged(function(pawn, e) ... end)                  -- both are typed from the function name
//   game.events.on("pawn.damaged", function(e: PawnDamagedEvent) ... end)    -- or annotate it yourself
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const repo = path.join(root, "..", "..");
const read = (f) => fs.readFileSync(f, "utf8").replace(/\r/g, "");

// ---- types ----------------------------------------------------------------------------------------------------

const KEYWORDS = new Set(["and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while", "export", "continue"]);
const safeName = (n) => (KEYWORDS.has(n) ? n + "_" : n);
const IDENT = /^[A-Za-z_][A-Za-z0-9_]*$/;

// Names that exist as Luau types. Anything else becomes any.
const known = new Set(["string", "number", "boolean", "any", "nil", "thread"]);

// LuaLS type text to Luau type text.
function luauType(text) {
  const src = String(text || "").trim();
  if (!src) return "any";
  let i = 0;
  const peek = () => src[i];
  const ws = () => {
    while (i < src.length && /\s/.test(src[i])) i++;
  };
  function ident() {
    const m = /^[A-Za-z_][A-Za-z0-9_.]*/.exec(src.slice(i));
    if (!m) return null;
    i += m[0].length;
    return m[0];
  }
  function parseUnion() {
    const parts = [parsePostfix()];
    ws();
    while (peek() === "|") {
      i++;
      ws();
      parts.push(parsePostfix());
      ws();
    }
    return parts.length === 1 ? parts[0] : parts.join(" | ");
  }
  function parsePostfix() {
    ws();
    let base = parsePrimary();
    for (;;) {
      ws();
      if (src.startsWith("[]", i)) {
        i += 2;
        base = `{${base}}`;
      } else if (peek() === "?") {
        i++;
        base = base.includes(" | ") || base.startsWith("(") ? `(${base})?` : `${base}?`;
      } else break;
    }
    return base;
  }
  function parsePrimary() {
    ws();
    const c = peek();
    if (c === "(") {
      i++;
      const inner = parseUnion();
      ws();
      if (peek() === ")") i++;
      return `(${inner})`;
    }
    if (c === "{") {
      // { a: T, b: U|nil }
      i++;
      const fields = [];
      ws();
      while (i < src.length && peek() !== "}") {
        ws();
        const name = ident();
        ws();
        let optional = false;
        if (peek() === "?") {
          optional = true;
          i++;
        }
        if (peek() === ":") {
          i++;
          let t = parseUnion();
          if (optional && !t.endsWith("?")) t = `${t.includes(" | ") ? `(${t})` : t}?`;
          if (name && IDENT.test(name) && !KEYWORDS.has(name)) fields.push(`${name}: ${t}`);
        }
        ws();
        if (peek() === ",") i++;
        ws();
      }
      if (peek() === "}") i++;
      return fields.length ? `{ ${fields.join(", ")} }` : "{ [any]: any }";
    }
    if (c === '"' || c === "'") {
      const q = c;
      const end = src.indexOf(q, i + 1);
      const s = src.slice(i, end + 1);
      i = end + 1;
      return s;
    }
    const name = ident();
    if (!name) {
      i = src.length;
      return "any";
    }
    if (name === "fun") {
      // fun(a: T, b?: U): R
      ws();
      const params = [];
      if (peek() === "(") {
        i++;
        ws();
        while (i < src.length && peek() !== ")") {
          ws();
          if (src.startsWith("...", i)) {
            i += 3;
            ws();
            let t = "any";
            if (peek() === ":") {
              i++;
              t = parseUnion();
            }
            params.push(`...${t}`);
          } else {
            const pn = ident() || "arg";
            ws();
            let opt = false;
            if (peek() === "?") {
              opt = true;
              i++;
            }
            let t = "any";
            if (peek() === ":") {
              i++;
              t = parseUnion();
            }
            params.push(`${safeName(pn)}: ${opt && !t.endsWith("?") ? `${t.includes(" | ") ? `(${t})` : t}?` : t}`);
          }
          ws();
          if (peek() === ",") i++;
          ws();
        }
        if (peek() === ")") i++;
      }
      ws();
      let ret = "()";
      if (peek() === ":") {
        i++;
        ret = parseUnion();
        ws();
        while (peek() === ",") {
          i++;
          ret += `, ${parseUnion()}`;
          ws();
        }
        if (ret.includes(",")) ret = `(${ret})`;
      }
      return `(${params.join(", ")}) -> ${ret}`;
    }
    if (name === "table") {
      if (peek() === "<") {
        i++;
        const k = parseUnion();
        ws();
        if (peek() === ",") i++;
        const v = parseUnion();
        ws();
        if (peek() === ">") i++;
        return `{ [${k}]: ${v} }`;
      }
      return "{ [any]: any }";
    }
    switch (name) {
      case "integer":
      case "number":
      case "float":
        return "number";
      case "function":
        return "(...any) -> ...any";
      case "userdata":
      case "lightuserdata":
        return "any";
      case "nil":
        return "nil";
      case "string":
      case "boolean":
      case "any":
        return name;
      default:
        return known.has(name) ? name : `__${name}`; // resolved after every type is known
    }
  }
  const out = parseUnion();
  return out;
}

// Replaces __Name markers with the name or any, once the known set is complete.
function resolve(text) {
  return text.replace(/__([A-Za-z_][A-Za-z0-9_.]*)/g, (m, n) => (known.has(n) ? n : "any"));
}

// ---- stub parsing -----------------------------------------------------------------------------------------------

const classes = new Map(); // name -> { fields: [{name, type, optional, doc}], methods: [{name, params, returns}], parent, hasLocal }
const aliases = new Map();
const domains = new Map(); // domain -> Map(fn -> { params:[{name,type,optional}], returns, typed })
const topLevel = new Map();

function addFn(map, name, params, returns, typed) {
  const prev = map.get(name);
  if (prev && prev.typed && !typed) return;
  map.set(name, { params, returns, typed });
}

function parseStub(file) {
  const lines = read(file).split("\n");
  let cls = null;
  let pending = { params: [], returns: [], deprecated: false };
  for (let n = 0; n < lines.length; n++) {
    const line = lines[n];
    let m;
    if ((m = /^---@class (\w+)(?:\s*:\s*(\w+))?/.exec(line))) {
      cls = classes.get(m[1]) || { fields: [], methods: [], parent: null };
      if (m[2]) cls.parent = m[2];
      classes.set(m[1], cls);
      cls.name = m[1];
      continue;
    }
    if ((m = /^---@field\s+(\w+)(\?)?\s+(\S+)(?:\s+(.*))?$/.exec(line)) && cls) {
      if (!cls.fields.some((f) => f.name === m[1])) cls.fields.push({ name: m[1], optional: !!m[2], type: m[3], doc: m[4] || "" });
      continue;
    }
    if ((m = /^---@alias (\w+)\s+(.+)$/.exec(line))) {
      aliases.set(m[1], m[2]);
      continue;
    }
    if ((m = /^---@param\s+(\.\.\.|\w+)(\?)?\s+(\S+(?:\s*\|\s*\S+)*)/.exec(line))) {
      pending.params.push({ name: m[1], optional: !!m[2], type: m[3] });
      continue;
    }
    if ((m = /^---@return\s+(\S+)/.exec(line))) {
      pending.returns.push(m[1]);
      continue;
    }
    if (/^---@deprecated/.test(line)) {
      pending.deprecated = true;
      continue;
    }
    if (/^---/.test(line)) continue;
    if ((m = /^function (\w+):(\w+)\((.*)\)\s*end\s*$/.exec(line))) {
      const c = classes.get(m[1]) || { name: m[1], fields: [], methods: [], parent: null };
      classes.set(m[1], c);
      const names = m[3].split(",").map((s) => s.trim()).filter(Boolean);
      if (!pending.deprecated) c.methods.push({ name: m[2], params: names.map((nm) => pending.params.find((p) => p.name === nm) || { name: nm, type: "any" }), returns: pending.returns });
      pending = { params: [], returns: [], deprecated: false };
      cls = null;
      continue;
    }
    if ((m = /^function (game(?:\.\w+)+)\((.*)\)\s*end\s*$/.exec(line))) {
      const parts = m[1].split(".").slice(1);
      const names = m[2].split(",").map((s) => s.trim()).filter(Boolean);
      const typed = pending.params.length > 0 || pending.returns.length > 0;
      const params = names.map((nm) => pending.params.find((p) => p.name === nm) || { name: nm, type: "any", optional: nm === "..." });
      if (!pending.deprecated || true) {
        if (parts.length === 1) addFn(topLevel, parts[0], params, pending.returns, typed);
        else if (parts.length === 2) {
          if (!domains.has(parts[0])) domains.set(parts[0], new Map());
          addFn(domains.get(parts[0]), parts[1], params, pending.returns, typed);
        }
      }
      pending = { params: [], returns: [], deprecated: false };
      continue;
    }
    if (line.trim() === "" || /^local \w+ = \{\}/.test(line) || /^\w+ = \{\}/.test(line)) {
      if (line.trim() === "") cls = null;
      continue;
    }
    pending = { params: [], returns: [], deprecated: false };
  }
}

for (const f of ["rimkit.lua", "rimkit_kits.lua", "rimkit_unc.lua"]) parseStub(path.join(root, "stubs", f));

// ---- which classes are objects ---------------------------------------------------------------------------------

const OBJECTS = new Set(["RimPawn", "RimThing", "RimMap", "RimFaction", "RimEntity", "RimObject", "RimAnomalies"]);
for (const [name, c] of classes) {
  if (c.methods.length > 0) OBJECTS.add(name);
}
for (const name of classes.keys()) known.add(name);
for (const name of aliases.keys()) known.add(name);
for (const n of ["RimPawnRef", "RimThingRef", "RimMapRef", "RimFactionRef"]) known.add(n);

// ---- events ------------------------------------------------------------------------------------------------------

const events = [];
const seenEvents = new Set();
for (const f of ["src/host/EventCatalog.cs", "src/host/EventCatalogMore.cs", "src/host/EventCatalogWork.cs"]) {
  const file = path.join(repo, f);
  if (!fs.existsSync(file)) continue;
  for (const m of read(file).matchAll(/\bAdd\(\s*"([a-z_.]+)",\s*"((?:[^"\\]|\\.)*)",\s*(true|false)/g)) {
    const pm = /Payload: ([^.]*)\./.exec(m[2]);
    if (seenEvents.has(m[1])) continue;
    seenEvents.add(m[1]);
    events.push({ name: m[1], fields: pm ? pm[1].split(",").map((s) => s.trim()).filter(Boolean) : [], hot: m[3] === "true" });
  }
}
const FIELD_TYPES = {
  pawn: "RimPawn", other: "RimPawn", doctor: "RimPawn", patient: "RimPawn", killer: "RimPawn", instigator: "RimPawn",
  thing: "RimThing", building: "RimThing", item: "RimThing",
  map: "RimMap", faction: "RimFaction", damage: "RimDamageInfo", dealt: "number", amount: "number", count: "number",
  def: "string", mode: "string", name: "string", label: "string", kind: "string", reason: "string", stat: "string", text: "string",
  hediff: "RimObject", job: "RimObject", quest: "RimObject", letter: "RimObject", ability: "RimObject", culprit: "RimObject",
};
const camel = (s) => s.split(/[._]/).map((p) => p[0].toUpperCase() + p.slice(1)).join("");
const eventType = (name) => `${camel(name)}Event`;
// The first argument of an on_<event> handler: the pawn, else the thing.
const subjectOf = (e) => (e.fields.includes("pawn") ? { name: "pawn", type: "RimPawn" } : e.fields.includes("thing") ? { name: "thing", type: "RimThing" } : null);

// ---- output ------------------------------------------------------------------------------------------------------

const out = [];
const emit = (s = "") => out.push(s);

emit("-- Generated by src/editor/tools/gen-luau-defs.js. Do not edit.");
emit("-- Luau type definitions for RimKit, for luau-lsp. Load it with luau-lsp.types.definitionFiles.");
emit("");
const refs = { RimPawnRef: "RimPawn | number", RimThingRef: "RimThing | RimPawn | number", RimMapRef: "RimMap | number", RimFactionRef: "RimFaction | number" };
for (const [n, t] of Object.entries(refs)) if (!aliases.has(n) && !classes.has(n)) emit(`type ${n} = ${t}`);
emit("");

const fieldLine = (f) => `${safeName(f.name)}: ${resolve(f.optional && !luauType(f.type).endsWith("?") ? `${luauType(f.type)}?` : luauType(f.type)).replace(/^\((.*)\)\?$/, "($1)?")}`;
const usable = (f) => IDENT.test(f.name) && !KEYWORDS.has(f.name);

// Data classes become table types.
for (const [name, alias] of aliases) emit(`type ${name} = ${resolve(luauType(alias))}`);
emit("");
for (const [name, c] of classes) {
  if (OBJECTS.has(name)) continue;
  const parent = c.parent && classes.get(c.parent) && !OBJECTS.has(c.parent) ? classes.get(c.parent) : null;
  const fields = [...(parent ? parent.fields : []), ...c.fields].filter(usable);
  // Extern types, not aliases: type aliases in a definitions file are not visible to the mod, and a mod annotates with these names.
  emit(`declare extern type ${name} with`);
  for (const f of fields) emit(`  ${fieldLine(f)}`);
  emit("end");
}
emit("");

// Thing methods come from the things kit and are shared by RimThing and RimPawn.
const thingMethods = classes.get("RimThing") ? classes.get("RimThing").methods : [];
const fnSig = (params, returns, self) => {
  const ps = params.filter((p) => p.name !== "self");
  const list = ps.map((p) => {
    if (p.name === "...") return "...any";
    let t = resolve(luauType(p.type));
    if (p.optional && !t.endsWith("?") && !t.startsWith("(")) t += "?";
    else if (p.optional && !t.endsWith("?")) t = `(${t})?`;
    return `${safeName(p.name)}: ${t}`;
  });
  if (self) list.unshift("self");
  const rets = returns.map((r) => resolve(luauType(r)));
  const ret = rets.length === 0 ? "()" : rets.length === 1 ? rets[0] : `(${rets.join(", ")})`;
  return { list: list.join(", "), ret };
};

for (const name of OBJECTS) {
  const c = classes.get(name) || { fields: [], methods: [], parent: null };
  emit(`declare extern type ${name} with`);
  const seen = new Set();
  for (const f of c.fields.filter(usable)) {
    if (seen.has(f.name)) continue;
    seen.add(f.name);
    emit(`  ${fieldLine(f)}`);
  }
  const methods = [...c.methods];
  if (name === "RimPawn" || (name !== "RimThing" && false)) methods.push(...thingMethods);
  const seenM = new Set();
  for (const m of methods) {
    if (seenM.has(m.name) || seen.has(m.name) || !IDENT.test(m.name) || KEYWORDS.has(m.name)) continue;
    seenM.add(m.name);
    const s = fnSig(m.params, m.returns, true);
    emit(`  function ${m.name}(${s.list}): ${s.ret}`);
  }
  emit("end");
  emit("");
}

// Event payloads and the typed events.on overloads.
emit("type EventFilter = { pawn: boolean?, humanlike: boolean?, colonist: boolean?, def: (string | { string })?, min_dealt: number?, [string]: any }");
emit("");
const overloads = [];
const perEvent = [];
for (const e of events) {
  const t = eventType(e.name);
  const fields = e.fields.filter((f) => IDENT.test(f)).map((f) => `${f}: ${FIELD_TYPES[f] || "any"}`);
  emit(`declare extern type ${t} with`);
  for (const f of fields) emit(`  ${f}`);
  emit("end");
  const subj = subjectOf(e);
  perEvent.push(`  on_${e.name.replace(/\./g, "_")}: (fn: (${subj ? subj.name + ": " + subj.type + ", " : ""}e: ${t}) -> (), filter: EventFilter?) -> (),`);
}
overloads.push("((name: string, fn: (e: any) -> ()) -> ())", "((name: string, filter: EventFilter, fn: (e: any) -> ()) -> ())");
emit("");

// Forms the native core adds on top of the kit table: effects take a thing or pawn as the anchor.
const EXTRA_OVERLOADS = {
  "effects.text": "(anchor: RimThing | RimPawn, text: string, color: string?) -> boolean",
  "effects.fleck": "(anchor: RimThing | RimPawn, def: string, opts: RimFleckOptions?) -> boolean",
  "effects.effecter": "(anchor: RimThing | RimPawn, def: string) -> boolean",
};

// game.<domain>
const domainNames = [...domains.keys()].sort();
for (const d of domainNames) {
  const members = domains.get(d);
  emit(`type Rimkit_${d} = {`);
  for (const [fname, sig] of [...members.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    if (!IDENT.test(fname) || KEYWORDS.has(fname)) continue;
    if (d === "events" && fname === "on") continue;
    if (d === "things" && false) continue;
    const s = sig.typed ? fnSig(sig.params, sig.returns, false) : { list: "...any", ret: "...any" };
    const extra = EXTRA_OVERLOADS[`${d}.${fname}`];
    emit(extra ? `  ${fname}: ((${s.list}) -> ${s.ret}) & (${extra}),` : `  ${fname}: (${s.list}) -> ${s.ret},`);
  }
  if (d === "events") {
    emit(`  on: ${overloads.join(" & ")},`);
    for (const line of perEvent) emit(line);
  }
  emit("}");
}
emit("");
emit("declare game: {");
for (const d of domainNames) emit(`  ${d}: Rimkit_${d},`);
if (!domains.has("events")) emit(`  events: { on: ${overloads.join(" & ")} },`);
emit("  emit: { [string]: (subject: any?, extra: any?) -> () },");
for (const [fname, sig] of [...topLevel.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
  if (!IDENT.test(fname) || KEYWORDS.has(fname) || domains.has(fname)) continue;
  const s = sig.typed ? fnSig(sig.params, sig.returns, false) : { list: "...any", ret: "...any" };
  emit(`  ${fname}: (${s.list}) -> ${s.ret},`);
}
emit("}");
// Built-in libraries: require("rimkit.signal") and require("rimkit.promise").
emit("declare extern type SignalConnection with");
emit("  connected: boolean");
emit("  function disconnect(self): ()");
emit("  function is_connected(self): boolean");
emit("end");
emit("declare extern type Signal with");
emit("  function connect(self, fn: (...any) -> ()): SignalConnection");
emit("  function once(self, fn: (...any) -> ()): SignalConnection");
emit("  function fire(self, ...: any): ()");
emit("  function wait(self): ...any");
emit("  function connection_count(self): number");
emit("  function disconnect_all(self): ()");
emit("  function destroy(self): ()");
emit("end");
emit("declare extern type Promise with");
emit("  status: string");
emit("  function and_then(self, on_resolved: ((any) -> any)?, on_rejected: ((any) -> any)?): Promise");
emit("  function catch(self, on_rejected: (any) -> any): Promise");
emit("  function finally(self, fn: () -> ()): Promise");
emit("  function await(self): (boolean, any)");
emit("  function cancel(self): ()");
emit("  function get_status(self): string");
emit("end");
emit("type SignalModule = { new: () -> Signal, is: (value: any) -> boolean }");
emit("type PromiseModule = {");
emit("  new: (executor: ((any) -> (), (any) -> (), ((() -> ()) -> ())) -> ()) -> Promise,");
emit("  resolve: (value: any) -> Promise,");
emit("  reject: (err: any) -> Promise,");
emit("  try: (fn: (...any) -> any, ...any) -> Promise,");
emit("  async: (fn: (...any) -> any, ...any) -> Promise,");
emit("  delay: (ticks: number) -> Promise,");
emit("  all: (list: {any}) -> Promise,");
emit("  race: (list: {any}) -> Promise,");
emit("  is: (value: any) -> boolean,");
emit("}");
emit("declare require: ((module: \"rimkit.signal\") -> SignalModule) & ((module: \"rimkit.promise\") -> PromiseModule) & ((module: any) -> any)");
emit("declare rimkit: { signal: SignalModule, promise: PromiseModule, version: string, api_level: number, assert_api: (min_level: number) -> boolean, strict_errors: (on: boolean?) -> boolean, [string]: any }");
// The test API, available in files under Tests/.
emit("type MockPawnOptions = { humanlike: boolean?, colonist: boolean?, name: string?, position: ({ x: number, z: number } | boolean)?, map: number?, def: string?, hp: number?, max_hp: number? }");
emit("type MockThingOptions = { def: string?, name: string?, hp: number?, max_hp: number?, stack: number?, position: ({ x: number, z: number } | boolean)?, map: number? }");
emit("declare describe: (name: string, body: () -> ()) -> ()");
emit("declare it: (name: string, fn: () -> ()) -> ()");
emit("declare before_each: (fn: () -> ()) -> ()");
emit("declare expect: (value: any) -> RimExpect");
emit("declare mock: { pawn: (opts: MockPawnOptions?) -> RimPawn, thing: (opts: MockThingOptions?) -> RimThing }");
emit("declare spy: { [string]: { [string]: any } }");
emit("declare effects: { [string]: any }");
emit("declare rim: { [string]: any }");
emit("declare log: { info: (message: string) -> (), error: (message: string) -> () }");
emit("");

const text = out.join("\n");

// The same per-event functions for the Lua language server, so the unknown-call check and LuaLS users know them too.
const lua = ["---@meta", "-- Generated by src/editor/tools/gen-luau-defs.js. Do not edit.", "-- One function per event: game.events.on_pawn_damaged(function(pawn, e) ... end, filter).", ""];
for (const e of events) {
  const fields = e.fields.filter((f) => IDENT.test(f));
  lua.push(`---@class ${eventType(e.name)}`);
  for (const f of fields) lua.push(`---@field ${f} ${FIELD_TYPES[f] || "any"}`);
  lua.push("");
  lua.push(`--- Runs fn when ${e.name} happens. The optional filter is described in docs/api/events.md.`);
  const sj = subjectOf(e);
  lua.push(`---@param fn fun(${sj ? sj.name + ": " + sj.type + ", " : ""}e: ${eventType(e.name)})`);
  lua.push("---@param filter? table");
  lua.push(`function game.events.on_${e.name.replace(/\./g, "_")}(fn, filter) end`, "");
}
const luaText = lua.join("\n");
const target = path.join(root, "stubs", "rimkit.d.luau");
const luaTarget = path.join(root, "stubs", "rimkit_events.lua");
if (process.argv.includes("--check")) {
  const same = fs.existsSync(target) && read(target) === text && fs.existsSync(luaTarget) && read(luaTarget) === luaText;
  console.log(same ? "luau defs: up to date" : "luau defs: stubs are out of date. Run node src/editor/tools/gen-luau-defs.js");
  process.exit(same ? 0 : 1);
}
fs.writeFileSync(target, text);
fs.writeFileSync(luaTarget, luaText);
console.log(`luau defs: ${classes.size} classes, ${domainNames.length} domains, ${events.length} events -> ${path.relative(repo, target)}`);
