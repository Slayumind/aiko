import AikoKit
import AppKit

/// The island itself: one ring per environment, and a percentage that slides out beside each ring
/// when the island unfolds (D-161). The twin of IslandPanel.xaml and IslandPanel.xaml.cs.
///
/// It draws what IslandLayout worked out and answers the mouse; it decides nothing. Its y grows
/// downwards, as the layout counts, so the numbers can be drawn as they are.
@MainActor
final class IslandView: NSView {
    private var cards: [CardState] = []
    private var edge = ScreenEdge.top
    private var docked = true

    /// 0 folded, 1 with the percentages fully out.
    var unfold = 0.0

    /// What the face is doing right now, if anything.
    var iconFrame = IconFrame.rings
    var faceFit = 0.0
    var face: FacePicture?

    var onPress: ((NSPoint) -> Void)?
    var onDrag: ((NSPoint) -> Void)?
    var onRelease: (() -> Void)?
    var onEnter: (() -> Void)?
    var onLeave: (() -> Void)?

    override var isFlipped: Bool { true }

    /// Nothing here ever takes the focus.
    override var acceptsFirstResponder: Bool { false }

    /// The island answers a click even when another app is in front. Without this the first click
    /// would only bring Aiko forward, and Aiko has nothing to bring forward.
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    func show(_ cards: [CardState], edge: ScreenEdge, docked: Bool = true) {
        self.cards = cards
        self.edge = edge
        self.docked = docked

        // The numbers go in words too: a ring and a colour say nothing to a screen reader, and
        // nothing at all to somebody who cannot tell the green from the red.
        toolTip = TrayText.tooltip(cards, newerVersion: nil)
        setAccessibilityLabel(toolTip)
        needsDisplay = true
    }

    var currentEdge: ScreenEdge { edge }

    /// The size the island asks for as it stands.
    var wanted: NSSize {
        let size = islandFrame().size
        return NSSize(width: size.width, height: size.height)
    }

    private func islandFrame() -> IslandFrame {
        IslandLayout.frame(
            labels: cards.map { words(for: $0).size },
            edge: edge,
            docked: docked,
            unfold: unfold,
            faceFit: faceFit)
    }

    // ---- Drawing ----

    /// How much of the island, in points from its top edge, is not drawn. While the island lands at
    /// the top of the screen its overshoot would show above the menu bar, and macOS does not let a
    /// window go under the bar (RESEARCH, 2026-09-18), so that part is cut away instead.
    var cutFromTheTop: Double = 0

    override func draw(_ dirtyRect: NSRect) {
        let frame = islandFrame()
        let body = NSRect(origin: .zero, size: bounds.size)

        // This view is flipped, so y grows downwards: cutting from the top starts the clip lower.
        let shown = cutFromTheTop > 0
            ? NSRect(x: 0, y: cutFromTheTop, width: body.width, height: max(0, body.height - cutFromTheTop))
            : body
        NSBezierPath(rect: shown).setClip()
        Theme.nsSurface.setFill()
        rounded(body, frame.corners).fill()

        // No shadow and no room around it, unlike the card: the island is pressed against the edge
        // of the screen. The line on the docked side is pushed off the view, so that side reads
        // flat and the rings still do not move (IslandLayout.padding).
        let line = frame.border
        let left: CGFloat = line.left > 0 ? 0.5 : -1
        let top: CGFloat = line.top > 0 ? 0.5 : -1
        let right: CGFloat = line.right > 0 ? 0.5 : -1
        let bottom: CGFloat = line.bottom > 0 ? 0.5 : -1
        let inside = NSRect(
            x: left, y: top, width: body.width - left - right, height: body.height - top - bottom)
        let edging = rounded(inside, frame.corners)
        edging.lineWidth = IslandLayout.line
        Theme.nsHairline.setStroke()
        edging.stroke()

        drawRings(frame)
        drawFace(frame)
    }

    private func drawRings(_ frame: IslandFrame) {
        guard iconFrame.ringOpacity > 0, iconFrame.ringScale > 0 else { return }

        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }

        // The rings and their percentages shrink away together, from the middle of the island.
        let scale = NSAffineTransform()
        scale.translateX(by: frame.content.centreX, yBy: frame.content.centreY)
        scale.scaleX(by: iconFrame.ringScale, yBy: iconFrame.ringScale)
        scale.translateX(by: -frame.content.centreX, yBy: -frame.content.centreY)
        scale.concat()

        let alpha = CGFloat(iconFrame.ringOpacity)
        let ring = RingArt.islandRing(IslandLayout.ringSize)

