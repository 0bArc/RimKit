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
})();
