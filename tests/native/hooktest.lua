local ids = rim.hooks.patch{
  type = "Verse.Thing", method = "TakeDamage", sig = {"Verse.DamageInfo"}, priority = 600, before = {"other.mod"},
  prefix = function(ctx)
    log.info("T1 amount=" .. tostring(ctx.named.amount) .. " who=" .. ctx.args[2].name .. " text=" .. ctx.args[3])
    log.info("T1 cell=" .. ctx.args[4].x .. "," .. ctx.args[4].z .. " mode=" .. ctx.arg_modes[4] .. " result=" .. tostring(ctx.result))
    ctx:set_arg(1, ctx.args[1] + 5)
    ctx:set_result(42)
    ctx:set_state({ n = 1 })
  end,
  postfix = function(ctx)
    log.info("T2 state=" .. ctx.state.n .. " result=" .. tostring(ctx.result))
  end,
  finalizer = function(ctx)
    log.info("T3 exception=" .. tostring(ctx.exception))
    ctx:suppress()
  end,
}
assert(ids.prefix and ids.postfix and ids.finalizer)

rim.hooks.replace_call{
  type = "Verse.Y", method = "Run", call = "Verse.Z.Add", nth = 2,
  fn = function(ctx) return ctx.args[1] + ctx.args[2] end,
}

events.on("pawn.died", function(p)
  log.info("T5 pawn=" .. p.pawn.name .. " dmg=" .. p.damage.def .. " " .. p.damage.amount)
end)
