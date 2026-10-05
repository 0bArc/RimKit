// Every op the host answers: registered ops (ApiRegistry.Register, R, D) and the older ops in the GameApi.cs switch.
// Shared by build-kits.js (kits.json lists them so mocks can reject a mistyped name) and check-ops.js.
const fs = require("fs");
const path = require("path");

const registerRe = /(?:ApiRegistry\.Register|\bR)\(\s*"([^"]+)"\s*,/g;
const dlcRe = /\bD\(\s*(?:"(\w+)"|(\w+))\s*,\s*"([^"]+)"/g;

const walk = (dir) =>
  fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = path.join(dir, e.name);
    return e.isDirectory() ? walk(p) : [p];
  });

function scanOps(root) {
  const ops = new Set();
  const gameApi = path.join(root, "src/host/GameApi.cs");
  if (fs.existsSync(gameApi)) {
    for (const m of fs.readFileSync(gameApi, "utf8").matchAll(/case "([a-z][a-z0-9_]*\.[a-z0-9_.]+)"/g)) ops.add(m[1]);
  }
  for (const file of walk(path.join(root, "src/host")).filter((f) => f.endsWith(".cs"))) {
    const text = fs.readFileSync(file, "utf8");
    if (!/ApiRegistry\.Register|static void Register/.test(text)) continue;
    for (const m of text.matchAll(registerRe)) ops.add(m[1]);
    for (const m of text.matchAll(dlcRe)) ops.add(m[3]);
  }
  return [...ops].sort();
}

module.exports = { scanOps };
