import AikoKit
import AppKit

/// How a ring is drawn, in one place: the menu bar icon draws it into a picture, the island draws
/// it into its own window. The twin of RingDrawing.cs on Windows, for the same reason — two copies
/// of the same arc would slowly stop looking alike.
///
/// Everything it draws is in y upwards points, so an island view that counts downwards turns the
/// page over first.
enum RingPaint {
    /// percent is nil when nothing has been reported yet, and then the ring is dashed: a different
    /// state, not zero per cent.
    static func draw(
        centre: NSPoint, radius: CGFloat, thickness: CGFloat, percent: Int?, tone: LimitTone,
        alpha: CGFloat = 1
    ) {
        guard let percent else {
            waiting(centre, radius, thickness, alpha)
            return
        }

        track(centre, radius, thickness, alpha)
        arc(centre, radius, thickness, percent, tone, alpha)
    }

    static func track(_ centre: NSPoint, _ radius: CGFloat, _ thickness: CGFloat, _ alpha: CGFloat) {
        let path = circle(centre, radius)
        path.lineWidth = thickness
        colour(RingArt.track, alpha).setStroke()
        path.stroke()
    }

    static func waiting(_ centre: NSPoint, _ radius: CGFloat, _ thickness: CGFloat, _ alpha: CGFloat) {
        let pattern = RingArt.dashPattern(radius: radius)
        let path = circle(centre, radius)
        path.lineWidth = thickness
        path.lineCapStyle = .butt
        path.setLineDash([pattern.dash, pattern.gap], count: 2, phase: 0)
        colour(RingArt.noData, alpha).setStroke()
        path.stroke()
    }

    static func arc(
        _ centre: NSPoint, _ radius: CGFloat, _ thickness: CGFloat, _ percent: Int, _ tone: LimitTone,
        _ alpha: CGFloat
    ) {
        let share = RingArt.arcShare(percent)
        if share <= 0 {
            return
        }

        let path = NSBezierPath()
        if share >= 1 {
            path.appendOval(in: square(centre, radius))
        } else {
            // The arc starts at the top and grows clockwise, the way a clock fills up. AppKit
            // measures angles from the right and counts them the other way, so the top is 90.
            path.appendArc(
                withCenter: centre,
                radius: radius,
                startAngle: 90,
                endAngle: 90 - (share * 360),
                clockwise: true)
        }

        path.lineWidth = thickness
        path.lineCapStyle = .round
        colour(RingArt.colour(for: tone), alpha).setStroke()
        path.stroke()
    }

    private static func colour(_ rgba: Rgba, _ alpha: CGFloat) -> NSColor {
        Theme.nsColour(rgba).withAlphaComponent(CGFloat(rgba.alpha) * alpha)
    }

    private static func circle(_ centre: NSPoint, _ radius: CGFloat) -> NSBezierPath {
        NSBezierPath(ovalIn: square(centre, radius))
    }

    private static func square(_ centre: NSPoint, _ radius: CGFloat) -> NSRect {
        NSRect(x: centre.x - radius, y: centre.y - radius, width: radius * 2, height: radius * 2)
    }
}
