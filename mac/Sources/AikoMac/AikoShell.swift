import AikoKit
import AppKit

/// The one owner of everything Aiko shows on macOS: the icon in the menu bar or the island at the
/// edge of the screen, the card under whichever of them is there, and the settings window. All of
/// them show the same numbers and the user swaps between the two places at will, so one class
/// decides who is told what and when.
///
/// The parts do the work: MenuBarIcon and MenuBarMenu, IslandHost, CardHost, ShellNumbers for what
/// the numbers are, StartupChores for what is put right at every start. This class only connects
/// them. The twin of AikoShell.cs on Windows.
@MainActor
final class AikoShell {
    /// The face on the island is drawn at the size of a ring.
    private static let islandFaceSize = IslandLayout.ringSize

    private let numbers = ShellNumbers()
    private let icon = MenuBarIcon()
    private let menu = MenuBarMenu()
    private let island = IslandHost()
    private let cards: CardHost

    private var watch: SnapshotWatch?
    private var updates: UpdateWatch?

    /// One settings window at a time (D-177). It is made when it is asked for and dropped when it
    /// closes, the way every other window of Aiko is.
    private var settings: SettingsWindow?
    private var terminal: TerminalHost?

    /// The face for two seconds after a session event (D-211), and the way to it and back.
    private let faces = FacePlay()
    private var mood: MoodPlay?

    private var selfTesting = false

    /// Set for a self test at the start. It must leave the machine as it found it.
    private var changingNothing = false

    init() {
        cards = CardHost(numbers: numbers, icon: icon, island: island)
        cards.onSettings = { [weak self] in self?.openSettings(at: nil) }

        icon.faces = faces
        icon.rows = { [weak self] in self?.numbers.iconRows() ?? (nil, nil) }
        icon.tooltip = { [weak self] in
            TrayText.tooltip(self?.numbers.cards() ?? [], newerVersion: self?.updates?.newerVersion)
        }
        icon.onMouseEntered = { [weak self] in self?.cards.startHover() }
        icon.onMouseExited = { [weak self] in self?.cards.cancelHover() }
        icon.onClick = { [weak self] in self?.onIconClicked() }
        icon.onMenu = { [weak self] in self?.onMenuRequested() }

        island.onCard = { [weak self] pinned in self?.cards.open(pinned: pinned) }
    }

    /// A self test draws one thing to be looked at and must leave the machine as it found it.
    /// So with changingNothing the start skips what writes: the note that the memory was shown
    /// (and the page opened for it), the plugin sync (which writes settings.json and runs claude)
    /// and the refresh of the commands in PATH.
    func show(changingNothing: Bool = false) {
        let watcher = SnapshotWatch(folder: Store.folders.snapshotsFolder)
        watcher.updated = { [weak self] in self?.onSnapshotsChanged() }
        watch = watcher
        numbers.watch = watcher

        faces.changed = { [weak self] in self?.onFaceStep() }
        let player = MoodPlay()
        player.faceChanged = { [weak self] face in self?.onFace(face) }
        mood = player
        self.changingNothing = changingNothing
        followMoods()

        numbers.noteWhereWeHaveNoAccess()
        applyPlace()

        let environments = Store.environments()
        Log.write("found \(environments.environments.count) environments, "
            + "\(watcher.byFile.count) snapshot files, showing the "
            + (Store.settings().place == .island ? "island" : "menu bar icon"))

        openChecklistIfNothingIsSetUp(environments, Store.settings(), changingNothing: changingNothing)

        if changingNothing {
            return
        }

        StartupChores.refreshPluginsAndCommands(environments)

        // Not in a self test: the check sends a request and writes down the reported week.
        let checks = UpdateWatch()
        checks.found = { [weak self] in self?.onNewerVersion() }
        updates = checks
    }

