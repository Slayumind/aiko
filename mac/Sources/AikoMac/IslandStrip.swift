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

    /// A rounded rectangle for NSVisualEffectView.maskImage: the documented way to give one
    /// corners. The image stretches from the middle, so it fits any size of the strip.
    private static func mask(radius: CGFloat) -> NSImage {
        let side = radius * 2 + 1
        let image = NSImage(size: NSSize(width: side, height: side), flipped: false) { rect in
            NSColor.black.setFill()
            NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius).fill()
            return true
        }
        image.capInsets = NSEdgeInsets(top: radius, left: radius, bottom: radius, right: radius)
        image.resizingMode = .stretch
        return image
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
    private let glide = Tween()
    private var edge: ScreenEdge?

    init() {
        panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 10, height: 10),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false)

        panel.isFloatingPanel = true
        // Above ordinary windows but under the island and under the menu bar, the way the Windows
        // strip sits under the taskbar: whatever reaches past the screen edge goes under them.
        panel.level = .floating
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.ignoresMouseEvents = true
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.collectionBehavior = [.canJoinAllSpaces, .transient, .ignoresCycle, .fullScreenAuxiliary]
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
        glass.maskImage = IslandStrip.mask(radius: IslandStrip.radius)
        glass.autoresizingMask = [.width, .height]

        // The hairline the island and the card carry, drawn in a view of its own so the glass
        // itself keeps no layer of ours.
        edging.autoresizingMask = [.width, .height]
        edging.wantsLayer = true
        edging.layer?.borderWidth = 1
        edging.layer?.borderColor = Theme.nsHairline.cgColor
        edging.layer?.cornerRadius = IslandStrip.radius
        glass.addSubview(edging)


        panel.contentView = glass
    }

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the pointer at once, which is what keeps it feeling attached.
    /// Keeps the hairline on the pane when it is resized. The glide moves the window frame by
    /// frame, so the edging is laid out on every step, not only at the end.
    private func layOutEdging() {
        edging.frame = NSRect(origin: .zero, size: panel.frame.size)
    }

    func place(on landing: Box, edge: ScreenEdge) {
        let moving = self.edge != nil && self.edge != edge
        self.edge = edge

        let pane = Screens.rect(
            IslandPlacement.pastTheEdge(landing, edge, by: Double(IslandStrip.radius)))

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
                self?.layOutEdging()
            }
            return
        }

        glide.stop()
        panel.setFrame(pane, display: true)
        layOutEdging()

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
