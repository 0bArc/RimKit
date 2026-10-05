-- Doubles the result of a getter. Used by hookbench to compare the JSON path with the fast path.
game.hooks.postfix("Verse.TickManager", "TickRateMultiplier", function(ctx)
  ctx:set_result(ctx.result * 2)
end)
