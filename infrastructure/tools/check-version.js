#!/usr/bin/env node
// One version for everything (RKS section 5). src/api/VERSION is the source. This checks that the editor package and
// the docs agree with it, and with --fix updates the editor package.
//
//   native core     reads src/api/VERSION through CMake            (checked by the host at startup)
//   host            reads src/api/VERSION through MSBuild          (checked by the host at startup)
//   editor package  src/editor/package.json "version"              (checked here, --fix rewrites it)
//   docs            references to rimkit-<version>.vsix            (checked here)
//
// Run: node infrastructure/tools/check-version.js [--fix]     Exit code 1 on a mismatch.
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..", "..");
const fix = process.argv.includes("--fix");
const read = (p) => fs.readFileSync(path.join(root, p), "utf8");

const version = read("src/api/VERSION").trim();
const apiLevel = read("src/api/API_LEVEL").trim();
let problems = 0;
const report = (msg) => {
  console.log(msg);
  problems++;
};

if (!/^\d+\.\d+\.\d+$/.test(version)) report(`src/api/VERSION must be MAJOR.MINOR.PATCH, got "${version}"`);
if (!/^\d+$/.test(apiLevel)) report(`src/api/API_LEVEL must be an integer, got "${apiLevel}"`);

// editor package
const pkgPath = "src/editor/package.json";
const pkg = JSON.parse(read(pkgPath));
if (pkg.version !== version) {
  if (fix) {
    pkg.version = version;
    fs.writeFileSync(path.join(root, pkgPath), JSON.stringify(pkg, null, 2) + "\n");
    console.log(`fixed ${pkgPath}: ${version}`);
  } else {
    report(`${pkgPath} version ${pkg.version} does not match VERSION ${version}`);
  }
}

// vsix names mentioned in docs and readmes must be the current one (older ones may appear in a changelog, so only
// the "install" style mentions are checked: lines containing --install-extension)
for (const f of ["README.md", "src/editor/README.md", "infrastructure/README.md"]) {
  if (!fs.existsSync(path.join(root, f))) continue;
  for (const line of read(f).split(/\r?\n/)) {
    const m = /--install-extension[^\n]*rimkit-(\d+\.\d+\.\d+)\.vsix/.exec(line);
    if (m && m[1] !== version) {
      if (fix) {
        fs.writeFileSync(path.join(root, f), read(f).replace(line, line.replace(m[1], version)));
        console.log(`fixed ${f}: ${version}`);
      } else {
        report(`${f} installs rimkit-${m[1]}.vsix but VERSION is ${version}`);
      }
    }
  }
}

// the generated native header and host class, when a build has produced them
for (const [file, pattern] of [
  ["src/host/obj/Release/net48/RimKitVersion.g.cs", /Version = "([^"]+)"/],
]) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) continue;
  const m = pattern.exec(fs.readFileSync(full, "utf8"));
  if (m && m[1] !== version) report(`${file} has ${m[1]}, rebuild the host (VERSION is ${version})`);
}

console.log(`check-version: ${version} (api_level ${apiLevel}), ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
