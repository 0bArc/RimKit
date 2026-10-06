#!/usr/bin/env node
/**
 * Build Stratware-themed static docs from infrastructure/docs + nav.json.
 * Run: node infrastructure/docs-site/build.js
 */
const fs = require("fs");
const path = require("path");
const { Marked } = require("marked");
const sanitizeHtml = require("sanitize-html");
const { markedHighlight } = require("./lib/marked-highlight.js");
const hljs = require("highlight.js");

const root = path.join(__dirname, "..");
const docsDir = path.join(root, "docs");
const outDir = path.join(root, "site");
const themeDir = path.join(__dirname, "theme");
const nav = JSON.parse(fs.readFileSync(path.join(__dirname, "nav.json"), "utf8"));
// Deploy under https://team.stratware.win/rimkit → DOCS_BASE=/rimkit
const BASE = String(process.env.DOCS_BASE || "").replace(/\/$/, "");

const SANITIZE = {
  allowedTags: sanitizeHtml.defaults.allowedTags.concat([
    "img",
    "h1",
    "h2",
    "h3",
    "h4",
    "pre",
    "code",
    "span",
    "div",
    "table",
    "thead",
    "tbody",
    "tr",
    "th",
    "td",
    "details",
    "summary",
    "button",
  ]),
  allowedAttributes: {
    a: ["href", "name", "target", "rel", "title", "class"],
    img: ["src", "alt", "title", "width", "height", "class"],
    code: ["class"],
    pre: ["class"],
    span: ["class"],
    div: ["class", "data-type"],
    button: ["type", "class"],
    th: ["style"],
    td: ["style"],
    h1: ["id"],
    h2: ["id"],
    h3: ["id"],
    h4: ["id"],
    "*": ["class", "id"],
  },
  allowedSchemes: ["http", "https", "mailto", "steam"],
  allowedSchemesByTag: {
    img: ["http", "https", "data"],
  },
  allowProtocolRelative: false,
};

const marked = new Marked(
  markedHighlight({
    langPrefix: "hljs language-",
    highlight(code, lang) {
      // Diagrams stay as plain text. app.js turns them into SVG in the browser.
      if (lang === "mermaid") return escapeHtml(code);
      if (lang && hljs.getLanguage(lang)) {
        return hljs.highlight(code, { language: lang }).value;
      }
      return hljs.highlightAuto(code).value;
    },
  })
);

// Set when a page has a diagram, so the library is copied into the site only then.
let usesMermaid = false;

// The page being rendered, so a relative link such as "../api/pawns.md" can be resolved against it.
let currentRel = "index.md";

function safeHref(href) {
  let url = href || "";
  if (!url) return "#";
  if (/^\s*(javascript|vbscript|data):/i.test(url)) return "#";
  if (!/^(https?:|mailto:|steam:|#|\/)/i.test(url)) {
    const m = /^([^#]*)(#.*)?$/.exec(url);
    const target = path.posix.normalize(path.posix.join(path.posix.dirname(currentRel), m[1]));
    if (m[1] && /\.md$/.test(target) && !target.startsWith("..")) {
      url = htmlHref(target) + (m[2] || "");
    } else if (m[1] && target.startsWith("..")) {
      // Outside the docs folder: the file in the repository on GitHub.
      const inRepo = path.posix.normalize(path.posix.join("infrastructure/docs", target));
      url = nav.repo_url + "/blob/main/" + inRepo + (m[2] || "");
    }
  }
  return url;
}

marked.use({
  renderer: {
    link({ href, title, text }) {
      const url = safeHref(href);
      const t = title ? ` title="${escapeAttr(title)}"` : "";
      const rel = /^https?:/i.test(url) ? ' rel="noopener noreferrer"' : "";
      return `<a href="${escapeAttr(url)}"${t}${rel}>${text}</a>`;
    },
  },
});

function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = path.join(dir, e.name);
    return e.isDirectory() ? walk(p) : [p];
  });
}

