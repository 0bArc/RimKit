#!/usr/bin/env node
// Rewrites the event catalog table in infrastructure/docs/api/events.md from the Add(...) rows in src/host/EventCatalog*.cs.
// Run: node infrastructure/tools/gen-events-doc.js [--check]
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "../..");
const files = ["src/host/EventCatalog.cs", "src/host/EventCatalogMore.cs"];
const rows = [];
for (const f of files) {
  const text = fs.readFileSync(path.join(root, f), "utf8");
  for (const m of text.matchAll(/\bAdd\(\s*"([a-z_.]+)",\s*"((?:[^"\\]|\\.)*)",\s*(true|false)/g)) {
    const [, name, desc, hot] = m;
    const pm = /Payload: ([^.]*)\./.exec(desc);
    const payload = pm ? pm[1].split(",").map((p) => "`" + p.trim() + "`").join(", ") : "none";
    const summary = desc.replace(/\s*Payload: [^.]*\./, "").replace(/\s*Hot\./, "").trim();
    rows.push({ name, summary, payload, hot: hot === "true" });
  }
}

const table = ["| Event | Payload | Hot | When |", "|-------|---------|-----|------|", ...rows.map((r) => `| \`${r.name}\` | ${r.payload} | ${r.hot ? "yes" : "no"} | ${r.summary.replace(/\|/g, "\\|")} |`)].join("\n");
const file = path.join(root, "infrastructure/docs/api/events.md");
const text = fs.readFileSync(file, "utf8").replace(/\r/g, "");
const start = text.indexOf("| Event | Payload | Hot");
const end = text.indexOf("\n\nNaming:", start);
if (start < 0 || end < 0) {
  console.log("events doc: could not find the catalog table in events.md");
  process.exit(1);
}

const next = text.slice(0, start) + table + text.slice(end);
if (process.argv.includes("--check")) {
  if (next !== text) {
    console.log("events doc: events.md is out of date. Run node infrastructure/tools/gen-events-doc.js");
    process.exit(1);
  }
  console.log(`events doc: ${rows.length} events up to date`);
} else {
  fs.writeFileSync(file, next);
  console.log(`events doc: wrote ${rows.length} events`);
}
