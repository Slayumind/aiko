import AikoKit
import AppKit

/// Everything Aiko shows on macOS: the icon in the menu bar and the card under it. One owner,
/// because both show the same numbers.
///
/// The twin of AikoShell.cs on Windows. The island, the settings window and the wizard are not
/// here yet; the menu items that would open them are marked below.
@MainActor
final class AikoShell: NSResponder {
    private static let iconSize: CGFloat = 18

    private var statusItem: NSStatusItem?
    private var watch: SnapshotWatch?
    private var hover: Timer?
    private var cardWatch: Timer?
    private var away = AwayWatch()
    private var card: CardWindow?

    /// Plan and sign-in per environment. Read when the card opens, not on every new number:
    /// .claude.json can be large, and neither the plan nor the sign-in changes between two answers.
    private var accounts: [String: CardAccount] = [:]

    /// Environments where Aiko's line is not in the Claude Code settings, so no numbers can ever
    /// arrive. Worked out when the settings change rather than every time the card is drawn.
    private var noAccess: Set<String> = []

    func show() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem = item

        if let button = item.button {
            button.target = self
            button.action = #selector(onClick)
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
            button.addTrackingArea(NSTrackingArea(
                rect: .zero,
                options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                owner: self,
                userInfo: nil))
        }

        let watcher = SnapshotWatch(folder: Store.folders.snapshotsFolder)
        watcher.updated = { [weak self] in self?.onSnapshotsChanged() }
        watch = watcher

        noteWhereWeHaveNoAccess()
        updateIcon()

