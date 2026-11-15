import AikoKit
import AppKit

/// The one owner of everything Aiko shows on macOS: the icon in the menu bar or the island at the
/// edge of the screen, and the terminal window with the settings in it. All of them show the same
/// numbers and the user swaps between the two places at will, so one class decides who is told
/// what and when.
///
/// The parts do the work: MenuBarIcon and MenuBarMenu, IslandHost, TerminalHost, ShellNumbers for what
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

    private var watch: SnapshotWatch?
    private var updates: UpdateWatch?

    /// The terminal window with the sessions (D-425), made on the first click and kept while Aiko runs.
    private lazy var terminal: TerminalHost = {
        let host = TerminalHost(
            limitsOf: { [weak self] name in self?.numbers.snapshot(for: name) },
            cardOf: { [weak self] in self?.numbers.card() ?? CardModel(updated: "", blocks: []) })
        host.onQuitAsked = { NSApp.terminate(nil) }
        host.onSettingsChanged = { [weak self] in self?.onRefresh() }
        host.onShownChanged = { [weak self] shown in
            self?.windowOpen = shown
            self?.applyPlace()
        }
        return host
    }()

    /// While the window is open it has the Dock icon, and the menu bar icon steps aside (D-410).
    private var windowOpen = false

    /// The face for two seconds after a session event (D-211), and the way to it and back.
    private let faces = FacePlay()
    private var mood: MoodPlay?

    private var selfTesting = false

    /// Set for a self test at the start. It must leave the machine as it found it.
    private var changingNothing = false

    init() {

        icon.faces = faces
        icon.rows = { [weak self] in self?.numbers.iconRows() ?? (nil, nil) }
        icon.tooltip = { [weak self] in
            TrayText.tooltip(self?.numbers.cards() ?? [], newerVersion: self?.updates?.newerVersion)
        }
        icon.onClick = { [weak self] in self?.onIconClicked() }
        icon.onMenu = { [weak self] in self?.onMenuRequested() }

        island.onClick = { [weak self] in self?.openWindow() }
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
        } else if !changingNothing, !showTerminalOnce(), app.openTerminalAtStartup, LoginItem.isEnabled() {
            // macOS cannot tell a start at login from a start by hand, so while Aiko starts at
            // login the switch opens the window at every start; Windows only at the start with it.
            terminal.show()
        }
    }

    /// The terminal window opens by itself once, when Aiko is set up (D-350). Says whether it
    /// opened now.
    private func showTerminalOnce() -> Bool {
        var app = Store.settings()
        guard !app.terminalShown, Store.environments().hasEnvironments else { return false }

        app.terminalShown = true
        Store.saveSettings(app)
        terminal.show()
        return true
    }

    func stop() {
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
            if windowOpen {
                icon.remove()
            } else {
                icon.add()
            }
        }
    }

    // ---- Doors for the self tests: they drive the shell through these and nothing else ----

    /// The settings panel at the zoom of the window: its frame, the bounds SwiftUI lays out in, and
    /// the height its content asks for.
    var settingsPanelForCheck: String { terminal.settingsPanelForCheck }

    /// Proof that the app starts, reads the snapshots and draws the icon, for a machine nobody is
    /// looking at. The shim has the same door under AIKO_SHIM_SELF_TEST.
    func selfTest(_ what: String, value: String? = nil) {
        selfTesting = true
        island.changingNothing = true

        if what == "settings" || what == "wizard" {
            SettingsCheck.run(what, value: value, shell: self)
            return
        }


        if what == "terminal" {
            TerminalCheck.run(terminal) { [weak self] in self?.icon.isShown ?? false }
            return
        }

        if what == "editor" {
            EditorCheck.run(terminal)
            return
        }

        if what == "voice" {
            VoiceCheck.run(terminal, wav: value)
            return
        }

        if what == "menu" {
            pickSettingsInTheMenu()
            return
        }

        if what != "1" {
            IslandCheck.run(what, shell: self)
            return
        }

        let rows = numbers.iconRows()
        Log.write("self test: icon \(describe(icon.frame))")
        Log.write("self test: tooltip \"\(icon.tooltipForCheck)\"")
        Log.write("self test: ring \(describe(rows.ring)), dot \(describe(rows.dot))")

        // Long enough for a new snapshot to arrive, then away: nothing here stays on screen.
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

        Log.write("self test: menu picked \"\(Strings.settings)\", settings open: \(terminal.settingsOpenForCheck)")
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
        if windowOpen {
            terminal.limitsChanged()
        }

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

    /// A click on the icon or the island opens the terminal window, as on Windows (D-399, D-425);
    /// before the first environment, the first run.
    private func onIconClicked() {
        openWindow()
    }

    private func openWindow() {
        if Store.environments().environments.isEmpty {
            _ = openSettings(at: nil)
        } else {
            terminal.show()
        }
    }

    private func onMenuRequested() {

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

        case .open:
            openWindow()
        case .settings:
            openSettings(at: nil)
        case .exit:
            NSApp.terminate(nil)
        }
    }

    /// Called after every change in the settings and from the menu: everything that depends
    /// on the settings is worked out again.
    private func onRefresh() {
        numbers.noteWhereWeHaveNoAccess()
        followMoods()
        applyPlace()
        icon.update()
        island.update(numbers.cards())
    }

    private func followMoods() {
        // A self test shows the faces it asks for. A session at work on this machine would bring
        // its own in the middle of a step.
        if !changingNothing {
            mood?.follow(Store.environments())
        }
    }

    // ---- The settings ----

    /// The settings live in the terminal window (D-374): a second ask moves the open ones to the
    /// page asked for, so two copies can never disagree about what is saved.
    @discardableResult
    func openSettings(at page: SettingsPage?, item: ChecklistItem? = nil) -> SettingsState {
        terminal.openSettings(at: page, item: item)
    }
}
