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

    /// Named in the tooltip when the daily update check found one.
    var newerVersion: String?

    /// 0 folded, 1 with the percentages fully out.
    var unfold = 0.0

    /// How many sessions wait, ended a turn, stopped and work (D-364): marks after the rings.
    var sessions = SessionCounts.none {
        didSet { needsDisplay = true }
    }

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
        tipText = TrayText.tooltip(cards, newerVersion: newerVersion)
        setAccessibilityLabel(tipText)
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
            faceFit: faceFit,
            sessions: marks().map { Self.number($0.count).size })
    }

    /// The marks in the order of Windows: waits, done, stopped, work; a mark only for a count above 0.
    private func marks() -> [(kind: MarkKind, count: Int)] {
        let all: [(kind: MarkKind, count: Int)] = [
            (.waiting, sessions.waiting), (.done, sessions.done), (.stopped, sessions.stopped), (.working, sessions.working),
        ]
        return all.filter { $0.count > 0 }
    }

    private enum MarkKind {
        case waiting, done, stopped, working
    }

    private static func number(_ count: Int) -> Words {
        Words("\(count)")
    }

    /// Whether a point of the window is on the mark of "waits", with 4 points to spare around it as
    /// on Windows: a click there opens the session that waits.
    func onWaitingMark(_ point: NSPoint) -> Bool {
        guard sessions.waiting > 0, let mark = islandFrame().marks.first else { return false }
        let local = convert(point, from: nil)
        let box = mark.sign
        let wide = Box(box.x - 4, box.y - 4, mark.number.right - box.x + 8, box.height + 8)
        return local.x >= wide.x && local.x <= wide.right && local.y >= wide.y && local.y <= wide.bottom
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
        Theme.nsCardFill.setFill()
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
        drawMarks(frame)
        drawFace(frame)
    }

    /// "Waits" with an orange pause, "done" with a green tick, "stopped" with a red cross and "work"
    /// with a grey dot, as in the list of sessions; each with its number.
    private func drawMarks(_ frame: IslandFrame) {
        let alpha = CGFloat(frame.marksOpacity)
        guard alpha > 0 else { return }
        for (spot, mark) in zip(frame.marks, marks()) {
            let sign = NSRect(x: spot.sign.x, y: spot.sign.y, width: spot.sign.width, height: spot.sign.height)
            switch mark.kind {
            case .waiting:
                stroke(BarMarks.pause, size: NSSize(width: 5, height: 10), in: sign, colour: Theme.nsCaution, alpha: alpha)
            case .done:
                stroke(BarMarks.check, size: NSSize(width: 24, height: 24), in: sign.insetBy(dx: 1, dy: 1),
                       colour: Theme.nsColour(RingArt.colour(for: .normal)), alpha: alpha, fill: true)
            case .stopped:
                stroke("M0,0 L8,8 M8,0 L0,8", size: NSSize(width: 8, height: 8), in: sign, colour: Theme.nsDestructive, alpha: alpha)
            case .working:
                Theme.nsMuted.withAlphaComponent(alpha).setFill()
                NSBezierPath(ovalIn: NSRect(x: sign.midX - 4, y: sign.midY - 4, width: 8, height: 8)).fill()
            }
            Self.number(mark.count).draw(at: NSPoint(x: spot.number.x, y: spot.number.y), opacity: alpha)
        }
    }

    /// A mark of the slayumind set, its box of size units centred in the field.
    private func stroke(_ data: String, size: NSSize, in field: NSRect, colour: NSColor, alpha: CGFloat, fill: Bool = false) {
        guard let context = NSGraphicsContext.current?.cgContext else { return }
        let scale = min(field.width / size.width, field.height / size.height, 1)
        context.saveGState()
        context.translateBy(x: field.midX - size.width * scale / 2, y: field.midY - size.height * scale / 2)
        context.scaleBy(x: scale, y: scale)
        context.addPath(BarMarks.path(data).cgPath)
        if fill {
            context.setFillColor(colour.withAlphaComponent(alpha).cgColor)
            context.fillPath()
        } else {
            context.setStrokeColor(colour.withAlphaComponent(alpha).cgColor)
            context.setLineWidth(BarMarks.lineWidth / scale)
            context.setLineCap(.round)
            context.strokePath()
        }
        context.restoreGState()
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

    /// The words of the numbers, shown in the tip of Aiko, as on the island of Windows.
    private var tipText = ""

    override func mouseEntered(with event: NSEvent) {
        Tips.hover(tipText, owner: ObjectIdentifier(self))
        onEnter?()
    }

    override func mouseExited(with event: NSEvent) {
        Tips.leave(owner: ObjectIdentifier(self))
        onLeave?()
    }

    override func mouseDown(with event: NSEvent) {
        Tips.leave(owner: ObjectIdentifier(self))
        onPress?(NSEvent.mouseLocation)
    }

    override func mouseDragged(with event: NSEvent) {
        onDrag?(NSEvent.mouseLocation)
    }

    override func mouseUp(with event: NSEvent) {
        onRelease?()
    }
}