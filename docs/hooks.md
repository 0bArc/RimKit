# Hooks

```lua
rim.hooks.prefix(typeName, methodName, fn)
rim.hooks.postfix(typeName, methodName, fn)
rim.hooks.remove(hookId)
```

Example type/method: `RimWorld.JobGiver_GetFood`, `TryGiveJob`.

Prefix return:

| Return | Meaning |
|--------|---------|
| `true` or omit | Run original |
| `false` | Skip original; job givers get null result |

Prefer postfix when only observing. Keep prefix filters tight. Re-test after game updates.

Harmony stays a workshop dependency. Do not ship it inside your mod.
