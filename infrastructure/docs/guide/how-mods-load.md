# How RimKit loads your mod

What happens between pressing Play and your first line of Lua running, and what keeps a broken mod from taking the game down. Use it to find out why a mod did not load.

## From the mod list to your code

```mermaid
flowchart TD
    start(["RimWorld starts"]) --> list["Mod list loads in order:<br/>Harmony, RimKit, your mod"]
    list --> hostinit["RimKit host starts<br/>and loads the C++ core"]
    hostinit --> gate{"pAuth gate<br/>checks"}
    gate -->|"hashes of RimKit and Harmony match Auth/allowlist.json<br/>and the Lua scan is clean"| open["gate open"]
    gate -->|"a hash does not match"| closed["gate closed: no Lua loads<br/>letter and log explain why"]
    open --> play(["first frame of Play"])
    play --> safe{"safe mode?<br/>-rimkit-safe, RIMKIT_SAFE, rimkit_safe.txt"}
    safe -->|yes| none["no Lua runs"]
    safe -->|no| each["for each running mod with a Lua/ folder"]
    each --> quar{"quarantined by<br/>the threat scan?"}
    quar -->|yes| skip["skipped, listed in the Hub"]
    quar -->|no| load["load every .lua file in the folder<br/>(path order, one folder per version folder)"]
    load --> reg["your files register handlers:<br/>events, hooks, timers, gizmos, windows"]
    reg --> onload["on_load handlers run"]
    onload --> run(["every frame: queued events,<br/>then on_tick"])
```

The gate is a check of RimKit's own files, not of your mod. A scan of your Lua for forbidden calls (`load`, `dofile`, `os`, `io`) is separate: a mod that fails it is quarantined and the other mods still load.

## What loading a mod does

```mermaid
sequenceDiagram
    participant Host as C#35; host
    participant Core as C++ core
    participant VM as Luau VM
    participant Mod as your files
    Host->>Core: set_mod_context("author.mymod")
    Host->>Core: load_directory("MyMod/Lua")
    Core->>Core: allow folder for require
    Core->>Core: read meta
    loop each .lua file, in path order
        Core->>VM: compile the file
        VM->>Mod: run the file
        Mod-->>Core: register handlers
    end
    Host->>Core: set_mod_context(none)
    Note over Core: Handlers are stored with the mod id,<br/>so errors and time are charged to it.
```

## What a mod can be

```mermaid
stateDiagram-v2
    [*] --> Found: listed in the mod list
    Found --> Quarantined: threat scan matched
    Found --> Loaded: scan clean, files run
    Loaded --> Running: on_load done
    Running --> Running: events, hooks, timers, ticks
    Running --> Reloaded: game.dev.reload, or a saved file in dev mode
    Reloaded --> Running: old handlers replaced
    Running --> Disabled: 20 Lua errors in 60 seconds
    Disabled --> [*]: other mods keep running
    Quarantined --> [*]
```

A disabled mod gets a letter that offers to turn it off for good. Fix the errors in the log and restart to run it again.

## From your editor to the game

```mermaid
flowchart LR
    edit["edit Lua/ and meta.lua<br/>in VS Code with luau-lsp"] --> test["rimkit mod test<br/>mock host, no game"]
    test --> check["rimkit mod check<br/>meta, Defs, patches, strings"]
    check --> ship["rimkit mod ship<br/>meta.lua becomes About.xml,<br/>folders copied into Mods/"]
    ship --> game["RimWorld loads it<br/>(first diagram)"]
    game --> hot["dev mode: game.dev.watch<br/>reloads Lua on save"]
    hot -.-> edit
    ship --> publish["rimkit publish<br/>Steam Workshop"]
```

## If your mod did not load

| What you see | Look at |
|--------------|---------|
| `LUA BLOCKED by builtin pAuth` | The gate is closed: reinstall RimKit, or run `node infrastructure/tools/release.js` after a rebuild |
| `Quarantined Lua skipped: <id>` | The threat scan matched your Lua. The log line names the file and rule |
| `Loading Lua from <id>` and then an error line | A runtime or syntax error in one of your files. The line number is the line in your file |
| Nothing at all for your mod | No `Lua/` folder, the mod is not enabled, or safe mode is on |
| `mod <id> was switched off after 20 Lua errors` | Fix the errors above that line and restart |

More in [troubleshooting](troubleshooting.md) and [architecture](../concepts/architecture.md).
