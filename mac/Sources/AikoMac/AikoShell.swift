import AikoKit
import AppKit

/// Everything Aiko shows on macOS: the icon in the menu bar or the island at the edge of the
/// screen, and the card under whichever of them is there. One owner, because both show the same
/// numbers and the user swaps between them at will.
///
/// The twin of AikoShell.cs on Windows. The settings window and the wizard are not here yet; the
/// menu items that would open them are marked below.
@MainActor
final class AikoShell: NSResponder {
    private static let iconSize: CGFloat = 18

    /// The face on the island is drawn at the size of a ring.
    private static let islandFaceSize = IslandLayout.ringSize

    private var statusItem: NSStatusItem?
    private var island: IslandWindow?
    private var fullScreen: FullScreenWatch?
    private var watch: SnapshotWatch?
    private var hover: Timer?
    private var cardWatch: Timer?
    private var away = AwayWatch()
    private var card: CardWindow?

    /// The face for two seconds after a session event (D-211), and the way to it and back.
    private let faces = FacePlay()
    private var mood: MoodPlay?

    /// The rows the icon last drew. Frames of a face transition reuse them instead of reading the
    /// settings and the snapshots sixty times a second.
    private var iconRows: (ring: CardRow?, dot: CardRow?) = (nil, nil)

    /// Plan and sign-in per environment. Read when the card opens, not on every new number:
    /// .claude.json can be large, and neither the plan nor the sign-in changes between two answers.
    private var accounts: [String: CardAccount] = [:]

    /// Environments where Aiko's line is not in the Claude Code settings, so no numbers can ever
    /// arrive. Worked out when the settings change rather than every time the card is drawn.
    private var noAccess: Set<String> = []

    func show() {
        let watcher = SnapshotWatch(folder: Store.folders.snapshotsFolder)
        watcher.updated = { [weak self] in self?.onSnapshotsChanged() }
        watch = watcher

        faces.changed = { [weak self] in self?.onFaceStep() }
        let player = MoodPlay()
        player.faceChanged = { [weak self] face in self?.onFace(face) }
        player.follow(Store.environments())
        mood = player

        noteWhereWeHaveNoAccess()
        applyPlace()

        let environments = Store.environments()
        Log.write("found \(environments.environments.count) environments, "
            + "\(watcher.byFile.count) snapshot files, showing the "
            + (Store.settings().place == .island ? "island" : "menu bar icon"))
    }

    func stop() {
        hover?.invalidate()
        cardWatch?.invalidate()
        card?.fadeAndClose()
        watch?.stop()
        mood?.stop()
        closeIsland()
        removeIcon()
    }

    // ---- Where Aiko sits: the menu bar icon or the island ----

    /// The icon or the island, whichever the settings say. Called again after a settings change,
    /// so a change takes effect at once. The default on macOS is the menu bar icon (D-250).
    private func applyPlace() {
        let settings = Store.settings()

        if settings.place == .island {
            removeIcon()
            showIsland(settings.island)
        } else {
            closeIsland()
            addIcon()
        }
    }

    private func showIsland(_ position: IslandPosition) {
        if island == nil {
            let window = IslandWindow()
            window.onCard = { [weak self] pinned in self?.openCard(pinned: pinned) }
            window.onMoved = { [weak self] moved in self?.saveIslandPosition(moved) }
            island = window
        }

        island?.show(cards(), at: position)

        // Only watched while the island is on screen: the menu bar icon has nothing to hide from.
        if fullScreen == nil {
            let watch = FullScreenWatch()
            watch.changed = { [weak self] full in self?.onFullScreen(full) }
            fullScreen = watch
        }

        onFullScreen(FullScreenWatch.isFullScreenInFront())
    }

    private func onFullScreen(_ full: Bool) {
        guard let island else { return }

        let hide = full && Store.settings().hideIslandInFullScreen
        guard island.isVisible == hide else { return }

        // Only a real change is worth a line: the space changes many times a minute.
        island.hide(hide)
        Log.write("island \(hide ? "hidden behind a full screen window" : "shown again")")
    }

    private func closeIsland() {
        fullScreen?.stop()
        fullScreen = nil
        island?.close()
        island = nil
    }

    private func saveIslandPosition(_ position: IslandPosition) {
        // A self test drags the island about; the person's settings are not its to change.
        guard !selfTesting else {
            Log.write("island would be saved at \(position.edge) \(String(format: "%.2f", position.along))")
            return
        }

        var settings = Store.settings()
        settings.island = position
        Store.saveSettings(settings)
        Log.write("island moved to \(position.edge) at \(String(format: "%.2f", position.along))")
    }

