#!/usr/bin/env node
// Checks documentation links, docs-site nav, dash rule, and generated reference freshness.
// Run: node infrastructure/tools/check-docs.js
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "../..");
process.chdir(root);

const docsDir = path.join("infrastructure", "docs");
const navPath = path.join("infrastructure", "docs-site", "nav.json");

function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => (e.isDirectory() ? walk(path.join(dir, e.name)) : [path.join(dir, e.name)]));
}

const pages = walk(docsDir).filter((f) => f.endsWith(".md"));
const files = [...pages, "README.md", "tests/README.md", "src/editor/README.md"];
const slug = (h) => h.toLowerCase().replace(/`/g, "").replace(/[^a-z0-9 -]/g, "").trim().replace(/ +/g, "-");
const stripCode = (t) => t.replace(/```[\s\S]*?```/g, "");

let problems = 0;
const report = (msg) => {
  console.log(msg);
  problems++;
};

let links = 0;
for (const f of files) {
  const text = stripCode(fs.readFileSync(f, "utf8"));
  for (const m of text.matchAll(/\]\(([^)\s]+)\)/g)) {
    const link = m[1];
    if (/^(https?:|mailto:|steam:)/.test(link)) continue;
    links++;
    const [p, anchor] = link.split("#");
    const target = p ? path.normalize(path.join(path.dirname(f), p)) : f;
    if (!fs.existsSync(target)) {
      report(`BROKEN LINK ${f}: ${link}`);
      continue;
    }
    if (anchor && target.endsWith(".md")) {
      const heads = [...stripCode(fs.readFileSync(target, "utf8")).matchAll(/^#{1,6} (.+)$/gm)].map((x) => slug(x[1]));
      if (!heads.includes(anchor)) report(`BROKEN ANCHOR ${f}: ${link}`);
    }
  }
}

function collectNavPaths(nodes, out = []) {
  for (const n of nodes || []) {
    if (n.path) out.push(n.path);
    if (n.children) collectNavPaths(n.children, out);
  }
  return out;
}
const nav = JSON.parse(fs.readFileSync(navPath, "utf8"));
const inNav = new Set(collectNavPaths(nav.tabs));
for (const rel of inNav) if (!fs.existsSync(path.join(docsDir, rel))) report(`NAV entry missing: ${rel}`);
for (const f of pages) {
  const rel = path.relative(docsDir, f).replace(/\\/g, "/");
  if (!inNav.has(rel)) report(`PAGE not in docs-site nav: ${rel}`);
}

for (const f of files) {
  const text = fs.readFileSync(f, "utf8");
  if (/[–—]/.test(text)) report(`DASH (em or en) in ${f}`);
}

// Lua examples only call game.* functions that exist (the same check the editor extension runs on a mod).
{
  const editorApi = require(path.join(root, "src", "editor", "api.js"));
  const known = editorApi.knownFunctions(path.join(root, "src", "editor", "stubs"));
  const guideFiles = fs.existsSync("guide") ? walk("guide").filter((f) => f.endsWith(".md")) : [];
  for (const f of [...pages, ...guideFiles]) {
    for (const m of fs.readFileSync(f, "utf8").matchAll(/```lua\n([\s\S]*?)```/g)) {
      for (const bad of editorApi.findUnknownCalls(m[1], known)) {
        const hint = editorApi.suggest(bad.name, known);
        report(`UNKNOWN CALL in ${f}: ${bad.name}${hint ? " (did you mean " + hint + "?)" : ""}`);
      }
    }
  }
}

// Every mod idea has a recipe in the cookbook: "**Name.**" at the start of a paragraph.
{
  const ideas = fs.readFileSync(path.join(docsDir, "mod-ideas.md"), "utf8");
  const cookbook = fs.readFileSync(path.join(docsDir, "guide", "cookbook.md"), "utf8");
  const names = [...ideas.matchAll(/^\| ([^|]+?) \| [^|]+\| [^|]+\| (?:Today|Kit) \|/gm)].map((m) => m[1].trim());
  for (const name of names) {
    if (!cookbook.includes(`**${name}.**`)) report(`COOKBOOK has no recipe for the mod idea "${name}"`);
  }
}

const refPath = path.join(docsDir, "api", "reference.md");
if (fs.existsSync(refPath)) {
  const before = fs.readFileSync(refPath, "utf8");
  require("child_process").execFileSync(process.execPath, [path.join("infrastructure", "tools", "gen-api-reference.js")], { stdio: "ignore" });
  const after = fs.readFileSync(refPath, "utf8");
  if (before !== after) report("infrastructure/docs/api/reference.md was out of date and has been regenerated, commit it");
}

// The generated API pages are current: types, host operations and the CLI reference.
for (const gen of ["gen-api-types.js", "gen-ops-doc.js", "gen-cli-doc.js", "gen-api-details.js"]) {
  try {
    require("child_process").execFileSync(process.execPath, [path.join("infrastructure", "tools", gen), "--check"], { stdio: "ignore" });
  } catch (e) {
    report(`generated page is out of date: run node infrastructure/tools/${gen}`);
  }
}

console.log(`check-docs: ${pages.length} pages, ${links} links, ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
