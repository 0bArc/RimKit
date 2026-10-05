-- Run with: rimkit mod test mod   (from the repo root). Builds every dev tools tab against the mock host.
local t = game.test

local function find_tab(node, id)
  for _, tab in ipairs(node.tabs) do if tab.id == id then return tab.content end end
end

t.describe("devtools", function()
  t.it("builds every tab", function()
    local dt = game.interop.get("rimkit.devtools")
    local node = dt.view({ state = {} })
    t.expect(node.type).to_be("tabs")
    t.expect(#node.tabs).to_be(7)
  end)
  t.it("the reflect tab asks for a root first", function()
    local dt = game.interop.get("rimkit.devtools")
    local tab = find_tab(dt.view({ state = {} }), "reflect")
    t.expect(tab.children[2].text).to_contain("Pick a root")
  end)
  t.it("browses a type, reads values and opens an object member", function()
    t.mock("reflect.members", {
      { name = "TicksGame", kind = "property", type = "Int32", static = false },
      { name = "Maps", kind = "property", type = "List", static = true },
      { name = "TickManager", kind = "property", type = "TickManager", static = true },
    })
    t.mock("reflect.static_get", function(a) if a.member == "TickManager" then return rim.wrap_map(9) else return 4 end end)
    t.mock("reflect.type", "Verse.TickManager")
    local dt = game.interop.get("rimkit.devtools")
    dt.on_event({ id = "ref_root:find", window = 1 })
    local tab = find_tab(dt.view({ state = {} }), "reflect")
    local scroll = tab.children[4]
    t.expect(scroll.type).to_be("scroll")
    local rows = scroll.child.children
    t.expect(#rows).to_be(3)            -- an instance property is listed but not read on a type root
    local has_open = false
    for _, c in ipairs(rows[3].children) do if c.id == "ref_open:TickManager" then has_open = true end end
    t.expect(has_open).to_be(true)
    dt.on_event({ id = "ref_open:TickManager", window = 1 })
    dt.on_event({ id = "ref_back", window = 1 })
  end)
  t.it("explains when typed reflection is off", function()
    t.mock("reflect.members", function() error("RK4002: typed reflection is off") end)
    local dt = game.interop.get("rimkit.devtools")
    dt.on_event({ id = "ref_root:find", window = 1 })
    local tab = find_tab(dt.view({ state = {} }), "reflect")
    t.expect(tab.children[2].text).to_contain("developer_reflect")
  end)
end)
