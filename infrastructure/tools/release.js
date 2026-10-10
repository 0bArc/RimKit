#!/usr/bin/env node
// One command for a release of the kit mod in mod/.
//   node infrastructure/tools/release.js            write mod/Auth/allowlist.json from the built files, then verify
//   node infrastructure/tools/release.js --verify   only verify (exit 1 on any mismatch)
//   node infrastructure/tools/release.js --deploy <dir>   also copy mod/ into <dir> and verify the copy
// Build the host (dotnet build src/host/RimLuaHost.csproj -c Release) and the native core first.
// Harmony is hashed from the Harmony mod found next to the Steam workshop folder, or from --harmony <path>.
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "../..");
const modDir = path.join(root, "mod");
const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const value = (name) => (args.includes(name) ? args[args.indexOf(name) + 1] : null);

// Defs and strings written in Lua become XML before the files are hashed and copied.
const cli = path.join(root, "bin", "rimkit.exe");
if (fs.existsSync(cli)) require("child_process").execFileSync(cli, ["mod", "defs", path.join(root, "mod")], { stdio: "ignore" });

const sha = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
let problems = 0;
const fail = (msg) => {
  console.log("release: " + msg);
  problems++;
};

function findHarmony() {
  const given = value("--harmony");
  if (given) return given;
  const candidates = [
    "C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll",
    "C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2009463077/v1.5/Assemblies/0Harmony.dll",
  ];
  return candidates.find((c) => fs.existsSync(c)) || null;
}

// The Helm control library, one file per platform. It is optional: a package without it simply has no Helm.
const helmFiles = { windows: "helm.dll", linux: "libhelm.so", macos: "libhelm.dylib" };

const hostPath = path.join(modDir, "Assemblies", "RimLuaHost.dll");
const nativePath = path.join(modDir, "Native", "rimlua_core.dll");
const allowPath = path.join(modDir, "Auth", "allowlist.json");
for (const p of [hostPath, nativePath]) if (!fs.existsSync(p)) fail("missing " + path.relative(root, p) + " (build first)");
if (problems) process.exit(1);

const host = sha(hostPath);
const native = sha(nativePath);

if (!flag("--verify")) {
  const harmonyPath = findHarmony();
  let harmony = [];
  if (harmonyPath) harmony = [sha(harmonyPath)];
  else if (fs.existsSync(allowPath)) harmony = JSON.parse(fs.readFileSync(allowPath, "utf8")).harmony || [];
  if (!harmony.length) fail("no Harmony hash: pass --harmony <path to 0Harmony.dll>");
  // Helm (the control library) is hashed per platform. A platform whose file is not here keeps the hash it had, so building on
  // Windows does not forget the Linux entry. "helm" stays the last key: AuthGate reads the text after it.
  const previous = fs.existsSync(allowPath) ? JSON.parse(fs.readFileSync(allowPath, "utf8")) : {};
  const helm = Object.assign({}, previous.helm && !Array.isArray(previous.helm) ? previous.helm : {});
  for (const [platform, file] of Object.entries(helmFiles)) {
    const p = path.join(modDir, "Native", file);
    if (fs.existsSync(p)) helm[platform] = [sha(p)];
  }
  const doc = {
    version: 1,
    note: "Accepted SHA256 digests for a Stratware release. Rebuild/ship updates this file.",
    harmony,
    rimkit_host: [host],
    rimkit_native: [native],
    helm,
  };
  if (!problems) {
    fs.mkdirSync(path.dirname(allowPath), { recursive: true });
    fs.writeFileSync(allowPath, JSON.stringify(doc, null, 2) + "\n");
    console.log("release: wrote " + path.relative(root, allowPath));
  }
}

function verifyTree(dir, label) {
  const h = path.join(dir, "Assemblies", "RimLuaHost.dll");
  const n = path.join(dir, "Native", "rimlua_core.dll");
  const a = path.join(dir, "Auth", "allowlist.json");
  if (![h, n, a].every((f) => fs.existsSync(f))) return fail(label + ": host, native or allowlist missing");
  const list = JSON.parse(fs.readFileSync(a, "utf8"));
  if (!(list.rimkit_host || []).includes(sha(h))) fail(label + ": host hash is not in the allowlist");
  if (!(list.rimkit_native || []).includes(sha(n))) fail(label + ": native hash is not in the allowlist");
  if (!(list.harmony || []).length) fail(label + ": allowlist has no Harmony hash");
  for (const [platform, file] of Object.entries(helmFiles)) {
    const p = path.join(dir, "Native", file);
    if (fs.existsSync(p) && !((list.helm || {})[platform] || []).includes(sha(p))) fail(label + ": " + file + " hash is not in the allowlist");
  }
  const about = path.join(dir, "About", "About.xml");
  if (!fs.existsSync(about) || !/<packageId>stratware\.rimkit<\/packageId>/.test(fs.readFileSync(about, "utf8")))
    fail(label + ": About.xml packageId must be stratware.rimkit");
}

verifyTree(modDir, "mod/");

const target = value("--deploy");
if (target && !problems) {
  const skip = new Set(["README.md", "Tests"]);
  const copy = (from, to) => {
    fs.mkdirSync(to, { recursive: true });
    for (const e of fs.readdirSync(from, { withFileTypes: true })) {
      if (skip.has(e.name) || e.name.endsWith(".pdb")) continue;
      const f = path.join(from, e.name);
      const t = path.join(to, e.name);
      e.isDirectory() ? copy(f, t) : fs.copyFileSync(f, t);
    }
  };
  copy(modDir, target);
  console.log("release: copied mod/ to " + target);
  verifyTree(target, "deployed copy");
}

console.log(`release: host ${host.slice(0, 8)} native ${native.slice(0, 8)}, ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
