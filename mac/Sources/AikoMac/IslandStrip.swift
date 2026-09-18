import AikoKit
import AppKit

/// The landing strip: a pane of frosted glass on the edge where the island in hand will land
/// (D-161, D-186).
///
/// A window of its own, because it sits on the edge while the island is somewhere else. It never
/// takes the focus or a click, and it exists only while the island is in hand. The twin of
/// IslandGhost.cs — where Windows has to ask for the acrylic accent by hand, macOS has
/// NSVisualEffectView, so the glass itself is three lines.
@MainActor
final class IslandStrip {
    /// The corner macOS gives its own panes. The strip reaches past the screen edge by exactly
    /// this, so the two corners on that side fall outside the screen and the edge reads flat.
    private static let radius: CGFloat = 8

    /// How see-through the whole pane is, on top of the material. The owner picks it by eye:
    /// AIKO_STRIP_ALPHA=0.6 and so on.
    private static var alpha: CGFloat {
        guard let text = ProcessInfo.processInfo.environment["AIKO_STRIP_ALPHA"],
              let value = Double(text) else { return 0.5 }
        return CGFloat(min(max(value, 0.1), 1))
    }

    /// The shape of the pane: the side against the screen edge is flat, the others carry the corner
    /// macOS gives its own panes, exactly as the island does (IslandLayout.corners).
    ///
    /// The corner named top is drawn at minY, so this wants a place where y grows downwards: the
    /// mask image is drawn flipped, and the layer that strokes the line gets the corners turned over.
    private static func shape(_ rect: NSRect, _ corners: Corners) -> NSBezierPath {
        let path = NSBezierPath()
        let topLeft = CGFloat(corners.topLeft)
        let topRight = CGFloat(corners.topRight)
        let bottomRight = CGFloat(corners.bottomRight)
        let bottomLeft = CGFloat(corners.bottomLeft)

        path.move(to: NSPoint(x: rect.minX + topLeft, y: rect.minY))
        path.line(to: NSPoint(x: rect.maxX - topRight, y: rect.minY))
        path.appendArc(
            from: NSPoint(x: rect.maxX, y: rect.minY),
            to: NSPoint(x: rect.maxX, y: rect.minY + topRight), radius: topRight)
        path.line(to: NSPoint(x: rect.maxX, y: rect.maxY - bottomRight))
        path.appendArc(
            from: NSPoint(x: rect.maxX, y: rect.maxY),
            to: NSPoint(x: rect.maxX - bottomRight, y: rect.maxY), radius: bottomRight)
        path.line(to: NSPoint(x: rect.minX + bottomLeft, y: rect.maxY))
        path.appendArc(
            from: NSPoint(x: rect.minX, y: rect.maxY),
            to: NSPoint(x: rect.minX, y: rect.maxY - bottomLeft), radius: bottomLeft)
        path.line(to: NSPoint(x: rect.minX, y: rect.minY + topLeft))
        path.appendArc(
            from: NSPoint(x: rect.minX, y: rect.minY),
            to: NSPoint(x: rect.minX + topLeft, y: rect.minY), radius: topLeft)
        path.close()
        return path
    }

    /// The same corners for a place where y grows upwards, which is how a layer counts.
    private static func turnedOver(_ corners: Corners) -> Corners {
        Corners(
            topLeft: corners.bottomLeft,
            topRight: corners.bottomRight,
            bottomRight: corners.topRight,
            bottomLeft: corners.topLeft)
    }

    /// The mask for NSVisualEffectView, the only thing that cuts the blur: a layer mask leaves it
    /// whole. Drawn at the pane's own size, so every corner keeps its radius.
    private static func mask(_ size: NSSize, _ corners: Corners) -> NSImage {
        NSImage(size: size, flipped: true) { rect in
            NSColor.black.setFill()
            shape(rect, corners).fill()
            return true
        }
    }

    /// Which of the system's own glasses to use. The owner picks it by eye, so it can be changed
    /// without a rebuild: AIKO_STRIP_MATERIAL=menu|popover|hud|window|sheet|sidebar.
    private static var material: NSVisualEffectView.Material {
        switch ProcessInfo.processInfo.environment["AIKO_STRIP_MATERIAL"] {
        case "menu": return .menu
        case "hud": return .hudWindow
        case "window": return .underWindowBackground
        case "sheet": return .sheet
        case "sidebar": return .sidebar
        case "popover": return .popover
        default: return .menu
        }
    }

