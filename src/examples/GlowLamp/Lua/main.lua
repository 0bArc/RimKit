-- GlowLamp
-- A building whose behavior is a Lua component. The Def in Defs/Lamp.xml gives the lamp the class "glow_pulse";
-- the functions below run for every lamp. Shows a Lua comp, data saved with the object, and the inspect text.

game.classes.define("comp", "glow_pulse", {
  spawn = function(thing, respawning)
    if not respawning then game.classes.data_set(thing, "pulses", "0") end
  end,

  -- Runs about every 250 ticks. Count a pulse, and float a spark every fourth one.
  tick_rare = function(thing)
    local pulses = (tonumber(game.classes.data_get(thing, "pulses")) or 0) + 1
    game.classes.data_set(thing, "pulses", tostring(pulses))
    if pulses % 4 == 0 then
      local info = game.things.info(thing)
      game.effects.text(info.map, info.x, info.z, game.ui.translate("glowlamp_Spark"), "good")
    end
  end,

  inspect_string = function(thing)
    return game.ui.translate("glowlamp_Pulses", game.classes.data_get(thing, "pulses") or "0")
  end,
})
