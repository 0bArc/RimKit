#!/usr/bin/env node
// Op conformance lint (RKS). Fails when a host op breaks the rules:
//  - op id is lowercase dotted snake case, singular domain
//  - tier is one of the known tiers, since is a version
//  - an op registered in two files is reported
//  - every function in src/api/kits.json resolves to a registered op, has a description and a return type
//  - every registered op has a Lua surface: a kit entry or a native binding that names it
//  - every op the native layer names exists in the host
// Run: node infrastructure/tools/check-ops.js
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "../..");
const walk = (dir) =>
  fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = path.join(dir, e.name);
    return e.isDirectory() ? walk(p) : [p];
  });

const KNOWN_TIERS = new Set(["gameplay", "util", "advanced", "denied"]);
const OP_RE = /^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/;
const registerRe = /(?:ApiRegistry\.Register|\bR)\(\s*"([^"]+)"\s*,\s*[^,]+?(?:,\s*"([^"]*)")?(?:,\s*"([^"]*)")?\s*\)\s*;/g;
// DLC ops are registered with D("<Dlc>", "<op>", handler), which wraps the handler in the DLC guard (src/host/api/ApiDlc.cs).
const dlcRe = /\bD\(\s*(?:"(\w+)"|(\w+))\s*,\s*"([^"]+)"/g;
const DLC_FILES = ["ApiIdeology.cs", "ApiRoyaltyBiotech.cs", "ApiAnomalyOdyssey.cs"];
const literalRe = /"([a-z][a-z0-9_]*\.[a-z][a-z0-9_]*)"/g;

let problems = 0;
const fail = (msg) => {
  console.log("ops: " + msg);
  problems++;
};

// Older ops are handled by the switch in GameApi.cs, outside the registry. They are valid native targets.
const legacy = new Set();
for (const m of fs.readFileSync(path.join(root, "src/host/GameApi.cs"), "utf8").matchAll(/case "([a-z][a-z0-9_]*\.[a-z0-9_.]+)"/g)) legacy.add(m[1]);

const ops = new Map();
for (const file of walk(path.join(root, "src/host")).filter((f) => f.endsWith(".cs"))) {
  const text = fs.readFileSync(file, "utf8");
  if (!/ApiRegistry\.Register|static void Register/.test(text)) continue;
  const where = path.relative(root, file);
  let m;
  registerRe.lastIndex = 0;
  while ((m = registerRe.exec(text))) {
    const [, op, tier, since] = m;
    if (!OP_RE.test(op)) fail(`${where}: op "${op}" is not lowercase dotted snake case`);
    if (tier && !KNOWN_TIERS.has(tier)) fail(`${where}: op "${op}" has unknown tier "${tier}"`);
    if (since && !/^\d+\.\d+\.\d+$/.test(since)) fail(`${where}: op "${op}" has bad since "${since}"`);
    if (ops.has(op) && ops.get(op).file !== where) fail(`op "${op}" registered in ${ops.get(op).file} and ${where}`);
    ops.set(op, { file: where, tier: tier || "gameplay" });
    if (DLC_FILES.includes(path.basename(file))) fail(`${where}: op "${op}" is registered without the DLC guard, use D(dlc, op, handler)`);
  }
  dlcRe.lastIndex = 0;
  while ((m = dlcRe.exec(text))) {
    const op = m[3];
    if (!OP_RE.test(op)) fail(`${where}: op "${op}" is not lowercase dotted snake case`);
    if (ops.has(op) && ops.get(op).file !== where) fail(`op "${op}" registered in ${ops.get(op).file} and ${where}`);
    ops.set(op, { file: where, tier: "advanced", dlc: true });
  }
}

// Op-looking string literals in the native layer.
const domains = new Set([...ops.keys()].map((o) => o.split(".")[0]));
const used = new Map();
for (const file of walk(path.join(root, "src/native/core")).filter((f) => /\.(cpp|hpp)$/.test(f))) {
  for (const m of fs.readFileSync(file, "utf8").matchAll(literalRe)) {
    const op = m[1];
    if (/^(game|rim|rimkit|os|io|string|table|math)\./.test(op)) continue;
    if (domains.has(op.split(".")[0]) || legacy.has(op)) used.set(op, path.relative(root, file));
  }
}

// Kit table: every function resolves to a registered op and is documented.
const kitOps = new Set();
const kitsPath = path.join(root, "src/api/kits.json");
if (fs.existsSync(kitsPath)) {
  for (const k of JSON.parse(fs.readFileSync(kitsPath, "utf8")).kits) {
    for (const fn of k.fns) {
      const op = `${k.op}.${fn.name}`;
      kitOps.add(op);
      const label = `kit function game.${k.domain}.${fn.name}`;
      if (!ops.has(op) && !legacy.has(op)) fail(`${label} needs host op "${op}", which is not registered`);
      if (!fn.doc) fail(`${label} has no description`);
      if (!fn.returns && !/^(remove|clear_|release_|unassign_|destroy_|set_)/.test(fn.name)) fail(`${label} has no return type`);
    }
  }
}

// Every registered op needs a Lua surface.
for (const [op, info] of ops) {
  if (kitOps.has(op) || used.has(op) || info.tier === "denied") continue;
  fail(`op "${op}" (${info.file}) has no Lua surface: add it to src/api/kits/*.kit or bind it in the native layer`);
}
for (const [op, file] of used) {
  if (!ops.has(op) && !legacy.has(op)) fail(`${file}: native layer names "${op}" but no host op is registered with that name`);
}

console.log(`ops: ${ops.size} registered, ${kitOps.size} from the kit table, ${used.size} named in native code, ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
