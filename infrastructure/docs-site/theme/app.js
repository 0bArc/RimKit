(function () {
  const menuBtn = document.querySelector("[data-menu]");
  if (menuBtn) {
    menuBtn.addEventListener("click", () => {
      document.body.classList.toggle("nav-open");
    });
  }

  document.querySelectorAll(".tabset").forEach((set) => {
    const buttons = [...set.querySelectorAll(".tabset__labels button")];
    const panels = [...set.querySelectorAll(".tabset__panel")];
    buttons.forEach((btn, i) => {
      btn.addEventListener("click", () => {
        buttons.forEach((b) => b.classList.remove("is-active"));
        panels.forEach((p) => p.classList.remove("is-active"));
        btn.classList.add("is-active");
        if (panels[i]) panels[i].classList.add("is-active");
      });
    });
  });

  // Click a row in an API table to see its signature, parameters, how it works and an example.
  (function () {
    const base0 = document.querySelector('meta[name="docs-base"]')?.getAttribute("content") || "";
    let data = null;
    let loading = null;
    const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    const md = (s) =>
      esc(s)
        .replace(/`([^`]+)`/g, "<code>$1</code>")
        .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="' + base0 + '$2">$1</a>');
    const load = () => (loading = loading || fetch(base0 + "/assets/api-details.json").then((r) => r.json()).then((d) => (data = d)));
    const lookup = (key) => {
      if (!data) return null;
      if (data.fns[key]) return data.fns[key];
      if (data.opToFn[key]) return data.fns[data.opToFn[key]];
      if (data.fns["game." + key]) return data.fns["game." + key];
      return data.ops[key] || null;
    };
    const typeLink = (t) => esc(t).replace(/\bRim\w+\b/g, (n) => '<a href="' + base0 + "/api/types/#" + n.toLowerCase() + '">' + n + "</a>");
    function panel(d) {
      const params = d.params.length
        ? '<table class="detail__params"><thead><tr><th>Parameter</th><th>Type</th><th>Meaning</th></tr></thead><tbody>' +
          d.params.map((p) => "<tr><td><code>" + esc(p.n) + (p.o ? "?" : "") + "</code></td><td>" + typeLink(p.t) + "</td><td>" + esc(p.m) + "</td></tr>").join("") +
          "</tbody></table>"
        : "";
      return (
        '<div class="detail"><div class="detail__sig"><code>' + esc(d.sig) + "</code></div>" +
        (d.doc ? '<p class="detail__doc">' + esc(d.doc) + "</p>" : "") +
        params +
        (d.returns ? '<p class="detail__ret"><strong>Returns</strong> ' + typeLink(d.returns) + "</p>" : "") +
        '<h4>Example</h4><pre><code class="hljs">' + d.code + "</code></pre>" +
        "<h4>How it works</h4><ul>" + d.how.map((h) => "<li>" + md(h) + "</li>").join("") + "</ul></div>"
      );
    }
    function toggle(tr) {
      const next = tr.nextElementSibling;
      if (next && next.classList.contains("detail-row")) {
        next.remove();
        tr.classList.remove("is-open");
        tr.setAttribute("aria-expanded", "false");
        return;
      }
      const d = lookup(tr.dataset.key);
      if (!d) return;
      const row = document.createElement("tr");
      row.className = "detail-row";
      const cell = document.createElement("td");
      cell.colSpan = tr.children.length;
      cell.innerHTML = panel(d);
      row.appendChild(cell);
      tr.after(row);
      tr.classList.add("is-open");
      tr.setAttribute("aria-expanded", "true");
    }
    const candidates = [];
    document.querySelectorAll(".prose tbody tr").forEach((tr) => {
      const c = tr.firstElementChild && tr.firstElementChild.querySelector("code");
      if (!c) return;
      const key = c.textContent.trim().replace(/\(.*$/, "");
      if (/^[a-z][a-z0-9_]*(\.[a-z0-9_]+)+$/.test(key)) candidates.push([tr, key]);
    });
    if (!candidates.length) return;
    load().then(() => {
      for (const [tr, key] of candidates) {
        if (!lookup(key)) continue;
        tr.dataset.key = key;
        tr.classList.add("expandable");
        tr.tabIndex = 0;
        tr.setAttribute("role", "button");
        tr.setAttribute("aria-expanded", "false");
        tr.addEventListener("click", (e) => {
          if (e.target.closest("a")) return;
          toggle(tr);
        });
        tr.addEventListener("keydown", (e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            toggle(tr);
          }
        });
      }
    });
  })();

  const input = document.querySelector("[data-search]");
  const box = document.querySelector("[data-search-results]");
  if (!input || !box) return;

  let pages = [];
  let ready = false;

  const base = document.querySelector('meta[name="docs-base"]')?.getAttribute("content") || "";
  fetch(`${base}/search.json`)
    .then((r) => r.json())
    .then((data) => {
      pages = data;
      ready = true;
    })
    .catch(() => {
      ready = false;
    });

  function close() {
    box.classList.remove("is-open");
    box.innerHTML = "";
  }

  function search(q) {
    const query = q.trim().toLowerCase();
    if (!query || !ready) {
      close();
      return;
    }
    const hits = [];
    for (const page of pages) {
      const titleHit = page.title.toLowerCase().includes(query);
      const textHit = page.text.toLowerCase().includes(query);
      if (!titleHit && !textHit) continue;
      let snippet = "";
      const idx = page.text.toLowerCase().indexOf(query);
      if (idx >= 0) {
        const start = Math.max(0, idx - 40);
        snippet = (start > 0 ? "..." : "") + page.text.slice(start, start + 110).trim();
      } else {
        snippet = page.text.slice(0, 110).trim();
      }
      hits.push({
        title: page.title,
        url: page.url,
        snippet,
        score: titleHit ? 0 : 1,
      });
      if (hits.length >= 12) break;
    }
    hits.sort((a, b) => a.score - b.score);
    if (!hits.length) {
      box.innerHTML = '<div class="search__hit"><strong>No results</strong><span>Try another term</span></div>';
      box.classList.add("is-open");
      return;
    }
    box.innerHTML = hits
      .map(
        (h) =>
          `<a class="search__hit" href="${h.url}"><strong>${escapeHtml(h.title)}</strong><span>${escapeHtml(h.snippet)}</span></a>`
      )
      .join("");
    box.classList.add("is-open");
  }

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  let timer = null;
  input.addEventListener("input", () => {
    clearTimeout(timer);
    timer = setTimeout(() => search(input.value), 80);
  });
  input.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      input.blur();
      close();
    }
  });
  document.addEventListener("click", (e) => {
    if (!e.target.closest(".search")) close();
  });
  // Diagrams: a fenced block marked mermaid becomes an SVG. The library is only on the page when there is a diagram.
  (function () {
    const blocks = [...document.querySelectorAll("pre > code.language-mermaid")];
    if (!blocks.length || typeof window.mermaid === "undefined") return;
    window.mermaid.initialize({
      startOnLoad: false,
      securityLevel: "strict",
      theme: "base",
      fontSize: 17,
      flowchart: { curve: "basis", htmlLabels: false, padding: 16, useMaxWidth: true, nodeSpacing: 45, rankSpacing: 55 },
      sequence: { useMaxWidth: true, actorFontSize: 17, messageFontSize: 16, noteFontSize: 15, width: 150, height: 52, messageMargin: 38, boxMargin: 12, mirrorActors: true },
      state: { useMaxWidth: true },
      themeVariables: {
        darkMode: true,
        background: "#000000",
        fontFamily: '"Segoe UI", system-ui, -apple-system, Arial, sans-serif',
        fontSize: "17px",
        primaryColor: "#0f0f0f",
        primaryTextColor: "#e6e6e6",
        primaryBorderColor: "#4aa3f0",
        secondaryColor: "#141414",
        secondaryBorderColor: "#383838",
        tertiaryColor: "#0a0a0a",
        tertiaryBorderColor: "#2a2a2a",
        lineColor: "#7a7a7a",
        textColor: "#e6e6e6",
        clusterBkg: "#0a0a0a",
        clusterBorder: "#2a2a2a",
        edgeLabelBackground: "#000000",
        noteBkgColor: "#141414",
        noteTextColor: "#e6e6e6",
        actorBkg: "#0f0f0f",
        actorBorder: "#4aa3f0",
        actorTextColor: "#e6e6e6",
        signalColor: "#a3a3a3",
        signalTextColor: "#e6e6e6",
      },
    });
    blocks.forEach(async (code, i) => {
      const pre = code.parentElement;
      const source = code.textContent;
      try {
        const out = await window.mermaid.render("rk-diagram-" + i, source);
        const fig = document.createElement("figure");
        fig.className = "diagram";
        fig.innerHTML = out.svg;
        pre.replaceWith(fig);
      } catch (e) {
        pre.classList.add("diagram-failed");
      }
    });
  })();
})();
