import Testing

@testable import AikoKit

struct IslandLayoutTests {
    /// "58%" in Geist Mono 11, near enough for a layout test.
    static let words = Extent(21, 13)

    static func two(_ edge: ScreenEdge, unfold: Double = 0, faceFit: Double = 0, docked: Bool = true) -> IslandFrame {
        IslandLayout.frame(
            labels: [words, words], edge: edge, docked: docked, unfold: unfold, faceFit: faceFit)
    }

    @Test
    func twoRingsOnATopEdgeMakeA66By34Island() {
        let frame = Self.two(.top)

        #expect(frame.size == Extent(66, 34))
    }

    @Test
    func twoRingsOnASideEdgeMakeA40By60Island() {
        let frame = Self.two(.left)

        #expect(frame.size == Extent(40, 60))
    }

    @Test
    func theDockedSideHasNoLineAndNoCorners() {
        let frame = Self.two(.top)

        #expect(frame.border.top == 0)
        #expect(frame.corners.topLeft == 0)
        #expect(frame.corners.topRight == 0)
        #expect(frame.corners.bottomLeft == IslandLayout.radius)
    }

    @Test(arguments: ScreenEdge.allCases)
    func inHandEveryCornerIsRoundedAndTheLineGoesAllRound(edge: ScreenEdge) {
        let frame = Self.two(edge, docked: false)

        #expect(frame.border == Edges(top: 1, right: 1, bottom: 1, left: 1))
        #expect(frame.corners == Corners.all(IslandLayout.radius))
    }

    /// The line that appears when the island is picked up comes out of the padding, so the rings
    /// stay where they were.
    @Test(arguments: ScreenEdge.allCases)
    func theRingsDoNotMoveByAPixelWhenTheLineAppears(edge: ScreenEdge) {
        let docked = Self.two(edge)
        let inHand = Self.two(edge, docked: false)

        #expect(docked.spots.map(\.ring) == inHand.spots.map(\.ring))
        #expect(docked.size == inHand.size)
    }

    @Test
    func theRingsStandInARowOnATopEdgeAndInAColumnOnASideOne() {
        let row = Self.two(.top).spots
        let column = Self.two(.left).spots

        #expect(row[1].ring.x == row[0].ring.right + IslandLayout.ringGap)
        #expect(row[0].ring.y == row[1].ring.y)
        #expect(column[1].ring.y == column[0].ring.bottom + IslandLayout.ringGap)
        #expect(column[0].ring.x == column[1].ring.x)
    }

    @Test
    func openingThePercentagesWidensAHorizontalIslandAndLeavesItsHeight() {
        let folded = Self.two(.top)
        let open = Self.two(.top, unfold: 1)

        #expect(open.size.height == folded.size.height)
        #expect(open.size.width == folded.size.width + (2 * (Self.words.width + IslandLayout.wordsGapBeside)))
    }

    @Test
    func openingThePercentagesMakesASideIslandTaller() {
        let folded = Self.two(.left)
        let open = Self.two(.left, unfold: 1)

        #expect(open.size.height == folded.size.height + (2 * (Self.words.height + IslandLayout.wordsGapUnder)))
        #expect(open.size.width > folded.size.width)
    }

    @Test
    func halfwayOpenHalfOfThePercentageIsOnShow() {
        let half = Self.two(.top, unfold: 0.5).spots[0]

        #expect(half.clip.width == (Self.words.width + IslandLayout.wordsGapBeside) / 2)
        #expect(half.clip.height == Self.words.height / 2)
    }

    /// The words keep their own size and slide out from under the cut, instead of being squashed.
    @Test
    func thePercentageIsCutOffRatherThanSqueezed() {
        let half = Self.two(.top, unfold: 0.5).spots[0]
        let open = Self.two(.top, unfold: 1).spots[0]

        #expect(half.words.width == open.words.width)
        #expect(half.words.height == open.words.height)
        #expect(half.words.x == half.clip.x + IslandLayout.wordsGapBeside)
    }

    @Test
    func foldedThereIsNothingOfThePercentageToDraw() {
        let folded = Self.two(.top).spots[0]

        #expect(folded.clip.width == 0)
        #expect(folded.clip.height == 0)
    }

    @Test(arguments: [(0.0, 0.0), (0.4, 0.0), (0.7, 0.5), (1.0, 1.0)])
    func theWordsFadeInOverTheLastPartOfTheWay(share: Double, opacity: Double) {
        expectClose(opacity, IslandLayout.wordsOpacity(share), places: 6)
    }

    /// The island closes in on the face, and the face has the same room on every side whatever
    /// edge the island is on (D-211).
    @Test(arguments: ScreenEdge.allCases)
    func withTheFaceTheIslandIsA34PointSquare(edge: ScreenEdge) {
        let frame = Self.two(edge, unfold: 1, faceFit: 1)

        #expect(frame.size == Extent(34, 34))
        #expect(frame.face.x == 8)
        #expect(frame.face.y == 8)
        #expect(frame.size.width - frame.face.right == 8)
        #expect(frame.size.height - frame.face.bottom == 8)
    }

    @Test
    func onTheWayToTheFaceTheIslandIsBetweenTheTwo() {
        let rings = Self.two(.top)
        let half = Self.two(.top, faceFit: 0.5)
        let face = Self.two(.top, faceFit: 1)

        #expect(half.size.width < rings.size.width)
        #expect(half.size.width > face.size.width)
    }

    /// The rings keep the size they asked for while the island closes in, so they only look smaller
    /// because the face motion scales them.
    @Test
    func theRingsStayInTheMiddleWhileTheIslandClosesIn() {
        let rings = Self.two(.top)
        let face = Self.two(.top, faceFit: 1)

        #expect(face.asked == rings.asked)
        expectClose(rings.spots[0].ring.centreX - rings.content.centreX,
                    face.spots[0].ring.centreX - face.content.centreX, places: 6)
    }

    @Test
    func withNothingSetUpThereIsOneRingAndNoPercentage() {
        let frame = IslandLayout.frame(labels: [], edge: .top, unfold: 1)

        #expect(frame.spots.count == 1)
        #expect(frame.spots[0].clip.width == 0)
        #expect(frame.size == Extent(40, 34))
    }
}
