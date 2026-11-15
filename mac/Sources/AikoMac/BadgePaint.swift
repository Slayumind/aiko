import AikoKit
import AppKit

/// The one sign of the sessions (D-409): the number of waiting sessions in orange, of stopped ones
/// in red, or a green dot for a finished turn, in the upper right corner of an icon. The twin of
/// BadgeDrawing.cs, on its 16 unit grid; this draws in AppKit's coordinates, with y going up.
enum BadgePaint {
    /// Dark text on the bright grounds, the same as the counts in the mockup.
    private static let text = Theme.nsColour(Rgba(0x17, 0x17, 0x17))

    static func draw(_ size: CGFloat, _ badge: SessionBadge) {
        guard let shape = shape(size, badge) else { return }
        let tone: LimitTone = switch badge.kind {
        case .waiting: .caution
        case .stopped: .critical
        default: .normal
        }
        Theme.nsColour(RingArt.colour(for: tone)).setFill()
        shape.fill()

        guard badge.kind != .done else { return }
        let number = numberText(badge, size)
        let box = shape.bounds
        let measured = number.size()
        number.draw(at: NSPoint(x: box.midX - measured.width / 2, y: box.midY - measured.height / 2))
    }

    /// Everything of the icon but the sign and a gap of one unit around it. Nil when there is no sign.
    static func cutUnder(_ size: CGFloat, _ badge: SessionBadge) -> NSBezierPath? {
        guard let wider = shape(size, badge, grow: size / 16) else { return nil }
        let cut = NSBezierPath(rect: NSRect(x: 0, y: 0, width: size, height: size))
        cut.append(wider)
        cut.windingRule = .evenOdd
        return cut
    }

    /// A dot for a finished turn, else a pill around the number; grow widens it on every side.
    private static func shape(_ size: CGFloat, _ badge: SessionBadge, grow: CGFloat = 0) -> NSBezierPath? {
        let scale = size / 16
        switch badge.kind {
        case .none:
            return nil
        case .done:
            let radius = 2.8 * scale
            let centre = NSPoint(x: size - radius - 0.2 * scale, y: size - radius - 0.2 * scale)
            let r = radius + grow
            return NSBezierPath(ovalIn: NSRect(x: centre.x - r, y: centre.y - r, width: r * 2, height: r * 2))
        default:
            let height = 9 * scale
            let width = max(height, numberText(badge, size).size().width + 3 * scale)
            let rect = NSRect(x: size - width, y: size - height, width: width, height: height).insetBy(dx: -grow, dy: -grow)
            return NSBezierPath(roundedRect: rect, xRadius: rect.height / 2, yRadius: rect.height / 2)
        }
    }

    /// A narrow bold face reads at seven pixels; the mono font of the app does not.
    private static func numberText(_ badge: SessionBadge, _ size: CGFloat) -> NSAttributedString {
        NSAttributedString(string: badge.text, attributes: [
            .font: NSFont.systemFont(ofSize: 7.5 * size / 16, weight: .bold),
            .foregroundColor: text,
        ])
    }
}
