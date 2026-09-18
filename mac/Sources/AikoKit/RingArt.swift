import Foundation

/// A colour of the palette, as plain numbers. AppKit and WPF spell a colour differently, and a
/// rule that names NSColor cannot be tested without a screen.
public struct Rgba: Sendable, Equatable {
    public let red: Int
    public let green: Int
    public let blue: Int
    public let alpha: Double

    public init(_ red: Int, _ green: Int, _ blue: Int, alpha: Double = 1) {
        self.red = red
        self.green = green
        self.blue = blue
        self.alpha = alpha
    }
}

/// How the icon is drawn, in numbers: the twin of RingIcon.cs and RingDrawing.cs on Windows.
///
/// Everything is said on a 16 unit grid and scaled to the real size, so the icon keeps the same
/// proportions whatever the menu bar asks for.
public enum RingArt {
    public static let grid = 16.0
    public static let centre = 8.0
    public static let ringRadius = 5.8
    public static let ringThickness = 2.2
    public static let dotRadius = 1.6

    /// The menu bar can be dark or light, and the icon is not told which. A see through grey works
    /// on both: it settles a step away from whatever is behind it instead of fighting it.
    public static let track = Rgba(0x9A, 0x9A, 0x9A, alpha: 0x66 / 255.0)
    public static let noData = Rgba(0x9A, 0x9A, 0x9A, alpha: 0x8C / 255.0)

    public static let normal = Rgba(0x00, 0xBC, 0x7D)
    public static let caution = Rgba(0xFE, 0x9A, 0x00)
    public static let critical = Rgba(0xFF, 0x64, 0x67)
    public static let muted = Rgba(0xA1, 0xA1, 0xA1)

    public static func colour(for tone: LimitTone) -> Rgba {
        switch tone {
        case .normal:
            return normal
        case .caution:
            return caution
        case .critical:
            return critical
        case .unknown:
            return muted
        }
    }

    /// Five short dashes for "no data", worked out from the circle itself so they always close it
    /// evenly, whatever the radius. Twelve looked right on paper but came out as a solid ring: the
    /// gaps were thinner than the pen, and antialiasing filled them in. Six at 55% still read as
    /// busy on the live tray, so the dashes are fewer and the gaps wider than the dashes.
    public static let dashes = 5
    public static let dashShareOfSegment = 0.4

    /// The dash and the gap in the same units as the radius. AppKit takes a dash pattern in
    /// points; WPF takes it in pen widths and divides by the thickness itself.
    public static func dashPattern(radius: Double) -> (dash: Double, gap: Double) {
        let segment = 2 * Double.pi * radius / Double(dashes)
        let dash = segment * dashShareOfSegment
        return (dash, segment - dash)
    }

    /// The share of the ring the arc covers, from 0 to 1. Nothing to draw at zero, a whole circle
    /// at a hundred.
    public static func arcShare(_ percent: Int) -> Double {
        min(max(Double(percent) / 100, 0), 1)
    }
}
