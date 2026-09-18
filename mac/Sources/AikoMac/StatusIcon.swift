import AikoKit
import AppKit

/// The icon in the menu bar: a ring for the chosen environment and a dot for the other one. Both
/// take their colour from the same thresholds as the card.
///
/// The twin of RingIcon.cs and RingDrawing.cs on Windows. The numbers are in RingArt, so the two
/// systems cannot drift apart; what is left here is AppKit drawing.
enum StatusIcon {
    /// ring: the environment the icon shows. dot: the other one, or nil with a single environment.
    /// Both nil means Claude Code has not reported yet, and the ring is dashed.
    static func image(size: CGFloat, ring: CardRow?, dot: CardRow?) -> NSImage {
        let image = NSImage(size: NSSize(width: size, height: size), flipped: false) { _ in
            // Drawing happens on a 16 unit grid and is scaled to the real size, so the icon keeps
            // the same proportions whatever the menu bar asks for.
            let scale = size / RingArt.grid
            let centre = NSPoint(x: RingArt.centre * scale, y: RingArt.centre * scale)
            let radius = RingArt.ringRadius * scale
            let thickness = RingArt.ringThickness * scale

            if let ring {
                drawTrack(centre, radius, thickness)
                drawArc(centre, radius, thickness, ring)
            } else {
                drawWaiting(centre, radius, thickness)
            }

            if ring != nil, let dot {
                Theme.nsColour(RingArt.colour(for: dot.tone)).setFill()
                circle(centre, RingArt.dotRadius * scale).fill()
            }

            return true
        }

        // The colours say what the state is, so the menu bar must not repaint the icon in its own
        // black or white.
        image.isTemplate = false
        return image
    }

    private static func drawTrack(_ centre: NSPoint, _ radius: CGFloat, _ thickness: CGFloat) {
        let path = circle(centre, radius)
        path.lineWidth = thickness
        Theme.nsColour(RingArt.track).setStroke()
        path.stroke()
    }

    private static func drawWaiting(_ centre: NSPoint, _ radius: CGFloat, _ thickness: CGFloat) {
        let pattern = RingArt.dashPattern(radius: radius)
        let path = circle(centre, radius)
        path.lineWidth = thickness
        path.lineCapStyle = .butt
        path.setLineDash([pattern.dash, pattern.gap], count: 2, phase: 0)
        Theme.nsColour(RingArt.noData).setStroke()
        path.stroke()
    }

    private static func drawArc(
        _ centre: NSPoint, _ radius: CGFloat, _ thickness: CGFloat, _ row: CardRow
    ) {
        let share = RingArt.arcShare(row.percent)
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
        Theme.nsColour(RingArt.colour(for: row.tone)).setStroke()
        path.stroke()
    }

    private static func circle(_ centre: NSPoint, _ radius: CGFloat) -> NSBezierPath {
        NSBezierPath(ovalIn: square(centre, radius))
    }

    private static func square(_ centre: NSPoint, _ radius: CGFloat) -> NSRect {
        NSRect(x: centre.x - radius, y: centre.y - radius, width: radius * 2, height: radius * 2)
    }
}