        let environments = Store.environments()
        Log.write("found \(environments.environments.count) environments, "
            + "\(watcher.byFile.count) snapshot files")
    }

    func stop() {
        hover?.invalidate()
        cardWatch?.invalidate()
        card?.fadeAndClose()
        watch?.stop()

        if let statusItem {
            NSStatusBar.system.removeStatusItem(statusItem)
        }
    }

    /// Proof that the app starts, reads the snapshots and lays the card out, for a machine nobody
    /// is looking at. The shim has the same door under AIKO_SHIM_SELF_TEST.
    func selfTest() {
        selfTesting = true
        openCard(pinned: true)

        let rows = currentRows()
        Log.write("self test: icon \(describe(iconFrame()))")
        Log.write("self test: tooltip \"\(statusItem?.button?.toolTip ?? "")\"")
        Log.write("self test: ring \(describe(rows.ring)), dot \(describe(rows.dot))")

        if let card {
            Log.write("self test: card \(describe(card.frame))")
        }

        let model = currentCard()
        Log.write("self test: header \"\(model.updated)\"")
        for block in model.blocks {
            // The plan itself is an account fact and stays out of the log, as on Windows.
            Log.write("self test: block \"\(block.name)\" plan shown: \(block.showsPlan) "
                + "state \"\(block.state)\" note \"\(block.showsNote ? block.note : "")\"")
            for row in block.rows {
                Log.write("self test: row \"\(row.name)\" \"\(row.percent)\" "
                    + "\"\(row.resets)\" \"\(row.pace)\" fill \(row.fill)")
            }
        }

        // Long enough for the card to draw and for a new snapshot to arrive, then away: nothing
        // here is meant to stay on screen.
        DispatchQueue.main.asyncAfter(deadline: .now() + 3) { NSApp.terminate(nil) }
    }

    private var selfTesting = false

    private func describe(_ row: CardRow?) -> String {
        guard let row else { return "none" }
        return "\(row.percent)% \(row.tone)"
    }

    private func describe(_ rect: NSRect?) -> String {
        guard let rect else { return "nowhere" }
        let size = "\(Int(rect.width))x\(Int(rect.height))"
        return size + " at \(Int(rect.minX)),\(Int(rect.minY))"
    }

    // ---- The icon ----

    private func updateIcon() {
        guard let button = statusItem?.button else { return }

        let rows = currentRows()
        button.image = StatusIcon.image(size: Self.iconSize, ring: rows.ring, dot: rows.dot)

        // The numbers go in the tooltip as well as in the ring: a screen reader has nothing else
        // to read, and the ring says nothing to one.
        // TODO: the newer version comes from the update check, which is not built yet.
        button.toolTip = TrayText.tooltip(cards(), newerVersion: nil)
    }

    /// The ring shows one environment and the dot the other. With nothing reported yet both are
    /// empty and the icon draws the dashed ring.
    private func currentRows() -> (ring: CardRow?, dot: CardRow?) {
        let withData = cards().filter(\.hasData)
        guard !withData.isEmpty else { return (nil, nil) }

        let chosen = Store.environments().ringEnvironment
        let ring = withData.first { $0.environment == chosen } ?? withData[0]
        let dot = withData.first { $0.environment != ring.environment }
        return (ring.iconRow, dot?.iconRow)
    }

    // ---- The numbers ----

    /// Every environment we know about, in the order the settings hold them. The card shows all of
    /// them, including the ones with nothing reported yet: an empty block says so in words.
    private func cards() -> [CardState] {
        let now = Date()
        // Read every time: the settings can rename an environment while Aiko runs, and the file
        // is small.
        let snapshots = EnvironmentSnapshots.combine(Store.environments(), watch?.byFile ?? [:])
        // TODO: direct mode has no poller on macOS yet, so the status line is the only source.
        return snapshots.map { CardState.from($0, now) }
    }

    private func currentCard() -> CardModel {
        CardModel.from(cards(), Date(), noAccess: noAccess, accounts: accounts)
    }

    private func noteWhereWeHaveNoAccess() {
        let patch = SettingsJsonPatch(.macOS)
        var missing: Set<String> = []

        for environment in Store.environments().environments where !environment.directMode {
            for folder in environment.configDirectories {
                let path = ClaudeSettingsEditor.pathIn(folder)
                let text = (try? String(contentsOfFile: path, encoding: .utf8)) ?? ""
                if !patch.hasOurLine(text) {
                    missing.insert(environment.name)
                }
            }
        }

        noAccess = missing
    }

    private func onSnapshotsChanged() {
        updateIcon()
        card?.update(currentCard())

        if selfTesting {
            Log.write("self test: new numbers, tooltip \"\(statusItem?.button?.toolTip ?? "")\"")
        }
    }

    // ---- The mouse ----

    override func mouseEntered(with event: NSEvent) {
        startHover()
    }

    override func mouseExited(with event: NSEvent) {
        hover?.invalidate()
        hover = nil
    }

    private func startHover() {
        // Already counting, or the card is up: nothing to start.
        guard card == nil, hover == nil else { return }

        hover = Timer.scheduledTimer(withTimeInterval: CardLifetime.hoverDelay, repeats: false) { _ in
            MainActor.assumeIsolated { self.onHoverFinished() }
        }
    }

    /// Nothing promises to say when the pointer left the icon, so it is checked here. Without this
    /// the card would appear long after the user walked away.
    private func onHoverFinished() {
        hover = nil
        let over = pointerOverIcon()
        Log.write("hover finished, pointer over icon: \(over)")
        if over {
            openCard(pinned: false)
        }
    }

    @objc private func onClick() {
        hover?.invalidate()
        hover = nil

        if NSApp.currentEvent?.type == .rightMouseUp {
            showMenu()
            return
        }

        // A left click opens the card. Swapping the ring instead left the click with no visible
        // answer beyond two colours trading places, which reads as a glitch.
        openCard(pinned: true)
    }

    // ---- The card ----

    private func openCard(pinned: Bool) {
        accounts = Store.accounts(Store.environments())

        if let open = card, !open.isClosing {
            // A second click on a card that is already pinned closes it, the way it opened.
            if pinned && open.isPinned {
                open.fadeAndClose()
                return
            }

            if pinned {
                open.pin()
            }
            open.update(currentCard())
            return
        }

        let window = CardWindow(model: currentCard())
        window.onSettings = { [weak self] in self?.openSettings() }
        window.onClosed = { [weak self] in
            guard let self, self.card === window else { return }
            self.card = nil
            self.cardWatch?.invalidate()
            self.cardWatch = nil
        }

        card = window
        if pinned {
            window.pin()
        }

        window.show(under: iconFrame())
        watchForTheMouseLeaving()

        Log.write("card opened at \(Int(window.frame.origin.x)),\(Int(window.frame.origin.y)) "
            + "size \(Int(window.frame.width))x\(Int(window.frame.height)), pinned: \(pinned)")
    }

    /// An unpinned card follows the mouse out. The pointer has to be away from both the icon and
    /// the card for two turns in a row, because there is a gap between them that it crosses on its
    /// way in. The watch runs only while an unpinned card is on screen, a few seconds at a time.
    private func watchForTheMouseLeaving() {
        guard let card, !card.isPinned else { return }

        away = AwayWatch()
        cardWatch?.invalidate()
        cardWatch = Timer.scheduledTimer(
            withTimeInterval: CardLifetime.watchInterval, repeats: true
        ) { _ in
            MainActor.assumeIsolated { self.onCardWatchTurn() }
        }
    }

    private func onCardWatchTurn() {
        guard let card, !card.isPinned else {
            stopCardWatch()
            return
        }

        let home = pointerOverIcon() || card.holds(NSEvent.mouseLocation)
        if away.turn(pointerIsHome: home) {
            stopCardWatch()
            card.fadeAndClose()
        }
    }

    private func stopCardWatch() {
        cardWatch?.invalidate()
        cardWatch = nil
    }

    /// Where the menu bar put our icon, in screen points. Empty before the menu bar has placed it.
    private func iconFrame() -> NSRect? {
        guard let frame = statusItem?.button?.window?.frame, frame.width > 0 else { return nil }
        return frame
    }

    private func pointerOverIcon() -> Bool {
        guard let frame = iconFrame() else { return false }
        return frame.contains(NSEvent.mouseLocation)
    }

    // ---- The menu ----

    private func showMenu() {
        let menu = NSMenu()
        menu.autoenablesItems = false

        // The click on the icon opens the card, so swapping the ring needs a home. Here it says
        // which environment it would show, which the click never did.
        if let other = Store.environments().dot?.name {
            add(menu, Strings.format(Strings.menuShowInRing, other), #selector(onSwapRing))
            menu.addItem(.separator())
        }

        // TODO: the settings window and the update check belong to the parts after this one.
        add(menu, Strings.settings, #selector(openSettings), enabled: false)
        add(menu, Strings.menuRefresh, #selector(onRefresh))
        add(menu, Strings.checkForUpdates, #selector(onCheckUpdates), enabled: false)
        menu.addItem(.separator())
        add(menu, Strings.quitAiko, #selector(onQuit))

        statusItem?.menu = menu
        statusItem?.button?.performClick(nil)
        statusItem?.menu = nil
    }

    private func add(_ menu: NSMenu, _ title: String, _ action: Selector, enabled: Bool = true) {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
        item.target = self
        item.isEnabled = enabled
        menu.addItem(item)
    }

    /// The chosen environment is kept in the environments file, not in a field here. It is a choice
    /// the user made, and a choice that quietly goes back on the next start is worse than none.
    @objc private func onSwapRing() {
        let environments = Store.environments()
        let swapped = environments.swapRing()
        guard swapped != environments else { return }

        Store.saveEnvironments(swapped)
        updateIcon()
    }

    @objc private func onRefresh() {
        noteWhereWeHaveNoAccess()
        updateIcon()
        card?.update(currentCard())
    }

    @objc private func openSettings() {
        // TODO: the settings window is the next part of the macOS app.
        Log.write("settings asked for, and there is no window yet")
    }

    @objc private func onCheckUpdates() {
        // TODO: the update check is not ported yet.
        Log.write("update check asked for, and there is none yet")
    }

    @objc private func onQuit() {
        NSApp.terminate(nil)
    }
}
