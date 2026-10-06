const vscode = require("vscode");
const path = require("path");
const fs = require("fs");
const { spawn } = require("child_process");
const api = require("./api");
const ecosystem = require("./ecosystem");

const RIM_COMPLETIONS = [
  { label: "rim.log", detail: "Log to Player.log", insert: 'rim.log("${1:msg}")' },
  { label: "rim.message", detail: "In-game message", insert: 'rim.message("${1:msg}")' },
  { label: "rim.on_load", detail: "After scripts load / play start", insert: "rim.on_load(function()\n\t$0\nend)" },
  { label: "rim.on_tick", detail: "Periodic tick", insert: "rim.on_tick(function()\n\t$0\nend)" },
  { label: "rim.invoke", detail: "Escape only: api.list / reflect.*", insert: 'rim.invoke("${1|api.list,reflect.get|}"${2:, { $3 }})' },
  { label: "game.hooks.before", detail: 'game.hooks.before["Type"].Method = fn', insert: 'game.hooks.before["${1:RimWorld.JobGiver_GetFood}"].${2:TryGiveJob} = function(ctx)\n\tlocal pawn = ctx.pawn or ctx.instance\n\t$0\n\treturn true\nend' },
  { label: "game.hooks.after", detail: 'game.hooks.after["Type"].Method = fn', insert: 'game.hooks.after["${1:Type}"].${2:Method} = function(ctx)\n\t$0\nend' },
  { label: "rim.events.postfix", detail: 'rim.events.postfix["Type.Method"] = fn', insert: 'rim.events.postfix["${1:Type.Method}"] = function(ctx)\n\t$0\nend' },
  { label: "rim.hooks.prefix", detail: "rim.hooks.prefix(type, method, fn [, opts])", insert: 'rim.hooks.prefix("${1:Type}", "${2:Method}", function(ctx)\n\t$0\n\treturn true\nend)' },
  { label: "rim.hooks.postfix", detail: "rim.hooks.postfix(type, method, fn [, opts])", insert: 'rim.hooks.postfix("${1:Type}", "${2:Method}", function(ctx)\n\t$0\nend)' },
  { label: "rim.hooks.finalizer", detail: "Runs even when the method throws", insert: 'rim.hooks.finalizer("${1:Type}", "${2:Method}", function(ctx)\n\t$0\nend)' },
  { label: "rim.hooks.patch", detail: "Prefix, postfix, finalizer, overload sig, priority (Advanced)", insert: 'rim.hooks.patch{\n\ttype = "${1:Verse.Thing}", method = "${2:TakeDamage}", sig = { "${3:Verse.DamageInfo}" },\n\tprefix = function(ctx)\n\t\t$0\n\tend,\n}' },
  { label: "rim.hooks.replace_call", detail: "Replace one call inside a method body with Lua (Advanced)", insert: 'rim.hooks.replace_call{\n\ttype = "${1:Type}", method = "${2:Method}", call = "${3:Verse.Rand.Range}",\n\tfn = function(ctx)\n\t\treturn $0\n\tend,\n}' },
  { label: "rim.hooks.remove", insert: "rim.hooks.remove(${1:hookId})" },
  { label: "rim.hooks.list", insert: "rim.hooks.list()" },
  { label: "events.on", detail: 'Named event with payload, e.g. "pawn.died" (Experimental)', insert: 'events.on("${1|pawn.died,pawn.spawned,thing.spawned,thing.damaged,hediff.added,job.started,game.loaded,incident.fired,research.finished|}", function(e)\n\t$0\nend)' },
  { label: "events.off", insert: 'events.off("${1:name}")' },
  { label: "events.list", detail: "All named events and whether installed", insert: "events.list()" },
  { label: "ctx:set_result", detail: "Hook context: set return value", insert: "ctx:set_result(${1:value})" },
  { label: "ctx:skip", detail: "Hook context: skip the original (prefix)", insert: "ctx:skip(${1})" },
  { label: "ctx:set_arg", detail: "Hook context: replace argument", insert: "ctx:set_arg(${1:1}, ${2:value})" },
  { label: "ctx:set_state", insert: "ctx:set_state(${1:value})" },
  { label: "ctx:suppress", detail: "Finalizer: swallow exception", insert: "ctx:suppress()" },
  { label: "rim.pawn.is_humanlike", insert: "rim.pawn.is_humanlike(${1:h})" },
  { label: "rim.pawn.faction_is_player", insert: "rim.pawn.faction_is_player(${1:h})" },
  { label: "rim.pawn.hunger", insert: "rim.pawn.hunger(${1:h})" },
  { label: "rim.pawn.map", insert: "rim.pawn.map(${1:h})" },
  { label: "rim.pawn.name", insert: "rim.pawn.name(${1:h})" },
  { label: "rim.pawn.make_controllable", detail: "Draftable control (animals/entities)", insert: "rim.pawn.make_controllable(${1:h})" },
  { label: "rim.pawn.release_control", insert: "rim.pawn.release_control(${1:h})" },
  { label: "rim.pawn.is_controllable", insert: "rim.pawn.is_controllable(${1:h})" },
  { label: "rim.map.nutrition", insert: "rim.map.nutrition(${1:map})" },
  { label: "rim.find.current_map", insert: "rim.find.current_map()" },
  { label: "rim.find.selected", insert: "rim.find.selected()" },
  { label: "game.reflect.static_call", detail: "Typed reflection (Advanced, needs developer_reflect)", insert: 'game.reflect.static_call("${1:Verse.GenText}", "${2:CapitalizeFirst}", ${3:"text"})' },
  { label: "game.reflect.get", insert: 'game.reflect.get(${1:obj}, "${2:Member}")' },
  { label: "game.reflect.set", insert: 'game.reflect.set(${1:obj}, "${2:Member}", ${3:value})' },
  { label: "game.reflect.call", insert: 'game.reflect.call(${1:obj}, "${2:Method}"${3})' },
  { label: "game.reflect.new", insert: 'game.reflect.new("${1:Verse.IntVec3}", ${2:0}, ${3:0}, ${4:0})' },
  { label: "game.reflect.members", insert: 'game.reflect.members("${1:Verse.TickManager}")' },
  { label: "game.pawns.skills", detail: "Skills with level, passion and xp (Experimental)", insert: "game.pawns.skills(${1:pawn})" },
  { label: "game.pawns.set_passion", insert: 'game.pawns.set_passion(${1:pawn}, "${2:Shooting}", "${3|Major,Minor,None|}")' },
  { label: "game.pawns.add_trait", detail: "Trait with degree", insert: 'game.pawns.add_trait(${1:pawn}, "${2:Beauty}", ${3:2})' },
  { label: "game.pawns.needs", insert: "game.pawns.needs(${1:pawn})" },
  { label: "game.pawns.set_need", insert: 'game.pawns.set_need(${1:pawn}, "${2:Food}", ${3:0.5})' },
  { label: "game.pawns.add_thought", insert: 'game.pawns.add_thought(${1:pawn}, "${2:Catharsis}")' },
  { label: "game.pawns.relations", insert: "game.pawns.relations(${1:pawn})" },
  { label: "game.pawns.timetable", insert: "game.pawns.timetable(${1:pawn})" },
  { label: "game.pawns.genes", detail: "Biotech only", insert: "game.pawns.genes(${1:pawn})" },
  { label: "game.ui.translate", detail: "Keyed language string", insert: 'game.ui.translate("${1:Key}"${2})' },
  { label: "rim.reflect.get", insert: 'rim.reflect.get(${1:h}, "${2:Member}")' },
  { label: "rim.reflect.call", insert: 'rim.reflect.call(${1:h}, "${2:Method}", "${3:args}")' },
  { label: "rim.wrap_entity", detail: "Wrap handle as RimEntity", insert: "rim.wrap_entity(${1:h})" },
  { label: "require(\"rimkit\")", detail: "API contract: version, api_level, tiers", insert: 'local rk = require("rimkit")\nrk.assert_api(0)\n$0' },
  { label: "game.anomalies:get", detail: "First entity by kind/def (Experimental)", insert: 'game.anomalies:get("${1:Revenant}")' },
  { label: "game.anomalies:list", detail: "Entity list on map", insert: "game.anomalies:list()" },
  { label: "game.anomalies:find", detail: "First entity matching predicate", insert: "game.anomalies:find(function(e)\n\treturn $0\nend)" },
  { label: "anomaly.get_on_map", insert: 'anomaly.get_on_map("${1:Revenant}")' },
  { label: "anomaly.is_entity", detail: "True for Anomaly entities", insert: "anomaly.is_entity(${1:h})" },
  { label: "anomaly.recruit", detail: "Make controllable (dog command)", insert: "anomaly.recruit(${1:h})" },
  { label: "anomaly.release_to_hostile", insert: "anomaly.release_to_hostile(${1:h})" },
  { label: "anomaly.list_on_map", insert: "anomaly.list_on_map(${1:map})" },
  { label: "anomaly.knock_out", insert: "anomaly.knock_out(${1:h})" },
  { label: "anomaly.find_platform", insert: "anomaly.find_platform(${1:hauler}, ${2:entity})" },
  { label: "anomaly.start_capture", insert: "anomaly.start_capture(${1:hauler}, ${2:entity}, ${3:platform})" },
  { label: "anomaly.dlc_active", insert: "anomaly.dlc_active()" },
  { label: "ui.on_map_float_menu", detail: "Lua right-click options (human hauler only)", insert: "ui.on_map_float_menu(function(ctx)\n\t$0\n\treturn nil\nend)" },
  { label: "ui.panel", detail: "Simple panel window", insert: 'ui.panel({ title = "${1:Title}", body = "${2}", checks = {}, list = {} })' },
  { label: "data.get", insert: 'data.get("${1:package_id}", "${2:key}")' },
  { label: "data.set", insert: 'data.set("${1:package_id}", "${2:key}", "${3:value}")' },
  { label: "health.has_hediff", insert: 'health.has_hediff(${1:h}, "${2:def}")' },
  { label: "building.set_power", insert: "building.set_power(${1:h}, ${2:true})" },
  { label: "work.set_priority", insert: 'work.set_priority(${1:h}, "${2:workType}", ${3:3})' },
  { label: "incident.try_fire", insert: 'incident.try_fire("${1:def}")' },
  { label: "audio.play", insert: 'audio.play("${1:soundDef}")' },
  { label: "input.binding_just_pressed", insert: 'input.binding_just_pressed("${1:DefName}")' },
];

