# Infrastructure

Docs site, generators, and related tooling. Not part of the shippable RimWorld mod package.

| Path | Role |
|------|------|
| `docs-site/` | Custom Stratware-themed static site builder |
| `docs/` | Author-facing Markdown |
| `tools/` | Generators and checkers |
| `site/` | Built HTML (local only, gitignored) |

```powershell
cd infrastructure/docs-site
npm install
npm run serve
```

Open http://127.0.0.1:8000/ . Watch mode: `npm run dev`.

Theme tokens match https://team.stratware.win/ (pure black, white borders, Inter).

```powershell
# from repo root
node infrastructure/tools/check-docs.js
node infrastructure/tools/gen-api-reference.js
node infrastructure/tools/gen-api-types.js
node infrastructure/tools/gen-ops-doc.js
node infrastructure/tools/gen-cli-doc.js
```
