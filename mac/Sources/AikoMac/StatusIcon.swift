import AikoKit
import AppKit

/// The icon in the menu bar: a ring for the chosen environment and a dot for the other one. Both
/// take their colour from the same thresholds as the card. After a session event the rings give
/// way to a face for two seconds (D-211).
///
/// The twin of RingIcon.cs on Windows. The numbers are in RingArt and the arc in RingPaint, so the
/// two systems cannot drift apart; what is left here is putting the picture together.
enum StatusIcon {
    /// ring: the environment the icon shows. dot: the other one, or nil with a single environment.
    /// Both nil means Claude Code has not reported yet, and the ring is dashed.
    /// frame: where the icon is between the rings and a face. Without one, only the rings.
    static func image(
        size: CGFloat,
        ring: CardRow?,
        dot: CardRow?,
        frame: IconFrame = .rings,
        face: FacePicture? = nil
    ) -> NSImage {
        let image = NSImage(size: NSSize(width: size, height: size), flipped: false) { _ in
            // Drawing happens on a 16 unit grid and is scaled to the real size, so the icon keeps
            // the same proportions whatever the menu bar asks for.
            let scale = size / RingArt.grid
            let centre = NSPoint(x: RingArt.centre * scale, y: RingArt.centre * scale)

            if frame.ringOpacity > 0 {
                drawRings(centre, scale, ring, dot, frame)
            }

            if let face, frame.showsFace {
                drawFace(face, size, centre, frame)
            }

            return true
        }

        // The colours say what the state is, so the menu bar must not repaint the icon in its own
        // black or white.
        image.isTemplate = false
        return image
    }

    private static func drawRings(
        _ centre: NSPoint, _ scale: CGFloat, _ ring: CardRow?, _ dot: CardRow?, _ frame: IconFrame
    ) {
        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }

        scaled(around: centre, by: frame.ringScale)

        let radius = RingArt.ringRadius * scale
        let thickness = RingArt.ringThickness * scale
        let alpha = CGFloat(frame.ringOpacity)

        // The arc grows back from zero when the rings return.
        let percent = ring.map { Int((Double($0.percent) * frame.ringSweep).rounded()) }
        RingPaint.draw(
            centre: centre,
            radius: radius,
            thickness: thickness,
            percent: percent,
            tone: ring?.tone ?? .unknown,
            alpha: alpha)

        if ring != nil, let dot {
            Theme.nsColour(RingArt.colour(for: dot.tone)).withAlphaComponent(alpha).setFill()
            let dotRadius = RingArt.dotRadius * scale
            NSBezierPath(ovalIn: NSRect(
                x: centre.x - dotRadius,
                y: centre.y - dotRadius,
                width: dotRadius * 2,
                height: dotRadius * 2)).fill()
        }
    }

    private static func drawFace(
        _ face: FacePicture, _ size: CGFloat, _ centre: NSPoint, _ frame: IconFrame
    ) {
        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }

        scaled(around: centre, by: frame.faceScale)
        FacePaint.drawUpwards(
            face,
            in: NSRect(x: 0, y: 0, width: size, height: size),
            alpha: CGFloat(frame.faceOpacity))
    }

    private static func scaled(around centre: NSPoint, by scale: Double) {
        let transform = NSAffineTransform()
        transform.translateX(by: centre.x, yBy: centre.y)
        transform.scaleX(by: scale, yBy: scale)
        transform.translateX(by: -centre.x, yBy: -centre.y)
        transform.concat()
    }
}