    /// On a fresh machine the checklist opens by itself: there is nothing for the icon to show and
    /// nothing tells the person where to start. Someone who set Aiko up before an item existed is
    /// shown that one item once (D-200).
    private func openChecklistIfNothingIsSetUp(
        _ environments: EnvironmentSettings, _ app: AppSettings, changingNothing: Bool
    ) {
        if !environments.hasEnvironments {
            openSettings(at: .checklist)
        } else if WizardChecklist.opensMeetAikoOnStart(app, environments) {
            openSettings(at: .checklist, item: .meetAiko)
        } else if WizardChecklist.opensPrivacyOnStart(app, environments) {
            openSettings(at: .checklist, item: .privacy)
        } else if !changingNothing, WizardChecklist.opensMemoryOnStart(app, environments) {
            // Once, for someone who turned Aiko on before she had a memory. The page says what the
            // memory reads and writes, and it is where she can be turned off. A self test skips
            // it: the page it was asked for is the one to show, and the note is a write.
            openSettings(at: .personality)
            var shown = app
            shown.memoryShown = true
            Store.saveSettings(shown)
        }
    }

    func stop() {
        terminal?.close()
        cards.stop()
        watch?.stop()
        updates?.stop()
        mood?.stop()
        island.close()
        icon.remove()
    }

    // ---- Where Aiko sits: the menu bar icon or the island ----

    /// The icon or the island, whichever the settings say. Called again after a settings change,
    /// so a change takes effect at once. The default on macOS is the menu bar icon (D-250).
    private func applyPlace() {
        let settings = Store.settings()

        if settings.place == .island {
            icon.remove()
            island.show(numbers.cards(), at: settings.island, newerVersion: updates?.newerVersion)
        } else {
            island.close()
            icon.add()
        }
    }

    // ---- Doors for the self tests: they drive the shell through these and nothing else ----

    /// Proof that the app starts, reads the snapshots and lays the card out, for a machine nobody
    /// is looking at. The shim has the same door under AIKO_SHIM_SELF_TEST.
    func selfTest(_ what: String, value: String? = nil) {
        selfTesting = true
        island.changingNothing = true

        if what == "settings" || what == "wizard" {
            SettingsCheck.run(what, value: value, shell: self)
            return
        }

        if what == "terminal" {
            TerminalWindowCheck.run(value ?? "talk", shell: self)
            return
        }

        if what == "menu" {
            pickSettingsInTheMenu()
            return
        }

        if what != "1", what != "card" {
            IslandCheck.run(what, shell: self)
            return
        }

        cards.open(pinned: true)

        let rows = numbers.iconRows()
        Log.write("self test: icon \(describe(icon.frame))")
        Log.write("self test: tooltip \"\(icon.tooltipForCheck)\"")
        Log.write("self test: ring \(describe(rows.ring)), dot \(describe(rows.dot))")

        if let card = cards.window {
            Log.write("self test: card \(describe(card.frame))")
        }

        let model = numbers.card()
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

    /// The menu of the icon, built the way a right click builds it. It is not put on screen: a
    /// shown menu waits for a person. "Settings" is picked in it, and the window has to open.
    private func pickSettingsInTheMenu() {
        let items = menu.build(otherEnvironment: ShellNumbers.otherEnvironment()) { [weak self] command in
            self?.run(command)
        }

        let titles = items.items.map { $0.isSeparatorItem ? "-" : $0.title }
        Log.write("self test: menu \(titles.joined(separator: " | "))")

        if let at = items.items.firstIndex(where: { $0.title == Strings.settings }) {
            items.performActionForItem(at: at)
        }

        Log.write("self test: menu picked \"\(Strings.settings)\", settings window open: \(settings != nil)")
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) { NSApp.terminate(nil) }
    }

    /// The island the self test drives, made if the settings have Aiko in the menu bar.
    func islandForCheck() -> IslandWindow {
        if !island.isShown {
            icon.remove()
            island.show(numbers.cards(), at: Store.settings().island, newerVersion: updates?.newerVersion)
        }

        return island.window!
    }

    /// One face, without waiting for a session to do anything. Only the self test calls it.
    func showFaceForCheck(_ face: AikoFace?) {
        onFace(face)
    }

    /// The card as the island would open it, and where it ended up. Only the self test calls these.
    func openCardForCheck(pinned: Bool) {
        cards.open(pinned: pinned)
    }

    func closeCardForCheck() {
        cards.closeForCheck()
    }

    var cardFrameForCheck: NSRect? { cards.window?.frame }

    /// The picture the menu bar holds right now, so a self test can see it change.
    var iconPictureForCheck: Data? { icon.pictureForCheck }

