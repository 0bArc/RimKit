#!/usr/bin/env node
/**
 * Serve infrastructure/site and optionally rebuild on change.
 * Run: node infrastructure/docs-site/serve.js [--watch]
 */
const http = require("http");
const fs = require("fs");
const path = require("path");
const { spawn } = require("child_process");

const root = path.join(__dirname, "..");
const siteDir = path.join(root, "site");
const docsDir = path.join(root, "docs");
const themeDir = path.join(__dirname, "theme");
const port = Number(process.env.PORT || 8000);
const watch = process.argv.includes("--watch");

const mime = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
  ".txt": "text/plain; charset=utf-8",
  ".md": "text/markdown; charset=utf-8",
};

function build() {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [path.join(__dirname, "build.js")], {
      stdio: "inherit",
      cwd: path.join(__dirname, "../.."),
    });
    child.on("exit", (code) => (code === 0 ? resolve() : reject(new Error(`build exit ${code}`))));
  });
}

function send(res, code, body, type) {
  res.writeHead(code, { "Content-Type": type || "text/plain; charset=utf-8", "Cache-Control": "no-store" });
  res.end(body);
}

function safeJoin(base, reqPath) {
  const decoded = decodeURIComponent(reqPath.split("?")[0]);
  const cleaned = path.normalize(decoded).replace(/^(\.\.[/\\])+/, "");
  const full = path.join(base, cleaned);
  if (!full.startsWith(base)) return null;
  return full;
}

const server = http.createServer((req, res) => {
  let urlPath = req.url || "/";
  if (urlPath === "/") urlPath = "/index.html";
  let file = safeJoin(siteDir, urlPath);
  if (!file) return send(res, 400, "Bad path");

  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) {
    const asHtml = file.endsWith(".html") ? file : file + ".html";
    const asIndex = path.join(file, "index.html");
    if (fs.existsSync(asHtml) && fs.statSync(asHtml).isFile()) file = asHtml;
    else if (fs.existsSync(asIndex)) file = asIndex;
    else return send(res, 404, "Not found");
  }

  const ext = path.extname(file).toLowerCase();
  fs.readFile(file, (err, data) => {
    if (err) return send(res, 500, "Read error");
    send(res, 200, data, mime[ext] || "application/octet-stream");
  });
});

async function start() {
  await build();
  server.listen(port, "127.0.0.1", () => {
    console.log(`docs-site: http://127.0.0.1:${port}/`);
  });

  if (!watch) return;
  let timer = null;
  const rebuild = () => {
    clearTimeout(timer);
    timer = setTimeout(() => {
      build().catch((e) => console.error(e.message));
    }, 200);
  };
  for (const dir of [docsDir, themeDir, __dirname]) {
    fs.watch(dir, { recursive: true }, rebuild);
  }
  console.log("docs-site: watching docs + theme");
}

start().catch((e) => {
  console.error(e);
  process.exit(1);
});
