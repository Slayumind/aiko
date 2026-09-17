import Testing

@testable import AikoKit

struct CubicBezierTests {
    @Test(arguments: [0.0, 1.0])
    func everyCurveStartsAt0AndEndsAt1(time: Double) {
        expectClose(time, CubicBezier.standard.ease(time), places: 6)
        expectClose(time, CubicBezier.spring.ease(time), places: 6)
    }

    @Test
    func timeOutsideTheAnimationIsClamped() {
        #expect(CubicBezier.standard.ease(-0.5) == 0)
        #expect(CubicBezier.standard.ease(1.5) == 1)
    }

    @Test(arguments: [0.1, 0.5, 0.9])
    func aStraightCurveIsLinear(time: Double) {
        expectClose(time, CubicBezier(1.0 / 3, 1.0 / 3, 2.0 / 3, 2.0 / 3).ease(time), places: 4)
    }

    @Test
    func standardIsMostOfTheWayThereEarly() {
        // 0.827631 is the same curve solved by bisection outside this code, to six places.
        expectClose(0.827631, CubicBezier.standard.ease(0.3), places: 5)
    }

    @Test
    func springGoesPastTheEndBeforeItSettles() throws {
        let peak = try #require((1...99).map { CubicBezier.spring.ease(Double($0) / 100.0) }.max())

        #expect(peak > 1.02, "peak was \(peak)")
        #expect(peak < 1.15, "peak was \(peak)")
    }

    @Test
    func standardNeverGoesBackwards() {
        let values = (0...100).map { CubicBezier.standard.ease(Double($0) / 100.0) }

        for (first, second) in zip(values, values.dropFirst()) {
            #expect(second >= first - 1e-9)
        }
    }
}
