#!/usr/bin/env node
// Builds the kit table from the readable sources in src/api/kits/*.kit.
//   writes src/api/kits.json            read by the native binder, the editor completions and the docs tools
//   writes src/editor/stubs/rimkit_kits.lua   Lua language server definitions
// Run: node infrastructure/tools/build-kits.js [--check]
//
// Source format (one file per kit family):
//   @kit domain=maps op=map subject=map:RimMap title="Maps"      starts a kit. subject=<param name>:<type>, or subject=none
//   @class RimMapInfo                                          starts a class for the stubs
//   id: integer | The map id                                   a class field ("name: type | doc", "name?: type | doc")
//   terrain(x: integer, z: integer) -> string | Terrain def name at a cell.
//   set_roof(x: integer, z: integer, def?: string) -> boolean | ...
//   spawn_at!(def: string) -> RimThing | ...                   "!" after the name: this function has no subject argument
// Argument types that are not primitives or wrapped game objects are sent to the host as a JSON table.
const fs = require("fs");
const path = require("path");
const { scanOps } = require("./lib-ops");

const root = path.join(__dirname, "../..");
const srcDir = path.join(root, "src", "api", "kits");
const files = fs.existsSync(srcDir) ? fs.readdirSync(srcDir).filter((f) => f.endsWith(".kit")).sort() : [];

const PLAIN = new Set(["string", "integer", "number", "boolean", "any", "RimPawn", "RimThing", "RimMap", "RimFaction", "RimEntity", "RimObject"]);
const isPlain = (t) => PLAIN.has(t) || /Ref$/.test(t) || /\|/.test(t) && t.split("|").every((p) => PLAIN.has(p.trim()) || /Ref$/.test(p.trim()));

const LUA_KEYWORDS = new Set(["and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while"]);
let problems = 0;
const fail = (file, n, msg) => {
  console.log(`build-kits: ${file}:${n}: ${msg}`);
  problems++;
};

const kits = [];
const classes = [];
let kit = null;
let cls = null;
const seen = new Set();

function splitArgs(text) {
  const out = [];
  let depth = 0;
  let cur = "";
  for (const ch of text) {
    if ((ch === "{" || ch === "<" || ch === "(") ) depth++;
    if ((ch === "}" || ch === ">" || ch === ")")) depth--;
    if (ch === "," && depth === 0) {
      out.push(cur.trim());
      cur = "";
    } else cur += ch;
  }
  if (cur.trim()) out.push(cur.trim());
  return out;
}

for (const file of files) {
  const lines = fs.readFileSync(path.join(srcDir, file), "utf8").replace(/\r/g, "").split("\n");
  lines.forEach((raw, i) => {
    const n = i + 1;
    const line = raw.trim();
    if (!line || line.startsWith("#")) return;
    let m;
    if ((m = /^@kit\s+(.*)$/.exec(line))) {
      const attrs = {};
      for (const a of m[1].matchAll(/(\w+)=("([^"]*)"|\S+)/g)) attrs[a[1]] = a[3] !== undefined ? a[3] : a[2];
      if (!attrs.domain || !attrs.op) return fail(file, n, "@kit needs domain= and op=");
      let subject = null;
      if (attrs.subject && attrs.subject !== "none") {
        const [name, type] = attrs.subject.split(":");
        subject = { name, type: type || "any" };
      }
      kit = { domain: attrs.domain, op: attrs.op, title: attrs.title || attrs.domain, page: attrs.page || attrs.domain, subject, fns: [] };
      kits.push(kit);
      cls = null;
      return;
    }
    if ((m = /^@class\s+(\w+)(?:\s*:\s*(\w+))?\s*(?:\|\s*(.*))?$/.exec(line))) {
      cls = { name: m[1], parent: m[2] || "", doc: m[3] || "", fields: [] };
      classes.push(cls);
      kit = null;
      return;
    }
    if ((m = /^@alias\s+(\w+)\s+(.+)$/.exec(line))) {
      classes.push({ alias: m[1], target: m[2] });
      return;
    }
    if (cls) {
      if ((m = /^(\w+)(\?)?\s*:\s*([^|]+?)\s*(?:\|\s*(.*))?$/.exec(line))) {
        cls.fields.push({ name: m[1], optional: !!m[2], type: m[3], doc: m[4] || "" });
        return;
      }
      return fail(file, n, "bad class field: " + line);
    }
    if (kit) {
      m = /^(\w+)(!)?\(([^)]*(?:\([^)]*\)[^)]*)*)\)\s*(?:->\s*([^|]+?))?\s*(?:\|\s*(.*))?$/.exec(line);
      if (!m) return fail(file, n, "bad function line: " + line);
      const [, name, free, argText, returns, doc] = m;
      const key = kit.domain + "." + name;
      if (LUA_KEYWORDS.has(name)) return fail(file, n, `${name} is a Lua keyword and cannot be a function name`);
      if (seen.has(key)) return fail(file, n, "duplicate " + key);
      seen.add(key);
      const args = splitArgs(argText).map((a) => {
        const am = /^(\w+)(\?)?\s*(?::\s*(.+))?$/.exec(a);
        if (!am) {
          fail(file, n, "bad argument: " + a);
          return { name: "arg", optional: false, type: "any" };
        }
        return { name: am[1], optional: !!am[2], type: (am[3] || "any").trim() };
      });
      kit.fns.push({ name, subject: free ? false : kit.subject !== null, args, returns: (returns || "").trim(), doc: (doc || "").trim() });
      return;
    }
    fail(file, n, "line outside a @kit or @class: " + line);
  });
}