        for (index, spot) in frame.spots.enumerated() {
            let row = index < cards.count ? cards[index].iconRow : nil
            // While the rings come back they draw their arcs again from nothing.
            let percent = row.map { Int((Double($0.percent) * iconFrame.ringSweep).rounded()) }
            let centre = NSPoint(x: spot.ring.centreX, y: spot.ring.centreY)

            upwards(around: centre.y) {
                RingPaint.draw(
                    centre: centre,
                    radius: ring.radius,
                    thickness: ring.thickness,
                    percent: percent,
                    tone: row?.tone ?? .unknown,
                    alpha: alpha)
            }

            drawWords(spot, index, alpha)
        }
    }

    private func drawWords(_ spot: IslandRingSpot, _ index: Int, _ alpha: CGFloat) {
        guard spot.clip.width > 0, spot.clip.height > 0, index < cards.count else { return }

        let opacity = CGFloat(IslandLayout.wordsOpacity(unfold)) * alpha
        guard opacity > 0 else { return }

        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }

        // The words keep their own size and are cut off, so they slide into view instead of being
        // squeezed (UnfoldPanel.cs).
        NSBezierPath(rect: NSRect(
            x: spot.clip.x, y: spot.clip.y, width: spot.clip.width, height: spot.clip.height)).setClip()

        let text = words(for: cards[index])
        text.draw(at: NSPoint(x: spot.words.x, y: spot.words.y), opacity: opacity)
    }

    private func drawFace(_ frame: IslandFrame) {
        guard let face, iconFrame.faceOpacity > 0 else { return }

        let scale = iconFrame.faceScale
        let box = NSRect(
            x: frame.face.centreX - (frame.face.width * scale / 2),
            y: frame.face.centreY - (frame.face.height * scale / 2),
            width: frame.face.width * scale,
            height: frame.face.height * scale)

        FacePaint.draw(face, in: box, alpha: CGFloat(iconFrame.faceOpacity))
    }

    /// Draws something that counts y upwards into this view, which counts it downwards.
    private func upwards(around y: CGFloat, _ draw: () -> Void) {
        NSGraphicsContext.saveGraphicsState()
        let flip = NSAffineTransform()
        flip.translateX(by: 0, yBy: y * 2)
        flip.scaleX(by: 1, yBy: -1)
        flip.concat()
        draw()
        NSGraphicsContext.restoreGraphicsState()
    }

    private func words(for card: CardState) -> Words {
        Words(card.iconRow.map { "\($0.percent)%" } ?? "—")
    }

    /// One percentage, measured and drawn with the same font.
    struct Words {
        let text: String
        private let attributes: [NSAttributedString.Key: Any]

        init(_ text: String) {
            self.text = text
            attributes = [
                .font: Theme.monoFont(Theme.textTiny),
                .foregroundColor: Theme.nsInk,
            ]
        }

        var size: Extent {
            let measured = (text as NSString).size(withAttributes: attributes)
            return Extent(ceil(measured.width), ceil(measured.height))
        }

        func draw(at point: NSPoint, opacity: CGFloat) {
            var faded = attributes
            faded[.foregroundColor] = Theme.nsInk.withAlphaComponent(opacity)
            (text as NSString).draw(at: point, withAttributes: faded)
        }
    }

    private func rounded(_ rect: NSRect, _ corners: Corners) -> NSBezierPath {
        let path = NSBezierPath()
        let topLeft = NSPoint(x: rect.minX, y: rect.minY)
        let topRight = NSPoint(x: rect.maxX, y: rect.minY)
        let bottomRight = NSPoint(x: rect.maxX, y: rect.maxY)
        let bottomLeft = NSPoint(x: rect.minX, y: rect.maxY)

        path.move(to: NSPoint(x: rect.minX + corners.topLeft, y: rect.minY))
        path.appendArc(from: topRight, to: bottomRight, radius: corners.topRight)
        path.appendArc(from: bottomRight, to: bottomLeft, radius: corners.bottomRight)
        path.appendArc(from: bottomLeft, to: topLeft, radius: corners.bottomLeft)
        path.appendArc(from: topLeft, to: topRight, radius: corners.topLeft)
        path.close()
        return path
    }

    // ---- The mouse ----

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        for area in trackingAreas {
            removeTrackingArea(area)
        }

        addTrackingArea(NSTrackingArea(
            rect: .zero,
            options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect],
            owner: self,
            userInfo: nil))
    }

    override func mouseEntered(with event: NSEvent) {
        onEnter?()
    }

    override func mouseExited(with event: NSEvent) {
        onLeave?()
    }

    override func mouseDown(with event: NSEvent) {
        onPress?(NSEvent.mouseLocation)
    }

    override func mouseDragged(with event: NSEvent) {
        onDrag?(NSEvent.mouseLocation)
    }

    override func mouseUp(with event: NSEvent) {
        onRelease?()
    }
}