    private let panel: NSPanel
    private let glass = NSVisualEffectView()
    private let edging = NSView()
    private let line = CAShapeLayer()
    private let glide = Tween()
    private var edge: ScreenEdge?

    init() {
        panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 10, height: 10),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false)

        panel.isFloatingPanel = true
        // The same level as the island. Lower than that, macOS pushes the pane out of the menu bar's
        // strip and it cannot touch the top edge (RESEARCH, 2026-09-18); the island is ordered above
        // it anyway while it is in hand.
        panel.level = WindowOrder.onTop
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.ignoresMouseEvents = true
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        // .auxiliary and .stationary keep the island where it is when the wallpaper is clicked and
        // every window slides aside: it belongs to the edge of the screen, not to a desk of windows.
        // A spike measured it (RESEARCH, 2026-09-18): with .transient the pane slid away with them.
        panel.collectionBehavior = [.canJoinAllSpaces, .auxiliary, .stationary, .ignoresCycle, .fullScreenAuxiliary]
        panel.animationBehavior = .none
        panel.alphaValue = IslandStrip.alpha

        // Dark glass, whatever the person's appearance: the island is always dark, and so is what
        // shows where it will land. Nothing of ours is drawn over it — a tint of our own only made
        // it muddy — and the corners come from a mask, not from the layer: rounding the layer of a
        // visual effect view flattens the blur and the pane stops letting the windows through.
        glass.material = IslandStrip.material
        glass.blendingMode = .behindWindow
        glass.state = .active
        glass.appearance = NSAppearance(named: .darkAqua)
        glass.autoresizingMask = [.width, .height]

        // The hairline the island and the card carry, drawn in a layer of its own so the glass keeps
        // nothing of ours: a layer on the effect view itself would flatten the blur.
        edging.autoresizingMask = [.width, .height]
        edging.wantsLayer = true
        edging.layer?.addSublayer(line)
        line.fillColor = nil
        line.strokeColor = Theme.nsHairline.cgColor
        line.lineWidth = 1
        glass.addSubview(edging)


        panel.contentView = glass
    }

    /// Lays out the shape of the pane after every move: the mask that cuts the blur and the line
    /// that draws its edge, both from the corners of the edge the strip sits on. The glide moves the
    /// window frame by frame, so this runs on every step, not only at the end.
    private func layOutPane() {
        let size = panel.frame.size
        guard size.width > 1, size.height > 1 else { return }

        let corners = IslandLayout.corners(edge ?? .top, docked: true)
        glass.maskImage = IslandStrip.mask(size, corners)

        edging.frame = NSRect(origin: .zero, size: size)
        line.frame = edging.bounds
        // Half a point inside, so the mask does not cut the line in half.
        line.path = IslandStrip.shape(edging.bounds.insetBy(dx: 0.5, dy: 0.5), IslandStrip.turnedOver(corners)).cgPath
    }

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the pointer at once, which is what keeps it feeling attached.
    func place(on landing: Box, edge: ScreenEdge) {
        let moving = self.edge != nil && self.edge != edge
        self.edge = edge

        // The pane is exactly where the island will land. It used to reach past the screen edge so
        // that the two corners on that side fell outside; macOS pushes a window back in instead, and
        // the mask gives the flat side now.
        let pane = Screens.rect(landing)

        if panel.isVisible, moving, !Motion.reduceMotion {
            let from = panel.frame.origin
            glide.run(Motion.expand, Motion.standard) { [panel, weak self] share in
                panel.setFrame(
                    NSRect(
                        x: from.x + ((pane.minX - from.x) * share),
                        y: from.y + ((pane.minY - from.y) * share),
                        width: pane.width,
                        height: pane.height),
                    display: true)
                self?.layOutPane()
            }
            return
        }

        glide.stop()
        panel.setFrame(pane, display: true)
        layOutPane()

        if !panel.isVisible {
            panel.orderFrontRegardless()
        }
    }

    func close() {
        glide.stop()
        panel.orderOut(nil)
        panel.close()
    }

    var frame: NSRect { panel.frame }
    var isVisible: Bool { panel.isVisible }
}
