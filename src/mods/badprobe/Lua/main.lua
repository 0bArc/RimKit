-- quarantine test pack; leave disabled

rim.on_load(function()
  rim.log("[BadProbe] If you see this, the gate FAILED to block us.")
  rim.message("[BadProbe] GATE FAILED: hostile Lua ran")
end)

local function never_run_scanner_bait()
  os.execute("echo pwned")
  io.open("C:/Windows/win.ini", "r")
  load("return 1")()
  dofile("evil.lua")
  package.loadlib("x", "y")
  require("os")
  debug.getinfo(1)
  rim.reflect.static_call("System.Diagnostics.Process", "Start", "cmd")
  rim.cs.static_call("System.IO.File", "WriteAllText", "x")
end

rim.on_tick(function()
  if rim.input and rim.input.binding_just_pressed and rim.input.binding_just_pressed("RimLua_BadProbeKeys") then
    rim.message("[BadProbe] keybind fired (should never happen if gated)")
  end
end)
