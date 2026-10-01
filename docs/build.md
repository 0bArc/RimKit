# Build

## Kit

```powershell
cmd /c src\native\build_release.bat
dotnet build .\src\host\RimLuaHost.csproj -c Release
```

CLI lands at `bin\rimkit.exe`.

```powershell
dotnet build .\src\host\RimLuaHost.csproj -c Release `
  /p:RimWorldDir="D:\SteamLibrary\steamapps\common\RimWorld" `
  /p:HarmonyPath="D:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll"
```

## rimkit

```text
rimkit init MyMod
rimkit mod create MyMod
rimkit mod sync [path]
rimkit mod ship [path] [ModsDir]
rimkit mod check [path]
rimkit build
```

Lua mods do not compile. `mod sync` / `mod ship` write About.xml from meta.lua.

Install kit repo + shipped mod under RimWorld `Mods/`. Enable Harmony → RimLuaKit → mod.
