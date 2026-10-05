-- A mod whose callbacks always fail. The error budget must switch it off after 20 errors.
game.hooks.postfix("Verse.Thing", "Destroy", function(ctx) error("noisy hook") end, { sig = { "Verse.DestroyMode" } })
game.events.on("thing.spawned", function() error("noisy event") end)
game.events.on_tick(function() error("noisy tick") end)

-- strict errors: the host fails pawn.name. Lenient returns nil, strict raises with the RK code.
local rk = require("rimkit")
local lenient = rim.pawn.name(5)
log.info("RESULT lenient=" .. tostring(lenient))
rk.strict_errors(true)
local ok, err = pcall(function() return rim.pawn.name(5) end)
rk.strict_errors(false)
log.info("RESULT strict " .. (ok and "did not raise" or ("raised " .. tostring(err):match("RK%d+"))))

-- explicit binder keys
game.hooks.has_patch("Verse.Thing", "TakeDamage")

-- hook cost report
for _, row in ipairs(game.hooks.list()) do
  if row.id == 1 then log.info("RESULT hooks calls=" .. tostring(row.calls)) end
end
