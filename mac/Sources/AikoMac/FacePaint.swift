import AikoKit
import AppKit

/// Turns the shapes of FaceArt into AppKit drawing (D-211, D-213). Vector all the way, so one face
/// serves an 18 point island, a menu bar icon and, later, a picture in a window.
///
/// The twin of Faces/FaceDrawing.cs on Windows. WPF reads the path data itself; here the core reads
/// it (SvgPath) and this only draws.
enum FacePaint {
    /// The face fitted into a box, in a context whose y grows downwards, as the art itself does.
    static func draw(_ picture: FacePicture, in box: NSRect, alpha: CGFloat = 1) {
        guard picture.viewSize > 0 else { return }

        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }

        let fit = NSAffineTransform()
        fit.translateX(by: box.minX, yBy: box.minY)
        fit.scaleX(by: box.width / picture.viewSize, yBy: box.height / picture.viewSize)
        fit.translateX(by: -picture.viewLeft, yBy: -picture.viewTop)
        fit.concat()

        for group in picture.groups {
            NSGraphicsContext.saveGraphicsState()

            if group.scale != 1 || group.offsetX != 0 || group.offsetY != 0 {
                let move = NSAffineTransform()
                move.translateX(by: group.offsetX, yBy: group.offsetY)
                move.translateX(by: group.centerX, yBy: group.centerY)
                move.scaleX(by: group.scale, yBy: group.scale)
                move.translateX(by: -group.centerX, yBy: -group.centerY)
                move.concat()
            }

            for shape in group.shapes {
                paint(shape, alpha)
            }

            NSGraphicsContext.restoreGraphicsState()
        }
    }

    /// The same face in a context whose y grows upwards, such as the picture the menu bar is
    /// handed: the page is turned over around the middle of the box first.
    static func drawUpwards(_ picture: FacePicture, in box: NSRect, alpha: CGFloat = 1) {
        NSGraphicsContext.saveGraphicsState()
        let flip = NSAffineTransform()
        flip.translateX(by: 0, yBy: box.midY * 2)
        flip.scaleX(by: 1, yBy: -1)
        flip.concat()
        draw(picture, in: box, alpha: alpha)
        NSGraphicsContext.restoreGraphicsState()
    }

    private static func paint(_ shape: FaceShape, _ alpha: CGFloat) {
        let path = NSBezierPath()
        add(SvgPath.steps(shape.data), to: path)

        if let holes = shape.holes, !holes.isEmpty {
            for hole in holes {
                add(SvgPath.steps(hole), to: path)
            }
            // The holes are cut out of the fill, so a highlight stays clear on any background.
            path.windingRule = .evenOdd
        } else {
            path.windingRule = .nonZero
        }

        if let fill = shape.fill, let colour = colour(fill, shape.opacity * Double(alpha)) {
            colour.setFill()
            path.fill()
        }

        if let stroke = shape.stroke, let colour = colour(stroke, shape.opacity * Double(alpha)) {
            path.lineWidth = shape.strokeWidth
            path.lineCapStyle = .round
            path.lineJoinStyle = .round
            colour.setStroke()
            path.stroke()
        }
    }

    private static func add(_ steps: [PathStep], to path: NSBezierPath) {
        for step in steps {
            switch step {
            case .move(let to):
                path.move(to: NSPoint(x: to.x, y: to.y))
            case .line(let to):
                // A line before any move would throw; the art never does it, but a file might.
                if path.isEmpty {
                    path.move(to: NSPoint(x: to.x, y: to.y))
                } else {
                    path.line(to: NSPoint(x: to.x, y: to.y))
                }
            case .curve(let one, let two, let to):
                if path.isEmpty {
                    path.move(to: NSPoint(x: one.x, y: one.y))
                }
                path.curve(
                    to: NSPoint(x: to.x, y: to.y),
                    controlPoint1: NSPoint(x: one.x, y: one.y),
                    controlPoint2: NSPoint(x: two.x, y: two.y))
            case .close:
                if !path.isEmpty {
                    path.close()
                }
            }
        }
    }

    /// The colours of the faces are written as #RRGGBB, the way the mockup wrote them.
    private static func colour(_ hex: String, _ opacity: Double) -> NSColor? {
        var text = hex
        if text.hasPrefix("#") {
            text.removeFirst()
        }

        guard text.count == 6, let value = Int(text, radix: 16) else {
            return nil
        }

        return NSColor(
            srgbRed: CGFloat((value >> 16) & 0xFF) / 255,
            green: CGFloat((value >> 8) & 0xFF) / 255,
            blue: CGFloat(value & 0xFF) / 255,
            alpha: CGFloat(min(max(opacity, 0), 1)))
    }
}