    func placeIslandForCheck(_ position: IslandPosition) {
        island.place(numbers.cards(), at: position)
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

    // ---- The numbers arrive ----

    private func onSnapshotsChanged() {
        mood?.onLimits(numbers.snapshots())

        icon.update()
        island.update(numbers.cards())
        cards.refresh()
        terminal?.limitsChanged()

        if selfTesting {
            Log.write("self test: new numbers, tooltip \"\(icon.tooltipForCheck)\"")
        }
    }

    /// The daily check found a newer version, or stopped seeing one. Only the words change.
    private func onNewerVersion() {
        icon.update()
        island.update(numbers.cards(), newerVersion: updates?.newerVersion)
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
        let ground: FaceGround = island.isShown || isDarkMenuBar ? .dark : .light
        let size = island.isShown ? Self.islandFaceSize : Double(MenuBarIcon.size)
        faces.show(FaceArt.draw(style, face, ground, size <= FaceArt.smallUpTo))
    }

    private var isDarkMenuBar: Bool {
        NSApp.effectiveAppearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua
    }

    /// One step of the way to a face and back. Only the picture changes, nothing is read again.
    private func onFaceStep() {
        if island.isShown {
            island.draw(faces)
        } else {
            icon.redraw()
        }
    }

    // ---- The icon is clicked ----

    /// A left click opens the card. Swapping the ring instead left the click with no visible
    /// answer beyond two colours trading places, which reads as a glitch.
    private func onIconClicked() {
        cards.cancelHover()
        cards.open(pinned: true)
    }

    private func onMenuRequested() {
        cards.cancelHover()

        let items = menu.build(otherEnvironment: ShellNumbers.otherEnvironment()) { [weak self] command in
            self?.run(command)
        }
        icon.show(items)
    }

    private func run(_ command: TrayCommand) {
        switch command {
        case .swapRing:
            // The swap is saved, and a self test saves nothing.
            if !changingNothing, ShellNumbers.swapRing() {
                icon.update()
            }
        case .terminal:
            openTerminal()
        case .settings:
            openSettings(at: nil)
        case .refresh:
            onRefresh()
        case .updates:
            openSettings(at: .general).startUpdateCheck()
        case .exit:
            NSApp.terminate(nil)
        }
    }

    /// Called after every change in the settings window and from the menu: everything that depends
    /// on the settings is worked out again.
    private func onRefresh() {
        numbers.noteWhereWeHaveNoAccess()
        followMoods()
        applyPlace()
        icon.update()
        island.update(numbers.cards())
        cards.refresh()
    }

    private func followMoods() {
        // A self test shows the faces it asks for. A session at work on this machine would bring
        // its own in the middle of a step.
        if !changingNothing {
            mood?.follow(Store.environments())
        }
    }

    // ---- The terminal ----

    /// One terminal window, as with the settings. Closing it ends its sessions' processes; the
    /// sessions come back from terminal.json the next time.
    @discardableResult
    func openTerminal() -> TerminalHost {
        if let open = terminal {
            open.activate()
            return open
        }

        let host = TerminalHost(
            settings: Store.environments(),
            sessions: Store.terminal(),
            newProcess: { ClaudeProcess() },
            limitsOf: { [weak self] in self?.numbers.snapshot(for: $0) })
        host.onClosed = { [weak self] in self?.terminal = nil }
        terminal = host
        host.show()
        Log.write("terminal opened")
        return host
    }

    // ---- The settings window ----

    /// The one settings window. A second ask brings the one that is open to the front, so two
    /// windows can never disagree about what is saved.
    @discardableResult
    func openSettings(at page: SettingsPage?, item: ChecklistItem? = nil) -> SettingsWindow {
        if let open = settings {
            if let page { open.state.show(page) }
            if let item { open.openChecklist(at: item) }
            open.show()
            return open
        }

        let window = SettingsWindow(page: page)
        window.onQuit = { NSApp.terminate(nil) }
        window.onChanged = { [weak self] in self?.onRefresh() }
        window.onClosed = { [weak self] in self?.settings = nil }
        window.onReopen = { [weak self] page in self?.openSettings(at: page) }

        settings = window
        if let item { window.openChecklist(at: item) }
        window.show()
        return window
    }
}
