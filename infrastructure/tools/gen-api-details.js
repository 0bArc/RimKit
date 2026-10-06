#!/usr/bin/env node
// Generates infrastructure/docs-site/theme/api-details.json: for every API function and host operation, what the docs site shows when a
// table row is clicked: the signature, a parameter table, how the call works, and a ready-to-run example.
//
// The facts come from src/api/kits.json (signatures), the Lua stubs (type fields) and the host source (tier, since). The example is
// built from the signature and the fields of the returned type. Hand written examples in infrastructure/api-src/examples.md replace the
// generated one for a function ("=== game.raids.fire ===" followed by a lua code block).
// Run: node infrastructure/tools/gen-api-details.js [--check]
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "../..");
const out = path.join(root, "infrastructure/docs-site/theme/api-details.json");
const hljs = require(path.join(root, "infrastructure/docs-site/node_modules/highlight.js"));
const kits = JSON.parse(fs.readFileSync(path.join(root, "src/api/kits.json"), "utf8")).kits;
const aliases = JSON.parse(fs.readFileSync(path.join(root, "src/api/aliases.json"), "utf8"));

// ---- types from the stubs
const classes = new Map();
for (const rel of ["src/editor/stubs/rimkit_kits.lua", "src/editor/stubs/rimkit.lua"]) {
  let cur = null;
  for (const line of fs.readFileSync(path.join(root, rel), "utf8").split(/\r?\n/)) {
    let m;
    if ((m = /^---@class (\w+)/.exec(line))) {
      cur = { name: m[1], fields: [] };
      classes.set(cur.name, cur);
    } else if ((m = /^---@field (\w+)(\?)? (\S+)/.exec(line))) {
      if (cur) cur.fields.push({ name: m[1], optional: !!m[2], type: m[3] });
    } else if (!/^---/.test(line)) {
      cur = null;
    }
  }
}

// ---- ops, tiers and versions from the host source
const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => (e.isDirectory() ? (["obj", "bin"].includes(e.name) ? [] : walk(path.join(dir, e.name))) : [path.join(dir, e.name)]));
const opInfo = new Map();
const registerRe = /(?:ApiRegistry\.Register|\bR)\(\s*"([^"]+)"\s*,\s*[^,]+?(?:,\s*"([^"]*)")?(?:,\s*"([^"]*)")?\s*\)\s*;/g;
const dlcRe = /\bD\(\s*(?:"(\w+)"|(\w+))\s*,\s*"([^"]+)"/g;
for (const file of walk(path.join(root, "src/host")).filter((f) => f.endsWith(".cs"))) {
  const text = fs.readFileSync(file, "utf8");
  for (const m of text.matchAll(registerRe)) opInfo.set(m[1], { tier: m[2] || "gameplay", since: m[3] || "", dlc: "" });
  for (const m of text.matchAll(dlcRe)) opInfo.set(m[3], { tier: "advanced", since: "", dlc: m[1] || "" });
}
for (const m of fs.readFileSync(path.join(root, "src/host/GameApi.cs"), "utf8").matchAll(/case "([a-z][a-z0-9_]*\.[a-z0-9_.]+)"/g)) {
  if (!opInfo.has(m[1])) opInfo.set(m[1], { tier: "legacy", since: "", dlc: "" });
}

// ---- hand written examples
const overrides = new Map();
const exPath = path.join(root, "infrastructure/api-src/examples.md");
if (fs.existsSync(exPath)) {
  for (const m of fs.readFileSync(exPath, "utf8").replace(/\r/g, "").matchAll(/^=== (game\.[\w.]+) ===\n```lua\n([\s\S]*?)```/gm)) overrides.set(m[1], m[2].replace(/\s+$/, ""));
}

