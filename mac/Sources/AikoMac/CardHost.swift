import AikoKit
import AppKit

/// The life of the card: the wait before it opens under a resting mouse, a click that pins it, and
/// the watch that closes an unpinned card when the mouse walks away. The card opens from the menu
/// bar icon or from the island, whichever is on screen. The twin of CardHost.cs.
@MainActor
final class CardHost {
    /// The gear on the card.
    var onSettings: (() -> Void)?
    var onTerminal: (() -> Void)?

    private let numbers: ShellNumbers
    private let icon: MenuBarIcon
    private let island: IslandHost

    private var hover: Timer?
    private var cardWatch: Timer?
    private var away = AwayWatch()
    private(set) var window: CardWindow?

    init(numbers: ShellNumbers, icon: MenuBarIcon, island: IslandHost) {
        self.numbers = numbers
        self.icon = icon
        self.island = island
    }

    /// New numbers or new settings: an open card is drawn again.
    func refresh() {
        window?.update(numbers.card())
    }

    func startHover() {
        // Already counting, or the card is up: nothing to start.
        guard window == nil, hover == nil else { return }

        hover = Timer.scheduledTimer(withTimeInterval: CardLifetime.hoverDelay, repeats: false) { _ in
            MainActor.assumeIsolated { self.onHoverFinished() }
        }
    }

    func cancelHover() {
        hover?.invalidate()
        hover = nil
    }

    func open(pinned: Bool) {
        numbers.readAccounts()

        if let open = window, !open.isClosing {
            // A second click on a card that is already pinned closes it, the way it opened.
            if pinned && open.isPinned {
                open.fadeAndClose()
                return
            }

            if pinned {
                open.pin()
            }
            open.update(numbers.card())
            return
        }

        let card = CardWindow(model: numbers.card())
        card.onSettings = { [weak self] in self?.onSettings?() }
        card.onTerminal = { [weak self] in self?.onTerminal?() }
        card.onOpenClaude = { [weak self] name in self?.openClaudeCode(named: name) }
        card.onClosed = { [weak self] in
            guard let self, self.window === card else { return }
            self.window = nil
            self.stopCardWatch()
        }

        window = card
        if pinned {
            card.pin()
        }

        if let island = island.window, !icon.isShown {
            // From the island the card opens right under it, or over it at the bottom edge.
            card.show(from: island.frame, above: island.edge == .bottom)
        } else {
            card.show(from: icon.frame)
        }

        watchForTheMouseLeaving()

        Log.write("card opened at \(Int(card.frame.origin.x)),\(Int(card.frame.origin.y)) "
            + "size \(Int(card.frame.width))x\(Int(card.frame.height)), pinned: \(pinned)")
    }

    /// The card away at once, for a self test that opens the next one right after.
    func closeForCheck() {
        window?.fadeAndClose()
        window = nil
    }

    func stop() {
        hover?.invalidate()
        cardWatch?.invalidate()
        window?.fadeAndClose()
    }

    /// Nothing promises to say when the pointer left the icon, so it is checked here. Without this
    /// the card would appear long after the user walked away.
    private func onHoverFinished() {
        hover = nil
        let over = icon.pointerIsOver
        Log.write("hover finished, pointer over icon: \(over)")
        if over {
            open(pinned: false)
        }
    }

    /// An unpinned card follows the mouse out. The pointer has to be away from the icon, the island
    /// and the card for two turns in a row, because there is a gap between them that it crosses on
    /// its way in. The watch runs only while an unpinned card is on screen, a few seconds at a time.
    private func watchForTheMouseLeaving() {
        guard let window, !window.isPinned else { return }

        away = AwayWatch()
        cardWatch?.invalidate()
        cardWatch = Timer.scheduledTimer(
            withTimeInterval: CardLifetime.watchInterval, repeats: true
        ) { _ in
            MainActor.assumeIsolated { self.onCardWatchTurn() }
        }
    }

    private func onCardWatchTurn() {
        guard let window, !window.isPinned else {
            stopCardWatch()
            return
        }

        // The island is home for the card the same way the icon is (D-148). Without it a card
        // opened from the island closed half a second later, before the mouse could reach it.
        let pointer = NSEvent.mouseLocation
        let home = icon.pointerIsOver || window.holds(pointer) || island.holds(pointer)
        if away.turn(pointerIsHome: home) {
            stopCardWatch()
            window.fadeAndClose()
        }
    }

    private func stopCardWatch() {
        cardWatch?.invalidate()
        cardWatch = nil
    }

    /// Opens Claude Code in the folder of one environment, from the card. A card that stays open
    /// behind a new terminal window is what the person asked for: they pressed a button on it.
    private func openClaudeCode(named environment: String) {
        guard let folder = Store.environments().environments
            .first(where: { $0.name == environment })?.configDirectories.first
        else {
            return
        }

        window?.pin()
        ClaudeLauncher.open(configFolder: folder, workingDirectory: Store.home)
    }
}
