import AikoKit
import AppKit
import SwiftUI

/// The card is a window of its own: no frame, see through background, above other windows and
/// never taking the focus. It is created when it is needed and closed, not hidden.
///
/// The twin of Card/CardWindow.xaml.cs on Windows.
@MainActor
final class CardWindow {
    private let panel: NSPanel
    private let state = CardViewState()

    var onSettings: (() -> Void)?
    var onClosed: (() -> Void)?

    /// A card opened by resting the mouse on the icon goes away when the mouse goes away. A card
    /// the user clicked for, or clicked on, stays until they close it.
    private(set) var isPinned = false

    /// True from the start of the fade: a click in that moment opens a new card instead of pinning
    /// the one on its way out.
    private(set) var isClosing = false

    init(model: CardModel) {
        state.model = model

        panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: Theme.cardWidth, height: 100),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false)

        panel.isFloatingPanel = true
        panel.level = .statusBar
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.isMovableByWindowBackground = false
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.animationBehavior = .none

        state.onSettings = { [weak self] in self?.onSettings?() }
        state.onClose = { [weak self] in self?.fadeAndClose() }

        let hosting = NSHostingView(rootView: CardHost(state: state))
        panel.setContentSize(hosting.fittingSize)
        panel.contentView = hosting
    }

    var frame: NSRect { panel.frame }

    func update(_ model: CardModel) {
        state.model = model
        guard let hosting = panel.contentView as? NSHostingView<CardHost> else { return }

        let size = hosting.fittingSize
        guard size != panel.frame.size else { return }

        // The card keeps its top edge: rows come and go, and a card that grew upwards would slide
        // out from under the pointer.
        let top = panel.frame.maxY
        panel.setContentSize(size)
        panel.setFrameOrigin(NSPoint(x: panel.frame.origin.x, y: top - size.height))
    }

    /// Touching the card at all keeps it: reading it, dragging it, or opening the settings from it.
    /// Somebody who reached for the card meant to use it.
    func pin() { isPinned = true }

    /// Shows the card already in place, under the status item. The size is asked for again first:
    /// a hosting view measures a little differently once it belongs to a window, and a card placed
    /// on the old size would sit a few points off.
    func show(under item: NSRect?) {
        if let hosting = panel.contentView as? NSHostingView<CardHost> {
            hosting.layoutSubtreeIfNeeded()
            panel.setContentSize(hosting.fittingSize)
        }

        place(under: item)
        panel.alphaValue = 1
        panel.orderFrontRegardless()

        // A turn later, so the card is drawn small and see through once before it grows.
        DispatchQueue.main.async { [state] in state.shown = true }
    }

    /// Goes away the way it came, quicker: a short fade, then the window is really closed.
    func fadeAndClose() {
        if isClosing {
            return
        }

        isClosing = true
        panel.ignoresMouseEvents = true

        guard !Motion.reduceMotion, panel.isVisible else {
            close()
            return
        }

        NSAnimationContext.runAnimationGroup { context in
            context.duration = Motion.hover
            context.timingFunction = Motion.timing(Motion.standard)
            panel.animator().alphaValue = 0
        } completionHandler: { [weak self] in
            // AppKit calls this back on the main thread, but the type does not say so.
            MainActor.assumeIsolated { self?.close() }
        }
    }

    private func close() {
        panel.orderOut(nil)
        panel.close()
        onClosed?()
    }

    /// The menu bar is at the top of every Mac screen, so the card hangs under the status item and
    /// is only nudged sideways to stay on screen.
    private func place(under item: NSRect?) {
        let size = panel.frame.size
        let screen = item.flatMap { rect in NSScreen.screens.first { $0.frame.intersects(rect) } }
            ?? NSScreen.main
        let room = screen?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1440, height: 900)

        // Without a status item to hang from, the card sits in the top right corner, where the
        // menu bar icons are anyway.
        let anchor = item ?? NSRect(x: room.maxX - size.width / 2, y: room.maxY, width: 0, height: 0)

        let left = min(
            max(anchor.midX - (size.width / 2), room.minX),
            max(room.minX, room.maxX - size.width))

        panel.setFrameOrigin(NSPoint(x: left, y: anchor.minY - size.height))
    }

    /// Whether the pointer, in screen points, is on the card.
    func holds(_ point: NSPoint) -> Bool {
        panel.frame.contains(point)
    }
}

/// What the view reads. A class, so the window can hand it new numbers without building the view
/// again.
@MainActor
final class CardViewState: ObservableObject {
    @Published var model = CardModel(updated: "", blocks: [])
    @Published var shown = false

    var onSettings: () -> Void = {}
    var onClose: () -> Void = {}
}

/// The card plus the way it arrives: it grows out of the status item, from a little smaller and a
/// few points below, with the spring everything else in Aiko uses (D-161).
struct CardHost: View {
    @ObservedObject var state: CardViewState

    var body: some View {
        CardView(model: state.model, onSettings: state.onSettings, onClose: state.onClose)
            .opacity(state.shown ? 1 : 0)
            .animation(Motion.curve(Motion.standard, Motion.hover), value: state.shown)
            .scaleEffect(state.shown ? 1 : Motion.popFrom, anchor: .top)
            .offset(y: state.shown ? 0 : Motion.popShift)
            .animation(Motion.curve(Motion.spring, Motion.settle), value: state.shown)
    }
}

extension Motion {
    /// The system is asked to show fewer animations: everything lands at its end state at once,
    /// the way Motion.IsOn works on Windows.
    static var reduceMotion: Bool {
        NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
    }

    static func curve(_ bezier: CubicBezier, _ duration: Double) -> Animation {
        .timingCurve(
            bezier.x1, bezier.y1, bezier.x2, bezier.y2,
            duration: or0(duration, reduceMotion: reduceMotion))
    }

    static func timing(_ bezier: CubicBezier) -> CAMediaTimingFunction {
        CAMediaTimingFunction(
            controlPoints: Float(bezier.x1), Float(bezier.y1), Float(bezier.x2), Float(bezier.y2))
    }
}

/// The card is dragged by its header. AppKit moves the window for us, so there is no mouse maths.
struct WindowDragArea: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView { DragView() }

    func updateNSView(_ view: NSView, context: Context) {}

    private final class DragView: NSView {
        override func mouseDown(with event: NSEvent) {
            window?.performDrag(with: event)
        }
    }
}
