game.events.on_load(function()
  log.info("[LuaModTemplate] loaded")
end)

-- Phase 2 example (optional):
-- game.jobs.register("wave", {
--   can_do = function(pawn) return pawn.is_colonist end,
--   execute = function(pawn)
--     log.info(pawn.name .. " wave job done")
--     return true
--   end
-- })
