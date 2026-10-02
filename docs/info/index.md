# Request a blocked feature

Prefer **named least-privilege** APIs over raw OS. High malware-class asks (keylog, free Process, free disk, silent net) are denied. Gameplay and jailed utils are in scope. See [Strong API domains](../api/index.md).

If your mod **needs** a blocked capability, do not invent a bypass. Ask for a first-class, allowlisted API instead.

## How to ask

1. Open an issue on the RimKit repo (or contact Team Stratware.win the way the project lists).  
2. Title it clearly, e.g. `Feature request: allowlisted save-backup copy`.  
3. In the body, include:

- **What the mod does** (one short RimWorld use case)  
- **Which blocked thing you hit** (e.g. write under Saves/, open Explorer, HTTP POST)  
- **Why in-game APIs are not enough**  
- **Least privilege**: exact paths, verbs, and limits you would accept (not “full io.open”)  
- **Risk notes**: what a malicious mod could do if this API shipped  

Example shape:

```text
Mod: Auto-backup before raid
Need: copy current save to <mod>/Backups/ only
Not enough: rim.ui.message alone
Acceptable API: rim.saves.backup_to_mod_folder() (no arbitrary path)
Risk: only writes under calling mod root; no Process.Start
```

## What we do

1. **Review** the request against security (sandbox, allowlist, AuthGate).  
2. **Decide** possible / possible with limits / not possible on the Lua path.  
3. **Reply** with that decision. If yes, it lands as a named `rim.*` (or host) op after review, not as raw `os`/`io`/`reflect`.  

“Not possible” usually means: use a normal C# `Assemblies/*.dll` mod, or keep the tool outside RimWorld.

## Rules for requests

- One capability per issue when you can.  
- Prefer **narrow** APIs over restoring `os` / `io` / `load`.  
- No guarantee of timeline or acceptance.  
- Shipping Lua that trips the threat scan **quarantines that pack only**; other Lua mods still load.  

## Related

- [Create a Lua mod](../mod-create/index.md)  
- [Lua API](../lua-api.md)  
- [Build / ship](../build.md)  
