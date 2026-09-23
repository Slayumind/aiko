import AikoKit
import AppKit

/// The island: a small window stuck to an edge of the screen, above other windows and never taking
/// the focus. The twin of IslandWindow.xaml and IslandWindow.xaml.cs.
///
/// It holds the window, the mouse and the timers. What the island looks like is IslandView's work,
/// and where it goes is IslandPlacement's.
@MainActor
final class IslandWindow {
    private let panel: NSPanel
    private let view = IslandView()

    private var position = IslandPosition.default
    private var cards: [CardState] = []

    /// The card is asked for by a click, or by resting the mouse on the island. True means a click:
    /// the card then stays until it is closed, the same as after a click on the menu bar icon.
    var onCard: ((Bool) -> Void)?

    /// Raised after a drag, with the place the island ended up in.
    var onMoved: ((IslandPosition) -> Void)?

    init() {
        panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 66, height: 34),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false)

        panel.isFloatingPanel = true
        // Above ordinary windows and above the menu bar. Under the bar the window server keeps the
        // island out of the bar's whole strip, which is a point taller than the bar itself, and the
        // island could never touch it. Above the bar it can, and it never covers it: it starts where
        // the bar ends.
        panel.level = WindowOrder.onTop
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.isMovableByWindowBackground = false

        // On every space, and out of Mission Control and Exposé: the island belongs to the edge of
        // the screen, not to a desk of windows.
        // .auxiliary and .stationary keep the island where it is when the wallpaper is clicked and
        // every window slides aside: it belongs to the edge of the screen, not to a desk of windows.
        // A spike measured it (RESEARCH, 2026-09-18): with .transient the pane slid away with them.
        panel.collectionBehavior = [.canJoinAllSpaces, .auxiliary, .stationary, .ignoresCycle, .fullScreenAuxiliary]
        panel.animationBehavior = .none

        view.onEnter = { [weak self] in self?.pointerCame() }
        view.onLeave = { [weak self] in self?.pointerLeft() }
        view.onPress = { [weak self] at in self?.press(at: Screens.point(at)) }
        view.onDrag = { [weak self] at in self?.hold(at: Screens.point(at)) }
        view.onRelease = { [weak self] in self?.letGo() }
        panel.contentView = view
    }

    // ---- Showing the numbers ----

    func show(_ cards: [CardState], at position: IslandPosition) {
        self.position = position
        self.cards = cards
        view.show(cards, edge: position.edge)

        fit(log: !panel.isVisible)

        if !panel.isVisible {
            panel.orderFrontRegardless()
        }
    }

    func update(_ cards: [CardState]) {
        let crossed = IslandReveal.toneRose(self.cards, cards)
        self.cards = cards

        // New numbers while the island is in hand change the rings, never where it is.
        if drag?.isDragging == true {
            view.show(cards, edge: handEdge, docked: false)
            return
        }

        view.show(cards, edge: position.edge)
        fit()

        if crossed {
            reveal()
        }
    }

    func close() {
        hoverTimer?.invalidate()
        revealEnds?.invalidate()
        unfolding.stop()
        landing.stop()
        strip?.close()
        strip = nil
        panel.orderOut(nil)
        panel.close()
    }

    /// Hidden while a full screen window is in front, if the settings ask for it (D-161).
    func hide(_ hidden: Bool) {
        if hidden {
            panel.orderOut(nil)
        } else if !panel.isVisible {
            panel.orderFrontRegardless()
        }
    }

    var isVisible: Bool { panel.isVisible }
    var frame: NSRect { panel.frame }

    /// Where the landing strip is, for the self test. Nil when the island is not in hand.
    var landingStrip: NSRect? { strip.map(\.frame) }
    var edge: ScreenEdge { drag?.isDragging == true ? handEdge : position.edge }

    /// Shows the landing strip on an edge and leaves it there, so a person or a screenshot can
    /// look at the glass. Only the self test calls these two.
    func showStripForCheck(on edge: ScreenEdge) {
        let size = view.wanted
        let work = Screens.work(under: Screens.box(panel.frame))
        let landing = IslandPlacement.place(
            IslandPosition(edge, position.along), Double(size.width), Double(size.height), work)

        let pane = IslandStrip()
        pane.place(on: landing, edge: edge)
        strip = pane
    }

    func hideStripForCheck() {
        strip?.close()
        strip = nil
    }

    /// Whether the pointer, in screen points, is on the island. The card is at home over the island
    /// as well as over the menu bar icon (D-148).
    func holds(_ point: NSPoint) -> Bool {
        panel.isVisible && panel.frame.contains(point)
    }

    /// The island takes exactly the size of what it holds, and then goes back to its edge. Called
    /// on every frame of anything that changes its size.
    private func fit(log: Bool = false) {
        let size = view.wanted
        let work = workArea()
        let placed = IslandPlacement.place(position, size.width, size.height, work)

        panel.setFrame(Screens.rect(placed), display: true)
        view.frame = NSRect(origin: .zero, size: size)
        view.needsDisplay = true

        if log {
            Log.write(
                "island placed: work \(Int(work.x)),\(Int(work.y)) \(Int(work.width))x\(Int(work.height)), "
                + "size \(Int(size.width))x\(Int(size.height)), at \(Int(placed.x)),\(Int(placed.y)) "
                + "on the \(name(position.edge)) edge")
        }
    }

    private func workArea() -> Box {
        panel.frame.width > 0
            ? Screens.work(under: Screens.box(panel.frame))
            : Screens.work(over: Point(0, 0))
    }

    private func name(_ edge: ScreenEdge) -> String {
        switch edge {
        case .top: return "top"
        case .bottom: return "bottom"
        case .left: return "left"
        case .right: return "right"
        }
    }

    // ---- Unfolding (D-161): on a hover, and by itself for a moment when a limit crosses a line ----

    private let unfolding = Tween()
    private var revealEnds: Timer?
    private var revealing = false

    var isUnfolded: Bool { view.unfold > 0.5 }

    /// Opens or closes the percentages. The window follows its own size while they slide, one frame
    /// at a time, and stops the moment they are done: no frames while nothing moves.
    func unfold(_ open: Bool, animate: Bool = true) {
        let to = open ? 1.0 : 0.0
        guard view.unfold != to || unfolding.isRunning else { return }

        let from = view.unfold
        guard animate else {
            unfolding.stop()
            view.unfold = to
            fit()
            return
        }

        unfolding.run(Motion.expand, Motion.standard) { [weak self] share in
            guard let self else { return }
            self.view.unfold = from + ((to - from) * share)
            self.fit()
        }
    }

    /// Opens the island for a moment without a hover, so numbers that just crossed a line are seen.
    private func reveal() {
        revealing = true
        revealEnds?.invalidate()
        revealEnds = Timer.scheduledTimer(withTimeInterval: IslandReveal.showFor, repeats: false) { _ in
            MainActor.assumeIsolated {
                self.revealing = false
                if !self.pointerIsOver {
                    self.unfold(false)
                }
            }
        }

        unfold(true)
    }

    // ---- The mouse ----

    private var hoverTimer: Timer?

    private var pointerIsOver: Bool { holds(NSEvent.mouseLocation) }

    private func pointerCame() {
        guard drag == nil else { return }

        hoverTimer?.invalidate()
        hoverTimer = Timer.scheduledTimer(withTimeInterval: CardLifetime.hoverDelay, repeats: false) { _ in
            MainActor.assumeIsolated {
                self.hoverTimer = nil
                if self.pointerIsOver {
                    self.onCard?(false)
                }
            }
        }

        unfold(true)
    }

    private func pointerLeft() {
        hoverTimer?.invalidate()
        hoverTimer = nil

        if !revealing, drag == nil {
            unfold(false)
        }
    }

    // ---- In hand: the island follows the pointer and the strip shows where it will land ----

    private var drag: IslandDrag?
    private var handEdge = ScreenEdge.top
    private var strip: IslandStrip?
    private let landing = Tween()

    /// The island taken in hand at this point of the screen. The mouse calls it, and so does the
    /// self test, which drags the island from code without touching the person's mouse.
    func press(at cursor: Point) {
        hoverTimer?.invalidate()
        hoverTimer = nil
        landing.stop()
        drag = IslandDrag(pressedAt: cursor, island: Screens.box(panel.frame))
    }

    func hold(at cursor: Point) {
        guard var held = drag else { return }

        let wasDragging = held.isDragging
        let dragging = held.hold(cursor)
        drag = held
        guard dragging else { return }

        if !wasDragging {
            startDrag()
        }

        let work = Screens.work(over: cursor)
        let edge = IslandPlacement.nearestEdge(cursor.x, cursor.y, work, handEdge)
        if edge != handEdge {
            // The rings lay themselves out for the new edge while the island is still in hand.
            handEdge = edge
            view.show(cards, edge: edge, docked: false)
        }

        let size = view.wanted
        let island = held.held(cursor, Extent(size.width, size.height))
        panel.setFrame(Screens.rect(island), display: true)
        view.frame = NSRect(origin: .zero, size: size)
        view.needsDisplay = true

        let wouldLand = IslandPlacement.dropAt(
            edge, island.centreX, island.centreY, island.width, island.height, work)
        strip?.place(
            on: IslandPlacement.place(wouldLand, island.width, island.height, work), edge: edge)
    }

    /// Lets the island go where it is, as the mouse button coming up would.
    func letGo() {
        guard let held = drag else { return }

        drag = nil
        strip?.close()
        strip = nil

        // A press that moved nothing is a click, and it opens the card.
        guard held.isDragging else {
            onCard?(true)
            return
        }

        let island = Screens.box(panel.frame)
        let work = Screens.work(under: island)
        position = IslandPlacement.dropAt(
            handEdge, island.centreX, island.centreY, island.width, island.height, work)
        land(from: island, work: work)
        onMoved?(position)
    }

    private func startDrag() {
        revealing = false
        revealEnds?.invalidate()
        unfold(false, animate: false)
        handEdge = position.edge

        // In hand the island is a whole thing of its own: every corner rounded and a line all round.
        view.show(cards, edge: handEdge, docked: false)
        strip = IslandStrip()

        // Both panes sit at the same level now, so the island is ordered over the glass once the
        // strip appears: in hand it is the thing the person is holding.
        panel.orderFrontRegardless()
    }

    /// The island flattens against its edge and settles into place with a small overshoot, the same
    /// spring as everything else Aiko moves (D-161).
    private func land(from: Box, work: Box) {
        view.show(cards, edge: position.edge)
        let size = view.wanted
        let target = IslandPlacement.place(position, size.width, size.height, work)
        view.frame = NSRect(origin: .zero, size: size)

        // At the bottom edge the overshoot of the spring goes under the Dock, the way it goes under
        // the taskbar on Windows. At the top there is nothing to go under: macOS pushes a window out
        // of the menu bar's strip instead of drawing it below, so the island keeps its level and the
        // view clips whatever would show above the bar.
        panel.level = position.edge == .bottom ? WindowOrder.underTheDock : WindowOrder.onTop
        let clipsUnderTheMenuBar = position.edge == .top

        landing.run(Motion.settle, Motion.spring) { [weak self] share in
            guard let self else { return }
            let step = Box(
                from.x + ((target.x - from.x) * share),
                from.y + ((target.y - from.y) * share),
                size.width,
                size.height)

            self.panel.setFrame(Screens.rect(step), display: true)

            // The overshoot at the top would show above the menu bar, so the part of the island that
            // reaches into the bar is simply not drawn: it reads as going under it.
            self.view.cutFromTheTop = clipsUnderTheMenuBar ? max(0, work.y - step.y) : 0
            self.view.needsDisplay = true
        } done: { [weak self] in
            guard let self else { return }
            self.panel.level = WindowOrder.onTop
            self.view.cutFromTheTop = 0
            self.fit(log: true)
        }
    }

    // ---- The face (D-211) ----

    /// One moment of the way to a face and back. The island changes size with it, so the window
    /// follows for the length of the step.
    func face(_ frame: IconFrame, fit faceFit: Double, picture: FacePicture?) {
        view.iconFrame = frame
        view.faceFit = faceFit
        view.face = picture

        if drag?.isDragging == true {
            view.needsDisplay = true
            return
        }

        fit()
    }
}
