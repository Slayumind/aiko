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

    private let panel: NSPanel
    private let glass = NSVisualEffectView()
    private let tint = NSView()
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

        // Dark glass, whatever the person's appearance: the island is always dark, and so is what
        // shows where it will land.
        glass.material = .hudWindow
        glass.blendingMode = .behindWindow
        glass.state = .active
        glass.appearance = NSAppearance(named: .darkAqua)
        glass.wantsLayer = true
        glass.layer?.cornerRadius = IslandStrip.radius
        glass.layer?.masksToBounds = true
        glass.autoresizingMask = [.width, .height]

        // The glass is 95 % see through, with Aiko's own surface laid over it at 5 %. Nothing else
        // is drawn on it, as the owner asked (D-186).
        tint.wantsLayer = true
        tint.layer?.backgroundColor = Theme.nsSurface.withAlphaComponent(0.05).cgColor
        tint.autoresizingMask = [.width, .height]
        glass.addSubview(tint)

        panel.contentView = glass
    }

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the pointer at once, which is what keeps it feeling attached.
    func place(on landing: Box, edge: ScreenEdge) {
        let moving = self.edge != nil && self.edge != edge
        self.edge = edge

        let pane = Screens.rect(
            IslandPlacement.pastTheEdge(landing, edge, by: Double(IslandStrip.radius)))
        tint.frame = NSRect(origin: .zero, size: pane.size)

        if panel.isVisible, moving, !Motion.reduceMotion {
            let from = panel.frame.origin
            glide.run(Motion.expand, Motion.standard) { [panel] share in
                panel.setFrame(
                    NSRect(
                        x: from.x + ((pane.minX - from.x) * share),
                        y: from.y + ((pane.minY - from.y) * share),
                        width: pane.width,
                        height: pane.height),
                    display: true)
            }
            return
        }

        glide.stop()
        panel.setFrame(pane, display: true)

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
