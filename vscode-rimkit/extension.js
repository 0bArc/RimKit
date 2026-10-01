const vscode = require("vscode");
const path = require("path");
const fs = require("fs");
const { spawn } = require("child_process");

const RIM_COMPLETIONS = [
  { label: "rim.log", detail: "Log to Player.log", insert: 'rim.log("${1:msg}")' },
  { label: "rim.message", detail: "In-game message", insert: 'rim.message("${1:msg}")' },
  { label: "rim.on_load", detail: "After scripts load / play start", insert: "rim.on_load(function()\n\t$0\nend)" },
  { label: "rim.on_tick", detail: "Periodic tick", insert: "rim.on_tick(function()\n\t$0\nend)" },
  { label: "rim.prefix", detail: 'rim.prefix["Type"].Method = fn', insert: 'rim.prefix["${1:RimWorld.JobGiver_GetFood}"].${2:TryGiveJob} = function(pawn)\n\t$0\n\treturn true\nend' },
  { label: "rim.prefixes", detail: "Alias of rim.prefix", insert: 'rim.prefixes["${1:Type}"].${2:Method} = function(pawn)\n\t$0\n\treturn true\nend' },
  { label: "rim.postfix", detail: 'rim.postfix["Type"].Method = fn', insert: 'rim.postfix["${1:Type}"].${2:Method} = function(pawn)\n\t$0\nend' },
  { label: "rim.events.prefix", detail: 'rim.events.prefix["Type.Method"] = fn', insert: 'rim.events.prefix["${1:RimWorld.JobGiver_GetFood.TryGiveJob}"] = function(pawn)\n\t$0\n\treturn true\nend' },
  { label: "rim.events.postfix", detail: 'rim.events.postfix["Type.Method"] = fn', insert: 'rim.events.postfix["${1:Type.Method}"] = function(pawn)\n\t$0\nend' },
  { label: "rim.hooks.prefix", detail: "Legacy: rim.hooks.prefix(type, method, fn)", insert: 'rim.hooks.prefix("${1:Type}", "${2:Method}", function(pawn)\n\t$0\n\treturn true\nend)' },
  { label: "rim.pawn.is_humanlike", insert: "rim.pawn.is_humanlike(${1:pawn})" },
  { label: "rim.pawn.faction_is_player", insert: "rim.pawn.faction_is_player(${1:pawn})" },
  { label: "rim.pawn.hunger", insert: "rim.pawn.hunger(${1:pawn})" },
  { label: "rim.pawn.map", insert: "rim.pawn.map(${1:pawn})" },
  { label: "rim.pawn.name", insert: "rim.pawn.name(${1:pawn})" },
  { label: "rim.map.nutrition", insert: "rim.map.nutrition(${1:map})" },
  { label: "rim.find.current_map", insert: "rim.find.current_map()" },
  { label: "rim.find.selected", insert: "rim.find.selected()" },
  { label: "rim.reflect.get", insert: 'rim.reflect.get(${1:h}, "${2:Member}")' },
  { label: "rim.reflect.call", insert: 'rim.reflect.call(${1:h}, "${2:Method}", "${3:args}")' },
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

function activate(context) {
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
      if (!doc.fileName.endsWith(".lua") && path.basename(doc.fileName) !== "meta.lua") return;
      const root = findModRoot(doc.fileName);
      if (!root) return;
      // Skip kit source tree accidental ships from src/native etc unless meta.lua present as mod
      if (root.includes(`${path.sep}src${path.sep}native`) || root.includes(`${path.sep}src${path.sep}host`)) return;
      await shipMod(root, true);
    })
  );

  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      { language: "lua" },
      {
        provideCompletionItems(document, position) {
          const line = document.lineAt(position).text.substring(0, position.character);
          if (!/\brim[\.\w\[]*$/.test(line) && !/\bprefix|\bevents|\bpawn|\bmap|\bfind/.test(line)) {
            // still offer when typing rim
            if (!/r$|ri$|rim$/.test(line.trim()) && !line.includes("rim")) {
              return;
            }
          }
          return RIM_COMPLETIONS.map((c) => {
            const item = new vscode.CompletionItem(c.label, vscode.CompletionItemKind.Snippet);
            item.detail = c.detail || "RimKit";
            item.insertText = new vscode.SnippetString(c.insert);
            item.documentation = new vscode.MarkdownString("Stratware RimKit Lua API");
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

module.exports = { activate, deactivate };
