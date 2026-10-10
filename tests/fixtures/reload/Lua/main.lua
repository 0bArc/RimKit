-- Registers one of everything a hot reload has to take away. The tests reload this mod and count what is left.
game.events.on_load(function()
  game.log.info("fixture loaded")
end)

game.events.on("pawn.damaged", function(e)
  game.log.info("damaged handler ran")
end)

game.events.on("thing.destroyed", function(e)
  game.log.info("destroyed handler ran")
end)

rim.on_tick(function()
  game.log.info("tick handler ran")
end)

game.timer.after(5, function()
  game.log.info("timer ran")
end)

game.dev.action("fixture action", function() end, "A debug action the fixture adds")
game.interop.publish("rimkit.fixture.reload", "1.0.0", { ping = function() return "pong" end })
