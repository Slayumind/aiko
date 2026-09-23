import Testing

@testable import AikoKit

struct IslandDragTests {
    /// An island of two rings on a top edge, taken by the middle.
    static let island = Box(100, 0, 66, 34)
    static let middle = Point(133, 17)

    @Test
    func aPressThatBarelyMovesIsStillAClick() {
        var drag = IslandDrag(pressedAt: Self.middle, island: Self.island)

        #expect(drag.hold(Point(135, 19)) == false)
        #expect(drag.isDragging == false)
    }

    @Test
    func threePointsAwayItIsADrag() {
        var drag = IslandDrag(pressedAt: Self.middle, island: Self.island)

        #expect(drag.hold(Point(136, 17)) == true)
    }

    @Test
    func threePointsDownIsADragToo() {
        var drag = IslandDrag(pressedAt: Self.middle, island: Self.island)

        #expect(drag.hold(Point(133, 20)) == true)
    }

    @Test
    func onceItIsADragItStaysOne() {
        var drag = IslandDrag(pressedAt: Self.middle, island: Self.island)
        drag.hold(Point(200, 200))

        #expect(drag.hold(Self.middle) == true)
    }

    /// The island turns from a row into a column in hand. The grip is a share of its size, so the
    /// pointer keeps holding the same spot of it.
    @Test
    func theGripIsKeptWhenTheIslandChangesShape() {
        let drag = IslandDrag(pressedAt: Self.middle, island: Self.island)

        let asRow = drag.corner(Point(500, 400), Extent(66, 34))
        let asColumn = drag.corner(Point(500, 400), Extent(40, 60))

        #expect(asRow == Point(500 - 33, 400 - 17))
        #expect(asColumn == Point(500 - 20, 400 - 30))
    }

    @Test
    func anIslandHeldByItsLeftEdgeHangsFromThere() {
        let drag = IslandDrag(pressedAt: Point(100, 0), island: Self.island)

        #expect(drag.corner(Point(700, 500), Extent(66, 34)) == Point(700, 500))
    }

    @Test
    func theIslandInHandIsABoxPlacementCanRead() {
        let drag = IslandDrag(pressedAt: Self.middle, island: Self.island)

        let held = drag.held(Point(500, 400), Extent(66, 34))

        #expect(held == Box(467, 383, 66, 34))
    }
}