    /// Proof that the app starts, reads the snapshots and lays the card out, for a machine nobody
    /// is looking at. The shim has the same door under AIKO_SHIM_SELF_TEST.
    func selfTest(_ what: String) {
        selfTesting = true

        if what != "1", what != "card" {
            IslandCheck.run(what, shell: self)
            return
        }

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

    /// The island the self test drives, made if the settings have Aiko in the menu bar.
    func islandForCheck() -> IslandWindow {
        if island == nil {
            removeIcon()
            showIsland(Store.settings().island)
        }

        return island!
    }

    /// One face, without waiting for a session to do anything. Only the self test calls it.
    func showFaceForCheck(_ face: AikoFace?) {
        onFace(face)
    }

    /// The card as the island would open it, and where it ended up. Only the self test calls these.
    func openCardForCheck(pinned: Bool) {
        openCard(pinned: pinned)
    }

    func closeCardForCheck() {
        card?.fadeAndClose()
        card = nil
    }

    var cardFrameForCheck: NSRect? { card?.frame }

    func placeIslandForCheck(_ position: IslandPosition) {
        island?.show(cards(), at: position)
    }

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

    private func addIcon() {
        guard statusItem == nil else { return }

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

        updateIcon()
    }

    private func removeIcon() {
        guard let statusItem else { return }
        NSStatusBar.system.removeStatusItem(statusItem)
        self.statusItem = nil
    }

    private func updateIcon() {
        guard let button = statusItem?.button else { return }

        iconRows = currentRows()
        redrawIcon()

        // The numbers go in the tooltip as well as in the ring: a screen reader has nothing else
        // to read, and the ring says nothing to one.
        // TODO: the newer version comes from the update check, which is not built yet.
        button.toolTip = TrayText.tooltip(cards(), newerVersion: nil)
    }

    /// One frame of a face transition: only the picture changes, not the tooltip.
    private func redrawIcon() {
        guard let button = statusItem?.button else { return }

        button.image = StatusIcon.image(
            size: Self.iconSize,
            ring: iconRows.ring,
            dot: iconRows.dot,
            frame: faces.frame,
            face: faces.picture)
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

    // ---- The face (D-211) ----

    /// A face comes or goes, on the menu bar icon or on the island, wherever Aiko lives.
    private func onFace(_ face: AikoFace?) {
        guard let face else {
            faces.hide()
            return
        }

        let style = Store.persona().face

        // The island is always dark, whatever the menu bar is. The icon follows the menu bar, which
        // follows the person's appearance.
        let ground: FaceGround = island != nil || isDarkMenuBar ? .dark : .light
        let size = island != nil ? Self.islandFaceSize : Double(Self.iconSize)
        faces.show(FaceArt.draw(style, face, ground, size <= FaceArt.smallUpTo))
    }

    private var isDarkMenuBar: Bool {
        NSApp.effectiveAppearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua
    }

    /// One step of the way to a face and back. Only the picture changes, nothing is read again.
    private func onFaceStep() {
        if let island {
            island.face(faces.frame, fit: faces.fit, picture: faces.picture)
        } else {
            redrawIcon()
        }
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
        mood?.onLimits(EnvironmentSnapshots.combine(Store.environments(), watch?.byFile ?? [:]))

        let cards = cards()
        updateIcon()
        island?.update(cards)
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

        if let island, statusItem == nil {
            // From the island the card opens right under it, or over it at the bottom edge.
            window.show(from: island.frame, above: island.edge == .bottom)
        } else {
            window.show(from: iconFrame())
        }

        watchForTheMouseLeaving()

        Log.write("card opened at \(Int(window.frame.origin.x)),\(Int(window.frame.origin.y)) "
            + "size \(Int(window.frame.width))x\(Int(window.frame.height)), pinned: \(pinned)")
    }

    /// An unpinned card follows the mouse out. The pointer has to be away from the icon, the island
    /// and the card for two turns in a row, because there is a gap between them that it crosses on
    /// its way in. The watch runs only while an unpinned card is on screen, a few seconds at a time.
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

        // The island is home for the card the same way the icon is (D-148). Without it a card
        // opened from the island closed half a second later, before the mouse could reach it.
        let pointer = NSEvent.mouseLocation
        let home = pointerOverIcon() || card.holds(pointer) || island?.holds(pointer) == true
        if away.turn(pointerIsHome: home) {
            stopCardWatch()
            card.fadeAndClose()
        }
    }

    private func stopCardWatch() {
        cardWatch?.invalidate()
        cardWatch = nil
    }

    /// Where the menu bar put our icon, in screen points. Empty before the menu bar has placed it,
    /// and while Aiko lives on the island instead.
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
        mood?.follow(Store.environments())
        applyPlace()
        updateIcon()
        island?.update(cards())
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
