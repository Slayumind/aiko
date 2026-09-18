import Foundation
import Testing

@testable import AikoKit

struct SvgPathTests {
    @Test
    func aMoveAndALine() {
        let steps = SvgPath.steps("M2 3 L8 9")

        #expect(steps == [.move(Point(2, 3)), .line(Point(8, 9))])
    }

    @Test
    func morePairsAfterAMoveAreLines() {
        let steps = SvgPath.steps("M0 0 4 0 4 4")

        #expect(steps == [.move(Point(0, 0)), .line(Point(4, 0)), .line(Point(4, 4))])
    }

    @Test
    func acrossAndDown() {
        let steps = SvgPath.steps("M2 3 H10 V20")

        #expect(steps == [.move(Point(2, 3)), .line(Point(10, 3)), .line(Point(10, 20))])
    }

    @Test
    func aRelativeLineCountsFromWhereItIs() {
        let steps = SvgPath.steps("M2 3 l4 -1 l4 -1")

        #expect(steps == [.move(Point(2, 3)), .line(Point(6, 2)), .line(Point(10, 1))])
    }

    @Test
    func closingComesBackToTheStart() {
        let steps = SvgPath.steps("M2 2 L6 2 Z l0 4")

        #expect(steps == [.move(Point(2, 2)), .line(Point(6, 2)), .close, .line(Point(2, 6))])
    }

    /// A quadratic curve becomes a cubic one with its control points two thirds of the way.
    @Test
    func aQuadraticCurveBecomesACubicOne() {
        let steps = SvgPath.steps("M0 0 Q3 3 6 0")

        #expect(steps == [.move(Point(0, 0)), .curve(Point(2, 2), Point(4, 2), Point(6, 0))])
    }

    @Test
    func aSmoothQuadraticTurnsTheLastControlPointAround() {
        let steps = SvgPath.steps("M0 0 Q3 3 6 0 T12 0")

        guard case .curve(let first, _, let to) = steps[2] else {
            Issue.record("the smooth curve is missing")
            return
        }

        // The control point 3,3 turned around 6,0 is 9,-3, and as a cubic that is 8,-2.
        #expect(first == Point(8, -2))
        #expect(to == Point(12, 0))
    }

    @Test
    func aSmoothCubicTurnsTheLastControlPointAround() {
        let steps = SvgPath.steps("M0 0 C1 2 3 2 4 0 S7 -2 8 0")

        guard case .curve(let first, let second, let to) = steps[2] else {
            Issue.record("the smooth curve is missing")
            return
        }

        #expect(first == Point(5, -2))
        #expect(second == Point(7, -2))
        #expect(to == Point(8, 0))
    }

    /// The circles of the faces are written as two half turns. Whatever the reader makes of them
    /// has to end where the arc ends and stay on the circle on the way.
    @Test
    func anArcEndsWhereItWasAskedTo() {
        let steps = SvgPath.steps("M2 8 A6 6 0 1 0 14 8")

        guard case .curve(_, _, let to) = steps.last else {
            Issue.record("the arc drew nothing")
            return
        }

        expectClose(14, to.x, places: 6)
        expectClose(8, to.y, places: 6)
    }

    @Test
    func anArcStaysOnItsCircle() {
        let steps = SvgPath.steps("M2 8 A6 6 0 1 0 14 8")
        var at = Point(2, 8)

        for step in steps.dropFirst() {
            guard case .curve(let one, let two, let to) = step else {
                Issue.record("an arc piece is not a curve")
                continue
            }

            // Both ends of a piece and its middle sit on the circle: a cubic follows a quarter
            // turn closely enough that nobody can see the difference at 18 points.
            let middle = Self.halfway(at, one, two, to)
            for point in [to, middle] {
                expectClose(6, sqrt(pow(point.x - 8, 2) + pow(point.y - 8, 2)), places: 2)
            }
            at = to
        }
    }

    /// The point in the middle of a cubic curve.
    private static func halfway(_ from: Point, _ one: Point, _ two: Point, _ to: Point) -> Point {
        Point(
            (from.x + (3 * one.x) + (3 * two.x) + to.x) / 8,
            (from.y + (3 * one.y) + (3 * two.y) + to.y) / 8)
    }

    @Test
    func aHalfTurnIsDrawnInTwoPieces() {
        let steps = SvgPath.steps("M2 8 A6 6 0 1 0 14 8")

        #expect(steps.count == 3)
    }

    @Test
    func anArcWithNoRadiusIsAStraightLine() {
        let steps = SvgPath.steps("M0 0 A0 0 0 0 1 4 4")

        #expect(steps == [.move(Point(0, 0)), .line(Point(4, 4))])
    }

    @Test
    func nothingAtAllDrawsNothing() {
        #expect(SvgPath.steps("") == [])
        #expect(SvgPath.steps("   ") == [])
    }

    /// Path data we cannot read stops there. A face drawn short beats a crash in the menu bar.
    @Test
    func aHalfWrittenCommandStopsThePath() {
        let steps = SvgPath.steps("M2 3 L8")

        #expect(steps == [.move(Point(2, 3))])
    }

    /// The real proof: every shape of every face reads as an outline that starts with a move.
    @Test
    func everyFaceOfEveryStyleReads() {
        for style in FaceStyle.allCases {
            for face in AikoFace.allCases {
                for ground in FaceGround.allCases {
                    for small in [false, true] {
                        let picture = FaceArt.draw(style, face, ground, small)
                        for group in picture.groups {
                            for shape in group.shapes {
                                let steps = SvgPath.steps(shape.data)
                                #expect(steps.count > 1, "\(style) \(face) \(ground) small \(small)")
                                if case .move = steps.first {
                                    continue
                                }
                                Issue.record("a shape does not start with a move")
                            }
                        }

                        for hole in Self.holes(in: picture) {
                            #expect(SvgPath.steps(hole).count > 1)
                        }
                    }
                }
            }
        }
    }

    private static func holes(in picture: FacePicture) -> [String] {
        picture.groups.flatMap { $0.shapes.compactMap(\.holes).flatMap { $0 } }
    }
}
