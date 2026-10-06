-- Run with: rimkit mod test
-- Tests run against a mock host, so no game is needed. mock.pawn() makes a pawn the game would describe, game.emit sends an event,
-- and expect(effects.text) checks what the mod asked the game to show.

describe("damage", function()
  it("says it loaded", function()
    game.test.start()
    expect(game.test.logged("loaded")).to_be(true)
  end)

  it("floats the damage over a hurt colonist in red", function()
    local pawn = mock.pawn({ colonist = true, position = { x = 12, z = 30 } })

    game.emit.pawn_damaged(pawn, 7.8)

    expect(effects.text).to_have_been_called({ target = pawn, text = "7", color = "#ff4d4d" })
  end)

  it("uses orange for a humanlike pawn that is not a colonist", function()
    local pawn = mock.pawn({ colonist = false })

    game.emit.pawn_damaged(pawn, 3)

    expect(effects.text).to_have_been_called({ target = pawn, text = "3", color = "#ffa040" })
  end)

  it("ignores animals and other pawns that are not humanlike", function()
    local pawn = mock.pawn({ humanlike = false })

    game.emit.pawn_damaged(pawn, 5)

    expect(effects.text).not_to_have_been_called()
  end)

  it("ignores hits that did no damage", function()
    local pawn = mock.pawn()

    game.emit.pawn_damaged(pawn, 0)

    expect(effects.text).not_to_have_been_called()
  end)

  it("ignores a pawn that is no longer on the map", function()
    local pawn = mock.pawn({ position = false })

    game.emit.pawn_damaged(pawn, 4)

    expect(effects.text).not_to_have_been_called()
  end)

  it("hurts the selected pawns when the key goes down", function()
    local pawn = mock.pawn()
    game.test.mock("selection.pawns", { pawn.handle })
    game.test.mock("input.binding_just_pressed", true)
    game.test.start()

    game.test.tick()

    expect(spy.thing.damage).to_have_been_called({ amount = 10, damage_def = "Cut" })
  end)

  it("does nothing while the key is up", function()
    local pawn = mock.pawn()
    game.test.mock("selection.pawns", { pawn.handle })
    game.test.mock("input.binding_just_pressed", false)
    game.test.start()

    game.test.tick()

    expect(spy.thing.damage).not_to_have_been_called()
  end)
end)
