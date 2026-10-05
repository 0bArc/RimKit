# Tests

Manual and semi-automatic checks for the hook and reflection layers. There is no CI yet.

## native/hooktest.cpp

Loads `rimlua_core.dll` with fake host callbacks and checks the hook protocol without the game: arguments, results, state, finalizer, call replacement, strings that contain braces, event payloads, and lazy event subscription.

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x64
cl /nologo /EHsc hooktest.cpp /Fe:hooktest.exe
hooktest.exe path\to\rimlua_core.dll hooktest.lua
```

One check, `"cont":false` after a prefix `set_result`, is expected to report FAIL here. The skip on a prefix result is applied by the C# host, not by the native layer.

## smoke/

An in-game mod that exercises `game.reflect`, `rim.hooks.*` and named events against a real map and prints `SMOKE PASS` and `SMOKE FAIL` lines to `Player.log`, ending with `SMOKE DONE pass=N fail=M`.

1. Copy `smoke/` to `RimWorld/Mods/rimkit_smoke` and enable it after RimKit.
2. Start the game with `-quicktest`. The test waits for a map and about 60 game ticks, then runs.
3. The test turns `developer_reflect` on and restores the previous value when it finishes.

The last run on RimWorld 1.6.4871 reported `pass=38 fail=0`.

## native/aliastest.cpp

Checks the UNC alias runtime without the game: canonical `game.<domain>` names exist, an old name works and logs exactly one deprecation line, and a legacy event name (`pawn_died`) is served through the canonical event with the old handler signature.

```bat
cl /nologo /EHsc aliastest.cpp /Fe:aliastest.exe
aliastest.exe path\to\rimlua_core.dll aliastest.lua
```

The smoke mod also covers the aliases in the real game: it calls canonical and legacy names side by side. Last run: `pass=38 fail=0`.

