// Phase 6 editor features: def name completion from the running game, go to definition for the API, a test runner,
// release checks and a new mod wizard. Registered from extension.js.
const vscode = require("vscode");
const fs = require("fs");
const path = require("path");
const eco = require("./api_ecosystem");
const { templates, fill } = require("./templates");

let defsCache = { file: null, mtime: 0, kinds: {} };
const output = vscode.window.createOutputChannel("RimKit");

function workspaceRoot() {
  const f = vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders[0];
  return f ? f.uri.fsPath : undefined;
}

function currentDefs() {
  const configured = vscode.workspace.getConfiguration("rimkit").get("defsFile");
  const files = configured ? [configured] : eco.defsFileCandidates(workspaceRoot());
  for (const f of files) {
    try {
      const st = fs.statSync(f);
      if (defsCache.file !== f || defsCache.mtime !== st.mtimeMs) defsCache = { file: f, mtime: st.mtimeMs, kinds: eco.loadDefs(f) };
      return defsCache.kinds;
    } catch (e) {
      // try the next candidate
    }
  }
  return {};
}

// helpers: { findModRoot, runRimkit } come from extension.js
function register(context, helpers) {
  const { findModRoot, runRimkit } = helpers;

  const rootOfActive = () => {
    const ed = vscode.window.activeTextEditor;
    const file = ed && ed.document.uri.fsPath;
    return (file && findModRoot(file)) || workspaceRoot();
  };

  const show = async (args, root) => {
    output.show(true);
    output.appendLine("> rimkit " + args.join(" "));
    const r = await runRimkit(args, root);
    output.appendLine((r.out || "") + (r.err || ""));
    return r;
  };

  // Def names from the running game inside string arguments: game.things.spawn_at("St...
  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      { language: "lua" },
      {
        provideCompletionItems(document, position) {
          const before = document.lineAt(position).text.substring(0, position.character);
          const kind = eco.defKindForContext(before);
          if (!kind) return undefined;
          const names = currentDefs()[kind];
          if (!names) {
            const hint = new vscode.CompletionItem("No def names yet: run the dev tools action 'RimKit: export def names for the editor'", vscode.CompletionItemKind.Text);
            hint.insertText = "";
            return [hint];
          }
          return names.map((n) => {
            const item = new vscode.CompletionItem(n, vscode.CompletionItemKind.Constant);
            item.detail = kind;
            return item;
          });
        },
      },
      '"',
      "'"
    )
  );

  // Go to definition: game.things.spawn_at jumps to its declaration in the bundled stubs.
  context.subscriptions.push(
    vscode.languages.registerDefinitionProvider({ language: "lua" }, {
      provideDefinition(document, position) {
        const range = document.getWordRangeAtPosition(position, /game\.\w+\.\w+/);
        if (!range) return undefined;
        const name = document.getText(range);
        const stubsDir = path.join(__dirname, "stubs");
        for (const f of fs.readdirSync(stubsDir).filter((x) => x.endsWith(".lua"))) {
          const text = fs.readFileSync(path.join(stubsDir, f), "utf8");
          const line = eco.stubLine(text, name);
          if (line >= 0) return new vscode.Location(vscode.Uri.file(path.join(stubsDir, f)), new vscode.Position(line, 0));
        }
        return undefined;
      },
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("rimkit.test", async () => {
      const root = rootOfActive();
      if (!root) return vscode.window.showErrorMessage("No RimKit mod found.");
      const r = await show(["mod", "test", root], root);
      const m = /pass=(\d+) fail=(\d+)/.exec(r.out || "");
      if (!m) return vscode.window.showWarningMessage("RimKit: no tests ran. See the RimKit output.");
      return (m[2] === "0" ? vscode.window.showInformationMessage : vscode.window.showErrorMessage)("RimKit tests: " + m[1] + " passed, " + m[2] + " failed");
    }),
    vscode.commands.registerCommand("rimkit.check", async () => {
      const root = rootOfActive();
      if (!root) return vscode.window.showErrorMessage("No RimKit mod found.");
      return show(["mod", "check", root], root);
    }),
    vscode.commands.registerCommand("rimkit.releaseCheck", async () => {
      const root = rootOfActive();
      if (!root) return vscode.window.showErrorMessage("No RimKit mod found.");
      const r = await show(["mod", "release-check", root], root);
      const m = /(\d+) failed, (\d+) warning/.exec(r.out || "");
      if (m) (m[1] === "0" ? vscode.window.showInformationMessage : vscode.window.showWarningMessage)("RimKit release check: " + m[1] + " failed, " + m[2] + " warning(s)");
      return undefined;
    }),
    vscode.commands.registerCommand("rimkit.assets", async () => {
      const root = rootOfActive();
      if (!root) return vscode.window.showErrorMessage("No RimKit mod found.");
      return show(["mod", "assets", root, "--fix"], root);
    }),
    vscode.commands.registerCommand("rimkit.diag", async () => show(["diag"], rootOfActive() || process.cwd())),
    vscode.commands.registerCommand("rimkit.newMod", async () => {
      const name = await vscode.window.showInputBox({
        prompt: "Mod name",
        placeHolder: "MyMod",
        validateInput: (v) => (/^[A-Za-z][A-Za-z0-9_-]*$/.test(v || "") ? null : "Start with a letter, then letters, digits, dash or underscore"),
      });
      if (!name) return undefined;
      const pick = await vscode.window.showQuickPick(
        templates.map((t) => ({ label: t.label, description: t.detail, template: t })),
        { placeHolder: "Starting point" }
      );
      if (!pick) return undefined;
      const dest = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, canSelectMany: false, openLabel: "Create the mod folder here" });
      if (!dest || !dest[0]) return undefined;
      const base = dest[0].fsPath;
      await show(["mod", "create", name, base], base);
      const modDir = path.join(base, name);
      if (!fs.existsSync(path.join(modDir, "meta.lua"))) return vscode.window.showErrorMessage("RimKit could not create the mod. See the RimKit output.");
      const id = name.replace(/[^A-Za-z0-9]/g, "").toLowerCase();
      for (const [rel, text] of Object.entries(pick.template.files)) {
        const target = path.join(modDir, rel);
        fs.mkdirSync(path.dirname(target), { recursive: true });
        fs.writeFileSync(target, fill(text, name, id));
      }
      if (pick.template.capabilities && pick.template.capabilities.length) {
        const metaPath = path.join(modDir, "meta.lua");
        const caps = pick.template.capabilities.map((c) => '"' + c + '"').join(", ");
        fs.writeFileSync(metaPath, fs.readFileSync(metaPath, "utf8").replace(/meta\.capabilities = \{[^}]*\}/, "meta.capabilities = { " + caps + " }"));
      }
      await runRimkit(["mod", "sync", modDir], modDir);
      return vscode.commands.executeCommand("vscode.openFolder", vscode.Uri.file(modDir), false);
    })
  );
}

module.exports = { register };
