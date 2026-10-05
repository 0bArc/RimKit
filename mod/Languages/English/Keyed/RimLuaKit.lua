-- Strings for the English language. rimkit mod sync turns this file into RimLuaKit.xml, which the game reads.
-- Edit this file, not the XML. {0}, {1} are filled in by game.ui.translate(key, a, b).
return {
  RimKit_DevToolsNeedDevMode = "The RimKit dev tools need Development mode. Turn it on in the game options.",
  RimKit_ModDisabledRestart  = "{0} is switched off in your mod list. Restart the game to apply it.",
  RimKit_ModFailed_Disable   = "Turn it off for good",
  RimKit_ModFailed_Keep      = "Keep it in the mod list",
  RimKit_ModFailed_Label     = "A mod stopped working",
  RimKit_ModFailed_Text      = "The mod {0} had too many Lua errors (last in {1}), so RimKit switched its scripts off for this session. The rest of the game is unaffected.\\n\\nYou can leave it off until you restart, or turn it off in your mod list for good.",
  RimLuaKitMarker            = "RimKit",
}
