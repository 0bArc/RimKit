/**
 * Minimal marked highlight helper (avoids extra dependency).
 * Compatible with marked v15 extensions.
 */
function markedHighlight(options) {
  const langPrefix = options.langPrefix || "language-";
  return {
    renderer: {
      code({ text, lang }) {
        const language = (lang || "").match(/\S*/)?.[0] || "";
        const highlighted = options.highlight(text, language);
        const cls = language ? ` class="${langPrefix}${language}"` : ` class="hljs"`;
        return `<pre><code${cls}>${highlighted}</code></pre>\n`;
      },
    },
  };
}

module.exports = { markedHighlight };
