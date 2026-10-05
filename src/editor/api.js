// Pure helpers for the RimKit editor extension: the kit table, the list of known game.* functions, and checks.
// No vscode dependency, so infrastructure/tools/check-editor.js can test them with node.
const fs = require("fs");
const path = require("path");

function loadKits(dir) {
  try {
    return JSON.parse(fs.readFileSync(path.join(dir, "kits.json"), "utf8")).kits || [];
  } catch (e) {
    return [];
  }
}

// "game.maps.terrain(map, x, z)" style signature for a kit function.
function signature(kit, fn) {
  const params = (fn.params || []).map((p) => (p.optional ? p.name + "?" : p.name));
  return `game.${kit.domain}.${fn.name}(${params.join(", ")})`;
}

// Snippet text for a completion: required parameters become tab stops.
function snippet(kit, fn) {
  const params = (fn.params || []).filter((p) => !p.optional);
  const stops = params.map((p, i) => `\${${i + 1}:${p.name}}`);
  return `game.${kit.domain}.${fn.name}(${stops.join(", ")})`;
}

// Markdown shown in hovers and completion details.
function describe(kit, fn) {
  const lines = ["```lua", signature(kit, fn) + (fn.returns ? " -> " + fn.returns : ""), "```"];
  if (fn.doc) lines.push(fn.doc);
  const typed = (fn.params || []).filter((p) => p.type && p.type !== "any");
  if (typed.length) lines.push("", typed.map((p) => `- \`${p.name}\`${p.optional ? " (optional)" : ""}: \`${p.type}\``).join("\n"));
  return lines.join("\n");
}

function kitCompletions(kits) {
  const items = [];
  for (const kit of kits) {
    for (const fn of kit.fns) {
      items.push({
        label: `game.${kit.domain}.${fn.name}`,
        detail: `${signature(kit, fn).replace(/^game\.\w+\./, "")}${fn.returns ? " -> " + fn.returns : ""}`,
        insert: snippet(kit, fn),
        documentation: describe(kit, fn),
      });
    }
  }
  return items;
}

// Every `function game.domain.name(` declared in the stub files, so a call can be checked.
function knownFunctions(stubsDir) {
  const known = new Set();
  let files = [];
  try {
    files = fs.readdirSync(stubsDir).filter((f) => f.endsWith(".lua"));
  } catch (e) {
    return known;
  }
  for (const f of files) {
    const text = fs.readFileSync(path.join(stubsDir, f), "utf8");
    for (const m of text.matchAll(/^function game\.(\w+)\.(\w+)\s*\(/gm)) known.add(`${m[1]}.${m[2]}`);
  }
  return known;
}

// Calls to game.<domain>.<name>( where the domain is known but the function is not. Domains the stubs do not
// know at all are left alone, because a mod can define its own tables.
function findUnknownCalls(text, known) {
  const domains = new Set([...known].map((k) => k.split(".")[0]));
  const stripped = text.replace(/--\[(=*)\[[\s\S]*?\]\1\]/g, (m) => m.replace(/[^\n]/g, " ")).replace(/--[^\n]*/g, (m) => " ".repeat(m.length));
  const found = [];
  for (const m of stripped.matchAll(/(?<![\w.:])game\.(\w+)\.(\w+)\s*\(/g)) {
    if (domains.has(m[1]) && !known.has(`${m[1]}.${m[2]}`)) {
      found.push({ start: m.index, end: m.index + m[0].length - (m[0].length - m[0].replace(/\s*\($/, "").length), name: `game.${m[1]}.${m[2]}` });
    }
  }
  return found;
}

// Closest known function name, for "did you mean".
function suggest(name, known) {
  const [domain, fn] = name.replace(/^game\./, "").split(".");
  let best = null;
  let bestScore = 3;
  for (const k of known) {
    const [d, f] = k.split(".");
    if (d !== domain) continue;
    const score = distance(fn, f);
    if (score < bestScore) {
      best = `game.${k}`;
      bestScore = score;
    }
  }
  return best;
}

function distance(a, b) {
  const dp = Array.from({ length: a.length + 1 }, (_, i) => [i, ...Array(b.length).fill(0)]);
  for (let j = 1; j <= b.length; j++) dp[0][j] = j;
  for (let i = 1; i <= a.length; i++) for (let j = 1; j <= b.length; j++) dp[i][j] = Math.min(dp[i - 1][j] + 1, dp[i][j - 1] + 1, dp[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
  return dp[a.length][b.length];
}

module.exports = { loadKits, signature, snippet, describe, kitCompletions, knownFunctions, findUnknownCalls, suggest };
