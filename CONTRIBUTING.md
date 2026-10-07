# Contributing to RimKit

RimKit is source available. Contributions are welcome when they improve the kit for mod authors and fit the project rules.

## Official contribution paths

### Report a bug

Open a GitHub issue at [github.com/0bArc/RimKit/issues](https://github.com/0bArc/RimKit/issues).

Include:

- RimWorld version and active DLC
- RimKit version or commit
- Operating system
- Minimal Lua or Luau example
- `Player.log` lines around the failure
- Exact command, expected result and actual result

Remove private paths, account names and unrelated log data before posting.

### Ask a question or request an API capability

Use a GitHub issue for API requests, usage questions, compatibility questions, documentation questions or other project questions. Search existing issues first.

For an API request, open one issue for one capability. Describe:

- The game behavior the mod needs
- Proposed Lua shape
- Whether it belongs in a stable, experimental or advanced surface
- Why existing kits, hooks or reflection do not solve it

Prefer a small, typed kit operation over a broad reflection escape. See the [RimKit Standard](infrastructure/docs/standard/rks.md).

### Write a useful issue

Write the request so another mod author can understand it without knowing your project. Use the format below for a change, API request or technical question. For a simple question, mark unused sections `N/A`.

Use this structure:

```text
Title: [area] short description of the change

Problem
What cannot be done today? Include the current error or limitation.

Use case
What mod or workflow needs this? Show a small Lua or Luau example when possible.

Expected change
Describe the behavior or API shape you want. Include parameters, return values and
whether the operation should be stable, experimental or advanced.

Alternatives
List existing kits, hooks, reflection calls or workarounds you tried.

Compatibility
Explain whether the change affects existing mods, saves, Defs or editor types.

Verification
Describe a test or in-game check that would prove the change works.
```

Keep one request focused on one outcome. A proposal is easier to review when it explains the problem before prescribing an implementation. Maintainers may adjust the API shape to match naming, safety and compatibility rules.

Issues that ignore this format, omit required reproduction details or combine unrelated requests may be ignored or closed without implementation. Do not open duplicate issues to avoid that outcome. Add the missing information to the existing issue instead.

## Moderation

Repository maintainers can moderate GitHub activity. Depending on the situation, they may:

- Hide or delete comments
- Lock an issue, pull request or discussion so no new comments can be added
- Close an issue or pull request
- Restrict issue, pull request or discussion participation
- Block a user from the repository

Do not repost a locked, closed or removed request. If a maintainer asks for changes, update the existing issue or pull request with the requested information.

### Submit code

Fork the repository, create a focused branch, and open a pull request against `main`.

Good pull requests:

- Solve one problem
- Explain the user-facing behavior
- Include tests or explain why tests cannot cover it
- Update API docs and Luau stubs when the author surface changes
- Update generated files through their generators
- Keep unrelated formatting and cleanup out of the diff

### Improve documentation

Documentation changes are welcome without code changes. Add or correct:

- Quickstart steps
- API examples
- Safety and compatibility notes
- Troubleshooting details
- Tutorials that can be verified against the current CLI

Use short examples that can be copied into a mod. Do not document an API name until the host, stubs and generated reference agree.

### Add or improve examples

Examples live under `src/examples`. A useful example includes:

- A valid `About` package
- Lua or Luau source
- Required Defs, patches, languages and metadata
- A focused README or Workshop description when needed
- A headless test when behavior can be tested without RimWorld

Keep examples small. Demonstrate one mod pattern instead of combining unrelated features.

### Improve the editor

Editor changes belong under `src/editor`. Update the Luau definitions, completions and editor documentation together when an API changes.

## Local checks

From the repository root:

```powershell
node infrastructure/tools/check-docs.js
node infrastructure/tools/check-editor.js
node infrastructure/tools/check-ops.js
```

Build and test the native core:

```powershell
cmake -S src/native -B build/native -A x64
cmake --build build/native --config Release
```

Run a mod test:

```powershell
.\bin\rimkit.exe mod test .\src\examples\damage
```

Build the documentation site:

```powershell
cd infrastructure/docs-site
npm install
npm run build
```

Run all checks that apply to the changed area. A pull request must not rely on generated output that is out of date.

## API change rules

An API change normally updates all of these:

1. Host registration and implementation
2. Native binding or operation mapping
3. Luau stubs and editor definitions
4. API source data and generated reference
5. Documentation and examples
6. Tests

Use the existing naming and stability rules. Do not silently rename a public operation. Add an alias and migration path when compatibility requires it.

## Pull request review

Maintainers may request smaller scope, stronger tests, safer capability boundaries or clearer documentation. A pull request can be closed when it conflicts with the project direction, duplicates existing work or exposes an unsafe unrestricted path.

Do not include compiled binaries, local logs, save files, credentials or unrelated generated dependencies.

## Security reports

Do not publish an exploitable vulnerability in a public issue. Contact the project maintainers privately through the channel listed on the project site and provide reproduction steps, affected versions and impact.

## License

By submitting a change, you prepare it for return to the RimKit project under the terms of the [RimKit License](LICENSE). Third-party code keeps its original license. Review [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) before copying external code.
