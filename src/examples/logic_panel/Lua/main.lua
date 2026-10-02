local PACK = "stratware.logicpanel"

rim.on_load(function()
  rim.log("[LogicPanel] loaded")
  local n = tonumber(data.get(PACK, "opens") or "0") or 0
  data.set(PACK, "opens", tostring(n + 1))
  rim.message("[LogicPanel] opens=" .. data.get(PACK, "opens"))
end)

rim.on_tick(function()
  if input.binding_just_pressed and input.binding_just_pressed("RimLua_LogicPanelOpen") then
    ui.panel({
      title = "Logic Panel",
      body = "Wave 1 demo. Opens counted in save data.",
      checks = { "Verbose" },
      list = { "data.get/set", "ui.panel", "health APIs" },
    })
  end
end)
