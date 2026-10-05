-- A well behaved mod that shares the Lua state with the noisy one. It must keep running.
game.events.on("thing.spawned", function() log.info("QUIET handled thing.spawned") end)
game.events.on("thing.despawned", function() log.info("QUIET handled thing.despawned") end)
