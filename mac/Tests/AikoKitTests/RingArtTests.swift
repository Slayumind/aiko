import Foundation
import Testing

@testable import AikoKit

struct RingArtTests {
    @Test(arguments: [
        (LimitTone.normal, Rgba(0x00, 0xBC, 0x7D)),
        (LimitTone.caution, Rgba(0xFE, 0x9A, 0x00)),
        (LimitTone.critical, Rgba(0xFF, 0x64, 0x67)),
        (LimitTone.unknown, Rgba(0xA1, 0xA1, 0xA1)),
    ])
    func everyToneHasTheColourOfThePalette(tone: LimitTone, expected: Rgba) {
        #expect(RingArt.colour(for: tone) == expected)
    }

    /// Limits have their own scale, white to violet (D-440), as RingDrawing.ColourFor on Windows.
    /// The menu bar may be light, where white is lost, so the icon draws the first step grey.
    @Test(arguments: [
        (LimitTone.normal, true, Rgba(0xFA, 0xFA, 0xFA)),
        (LimitTone.normal, false, Rgba(0xA1, 0xA1, 0xA1)),
        (LimitTone.caution, true, Rgba(0xC9, 0xB6, 0xFF)),
        (LimitTone.critical, false, Rgba(0x9D, 0x7B, 0xFF)),
        (LimitTone.unknown, true, Rgba(0xA1, 0xA1, 0xA1)),
    ])
    func aLimitGoesFromWhiteToViolet(tone: LimitTone, onDark: Bool, expected: Rgba) {
        #expect(RingArt.limitColour(for: tone, onDark: onDark) == expected)
    }

    @Test
    func theTrackAndTheWaitingRingAreTheSameSeeThroughGrey() {
        #expect(RingArt.track.red == 0x9A)
        #expect(RingArt.noData.red == 0x9A)
        expectClose(0.4, RingArt.track.alpha, places: 2)
        expectClose(0.549, RingArt.noData.alpha, places: 3)
    }

    @Test
    func theIconIsDrawnOnASixteenUnitGrid() {
        #expect(RingArt.grid == 16)
        #expect(RingArt.centre == 8)
        #expect(RingArt.ringRadius == 5.8)
        #expect(RingArt.ringThickness == 2.2)
        #expect(RingArt.dotRadius == 1.6)
    }

    @Test
    func fiveDashesAndFiveGapsCloseTheCircleExactly() {
        let radius = RingArt.ringRadius
        let pattern = RingArt.dashPattern(radius: radius)
        let circumference = 2 * Double.pi * radius

        expectClose(circumference, Double(RingArt.dashes) * (pattern.dash + pattern.gap), places: 9)
    }

    @Test
    func theGapsAreWiderThanTheDashes() {
        let pattern = RingArt.dashPattern(radius: RingArt.ringRadius)

        #expect(pattern.gap > pattern.dash)
    }

    @Test(arguments: [(-5, 0.0), (0, 0.0), (42, 0.42), (100, 1.0), (140, 1.0)])
    func theArcNeverGoesPastAWholeCircle(percent: Int, expected: Double) {
        expectClose(expected, RingArt.arcShare(percent), places: 6)
    }

    /// The island has room the menu bar has not, so its ring is drawn with a pen that is a share
    /// of the ring itself (RingGauge.cs).
    @Test
    func anIslandRingIsMadeOfItsOwnSize() {
        let ring = RingArt.islandRing(IslandLayout.ringSize)

        expectClose(3.24, ring.thickness, places: 6)
        expectClose(7.38, ring.radius, places: 6)
    }

    @Test
    func anIslandRingWithItsPenStaysInsideItsSquare() {
        let ring = RingArt.islandRing(IslandLayout.ringSize)

        expectClose(IslandLayout.ringSize, (ring.radius * 2) + ring.thickness, places: 6)
    }
}
