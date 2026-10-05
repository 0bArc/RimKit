-- The job Lua mods use for scripted work (game.jobs.register). rimkit mod sync turns this file into the XML the game reads.
def("JobDef", "RimLua_Scripted", {
  driverClass = "RimKit.JobDriver_RimLua",
  reportString = "doing scripted work.",
  allowOpportunisticPrefix = true,
  casualInterruptible = true,
})
