-- Strings for the English language. rimkit mod sync turns this file into TameAnomalies.xml, which the game reads.
-- Edit this file, not the XML. {0}, {1} are filled in by game.ui.translate(key, a, b).
return {
  RimLuaTameAnomaliesMarker = "Tame Anomalies",

  TameAnomalies_Unnamed = "the entity",
  TameAnomalies_NoSelection = "[TameAnomalies] Select an entity first.",
  TameAnomalies_NotAnEntity = "[TameAnomalies] That is not an anomaly entity.",
  TameAnomalies_AlreadyOurs = "[TameAnomalies] That entity is already recruited.",
  TameAnomalies_Recruited = "[TameAnomalies] Recruited {0}. Command it like a drafted animal.",
  TameAnomalies_RecruitFailed = "[TameAnomalies] Could not recruit {0}.",
  TameAnomalies_Released = "[TameAnomalies] Released {0}.",
  TameAnomalies_ReleaseFailed = "[TameAnomalies] {0} is not recruited, nothing to release.",
  TameAnomalies_ReleasedCount = "[TameAnomalies] Released {0} recruited entities.",
  TameAnomalies_NothingToRelease = "[TameAnomalies] No recruited entities to release.",
  TameAnomalies_KnockedOut = "[TameAnomalies] Knocked out {0}.",
  TameAnomalies_KnockOutFailed = "[TameAnomalies] Could not knock out {0}.",
  TameAnomalies_CaptureStarted = "[TameAnomalies] Capture started.",
  TameAnomalies_CaptureFailed = "[TameAnomalies] Capture failed. Knock the entity out first.",

  TameAnomalies_MenuRecruit = "Recruit to colony (RimKit)",
  TameAnomalies_MenuRelease = "Release (RimKit)",
  TameAnomalies_MenuKnockOut = "Knock out (anesthetic)",
  TameAnomalies_MenuCapture = "Capture (holding platform)",
  TameAnomalies_MenuNoPlatform = "Cannot capture: no holding platform",
}
