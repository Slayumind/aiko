import AikoKit
import AppKit

/// The island while Aiko lives at the edge of the screen: the window itself, hiding it behind a
/// full screen window, and remembering where the person dragged it. The twin of IslandHost.cs.
@MainActor
final class IslandHost {
    /// The island asks for the card: true for a click, which pins it.
    var onCard: ((Bool) -> Void)?

    /// Set by a self test, which drags the island about. The settings of the person are not its
    /// to change.
    var changingNothing = false

    private(set) var window: IslandWindow?
    private var fullScreen: FullScreenWatch?
    private var fullScreenInFront = false
    private var underTerminal = false

    var isShown: Bool { window != nil }

    func show(_ cards: [CardState], at position: IslandPosition, newerVersion: String?) {
        if window == nil {
            let made = IslandWindow()
            made.onCard = { [weak self] pinned in self?.onCard?(pinned) }
            made.onMoved = { [weak self] moved in self?.savePosition(moved) }
            window = made
        }

        window?.newerVersion = newerVersion
        window?.show(cards, at: position)

        // Only watched while the island is on screen: the menu bar icon has nothing to hide from.
        if fullScreen == nil {
            let watch = FullScreenWatch()
            watch.changed = { [weak self] full in self?.onFullScreen(full) }
            fullScreen = watch
        }

        onFullScreen(FullScreenWatch.isFullScreenInFront())
    }

    func close() {
        fullScreen?.stop()
        fullScreen = nil
        window?.close()
        window = nil
    }

    func update(_ cards: [CardState]) {
        window?.update(cards)
    }

    /// The words about a newer version, when the daily check found one or stopped seeing one.
    func update(_ cards: [CardState], newerVersion: String?) {
        window?.newerVersion = newerVersion
        window?.update(cards)
    }

    /// The island somewhere else, for a self test. Nothing is saved.
    func place(_ cards: [CardState], at position: IslandPosition) {
        window?.show(cards, at: position)
    }

    /// One step of the way to a face and back, drawn on the island.
    func draw(_ faces: FacePlay) {
        window?.face(faces.frame, fit: faces.fit, picture: faces.picture)
    }

    func holds(_ point: NSPoint) -> Bool {
        window?.holds(point) == true
    }

    /// The window of the terminal is on screen (D-310): its title shows the limit, so the island
    /// steps aside. A minimized or closed terminal brings it back.
    func followTerminal(_ inView: Bool) {
        underTerminal = inView
        apply()
    }

    private func onFullScreen(_ full: Bool) {
        fullScreenInFront = full
        apply()
    }

    private func apply() {
        guard let window else { return }

        let hide = (fullScreenInFront && Store.settings().hideIslandInFullScreen) || underTerminal
        guard window.isVisible == hide else { return }

        // Only a real change is worth a line: the space changes many times a minute.
        window.hide(hide)
        let why = underTerminal ? "hidden while the terminal is open" : "hidden behind a full screen window"
        Log.write("island \(hide ? why : "shown again")")
    }

    private func savePosition(_ position: IslandPosition) {
        // A self test drags the island about; the person's settings are not its to change.
        guard !changingNothing else {
            Log.write("island would be saved at \(position.edge) \(String(format: "%.2f", position.along))")
            return
        }

        var settings = Store.settings()
        settings.island = position
        Store.saveSettings(settings)
        Log.write("island moved to \(position.edge) at \(String(format: "%.2f", position.along))")
    }
}