// kits.json: what the native binder needs plus what the docs and the editor show.
const json = {
  generated: "Generated by infrastructure/tools/build-kits.js from src/api/kits/*.kit. Do not edit by hand.",
  // Every host op name, so the test mocks can reject a mistyped one.
  ops: scanOps(root),
  kits: kits.map((k) => ({
    domain: k.domain,
    op: k.op,
    title: k.title,
    page: k.page,
    subject: k.subject !== null,
    fns: k.fns.map((f) => ({
      name: f.name,
      subject: f.subject,
      keys: f.args.map((a) => a.name + (a.optional ? "?" : "") + (/^(function|fun\()/.test(a.type) ? ":function" : isPlain(a.type) ? "" : ":table")),
      // Parameters as the editor shows them, subject first.
      params: (f.subject ? [{ name: k.subject.name, type: k.subject.type }] : []).concat(f.args.map((a) => ({ name: a.name, type: a.type, optional: a.optional || undefined }))),
      returns: f.returns,
      doc: f.doc,
    })),
  })),
};
const jsonText = JSON.stringify(json, null, 1) + "\n";

// Lua stubs.
const out = ["---@meta", "-- Generated by infrastructure/tools/build-kits.js from src/api/kits/*.kit. Do not edit by hand.", ""];
for (const c of classes) {
  if (c.alias) {
    out.push(`---@alias ${c.alias} ${c.target}`, "");
    continue;
  }
  if (c.doc) out.push(`--- ${c.doc}`);
  out.push(`---@class ${c.name}${c.parent ? " : " + c.parent : ""}`);
  for (const f of c.fields) out.push(`---@field ${f.name}${f.optional ? "?" : ""} ${f.type}${f.doc ? " " + f.doc : ""}`);
  out.push("");
}
for (const k of kits) {
  out.push(`game.${k.domain} = game.${k.domain} or {}`, "");
  for (const f of k.fns) {
    if (f.doc) out.push(`--- ${f.doc}`);
    const params = [];
    if (f.subject) params.push({ name: k.subject.name, type: k.subject.type, optional: false });
    params.push(...f.args);
    for (const p of params) out.push(`---@param ${p.name}${p.optional ? "?" : ""} ${p.type}`);
    if (f.returns) out.push(`---@return ${f.returns}`);
    out.push(`function game.${k.domain}.${f.name}(${params.map((p) => p.name).join(", ")}) end`, "");
  }
}
const luaText = out.join("\n");

const jsonPath = path.join(root, "src", "api", "kits.json");
const luaPath = path.join(root, "src", "editor", "stubs", "rimkit_kits.lua");
// Native test input: every function the table promises must exist as a Lua function.
const expectLines = [
  "-- Generated by infrastructure/tools/build-kits.js. Do not edit by hand.",
  "local missing = {}",
  "local function need(domain, name)",
  "  local d = game[domain]",
  '  if type(d) ~= "table" or type(d[name]) ~= "function" then missing[#missing + 1] = domain .. "." .. name end',
  "end",
];
for (const k of kits) for (const fn of k.fns) expectLines.push(`need("${k.domain}", "${fn.name}")`);
const total = kits.reduce((n, k) => n + k.fns.length, 0);
expectLines.push('if #missing > 0 then error("missing kit functions: " .. table.concat(missing, ", ")) end', `log.info("KITS OK ${total}")`, "");
const expectText = expectLines.join("\n");
const expectPath = path.join(root, "tests", "native", "kits_expect.lua");
const norm = (s) => s.replace(/\r/g, "");
if (process.argv.includes("--check")) {
  const same = fs.existsSync(jsonPath) && norm(fs.readFileSync(jsonPath, "utf8")) === jsonText && fs.existsSync(luaPath) && norm(fs.readFileSync(luaPath, "utf8")) === luaText && fs.existsSync(expectPath) && norm(fs.readFileSync(expectPath, "utf8")) === expectText;
  if (!same) {
    console.log("build-kits: generated files are out of date. Run node infrastructure/tools/build-kits.js");
    problems++;
  }
} else if (!problems) {
  fs.writeFileSync(jsonPath, jsonText);
  fs.writeFileSync(path.join(root, "src", "editor", "kits.json"), jsonText);
  fs.writeFileSync(luaPath, luaText);
  fs.writeFileSync(expectPath, expectText);
}
const fnCount = kits.reduce((n, k) => n + k.fns.length, 0);
console.log(`build-kits: ${kits.length} kits, ${fnCount} functions, ${classes.length} types, ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
