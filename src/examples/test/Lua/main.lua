-- test
-- Starts here. The game runs this file when a game is loaded.
-- Read the docs for every game.* function: infrastructure/docs/api, or hover over it in VS Code with the RimKit extension.

game.events.on_load(function()
  game.log.info("[test] loaded")
  game.ui.message(game.ui.translate("test_Loaded"))
end)

