-- RimKit marker for pawns the player can draft and order (see PawnControlBridge). It is defined here, not only in code, because saves
-- contain this hediff and the def must exist before a save is loaded. rimkit mod sync turns this file into the XML the game reads.
def("HediffDef", "RimLua_Controllable", {
  label = "rimlua controlled",
  description = "RimKit marker: player can draft and order this pawn.",
  hediffClass = "Hediff",
  defaultLabelColor = rgb(0.6, 0.8, 1.0),
  isBad = false,
  everCurableByItem = false,
  tendable = false,
  displayWound = false,
  maxSeverity = 1,
  initialSeverity = 1,
})
