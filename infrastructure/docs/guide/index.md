# Guide

How to author, test, and ship a RimKit Lua mod.

## Start

1. [Quickstart](quickstart.md): first mod in about ten minutes
2. [Mod structure](mod-structure.md): folders RimKit and RimWorld expect
3. [meta.lua](meta.md): identity, version, dependencies, capabilities
4. [Defs and strings](defs-xml.md): XML Defs next to Lua
5. [Build and ship](build.md): sync, package, install locally

## Learn

- [Tutorials](tutorials.md): step-by-step builds
- [Cookbook](cookbook.md): short recipes for common tasks
- [Capabilities](capabilities.md): declare what your mod is allowed to touch

## Quality

- [Testing](testing.md): `Tests/*.luau` and `rimkit mod test`
- [Save safety](save-safety.md): stamps, migrations, load failures
- [Performance](performance.md): ticks, hooks, and profiler tips
- [Security](security.md): sandbox and what Lua cannot do
- [Troubleshooting](troubleshooting.md): common errors

## Ship

- [Publishing](publishing.md): Workshop checklist and `rimkit publish`
- [Request a feature](request-a-feature.md): when the API is missing a surface

!!! tip "API next door"
    Function lists live under the **API** tab: start at [overview](../api/overview.md) or the full [reference](../api/reference.md).
