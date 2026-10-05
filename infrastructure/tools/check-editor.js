#!/usr/bin/env node
// Editor extension checks that need no VS Code: the kit completions cover every kit function, every function in
// the stubs is known, unknown calls are found, and the packaged files agree with the sources.
// Run: node infrastructure/tools/check-editor.js
const fs = require("fs");
const path = require("path");
const api = require(path.join(__dirname, "../../src/editor/api.js"));

const root = path.join(__dirname, "../..");
const editor = path.join(root, "src", "editor");
let problems = 0;
const fail = (m) => {
  console.log("editor: " + m);
  problems++;
};

const kits = api.loadKits(editor);
if (!kits.length) fail("src/editor/kits.json is missing or empty (build the native core, or copy src/api/kits.json)");
const source = fs.readFileSync(path.join(root, "src/api/kits.json"), "utf8");
if (fs.existsSync(path.join(editor, "kits.json")) && fs.readFileSync(path.join(editor, "kits.json"), "utf8") !== source) fail("src/editor/kits.json differs from src/api/kits.json");

const known = api.knownFunctions(path.join(editor, "stubs"));
const items = api.kitCompletions(kits);
let fns = 0;
for (const kit of kits) {
  for (const fn of kit.fns) {
    fns++;
    if (!known.has(`${kit.domain}.${fn.name}`)) fail(`stub missing for game.${kit.domain}.${fn.name}`);
    const label = `game.${kit.domain}.${fn.name}`;
    const item = items.find((i) => i.label === label);
    if (!item) fail(`no completion for ${label}`);
    else if (!item.documentation.includes(fn.doc)) fail(`completion for ${label} lacks its description`);
  }
}

// Unknown call detection.
const bad = api.findUnknownCalls("game.maps.terain(map, 1, 2)\n-- game.maps.nothing()\nlocal x = mymod.maps.q()\ngame.maps.terrain(map, 1, 2)\n", known);
if (bad.length !== 1 || bad[0].name !== "game.maps.terain") fail("unknown call detection is wrong: " + JSON.stringify(bad));
if (api.suggest("game.maps.terain", known) !== "game.maps.terrain") fail("did-you-mean did not suggest game.maps.terrain");

// Def name completion contexts, go to definition and templates (Phase 6).
const eco = require(path.join(editor, "api_ecosystem.js"));
const ctx = [
  ['game.things.spawn_at("St', "ThingDef"],
  ['game.pawns.give_hediff(p, "Fl', "HediffDef"],
  ['game.defs.set("ThingDef", "St', "ThingDef"],
  ['game.defs.set("StatDef", "Mar', "StatDef"],
  ['game.incidents.fire("Raid', "IncidentDef"],
  ["game.maps.terrain(map, ", null],
  ['local x = "Steel', null],
];
for (const [text, kind] of ctx) if (eco.defKindForContext(text) !== kind) fail(`def kind for ${JSON.stringify(text)} should be ${kind}, got ${eco.defKindForContext(text)}`);
const stubText = fs.readFileSync(path.join(editor, "stubs", "rimkit.lua"), "utf8");
if (eco.stubLine(stubText, "game.interop.publish") < 0) fail("go to definition cannot find game.interop.publish in the stubs");
if (eco.stubLine(stubText, "game.nothing.here") !== -1) fail("go to definition found a function that does not exist");
const tpl = require(path.join(editor, "templates.js"));
for (const t of tpl.templates) {
  for (const [rel, text] of Object.entries(t.files)) if (/\{\{(?!name|id)/.test(text)) fail(`template ${t.id} ${rel} has an unknown placeholder`);
  if (!tpl.fill("{{name}}-{{id}}", "My", "my").startsWith("My-my")) fail("template fill is wrong");
}
const mainFiles = ["extension.js", "ecosystem.js"].map((f) => fs.readFileSync(path.join(editor, f), "utf8"));
const pkgJson = JSON.parse(fs.readFileSync(path.join(editor, "package.json"), "utf8"));
for (const cmd of pkgJson.contributes.commands.map((c) => c.command)) {
  if (!mainFiles.some((t) => t.includes(`"${cmd}"`))) fail(`command ${cmd} is declared in package.json but never registered`);
}

// package.json is in step with the version source.
const version = fs.readFileSync(path.join(root, "src/api/VERSION"), "utf8").trim();
const pkg = JSON.parse(fs.readFileSync(path.join(editor, "package.json"), "utf8"));
if (pkg.version !== version) fail(`package.json version ${pkg.version} differs from src/api/VERSION ${version}`);

console.log(`editor: ${fns} kit functions, ${known.size} known functions, ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
