-- Comment-only bait: after long-bracket strip, these lines must NOT remain.
-- If scanner false-positives on comments, gate is too noisy.
--[=[
  os.execute("hidden in long bracket")
  io.open("/tmp/x")
]=]

--[[
load("return 1")
]]

-- Line comment should also be stripped:
-- os.execute("line comment only")

rim.on_load(function()
  rim.log("[BadProbe] comment-only file loaded (clean after strip)")
end)
