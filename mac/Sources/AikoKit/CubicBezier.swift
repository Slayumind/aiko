import Foundation

/// The CSS cubic-bezier timing curve, which neither WPF nor SwiftUI gives us as it is.
///
/// The accepted prototype was built in the browser with two curves: an easing for anything that
/// opens and a spring that overshoots a little when something lands (DESIGN.md, D-161). This is
/// the same maths, so the windows move the way the prototype did.
public struct CubicBezier: Sendable, Equatable {
    public let x1: Double
    public let y1: Double
    public let x2: Double
    public let y2: Double

    public init(_ x1: Double, _ y1: Double, _ x2: Double, _ y2: Double) {
        self.x1 = x1
        self.y1 = y1
        self.x2 = x2
        self.y2 = y2
    }

    /// Opening, appearing, hover: fast start, soft landing.
    public static let standard = CubicBezier(0.2, 0.8, 0.2, 1)

    /// Release: goes a little past the end and settles back.
    public static let spring = CubicBezier(0.3, 1.4, 0.5, 1)

    /// Progress along the curve at a share of the time, both from 0 to 1. The curve is given by x,
    /// so x is solved for t first, and y at that t is the answer.
    public func ease(_ time: Double) -> Double {
        if time <= 0 {
            return 0
        }

        if time >= 1 {
            return 1
        }

        return CubicBezier.sample(y1, y2, solveForX(time))
    }

    private func solveForX(_ x: Double) -> Double {
        // Newton first: a few steps are enough for every curve Aiko uses.
        var t = x
        for _ in 0..<8 {
            let error = CubicBezier.sample(x1, x2, t) - x
            if abs(error) < 1e-6 {
                return t
            }

            let slope = CubicBezier.slope(x1, x2, t)
            if abs(slope) < 1e-6 {
                break
            }

            t -= error / slope
        }

        // Bisection when Newton cannot settle, which happens where the curve is almost flat.
        var low = 0.0
        var high = 1.0
        t = x
        for _ in 0..<40 {
            let value = CubicBezier.sample(x1, x2, t)
            if abs(value - x) < 1e-6 {
                break
            }

            if value < x {
                low = t
            } else {
                high = t
            }

            t = (low + high) / 2
        }

        return t
    }

    private static func sample(_ p1: Double, _ p2: Double, _ t: Double) -> Double {
        ((((1 - (3 * p2)) + (3 * p1)) * t + ((3 * p2) - (6 * p1))) * t + (3 * p1)) * t
    }

    private static func slope(_ p1: Double, _ p2: Double, _ t: Double) -> Double {
        (3 * ((1 - (3 * p2)) + (3 * p1)) * t * t) + (2 * ((3 * p2) - (6 * p1)) * t) + (3 * p1)
    }
}
