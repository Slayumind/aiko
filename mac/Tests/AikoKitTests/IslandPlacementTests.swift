import Testing

@testable import AikoKit

struct IslandPlacementTests {
    /// A 1920 by 1080 screen with the taskbar at the bottom.
    static let work = Box(0, 0, 1920, 1040)

    static let width = 160.0
    static let height = 34.0

    @Test
    func anIslandNearTheTopSticksToTheTop() {
        let dropped = Box(880, 12, Self.width, Self.height)

        #expect(IslandPlacement.nearest(dropped, Self.work).edge == .top)
    }

    @Test
    func anIslandNearTheRightSticksToTheRight() {
        let dropped = Box(1740, 500, Self.width, Self.height)

        #expect(IslandPlacement.nearest(dropped, Self.work).edge == .right)
    }

    @Test
    func anIslandNearTheBottomSticksToTheBottom() {
        let dropped = Box(400, 1000, Self.width, Self.height)

        #expect(IslandPlacement.nearest(dropped, Self.work).edge == .bottom)
    }

    @Test
    func theMiddleOfTheTopEdgeIsAHalf() {
        let middle = Box((Self.work.width - Self.width) / 2, 0, Self.width, Self.height)

        expectClose(0.5, IslandPlacement.nearest(middle, Self.work).along, places: 3)
    }

    @Test
    func thePlaceIsKeptAsAShareSoASmallerScreenKeepsIt() {
        let position = IslandPosition(.top, 0.5)

        let onBig = IslandPlacement.place(position, Self.width, Self.height, Self.work)
        let onSmall = IslandPlacement.place(position, Self.width, Self.height, Box(0, 0, 1280, 700))

        expectClose(Self.work.centreX, onBig.centreX, places: 3)
        expectClose(640, onSmall.centreX, places: 3)
    }

    @Test
    func aSavedPlaceComesBackTheSame() {
        let dropped = Box(1500, 3, Self.width, Self.height)

        let position = IslandPlacement.nearest(dropped, Self.work)
        let back = IslandPlacement.place(position, Self.width, Self.height, Self.work)

        expectClose(dropped.x, back.x, places: 3)
        expectClose(Self.work.y, back.y, places: 3)
    }

    @Test
    func theIslandIsPressedAgainstItsEdge() {
        expectClose(
            Self.work.y,
            IslandPlacement.place(IslandPosition(.top, 0.3), Self.width, Self.height, Self.work).y,
            places: 3)
        expectClose(
            Self.work.bottom - Self.height,
            IslandPlacement.place(IslandPosition(.bottom, 0.3), Self.width, Self.height, Self.work).bottom - Self.height,
            places: 3)
        expectClose(
            Self.work.x,
            IslandPlacement.place(IslandPosition(.left, 0.3), Self.width, Self.height, Self.work).x,
            places: 3)
        expectClose(
            Self.work.right - Self.width,
            IslandPlacement.place(IslandPosition(.right, 0.3), Self.width, Self.height, Self.work).x,
            places: 3)
    }

    @Test
    func anIslandWiderThanTheScreenHasNowhereToSlide() {
        let narrow = Box(0, 0, 100, 800)

        let placed = IslandPlacement.place(IslandPosition(.top, 0.8), Self.width, Self.height, narrow)

        expectClose(narrow.x, placed.x, places: 3)
    }

    @Test
    func aShareOutsideTheScreenIsPulledBackIn() {
        let placed = IslandPlacement.place(IslandPosition(.top, 4), Self.width, Self.height, Self.work)

        expectClose(Self.work.right - Self.width, placed.x, places: 3)
    }

    @Test
    func ringsStandInARowOnTopAndBottomAndInAColumnOnTheSides() {
        #expect(IslandPlacement.isHorizontal(.top))
        #expect(IslandPlacement.isHorizontal(.bottom))
        #expect(!IslandPlacement.isHorizontal(.left))
        #expect(!IslandPlacement.isHorizontal(.right))
    }

    @Test
    func aSecondScreenToTheLeftHasNegativeCoordinatesAndStillWorks() {
        // Windows puts a monitor left of the main one at a negative x, and so does macOS.
        let second = Box(-1920, 0, 1920, 1080)
        let dropped = Box(-1000, 8, Self.width, Self.height)

        let position = IslandPlacement.nearest(dropped, second)
        let back = IslandPlacement.place(position, Self.width, Self.height, second)

        #expect(position.edge == .top)
        expectClose(dropped.x, back.x, places: 3)
    }

    // ---- the island in hand ----

    @Test(arguments: [
        (960.0, 30.0, ScreenEdge.top),
        (960.0, 1000.0, ScreenEdge.bottom),
        (40.0, 520.0, ScreenEdge.left),
        (1880.0, 520.0, ScreenEdge.right),
    ])
    func theLandingStripGoesToTheEdgeNearestTheMiddle(x: Double, y: Double, edge: ScreenEdge) {
        #expect(IslandPlacement.nearestEdge(x, y, Self.work) == edge)
    }

    @Test
    func nearACornerTheEdgeInUseHoldsUntilAnotherIsClearlyCloser() {
        // 60 from the top, 50 from the left: the left is closer, but by less than the stickiness.
        #expect(IslandPlacement.nearestEdge(50, 60, Self.work, .top) == .top)
        #expect(IslandPlacement.nearestEdge(50, 60, Self.work) == .left)

        // 80 from the top: now the left is closer by 30, and the strip moves.
        #expect(IslandPlacement.nearestEdge(50, 80, Self.work, .top) == .left)
    }

    @Test
    func lettingGoKeepsThePlaceAlongTheEdgeWhereTheMiddleWas() {
        let position = IslandPlacement.dropAt(.top, 960, 200, Self.width, Self.height, Self.work)
        let placed = IslandPlacement.place(position, Self.width, Self.height, Self.work)

        #expect(position.edge == .top)
        expectClose(960, placed.centreX, places: 3)
        #expect(placed.y == 0)
    }

    @Test
    func lettingGoPastTheEndOfAnEdgeStopsAtTheEnd() {
        let position = IslandPlacement.dropAt(.right, 1900, 1200, 34, 60, Self.work)

        #expect(position.along == 1)
        expectClose(1040 - 60, IslandPlacement.place(position, 34, 60, Self.work).y, places: 3)
    }

    /// The landing strip reaches past the screen edge by its own corner radius, so the corners on
    /// that side fall outside the screen and the edge reads flat (D-186).
    @Test
    func theLandingStripReachesPastTheEdgeItSitsOn() {
        let strip = Box(100, 0, 200, 60)

        #expect(IslandPlacement.pastTheEdge(strip, .top, by: 8) == Box(100, -8, 200, 68))
        #expect(IslandPlacement.pastTheEdge(strip, .bottom, by: 8) == Box(100, 0, 200, 68))
        #expect(IslandPlacement.pastTheEdge(strip, .left, by: 8) == Box(92, 0, 208, 60))
        #expect(IslandPlacement.pastTheEdge(strip, .right, by: 8) == Box(100, 0, 208, 60))
    }
}
