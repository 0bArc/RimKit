# Don't Eat (RimWorld 1.6)

A small Harmony mod that blocks player-controlled humanlike pawns from taking self-feeding jobs when colony food reserves are low.

## What this mod currently does

- Patches `JobGiver_GetFood.TryGiveJob` with a Harmony Prefix.
- If a pawn is:
  - humanlike,
  - in the player faction,
  - on a map with a resource counter,
- then food seeking is blocked when `TotalHumanEdibleNutrition < 10`.
- Adds an in-game dev GUI window for live testing and tuning.
- Provides a keybinding def: `DontEat_ToggleDevWindow` (default `F8`).
- Provides fallback hotkey `Ctrl+F8` if keybinding defs are not loaded yet.

## Folder structure

- `About/About.xml` - mod metadata and dependency declaration.
- `Source/DontEat.csproj` - C# project for compiling the mod DLL.
- `Source/DontEatLogic.cs` - Harmony startup and patch logic.
- `Assemblies/` - output folder for built DLL.

## Build

From this mod root folder:

```powershell
dotnet build .\Source\DontEat.csproj -c Release
```

Output DLL:

- `Assemblies/DontEat.dll`

## Build path overrides (if needed)

The project auto-detects common Windows Steam paths. If your install is different, pass these:

```powershell
dotnet build .\Source\DontEat.csproj -c Release /p:RimWorldDir="D:\SteamLibrary\steamapps\common\RimWorld" /p:HarmonyPath="D:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll"
```

## In-game setup

1. Subscribe to or install Harmony (`brrainz.harmony`).
2. Put this mod folder in your RimWorld `Mods` directory (or symlink it).
3. Enable mods in this order:
   1. Harmony
   2. Don't Eat
4. Restart when prompted by RimWorld.

## In-game controls

- Press `F8` to toggle the Don't Eat dev GUI window.
- You can rebind the key from RimWorld's Controls menu:
  - search for `toggle don't eat dev window`
- Fallback shortcut: `Ctrl+F8`

## GUI features

- View current map human-edible nutrition.
- View selected pawn hunger percentage.
- Recount map food immediately.
- Spawn 10 simple meals near selected pawn.
- Tune food threshold and emergency hunger override live.
- Toggle debug logging and in-game debug messages.

## Learn RimWorld modding properly (recommended path)

1. Start with mod folder and About.xml fundamentals.
2. Set up a C# class library that outputs to `Assemblies`.
3. Reference game managed DLLs + Harmony DLL (without bundling Harmony in your mod).
4. Use decompiled game code to verify patch targets and signatures before patching.
5. Prefer Postfix patches when possible for compatibility; use Prefix skip only when necessary.
6. Re-test after each game update and review the RimWorld 1.6 mod update notes.

## Notes for maintainability

- Harmony patch methods are static by design.
- Prefix methods returning `false` skip the original method.
- Keep patch scope narrow (faction/map/condition checks) to reduce mod conflicts.

## RimWorld 1.6 troubleshooting notes

- `ResourceCounter.TotalFood` is gone in 1.6. Use `TotalHumanEdibleNutrition`.
- Do not add unsupported fields to `About.xml` (for example `modClass` in this setup); unsupported fields can break metadata loading.
- For GUI windows and keyboard polling, make sure your project references:
  - `UnityEngine.IMGUIModule.dll`
  - `UnityEngine.InputLegacyModule.dll`
