import AikoKit
import AppKit

/// The icon in the menu bar. It draws the ring, the dot and a face, keeps the tooltip, and says
/// what the mouse did. What the numbers are and what a click means is not its business: it asks for
/// the rows and the tooltip, and calls back. The twin of TrayIcon.cs.
@MainActor
final class MenuBarIcon: NSResponder {
    static let size: CGFloat = 18

    /// Where the rows and the words come from. Set by the shell before the icon is added.
    var rows: () -> (ring: CardRow?, dot: CardRow?) = { (nil, nil) }
    var tooltip: () -> String = { "" }

    /// The face being played, shared with the island: Aiko is in one place at a time.
    var faces: FacePlay?

    /// A left click on the icon.
    var onClick: (() -> Void)?

    /// A right click: the menu is asked for.
    var onMenu: (() -> Void)?

    var onMouseEntered: (() -> Void)?
    var onMouseExited: (() -> Void)?

    private var statusItem: NSStatusItem?

    /// The rows the icon last drew. Frames of a face transition reuse them instead of reading the
    /// settings and the snapshots sixty times a second.
    private var lastRows: (ring: CardRow?, dot: CardRow?) = (nil, nil)

    var isShown: Bool { statusItem != nil }

    func add() {
        guard statusItem == nil else { return }

        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem = item

        if let button = item.button {
            button.target = self
            button.action = #selector(clicked)
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
            button.addTrackingArea(NSTrackingArea(
                rect: .zero,
                options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                owner: self,
                userInfo: nil))
        }

        update()
    }

    func remove() {
        guard let statusItem else { return }
        NSStatusBar.system.removeStatusItem(statusItem)
        self.statusItem = nil
    }

    /// New numbers: the picture and the tooltip are both made again.
    func update() {
        guard let button = statusItem?.button else { return }

        lastRows = rows()
        redraw()

        // The numbers go in the tooltip as well as in the ring: a screen reader has nothing else
        // to read, and the ring says nothing to one.
        button.toolTip = tooltip()
    }

    /// One frame of a face transition: only the picture changes, not the tooltip.
    func redraw() {
        guard let button = statusItem?.button else { return }

        button.image = StatusIcon.image(
            size: Self.size,
            ring: lastRows.ring,
            dot: lastRows.dot,
            frame: faces?.frame ?? .rings,
            face: faces?.picture)
    }

    /// Where the menu bar put our icon, in screen points. Empty before the menu bar has placed it,
    /// and while Aiko lives on the island instead.
    var frame: NSRect? {
        guard let frame = statusItem?.button?.window?.frame, frame.width > 0 else { return nil }
        return frame
    }

    var pointerIsOver: Bool {
        guard let frame else { return false }
        return frame.contains(NSEvent.mouseLocation)
    }

    /// The menu under the icon. The call returns when the menu closes.
    func show(_ menu: NSMenu) {
        statusItem?.menu = menu
        statusItem?.button?.performClick(nil)
        statusItem?.menu = nil
    }

    /// What a self test reads back.
    var rowsForCheck: (ring: CardRow?, dot: CardRow?) { lastRows }
    var tooltipForCheck: String { statusItem?.button?.toolTip ?? "" }
    var pictureForCheck: Data? { statusItem?.button?.image?.tiffRepresentation }

    override func mouseEntered(with event: NSEvent) {
        onMouseEntered?()
    }

    override func mouseExited(with event: NSEvent) {
        onMouseExited?()
    }

    @objc private func clicked() {
        if NSApp.currentEvent?.type == .rightMouseUp {
            onMenu?()
        } else {
            onClick?()
        }
    }
}