function slugify(text) {
  return String(text)
    .replace(/<[^>]+>/g, "")
    .toLowerCase()
    .replace(/`/g, "")
    .replace(/[^a-z0-9 -]/g, "")
    .trim()
    .replace(/ +/g, "-");
}

function escapeAttr(s) {
  return String(s).replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");
}

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// a/b.md is written to a/b/index.html so the address is /a/b/ with no extension. A page called index.md stays index.html.
function mdToHtmlPath(rel) {
  if (rel === "index.md") return "index.html";
  if (/(^|\/)index\.md$/.test(rel)) return rel.replace(/\.md$/, ".html");
  return rel.replace(/\.md$/, "/index.html");
}

function withBase(p) {
  const pathPart = p.startsWith("/") ? p : `/${p}`;
  return `${BASE}${pathPart}`;
}

function htmlHref(rel) {
  if (rel === "index.md") return withBase("/");
  return withBase("/" + mdToHtmlPath(rel).replace(/\\/g, "/").replace(/index\.html$/, ""));
}

function collectPaths(nodes, out = []) {
  for (const n of nodes || []) {
    if (n.path) out.push(n.path);
    if (n.children) collectPaths(n.children, out);
  }
  return out;
}

function findTabFor(rel) {
  for (const tab of nav.tabs) {
    const paths = collectPaths([tab]);
    if (paths.includes(rel) || (tab.path === rel)) return tab.title;
    if (!tab.path && tab.children) {
      const nested = collectPaths(tab.children);
      if (nested.includes(rel)) return tab.title;
    }
  }
  // Home special-case
  if (rel === "index.md") return "Home";
  // Prefer first path segment match
  const seg = rel.split("/")[0];
  for (const tab of nav.tabs) {
    if (tab.path && tab.path.startsWith(seg)) return tab.title;
    if ((tab.children || []).some((c) => (c.path || "").startsWith(seg + "/") || collectPaths([c]).some((p) => p.startsWith(seg + "/")))) {
      return tab.title;
    }
  }
  return null;
}

function breadcrumbs(rel, title) {
  const parts = [{ label: "Home", href: "/" }];
  const tab = findTabFor(rel);
  if (tab && tab !== "Home") {
    const tabNode = nav.tabs.find((t) => t.title === tab);
    parts.push({
      label: tab,
      href: tabNode && tabNode.path ? htmlHref(tabNode.path) : null,
    });
  }
  if (rel !== "index.md") {
    parts.push({ label: title, href: null });
  }
  return parts
    .map((p, i) => {
      if (i === parts.length - 1) return `<span>${escapeHtml(p.label)}</span>`;
      if (p.href) return `<a href="${escapeAttr(p.href)}">${escapeHtml(p.label)}</a><span>/</span>`;
      return `<span>${escapeHtml(p.label)}</span><span>/</span>`;
    })
    .join("");
}

function renderNavTree(nodes, current, depth = 0) {
  let html = "";
  for (const n of nodes || []) {
    if (n.path && !n.children) {
      const active = n.path === current ? " is-active" : "";
      html += `<a class="${active.trim()}" href="${htmlHref(n.path)}">${escapeHtml(n.title)}</a>`;
      continue;
    }
    if (n.children) {
      if (n.path) {
        const active = n.path === current ? " is-active" : "";
        html += `<a class="${active.trim()}" href="${htmlHref(n.path)}">${escapeHtml(n.title)}</a>`;
      } else if (depth === 0) {
        html += `<div class="nav-group__title">${escapeHtml(n.title)}</div>`;
      } else {
        html += `<div class="nav-group__title">${escapeHtml(n.title)}</div>`;
      }
      html += `<div class="nav-nested">${renderNavTree(n.children, current, depth + 1)}</div>`;
    }
  }
  return html;
}

function sidebarFor(rel) {
  const tabTitle = findTabFor(rel);
  const tab = nav.tabs.find((t) => t.title === tabTitle);
  if (!tab || !tab.children || !tab.children.length) return "";
  return `<aside class="sidebar"><p class="sidebar__label">${escapeHtml(tab.title)}</p><nav class="nav">${renderNavTree(tab.children, rel)}</nav></aside>`;
}

function renderInlineMarkdown(md) {
  return sanitizeHtml(String(marked.parse(md || "")), SANITIZE);
}

function preprocessMarkdown(src) {
  let text = src.replace(/^<!--[\s\S]*?-->\s*/m, "");

  // Admonitions: !!! tip "Title"\n    body
  text = text.replace(/^!!!\s+(\w+)(?:\s+"([^"]*)")?\s*\n((?:[ \t]+.*\n?)*)/gm, (_, type, title, body) => {
    const inner = body
      .split("\n")
      .map((l) => l.replace(/^[ \t]{4}/, ""))
      .join("\n")
      .trim();
    const label = title || type;
    return `\n\n<div class="admonition" data-type="${type}"><p class="admonition__title">${escapeHtml(label)}</p>${renderInlineMarkdown(inner)}</div>\n\n`;
  });

  // Content tabs: === "Label" then indented body (blank lines allowed)
  const lines = text.split("\n");
  const out = [];
  for (let i = 0; i < lines.length; ) {
    const start = lines[i].match(/^=== "([^"]+)"\s*$/);
    if (!start) {
      out.push(lines[i]);
      i++;
      continue;
    }
    const tabs = [];
    while (i < lines.length) {
      const head = lines[i].match(/^=== "([^"]+)"\s*$/);
      if (!head) break;
      i++;
      const body = [];
      while (i < lines.length) {
        const line = lines[i];
        if (/^=== "/.test(line)) break;
        if (line.trim() === "") {
          body.push("");
          i++;
          continue;
        }
        if (/^[ \t]{4}/.test(line) || /^\t/.test(line)) {
          body.push(line.replace(/^(?: {4}|\t)/, ""));
          i++;
          continue;
        }
        break;
      }
      // drop trailing blank lines inside a panel
      while (body.length && body[body.length - 1] === "") body.pop();
      tabs.push({ label: head[1], body: body.join("\n").trim() });
    }
    if (!tabs.length) continue;
    const labels = tabs
      .map((t, idx) => `<button type="button"${idx === 0 ? ' class="is-active"' : ""}>${escapeHtml(t.label)}</button>`)
      .join("");
    const panels = tabs
      .map((t, idx) => `<div class="tabset__panel${idx === 0 ? " is-active" : ""}">${renderInlineMarkdown(t.body)}</div>`)
      .join("\n");
    out.push("", `<div class="tabset"><div class="tabset__labels">${labels}</div>${panels}</div>`, "");
  }
  text = out.join("\n");

  return text;
}

function extractTitle(md, fallback) {
  const m = md.match(/^#\s+(.+)$/m);
  return m ? m[1].replace(/`/g, "").trim() : fallback;
}