// ---- placeholders
const base = (t) => (t || "").replace(/\?$/, "").split("|")[0].trim();
const isPawn = (t) => /^(RimPawn|RimPawnRef|RimEntity)$/.test(base(t));
const isThing = (t) => /^(RimThing|RimThingRef)$/.test(base(t));
const isMap = (t) => /^(RimMap|RimMapRef)$/.test(base(t));
const isFaction = (t) => /^(RimFaction|RimFactionRef)$/.test(base(t));
const isObject = (t) => base(t) === "RimObject";
const STRING_SAMPLE = {
  def: '"DefName"', skill: '"Shooting"', stat: '"MarketValue"', recipe: '"Make_ComponentIndustrial"', speed: '"Fast"', quality: '"Good"',
  label: '"My label"', title: '"My title"', text: '"Hello"', name: '"Name"', key: '"my_key"', id: '"my_id"', package_id: '"my.mod"', kind: '"Colonist"',
  category: '"Misc"', tag: '"my_tag"', file: '"file.txt"', content: '"text"', color: '"#ffaa00"', texture: '"Things/Item/MyItem"', path: '"path"',
  family: '"comp"', lua_class: '"my_class"', xpath: '"/Defs"', mode: '"Normal"', status: '"Prisoner"', root: '"Root"',
};
const NUMBER_SAMPLE = { x: "50", z: "50", x2: "60", z2: "60", x1: "40", z1: "40", radius: "5", amount: "10", count: "1", level: "3", hour: "8", ticks: "600", days: "1", priority: "2", value: "1", delta: "5", index: "0", size: "1", width: "10", height: "10", tile: "1", points: "300", severity: "0.5", degree: "1" };
function literal(p) {
  const t = base(p.type);
  if (isPawn(p.type) || isThing(p.type) || isMap(p.type) || isFaction(p.type) || isObject(p.type)) return p.name;
  if (/^fun\(/.test(t) || t === "function") return "function() end";
  if (t === "string") return STRING_SAMPLE[p.name] || '"text"';
  if (t === "integer") return NUMBER_SAMPLE[p.name] || "1";
  if (t === "number") return NUMBER_SAMPLE[p.name] || "1.0";
  if (t === "boolean") return "true";
  if (/\[\]$/.test(t)) return "{}";
  const c = classes.get(t);
  if (c) {
    const req = c.fields.filter((f) => !f.optional);
    if (!req.length) return "{}";
    return "{ " + req.slice(0, 3).map((f) => f.name + " = " + literal({ name: f.name, type: f.type })).join(", ") + " }";
  }
  if (t === "table") return "{}";
  return "value";
}
function setupFor(p) {
  const t = p.type;
  if (isPawn(t)) return `local ${p.name} = game.selection.pawns()[1]`;
  if (isThing(t)) return `local ${p.name} = game.selection.inspected().thing`;
  if (isMap(t)) return `local ${p.name} = game.maps.current()`;
  if (isFaction(t)) return `local ${p.name} = game.factions.player()`;
  if (isObject(t)) return `local ${p.name} = game.world.settlements()[1].object`;
  return null;
}
const SHOW = (f) => f.name;
function usageLines(returns, v) {
  const r = (returns || "").trim();
  if (!r) return [];
  const optional = /\?$/.test(r);
  const t = r.replace(/\?$/, "");
  const wrap = (lines) => (optional ? [`if ${v} then`, ...lines.map((l) => "  " + l), "end"] : lines);
  if (t === "boolean") return [`print(${v})`];
  if (/^(string|integer|number)$/.test(t)) return [`print(${v})`];
  if (/^table<string, ?(\w+)>/.test(t) || t === "table" || t === "any") return [`print(game.json.encode(${v}))`];
  if (/^string\[\]$/.test(t)) return ["for _, name in ipairs(" + v + ") do print(name) end"];
  const arr = /^(\w+)\[\]$/.exec(t);
  const one = arr ? arr[1] : t;
  const show = (item) => {
    if (one === "RimPawn" || one === "RimEntity" || one === "RimFaction") return `print(${item}.name)`;
    if (one === "RimThing") return `print(${item}.label)`;
    if (one === "RimMap") return `print(${item}.width, ${item}.height)`;
    if (one === "RimObject") return `print(${item}.type_name)`;
    const c = classes.get(one);
    if (c) {
      const fs2 = c.fields.filter((f) => !f.optional && /^(string|integer|number|boolean)$/.test(base(f.type))).slice(0, 3).map((f) => `${item}.${f.name}`);
      if (fs2.length) return `print(${fs2.join(", ")})`;
    }
    return `print(${item})`;
  };
  if (arr) return ["for _, item in ipairs(" + v + ") do", "  " + show("item"), "end"];
  return wrap([show(v)]);
}
function resultName(fn, returns) {
  const r = base(returns).replace(/\[\]$/, "");
  if (!returns) return "";
  if (/\[\]$/.test(returns.replace(/\?$/, ""))) return fn.name.replace(/^(get_|list_)/, "");
  if (r === "boolean") return "ok";
  if (/^(string|integer|number)$/.test(r)) return "value";
  return "result";
}

// ---- parameter meanings
const PARAM = {
  pawn: "The pawn. A RimPawn or its handle", thing: "The thing. A RimThing or its handle", map: "The map. A RimMap or its handle", faction: "The faction. A RimFaction or its handle",
  other: "The other pawn", def: "Def name, for example Steel", skill: "Skill def name, for example Shooting", stat: "Stat def name, for example MarketValue", x: "Cell x coordinate", z: "Cell z coordinate",
  x2: "Second cell x coordinate", z2: "Second cell z coordinate", radius: "Radius in cells", amount: "How much", count: "How many", level: "Level", hour: "Hour of the day, 0 to 23", ticks: "Number of ticks",
  label: "Name shown to the player", text: "Text", title: "Title", opts: "Table of options", package_id: "Package id of your mod", id: "Id returned by an earlier call", key: "Key name",
  value: "The new value", index: "Position, counted from 0", priority: "Priority, higher runs first", speed: "Speed name: Normal, Fast, Superfast or Ultrafast", quality: "Quality name, for example Normal",
  recipe: "Recipe def name", kind: "Kind name", category: "Category name", color: "Colour as a hex string such as #ffaa00", path: "Path", texture: "Texture path inside your mod's Textures folder",
};
function paramMeaning(p, subjectName) {
  if (p.name === subjectName) return PARAM[p.name] || "The object the call is about";
  if (PARAM[p.name]) return PARAM[p.name];
  const t = base(p.type);
  if (classes.has(t) && !/^Rim(Pawn|Thing|Map|Faction|Object|Entity)/.test(t)) return `Table, see ${t}`;
  if (/^fun\(/.test(t) || t === "function") return "A Lua function the game calls";
  return { string: "Text", integer: "Whole number", number: "Number", boolean: "true or false", any: "Any value", table: "Table" }[t] || "";
}

// ---- build
const DLC = new Set(["ideology", "royalty", "biotech", "anomaly", "odyssey"]);
const fns = {};
const opToFn = {};
const luaName = (k, f) => (k.domain === "things" && f.subject ? `thing:${f.name}` : `game.${k.domain}.${f.name}`);
let count = 0;
for (const k of kits) {
  for (const f of k.fns) {
    const key = luaName(k, f);
    const op = `${k.op}.${f.name}`;
    const info = opInfo.get(op) || { tier: "gameplay", since: "", dlc: "" };
    const subject = f.subject && f.params[0] ? f.params[0] : null;
    const args = f.params.map((p) => ({ name: p.name, type: p.type, optional: !!p.optional }));
    const sig = `${key}(${args.map((p) => p.name + (p.optional ? "?" : "")).join(", ")})${f.returns ? " -> " + f.returns : ""}`;

    // example
    let code = overrides.get(key);
    let generated = false;
    if (!code) {
      generated = true;
      const lines = [];
      const seen = new Set();
      for (const p of args) {
        const s = setupFor(p);
        if (s && !seen.has(p.name)) { lines.push(s); seen.add(p.name); }
      }
      const callArgs = args.filter((p) => !p.optional).map(literal).join(", ");
      const v = resultName(f, f.returns);
      lines.push((v ? `local ${v} = ` : "") + `${key}(${callArgs})`);
      lines.push(...usageLines(f.returns, v));
      code = lines.join("\n");
    }

    // how it works
    const how = [];
    const keysDoc = args.map((p, i) => (i === 0 && subject ? "`h` (the " + p.name + ")" : "`" + p.name + "`")).join(", ");
    how.push(`Sends the host operation \`${op}\`${args.length ? " with " + keysDoc : ""}. A wrapped game object is sent as its handle.`);
    const ret = (f.returns || "").trim();
    if (!ret) how.push("The host answers with nothing to return. The call finishes when the game has done the work.");
    else if (/Rim(Pawn|Thing|Map|Faction)\b/.test(ret)) how.push(`The host answers with handles. RimKit wraps them, so you get ${/\[\]$/.test(ret) ? "a list of wrapped objects" : "a wrapped object"} you can pass straight into other functions.`);
    else if (classes.has(base(ret).replace(/\[\]$/, ""))) how.push(`The host answers with ${/\[\]$/.test(ret) ? "a list of tables" : "a table"} shaped like ${base(ret).replace(/\[\]$/, "")}. Fields marked optional are missing when they do not apply.`);
    else how.push("The host answers with a plain value, which arrives unchanged.");
    const errs = [];
    if (subject) errs.push(`\`RK2001\` when the ${subject.name} handle is stale or null`);
    if (args.some((p) => p.name === "def" || /Def$/.test(p.name))) errs.push("`RK3001` when a def name does not exist");
    if (args.some((p) => !p.optional && base(p.type) !== "any")) errs.push("`RK1001` for a missing or wrong argument");
    if (DLC.has(k.domain)) errs.push(`\`RK3003\` when the ${k.domain.charAt(0).toUpperCase() + k.domain.slice(1)} expansion is not active`);
    how.push(errs.length ? "A failed call raises a Lua error that starts with its code: " + errs.join(", ") + "." : "A failed call raises a Lua error that starts with an RK code.");
    how.push(`Tier: ${info.tier}${info.since ? ", since " + info.since : ""}. ${info.tier === "advanced" ? "Advanced functions can reach deep into the game, so they are gated by capabilities and settings." : "Allowed for every mod."}`);
    how.push(`In a test, replace the host with \`t.mock("${op}", answer)\`.`);

    fns[key] = {
      sig,
      doc: f.doc || "",
      op,
      page: k.page,
      tier: info.tier,
      params: args.map((p) => ({ n: p.name, t: p.type, o: p.optional, m: paramMeaning(p, subject && subject.name) })),
      returns: ret,
      how,
      raw: code,
      code: hljs.highlight(code, { language: "lua" }).value,
      custom: !generated,
    };
    opToFn[op] = key;
    count++;
  }
}

// operations with no kit function: older bindings. The Lua name comes from the alias table.
const aliasByOp = new Map();
for (const g of aliases.groups) {
  for (const entry of g.names) {
    const gt = entry.indexOf(">");
    const oldName = gt >= 0 ? entry.slice(0, gt) : entry;
    const newName = gt >= 0 ? entry.slice(gt + 1) : entry;
    const oldDomain = g.from.replace(/^rim\./, "");
    aliasByOp.set(`${oldDomain}.${oldName}`, newName.includes(".") ? `game.${newName}` : `${g.to}.${newName}`);
  }
}
const ops = {};
for (const [op, info] of opInfo) {
  if (opToFn[op]) continue;
  const luaName = aliasByOp.get(op) || null;
  const sample = luaName ? luaName + "(...)" : "";
  const howItems = [
    luaName ? `Called from Lua as \`${luaName}\`. This is an older binding: its argument list is in the [reference](/api/reference/).` : `Reached through \`rim.invoke("${op}", args)\`, or a native binding of the same name.`,
    `Tier: ${info.tier}${info.since ? ", since " + info.since : ""}.`,
    `In a test, replace the host with \`t.mock("${op}", answer)\`.`,
  ];
  const codeText = luaName ? `local result = ${sample}` : `local result = rim.invoke("${op}", { h = handle })`;
  ops[op] = { sig: luaName || op, doc: "", op, tier: info.tier, params: [], returns: "", how: howItems, raw: codeText, code: hljs.highlight(codeText, { language: "lua" }).value, custom: false };
}

const json = JSON.stringify({ fns, ops, opToFn }) + "\n";
if (process.argv.includes("--check")) {
  const same = fs.existsSync(out) && fs.readFileSync(out, "utf8") === json;
  console.log(`api details: ${count} functions, ${Object.keys(ops).length} other ops, ${same ? "up to date" : "OUT OF DATE, run node infrastructure/tools/gen-api-details.js"}`);
  process.exit(same ? 0 : 1);
}
fs.writeFileSync(out, json);
// every generated example, one block per domain, so the CLI can check the Lua parses
const check = {};
for (const [key, d] of Object.entries(fns)) (check[key.split(".")[1]] = check[key.split(".")[1]] || []).push(`do\n${d.raw}\nend`);
fs.writeFileSync(path.join(root, "infrastructure/docs-site/theme/.api-examples-check.json"), JSON.stringify(check));
console.log(`api details: wrote ${count} functions and ${Object.keys(ops).length} other ops (${(json.length / 1024).toFixed(0)} KB)`);
