-- Run with: rimkit mod test src/examples/MoreChairs
local t = game.test

-- A pawn (handle 5) standing at cell 10,12 where one chair (handle 21) of the given def stands.
local function pawn_on(chair_def)
  t.mock("pawn.map", 1)
  t.mock("thing.info", function(a)
    if a.h == 5 then return { x = 10, z = 12 } end
    return { def = chair_def }
  end)
  t.mock("map.things_at", { 21 })
  t.mock("pawn.add_thought", true)
end

local function ate(chairs, job, condition)
  return chairs.on_job_ended({ pawn = rim.wrap(5), job = job, condition = condition })
end

t.describe("MoreChairs", function()
  t.it("gives a mood boost for a meal eaten in a comfortable chair", function()
    pawn_on("MoreChairs_Rocker")
    t.expect(ate(game.interop.get("morechairs"), "Ingest", "Succeeded")).to_be(true)
    t.expect(t.calls("pawn.add_thought")[1].args.def).to_be("MoreChairs_ComfySeat")
  end)

  t.it("gives nothing for the plain stool", function()
    pawn_on("MoreChairs_Stool")
    t.expect(ate(game.interop.get("morechairs"), "Ingest", "Succeeded")).to_be(false)
    t.expect(#t.calls("pawn.add_thought")).to_be(0)
  end)

  t.it("ignores other jobs and meals that were interrupted", function()
    pawn_on("MoreChairs_Gamer")
    local chairs = game.interop.get("morechairs")
    t.expect(ate(chairs, "Wait", "Succeeded")).to_be(false)
    t.expect(ate(chairs, "Ingest", "Incompletable")).to_be(false)
  end)

  t.it("ignores a pawn that is not on a map", function()
    t.mock("pawn.map", 0)
    t.expect(ate(game.interop.get("morechairs"), "Ingest", "Succeeded")).to_be(false)
  end)

  t.it("reacts to the job.ended event", function()
    pawn_on("MoreChairs_Gamer")
    t.start()
    t.emit("job.ended", { pawn = rim.wrap(5), job = "Ingest", condition = "Succeeded" })
    t.expect(#t.calls("pawn.add_thought")).to_be(1)
  end)

  t.it("finds no texture problem when every texture exists", function()
    t.mock("graphics.texture_info", { exists = true, width = 128, height = 128 })
    t.expect(#game.interop.get("morechairs").missing_textures()).to_be(0)
  end)

  t.it("names the chair whose texture is missing", function()
    t.mock("graphics.texture_info", function(a) return { exists = not a.path:find("Rocker", 1, true) } end)
    local missing = game.interop.get("morechairs").missing_textures()
    t.expect(#missing).to_be(1)
    t.expect(missing[1]).to_be("MoreChairs_Rocker")
  end)

  t.it("logs an error naming the chairs when textures are missing", function()
    t.mock("graphics.texture_info", { exists = false })
    t.start()
    t.expect(t.logged("missing texture for MoreChairs_Gamer, MoreChairs_Rocker, MoreChairs_Stool")).to_be(true)
  end)
end)
