import Foundation

/// How Aiko moves, in one place (DESIGN.md, D-161). The twin of Motion.cs on Windows, in seconds
/// because that is what both AppKit and SwiftUI count in.
///
/// Everything here runs only in answer to the mouse, the keyboard or new data. Nothing loops, and
/// an animation that has finished leaves no timer behind.
public enum Motion {
    public static let press = 0.090
    public static let hover = 0.120
    public static let expand = 0.180
    public static let settle = 0.280

    public static let pressedScale = 0.97
    public static let appearShift = 4.0

    /// The card grows out of the icon: from a little smaller and a few points towards it.
    public static let popFrom = 0.94
    public static let popShift = 6.0

    public static let standard = CubicBezier.standard
    public static let spring = CubicBezier.spring

    /// Zero when the system is asked to show fewer animations, so the window lands at its end
    /// state at once instead of being animated faster.
    public static func or0(_ duration: Double, reduceMotion: Bool) -> Double {
        reduceMotion ? 0 : duration
    }
}