function extractToc(html) {
  const items = [];
  const re = /<h([23])\s+id="([^"]+)"[^>]*>([\s\S]*?)<\/h\1>/g;
  let m;
  while ((m = re.exec(html))) {
    items.push({ level: Number(m[1]), id: m[2], text: m[3].replace(/<[^>]+>/g, "") });
  }
  return items;
}

function addHeadingIds(html) {
  return html.replace(/<h([2-4])>([\s\S]*?)<\/h\1>/g, (_, level, inner) => {
    const id = slugify(inner);
    return `<h${level} id="${id}">${inner}</h${level}>`;
  });
}

function layout({ title, description, rel, bodyHtml, tocHtml, hasSidebar, hasToc }) {
  const tabTitle = findTabFor(rel) || "Home";
  const tabs = nav.tabs
    .map((t) => {
      const href =
        t.title === "Home"
          ? withBase("/")
          : t.path
            ? htmlHref(t.path)
            : t.children && t.children[0] && t.children[0].path
              ? htmlHref(t.children[0].path)
              : "#";
      const cls = t.title === tabTitle ? ' class="is-active"' : "";
      return `<a${cls} href="${href}">${escapeHtml(t.title)}</a>`;
    })
    .join("");

  let shellClass = "shell";
  if (!hasSidebar && !hasToc) shellClass = "shell shell--flat";
  else if (!hasSidebar && hasToc) shellClass = "shell shell--toc-only";
  else if (hasSidebar && !hasToc) shellClass = "shell shell--no-toc";
  const side = hasSidebar ? sidebarFor(rel) : "";
  const toc = hasToc
    ? `<aside class="toc"><p class="toc__label">On this page</p><nav>${tocHtml}</nav></aside>`
    : "";

  // Mermaid is served from the site itself (the page policy only allows scripts from 'self') and only on pages that have a diagram.
  let diagramScript = "";
  if (bodyHtml.includes("language-mermaid")) {
    usesMermaid = true;
    diagramScript = `<script src="${withBase("/assets/mermaid.min.js")}"></script>
  `;
  }

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${escapeHtml(title)} | ${escapeHtml(nav.site_name)}</title>
  <meta name="description" content="${escapeAttr(description || nav.site_description)}">
  <link rel="icon" href="https://team.stratware.win/icon.png">
  <meta http-equiv="Content-Security-Policy" content="default-src 'self'; img-src 'self' https: data:; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; script-src 'self'; connect-src 'self'; base-uri 'self'; form-action 'none'; object-src 'none'; frame-ancestors 'none'">
  <meta name="docs-base" content="${escapeAttr(BASE)}">
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="${withBase("/assets/styles.css")}">
</head>
<body>
  <header class="top-nav">
    <div class="top-nav__inner">
      <button class="menu-btn" type="button" data-menu aria-label="Open menu">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 12h16M4 18h16M4 6h16"/></svg>
      </button>
      <a class="brand" href="${withBase("/")}">${escapeHtml(nav.site_name)}</a>
      <div class="search">
        <svg class="search__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
        <input class="search__input" type="search" placeholder="Search" data-search autocomplete="off">
        <div class="search__results" data-search-results></div>
      </div>
      <a class="repo" href="${escapeAttr(nav.repo_url)}" target="_blank" rel="noopener noreferrer">
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 .5C5.37.5 0 5.87 0 12.5c0 5.3 3.44 9.8 8.2 11.39.6.11.82-.26.82-.58v-2.03c-3.34.73-4.04-1.61-4.04-1.61-.55-1.39-1.33-1.76-1.33-1.76-1.09-.75.08-.73.08-.73 1.2.08 1.84 1.24 1.84 1.24 1.07 1.83 2.8 1.3 3.49.99.11-.78.42-1.3.76-1.6-2.66-.3-5.46-1.33-5.46-5.93 0-1.31.47-2.38 1.24-3.22-.12-.3-.54-1.52.12-3.17 0 0 1.01-.32 3.3 1.23a11.5 11.5 0 0 1 6 0c2.29-1.55 3.3-1.23 3.3-1.23.66 1.65.24 2.87.12 3.17.77.84 1.24 1.91 1.24 3.22 0 4.61-2.8 5.62-5.48 5.92.43.37.81 1.1.81 2.22v3.29c0 .32.22.7.82.58C20.56 22.3 24 17.8 24 12.5 24 5.87 18.63.5 12 .5z"/></svg>
        <span>${escapeHtml(nav.repo_name)}</span>
      </a>
    </div>
  </header>
  <nav class="tabs"><div class="tabs__inner">${tabs}</div></nav>
  <div class="${shellClass}">
    ${side}
    <main class="content">
      <div class="crumbs${rel === "index.md" ? " crumbs--home" : ""}">${breadcrumbs(rel, title)}<a class="crumbs__source" href="${escapeAttr(nav.repo_url + "/blob/main/infrastructure/docs/" + rel)}" target="_blank" rel="noopener noreferrer">View source (.md)</a></div>
      <article class="prose${rel === "index.md" ? " prose--home" : ""}">${bodyHtml}</article>
    </main>
    ${toc}
  </div>
  <footer class="footer">
    <span>© 2026 Stratware.win</span>
    <span>Efficiency First</span>
  </footer>
  ${diagramScript}<script src="${withBase("/assets/app.js")}"></script>
</body>
</html>
`;
}

function ensureDir(p) {
  fs.mkdirSync(p, { recursive: true });
}

function main() {
  fs.rmSync(outDir, { recursive: true, force: true });
  ensureDir(outDir);
  ensureDir(path.join(outDir, "assets"));
  fs.copyFileSync(path.join(themeDir, "styles.css"), path.join(outDir, "assets", "styles.css"));
  fs.copyFileSync(path.join(themeDir, "app.js"), path.join(outDir, "assets", "app.js"));
  if (fs.existsSync(path.join(themeDir, "api-details.json"))) fs.copyFileSync(path.join(themeDir, "api-details.json"), path.join(outDir, "assets", "api-details.json"));

  const navPaths = new Set(collectPaths(nav.tabs));
  const allMd = walk(docsDir).filter((f) => f.endsWith(".md"));
  const searchIndex = [];
  let built = 0;

  for (const file of allMd) {
    const rel = path.relative(docsDir, file).replace(/\\/g, "/");
    const raw = fs.readFileSync(file, "utf8");
    const title = extractTitle(raw, path.basename(rel, ".md"));
    const processed = preprocessMarkdown(raw);
    currentRel = rel;
    let bodyHtml = marked.parse(processed);
    // Links written as raw HTML (the home page cards and buttons) get the same .md to page rewrite as markdown links.
    bodyHtml = bodyHtml.replace(/<a ([^>]*?)href="([^"]+)"/g, (m, before, href) => `<a ${before}href="${escapeAttr(safeHref(href))}"`);
    bodyHtml = sanitizeHtml(String(bodyHtml), SANITIZE);
    bodyHtml = addHeadingIds(bodyHtml);
    const toc = extractToc(bodyHtml);
    const tocHtml = toc
      .map((t) => `<a href="#${escapeAttr(t.id)}" style="padding-left:${(t.level - 2) * 0.55 + 0.45}rem">${escapeHtml(t.text)}</a>`)
      .join("");

    const hasSidebar = Boolean(sidebarFor(rel));
    const hasToc = toc.length > 0 && rel !== "index.md";
    const page = layout({
      title,
      description: nav.site_description,
      rel,
      bodyHtml,
      tocHtml,
      hasSidebar,
      hasToc,
    });

    const outRel = mdToHtmlPath(rel);
    const outPath = path.join(outDir, outRel);
    ensureDir(path.dirname(outPath));
    fs.writeFileSync(outPath, page, "utf8");
    built++;

    const text = raw
      .replace(/```[\s\S]*?```/g, " ")
      .replace(/[#>*`|_\[\]()!-]/g, " ")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 8000);
    searchIndex.push({ title, url: htmlHref(rel), text, inNav: navPaths.has(rel) });
  }

  if (usesMermaid) {
    const lib = path.join(__dirname, "node_modules", "mermaid", "dist", "mermaid.min.js");
    if (!fs.existsSync(lib)) throw new Error("a page has a diagram but mermaid is not installed: run npm install in infrastructure/docs-site");
    fs.copyFileSync(lib, path.join(outDir, "assets", "mermaid.min.js"));
  }

  fs.writeFileSync(path.join(outDir, "search.json"), JSON.stringify(searchIndex), "utf8");
  console.log(`docs-site: built ${built} pages -> ${path.relative(process.cwd(), outDir)}`);
}

main();