function findModRoot(filePath) {
  let dir = path.dirname(filePath);
  for (let i = 0; i < 8; i++) {
    if (fs.existsSync(path.join(dir, "meta.lua"))) {
      return dir;
    }
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

function runRimkit(args, cwd) {
  const cfg = vscode.workspace.getConfiguration("rimkit");
  const exe = cfg.get("executable") || "rimkit";
  const modsDir = cfg.get("modsDir") || "";
  const fullArgs = [...args];
  if (modsDir && args[0] === "mod" && args[1] === "ship" && args.length === 3) {
    fullArgs.push(modsDir);
  }
  return new Promise((resolve) => {
    const child = spawn(exe, fullArgs, { cwd, shell: true });
    let out = "";
    let err = "";
    child.stdout.on("data", (d) => (out += d.toString()));
    child.stderr.on("data", (d) => (err += d.toString()));
    child.on("close", (code) => resolve({ code, out, err }));
    child.on("error", (e) => resolve({ code: 1, out, err: String(e) }));
  });
}

async function shipMod(modRoot, silent) {
  const r = await runRimkit(["mod", "ship", modRoot], modRoot);
  const msg = (r.out || r.err || "").trim() || `exit ${r.code}`;
  if (r.code === 0) {
    if (!silent) vscode.window.showInformationMessage(`RimKit ship: ${msg}`);
    else vscode.window.setStatusBarMessage(`RimKit shipped`, 2500);
  } else {
    vscode.window.showErrorMessage(`RimKit ship failed: ${msg}`);
  }
}

// ---------------------------------------------------------------- UNC aliases (language server features)

function loadAliases() {
  try {
    return JSON.parse(fs.readFileSync(path.join(__dirname, "aliases.json"), "utf8"));
  } catch (e) {
    return null;
  }
}

function expandRules(aliases) {
  const rules = [];
  if (!aliases) return rules;
  for (const g of aliases.groups || []) {
    for (const entry of g.names || []) {
      const gt = entry.indexOf(">");
      const oldName = gt >= 0 ? entry.slice(0, gt) : entry;
      const newName = gt >= 0 ? entry.slice(gt + 1) : entry;
      const oldPath = `${g.from}.${oldName}`;
      const newPath = newName.includes(".") ? `game.${newName}` : `${g.to}.${newName}`;
      rules.push({ oldPath, newPath, deprecate: g.deprecate !== false && oldPath !== newPath, prefix: "" });
    }
  }
  for (const m of aliases.methods || []) {
    rules.push({ oldPath: m.from, newPath: m.to, deprecate: true, prefix: ":" });
  }
  return rules;
}

const KITS = api.loadKits(__dirname);
const KIT_ITEMS = api.kitCompletions(KITS);
const KNOWN = api.knownFunctions(path.join(__dirname, "stubs"));
const KIT_BY_NAME = new Map();
for (const kit of KITS) for (const fn of kit.fns) KIT_BY_NAME.set(`game.${kit.domain}.${fn.name}`, { kit, fn });

const ALIASES = loadAliases();
const ALL_RULES = expandRules(ALIASES);
const DEPRECATED_RULES = ALL_RULES.filter((r) => r.deprecate).sort((a, b) => b.oldPath.length - a.oldPath.length);

function isIdentChar(c) {
  return c !== undefined && /[A-Za-z0-9_]/.test(c);
}

// Same token rules as `rimkit migrate`: a match may not continue an identifier or a field path.
function findDeprecated(text) {
  const found = [];
  const taken = [];
  for (const rule of DEPRECATED_RULES) {
    let pos = 0;
    while ((pos = text.indexOf(rule.oldPath, pos)) !== -1) {
      const end = pos + rule.oldPath.length;
      const before = pos === 0 ? undefined : text[pos - 1];
      const after = text[end];
      const okBefore = rule.prefix ? before === rule.prefix : !isIdentChar(before) && before !== "." && before !== ":";
      const overlaps = taken.some(([s, e]) => pos < e && end > s);
      if (okBefore && !isIdentChar(after) && !overlaps) {
        found.push({ start: pos, end, rule });
        taken.push([pos, end]);
      }
      pos = end;
    }
  }
  return found;
}

function refreshDiagnostics(doc, collection) {
  if (!isLuaDoc(doc)) return;
  const diagnostics = [];
  for (const hit of findDeprecated(doc.getText())) {
    const range = new vscode.Range(doc.positionAt(hit.start), doc.positionAt(hit.end));
    const d = new vscode.Diagnostic(
      range,
      `${hit.rule.oldPath} is deprecated. Use ${hit.rule.newPath} (the old name works until ${ALIASES.removed_in}).`,
      vscode.DiagnosticSeverity.Warning
    );
    d.source = "rimkit";
    d.code = "rimkit.deprecated";
    d.tags = [vscode.DiagnosticTag.Deprecated];
    d.relatedInformation = undefined;
    d.rimkitReplacement = hit.rule.newPath;
    diagnostics.push(d);
  }
  const text = doc.getText();
  // A call to a function that is not in the API, with a suggestion when a close name exists.
  for (const hit of KNOWN.size ? api.findUnknownCalls(text, KNOWN) : []) {
    const range = new vscode.Range(doc.positionAt(hit.start), doc.positionAt(hit.end));
    const near = api.suggest(hit.name, KNOWN);
    const d = new vscode.Diagnostic(
      range,
      `${hit.name} is not in the RimKit API.${near ? " Did you mean " + near + "?" : ""}`,
      vscode.DiagnosticSeverity.Warning
    );
    d.source = "rimkit";
    d.code = "rimkit.unknown-function";
    diagnostics.push(d);
  }
  for (const e of (ALIASES && ALIASES.events) || []) {
    for (const quote of ['"', "'"]) {
      const literal = `${quote}${e.from}${quote}`;
      let pos = 0;
      while ((pos = text.indexOf(literal, pos)) !== -1) {
        const range = new vscode.Range(doc.positionAt(pos), doc.positionAt(pos + literal.length));
        const d = new vscode.Diagnostic(
          range,
          `Event ${literal} is deprecated. Use "${e.to}" and read e.${e.payload_key} from the payload.`,
          vscode.DiagnosticSeverity.Information
        );
        d.source = "rimkit";
        d.code = "rimkit.deprecated-event";
        d.tags = [vscode.DiagnosticTag.Deprecated];
        diagnostics.push(d);
        pos += literal.length;
      }
    }
  }
  collection.set(doc.uri, diagnostics);
}

class DeprecatedFixProvider {
  provideCodeActions(doc, range, context) {
    const actions = [];
    for (const d of context.diagnostics) {
      if (d.code !== "rimkit.deprecated") continue;
      const replacement = d.rimkitReplacement || (/Use (\S+) \(/.exec(d.message) || [])[1];
      if (!replacement) continue;
      const fix = new vscode.CodeAction(`Replace with ${replacement}`, vscode.CodeActionKind.QuickFix);
      fix.diagnostics = [d];
      fix.edit = new vscode.WorkspaceEdit();
      fix.edit.replace(doc.uri, d.range, replacement);
      fix.isPreferred = true;
      actions.push(fix);
    }
    const hits = findDeprecated(doc.getText());
    if (hits.length > 0) {
      const all = new vscode.CodeAction(`Replace all ${hits.length} deprecated RimKit name(s) in this file`, vscode.CodeActionKind.QuickFix);
      all.edit = new vscode.WorkspaceEdit();
      for (const hit of hits) {
        const r = new vscode.Range(doc.positionAt(hit.start), doc.positionAt(hit.end));
        all.edit.replace(doc.uri, r, hit.rule.newPath);
      }
      actions.push(all);
    }
    return actions;
  }
}

// Canonical names become completion items next to the hand written snippets.
function canonicalCompletions() {
  const seen = new Set();
  const items = [];
  for (const r of ALL_RULES) {
    if (seen.has(r.newPath) || r.prefix) continue;
    seen.add(r.newPath);
    items.push({ label: r.newPath, detail: "RimKit (canonical)", insert: `${r.newPath}($0)` });
  }
  return items;
}

// RimKit mods are Luau. luau-lsp registers the .lua language as "luau" in some versions, so both ids count as a mod file.
const LUA_SELECTOR = [{ language: "lua" }, { language: "luau" }];
function isLuaDoc(doc) {
  return doc && (doc.languageId === "lua" || doc.languageId === "luau");
}

// RimKit runs Luau. luau-lsp checks and completes mods from stubs/rimkit.d.luau, and the Lua language server is turned down in the
// workspace because it cannot read the syntax.
async function setupLuau(context, ask) {
  const folder = vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders[0];
  if (!folder) return;
  const defs = path.join(__dirname, "stubs", "rimkit.d.luau");
  if (!fs.existsSync(defs)) return;
  const cfg = vscode.workspace.getConfiguration("luau-lsp");
  const current = cfg.get("types.definitionFiles");
  const has = Array.isArray(current) ? current.includes(defs) : current && Object.values(current).includes(defs);
  if (has) return;
  if (ask) {
    if (context.workspaceState.get("rimkit.luauAsked")) return;
    await context.workspaceState.update("rimkit.luauAsked", true);
    const pick = await vscode.window.showInformationMessage(
      "RimKit mods are Luau. Point luau-lsp at the RimKit definitions for typed events and completion in this workspace?",
      "Set up",
      "Not now"
    );
    if (pick !== "Set up") return;
  }
  const target = vscode.ConfigurationTarget.Workspace;
  if (Array.isArray(current)) await cfg.update("types.definitionFiles", [...current, defs], target);
  else await cfg.update("types.definitionFiles", { ...(current || {}), "@rimkit": defs }, target);
  await cfg.update("types.roblox", false, target);
  await cfg.update("platform.type", "standard", target);
  await cfg.update("sourcemap.enabled", false, target);
  // luau-lsp fills call arguments with their parameter names ("fn"). A handler is better written as a function.
  await cfg.update("completion.fillCallArguments", false, target);
  // Let luau-lsp own the .lua files of this workspace, so completion and types come from it.
  const files = vscode.workspace.getConfiguration("files");
  const assoc = files.get("associations") || {};
  if (assoc["*.lua"] !== "luau") await files.update("associations", { ...assoc, "*.lua": "luau" }, target);
  const lua = vscode.workspace.getConfiguration("Lua");
  for (const key of ["diagnostics.enable", "completion.enable", "hover.enable", "signatureHelp.enable"]) {
    try {
      await lua.update(key, false, target);
    } catch (e) {
      // The older Lua language server is not installed, so there is nothing to turn down.
    }
  }
  if (!vscode.extensions.getExtension("JohnnyMorganz.luau-lsp")) {
    const pick = await vscode.window.showInformationMessage("RimKit: install the Luau Language Server extension (JohnnyMorganz.luau-lsp) for typed mods.", "Install");
    if (pick === "Install") await vscode.commands.executeCommand("workbench.extensions.installExtension", "JohnnyMorganz.luau-lsp");
  }
}

// luau-lsp reads the definitions file once, when its server starts. After the extension or the file changed, restart the server
// once so new names (rimkit.signal, a new event) are known without anyone reloading the window by hand.
function reloadLuauServerWhenDefinitionsChanged(context) {
  try {
    const defs = path.join(__dirname, "stubs", "rimkit.d.luau");
    const stamp = String(fs.statSync(defs).mtimeMs);
    const key = "rimkit.definitionsStamp";
    if (context.globalState.get(key) === stamp) return;
    context.globalState.update(key, stamp);
    setTimeout(() => {
      vscode.commands.executeCommand("luau-lsp.reloadServer").then(undefined, () => {});
    }, 4000);
  } catch (e) {
    // luau-lsp is not installed or the file is missing: nothing to reload.
  }
}

function activate(context) {
  reloadLuauServerWhenDefinitionsChanged(context);
  ecosystem.register(context, { findModRoot, runRimkit });
  const diagnostics = vscode.languages.createDiagnosticCollection("rimkit");
  context.subscriptions.push(diagnostics);
  if (ALIASES) {
    vscode.workspace.textDocuments.forEach((d) => refreshDiagnostics(d, diagnostics));
    context.subscriptions.push(
      vscode.workspace.onDidOpenTextDocument((d) => refreshDiagnostics(d, diagnostics)),
      vscode.workspace.onDidChangeTextDocument((e) => refreshDiagnostics(e.document, diagnostics)),
      vscode.workspace.onDidCloseTextDocument((d) => diagnostics.delete(d.uri)),
      vscode.languages.registerCodeActionsProvider(LUA_SELECTOR, new DeprecatedFixProvider(), {
        providedCodeActionKinds: [vscode.CodeActionKind.QuickFix],
      })
    );
  }
  // require("<TAB> lists the modules RimKit can load: its built-in libraries and the files in the mod's own Lua folder.
  // luau-lsp only offers folders here, because it resolves require by file path while RimKit resolves it by name.
  function ownModules(fileName) {
    const root = findModRoot(fileName);
    const out = [];
    if (!root) return out;
    const luaDir = path.join(root, "Lua");
    const walk = (dir) => {
      let entries = [];
      try {
        entries = fs.readdirSync(dir, { withFileTypes: true });
      } catch (e) {
        return;
      }
      for (const entry of entries) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(full);
        else if (/\.luau?$/.test(entry.name)) out.push(path.relative(luaDir, full).replace(/\.luau?$/, "").split(path.sep).join("."));
      }
    };
    walk(luaDir);
    return out;
  }
  const BUILT_IN_MODULES = [
    { name: "rimkit", doc: "Version, tiers and the built-in libraries. The global rimkit is the same table." },
    { name: "rimkit.signal", doc: "Events for your own code: Signal.new, connect, once, fire, wait. Also available as rimkit.signal." },
    { name: "rimkit.promise", doc: "A value that arrives later: Promise.new, and_then, catch, await, delay, all. Also available as rimkit.promise." },
  ];
  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      LUA_SELECTOR,
      {
        provideCompletionItems(document, position) {
          const line = document.lineAt(position).text.substring(0, position.character);
          if (!/\brequire\s*\(?\s*["'][\w.]*$/.test(line)) return undefined;
          const own = ownModules(document.fileName).filter((n) => !BUILT_IN_MODULES.some((b) => b.name === n));
          const items = BUILT_IN_MODULES.map((m) => {
            const item = new vscode.CompletionItem(m.name, vscode.CompletionItemKind.Module);
            item.detail = "RimKit built-in";
            item.documentation = new vscode.MarkdownString(m.doc);
            item.sortText = "0" + m.name;
            return item;
          });
          for (const name of own) {
            const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.File);
            item.detail = "this mod";
            item.sortText = "1" + name;
            items.push(item);
          }
          return items;
        },
      },
      '"',
      "'",
      "."
    )
  );

  // game.events.<TAB> inserts a whole handler: on_pawn_damaged(function(pawn, e) ... end), with the cursor in the body.
  const eventHandlers = (() => {
    try {
      const text = fs.readFileSync(path.join(__dirname, "stubs", "rimkit_events.lua"), "utf8");
      return [...text.matchAll(/---@param fn fun\(([^)]*)\)[\s\S]*?function game\.events\.(on_\w+)\(/g)].map((m) => ({
        name: m[2],
        params: m[1].split(",").map((p) => p.split(":")[0].trim()).join(", "),
      }));
    } catch (e) {
      return [];
    }
  })();
  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      LUA_SELECTOR,
      {
        provideCompletionItems(document, position) {
          const line = document.lineAt(position).text.substring(0, position.character);
          if (!/\bgame\.events\.\w*$/.test(line)) return undefined;
          return eventHandlers.map(({ name, params }) => {
            const item = new vscode.CompletionItem({ label: name, description: "handler" }, vscode.CompletionItemKind.Snippet);
            item.insertText = new vscode.SnippetString(name + "(function(" + params + ")\n\t$0\nend)");
            item.filterText = name;
            item.sortText = "0" + name;
            item.detail = "game.events." + name + "(function(" + params + ") ... end, filter?)";
            item.documentation = new vscode.MarkdownString("Inserts a handler. The editor types its arguments from the event name.");
            return item;
          });
        },
      },
      "."
    )
  );

  context.subscriptions.push(vscode.commands.registerCommand("rimkit.setupLuau", () => setupLuau(context, false)));
  if (vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders.some((f) => fs.existsSync(path.join(f.uri.fsPath, "meta.lua")))) {
    setupLuau(context, true).catch(() => {});
  }
  context.subscriptions.push(
    vscode.workspace.onDidOpenTextDocument((d) => {
      if (isLuaDoc(d)) setupLuau(context, true).catch(() => {});
    })
  );
  const openLua = vscode.window.activeTextEditor && vscode.window.activeTextEditor.document;
  if (openLua && isLuaDoc(openLua)) setupLuau(context, true).catch(() => {});

  context.subscriptions.push(
    vscode.commands.registerCommand("rimkit.migrate", async () => {
      const ed = vscode.window.activeTextEditor;
      const file = ed?.document?.uri?.fsPath;
      const root = file ? findModRoot(file) : vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
      if (!root) {
        vscode.window.showErrorMessage("No meta.lua mod root found.");
        return;
      }
      const dry = await runRimkit(["migrate", root], root);
      const summary = (dry.out || dry.err || "").trim().split(/\r?\n/).slice(-3).join(" | ");
      const pick = await vscode.window.showInformationMessage(`RimKit migrate (dry run): ${summary}`, { modal: false }, "Apply", "Cancel");
      if (pick !== "Apply") return;
      const done = await runRimkit(["migrate", root, "--write"], root);
      vscode.window.showInformationMessage(`RimKit migrate: ${(done.out || done.err || "").trim().split(/\r?\n/).slice(-1)[0]}`);
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("rimkit.ship", async () => {
      const ed = vscode.window.activeTextEditor;
      const file = ed?.document?.uri?.fsPath;
      const root = file ? findModRoot(file) : vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
      if (!root) {
        vscode.window.showErrorMessage("No meta.lua mod root found.");
        return;
      }
      await shipMod(root, false);
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("rimkit.sync", async () => {
      const ed = vscode.window.activeTextEditor;
      const file = ed?.document?.uri?.fsPath;
      const root = file ? findModRoot(file) : null;
      if (!root) {
        vscode.window.showErrorMessage("No meta.lua mod root found.");
        return;
      }
      const r = await runRimkit(["mod", "sync", root], root);
      vscode.window.showInformationMessage((r.out || r.err || "").trim());
    })
  );

  context.subscriptions.push(
    vscode.workspace.onDidSaveTextDocument(async (doc) => {
      if (!vscode.workspace.getConfiguration("rimkit").get("autoShipOnSave")) return;
      if (!doc.fileName.endsWith(".lua") && !doc.fileName.endsWith(".luau") && path.basename(doc.fileName) !== "meta.lua") return;
      const root = findModRoot(doc.fileName);
      if (!root) return;
      // Skip kit source tree accidental ships from src/native etc unless meta.lua present as mod
      if (root.includes(`${path.sep}src${path.sep}native`) || root.includes(`${path.sep}src${path.sep}host`)) return;
      await shipMod(root, true);
    })
  );

  // Hover: signature, description and parameter types of any kit function.
  context.subscriptions.push(
    vscode.languages.registerHoverProvider(LUA_SELECTOR, {
      provideHover(document, position) {
        const range = document.getWordRangeAtPosition(position, /game\.\w+\.\w+/);
        if (!range) return undefined;
        const entry = KIT_BY_NAME.get(document.getText(range));
        if (!entry) return undefined;
        return new vscode.Hover(new vscode.MarkdownString(api.describe(entry.kit, entry.fn)), range);
      },
    })
  );

  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      LUA_SELECTOR,
      {
        provideCompletionItems(document, position) {
          const line = document.lineAt(position).text.substring(0, position.character);
          if (
            !/\brim[\.\w\[]*$/.test(line) &&
            !/\b(game|prefix|events|pawn|map|find|anomaly|ui|data|health|building|work|incident|audio|input)\b/.test(line)
          ) {
            if (!/r$|ri$|rim$|ano$|anom/.test(line.trim()) && !line.includes("rim") && !line.includes("anomaly")) {
              return;
            }
          }
          return RIM_COMPLETIONS.concat(canonicalCompletions(), KIT_ITEMS).map((c) => {
            const item = new vscode.CompletionItem(c.label, vscode.CompletionItemKind.Snippet);
            item.detail = c.detail || "RimKit";
            item.insertText = new vscode.SnippetString(c.insert);
            item.documentation = new vscode.MarkdownString(c.documentation || "Stratware RimKit Lua API");
            return item;
          });
        },
      },
      ".",
      "[",
      '"'
    )
  );
}

function deactivate() {}

module.exports = { activate, deactivate, findDeprecated };
