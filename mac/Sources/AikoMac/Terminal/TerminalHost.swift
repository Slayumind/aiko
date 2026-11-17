import AikoKit
import AppKit
import SwiftTerm
import SwiftUI

/// The terminal behind its window (D-349, D-425): the sessions of terminal.json, a real terminal for
/// each session that was opened, and what is chosen. The twin of TerminalHost.cs.
///
/// The terminals live while Aiko runs: closing the window only hides it, so the agents work on and
/// their screens stay as they were. While the window is open Aiko has an icon in the Dock, as the
/// terminal window has a button on the taskbar of Windows; closed, Aiko goes back to the menu bar.
@MainActor
final class TerminalHost: NSObject, NSWindowDelegate, LocalProcessTerminalViewDelegate {
    let state = TerminalState()
    var window: NSWindow?
    let stage = NSView()
    var sessions = Store.terminal()
    var model: TerminalWindowModel
    var terminals: [String: LocalProcessTerminalView] = [:]

    /// When a program ended, for its row and for the count of stopped sessions.
    var exitedAt: [String: Int64] = [:]

    // What each session does (TerminalHost+Pulse.swift): the title of Claude Code, its last hook
    // event, when the person last looked at it and last typed into it.
    let events = TerminalEvents()
    var titles: [String: ClaudeTitle] = [:]
    var lastEvents: [String: TerminalEvent] = [:]
    var seenAt: [String: Int64] = [:]
    var answeredAt: [String: Int64] = [:]
    var activities: [String: TerminalActivity] = [:]
    var sessionCounts = SessionCounts.none

    /// The Dock badge asked macOS for leave once in this run; macOS keeps the answer.
    var askedToBadge = false

    /// The counts changed: the menu bar icon and the island show them.
    var onSessionsChanged: ((SessionCounts) -> Void)?

    /// A session started to wait and the sound is due (D-375): the shell plays it.
    var onWaitingStarted: (() -> Void)?

    /// Why a session has no terminal, until it is started again (D-421).
    var problems: [String: StartProblem] = [:]

    /// The running session the person asked to close: the question stands until it is answered.
    var closing: String?

    /// A step that stops sessions waits for the person's answer (D-351): why, and what to do
    /// with the answer.
    var stopping: (reason: StopReason, answer: (Bool) -> Void)?

    /// The page of the settings that was open when the question came. A no opens it again.
    var settingsBeforeAsking: SettingsPage?

    /// Aiko is on its way out: the programs end because Aiko ends them, and the sessions stay as
    /// they are for the next start.
    var quitting = false

    /// Set by a self test: nothing is written to terminal.json.
    var changingNothing = false

    /// The limits of an environment, as the menu bar icon has them: the ring and percentage of its pill.
    let limitsOf: (String) -> LimitSnapshot?

    /// The width of the session list, as TerminalPanel.ListWidth.
    static let listWidth: CGFloat = 252
    var listWidthConstraint: NSLayoutConstraint?
    var holderWatch: NSObjectProtocol?
    var keyWatch: Any?

    /// A zoom a self test puts on the window without writing the setting.
    var zoomForCheck: CGFloat?

    /// The loader over a terminal that has drawn nothing yet (D-368), and the terminal it is over.
    var loader: NSView?
    weak var loaderFor: NSView?

    /// The views that follow the zoom of the window, and the two sizes of AppKit that grow with it.
    var zoomFollowers: [(CGFloat) -> Bool] = []

    /// For --try-terminal: whether each zoomed view that is still there sees its room.
    var zoomChecks: [() -> Bool?] = []
    var zoomedSizes: (bar: NSLayoutConstraint, list: NSLayoutConstraint)?
    var bar: NSView?

    /// The settings while they are open, and their panel over the list and the terminal (D-374).
    var settings: SettingsState?
    var settingsCover: NSView?

    /// The palette over the window while it is open, and what it finds outside Aiko (D-353).
    var paletteCover: NSView?
    let shelf = ConversationShelf()
    var conversations: [Conversation] = []

    /// The messages in place of the terminal. Hidden while the terminal shows, so it takes no click.
    var stageCover: NSView?

    /// The words of every environment, as the menu bar icon has them: the active card of the header.
    let cardOf: () -> CardModel

    /// The strip of the open header and its height, which grows and folds (D-373).
    var headerHeight: NSLayoutConstraint?

    /// The editor under the terminal and the line of keys under it (D-354, AIKO-505).
    let editor = EditorView()
    let keysLine = NSTextField(labelWithString: "")
    var editorCollapsed: NSLayoutConstraint?

    /// The bar in place of the editor when Claude Code ended by itself (D-351).
    let stoppedBar = StoppedBar()
    var stoppedCollapsed: NSLayoutConstraint?

    /// Whether the open session has room for the editor: a terminal whose program runs.
    var editorRoom = false

    /// The blocks of each command line with the marks of its shell (D-412).
    var shells: [String: ShellPane] = [:]

    /// Voice input of the editor (D-313, D-354), made with the window.
    var voice: VoiceDictation?

    init(limitsOf: @escaping (String) -> LimitSnapshot?, cardOf: @escaping () -> CardModel) {
        self.limitsOf = limitsOf
        self.cardOf = cardOf
        let environments = Store.environments().environments.map(\.name)
        model = TerminalWindowModel.opened(Store.terminal(), environments)
        super.init()
        state.onEnvironment = { [weak self] name in self?.toEnvironment(name) }
        state.onChoose = { [weak self] id in self?.choose(id) }
        state.onNew = { [weak self] folder, kind in self?.start(in: folder, kind: kind) }
        state.onNewInOtherFolder = { [weak self] in self?.pickFolderQuickly() }
        state.onSearch = { [weak self] in self?.openSessionPalette() }
        state.onToggleList = { [weak self] in self?.toggleList() }
        state.onToggleHeader = { [weak self] in self?.toggleHeader() }
        state.onSettings = { [weak self] in self?.toggleSettings() }
        wireSessionActions()
        wirePulse()
    }

    var isShown: Bool { window?.isVisible == true }

    /// The window was shown or closed.
    var onShownChanged: ((Bool) -> Void)?

    /// "Quit Aiko" on the general page: the shell quits.
    var onQuitAsked: (() -> Void)?

    /// A setting was saved: the shell follows it while the settings are still open.
    var onSettingsChanged: (() -> Void)?

    /// New numbers arrived: the pills show them.
    func limitsChanged() {
        state.tabs = tabs()
        state.header = header()
    }

    /// Opens the window, or brings it forward.
    func show() {
        let window = self.window ?? makeWindow()
        self.window = window
        NSApp.setActivationPolicy(.regular)
        draw()
        window.makeKeyAndOrderFront(nil)
        placeWindowButtons(window)
        NSApp.activate(ignoringOtherApps: true)
        onShownChanged?(true)
    }

    /// The window goes; the programs in it go on.
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        rememberPlace()
        voice?.close()
        sender.orderOut(nil)
        NSApp.setActivationPolicy(.accessory)
        onShownChanged?(false)
        return false
    }

    // ---- what the person does ----

    func toEnvironment(_ name: String) {
        model = model.toEnvironment(name, sessions)
        draw()
    }

    func choose(_ id: String) {
        model = model.toSession(id, sessions)
        draw()
    }

    func start(in folder: String, kind: SessionKind) {
        let id = UUID().uuidString.lowercased()
        let name = SessionName.defaultName(folder, taken: sessions.namesIn(model.environment, folder))
        sessions = sessions.add(
            id: id, environment: model.environment, folder: folder, name: name, now: Self.now(), kind: kind)
        save()
        model = model.toSession(id, sessions)
        draw()
    }

    /// A folder from the dialog of the system, starting at the folder of the open session.
    func chooseFolder() -> String? {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.allowsMultipleSelection = false
        panel.directoryURL = URL(fileURLWithPath: model.session(sessions)?.folder ?? Store.home)
        guard panel.runModal() == .OK, let url = panel.url else { return nil }
        return url.path
    }

    /// The list from the core, and the terminal of the chosen session on the stage.
    func draw() {
        let environments = Store.environments().environments.map(\.name)
        state.environments = environments
        state.environment = model.environment
        state.chosen = model.sessionId

        // A question or a message takes the place of the terminals rather than covering them. The
        // terminal is started first, so a session that cannot start says why at once.
        var shown: LocalProcessTerminalView?
        if stageMessage() == .none, let session = model.session(sessions) {
            shown = terminal(for: session)
        }
        state.stage = stageMessage()

        // The rows after the terminal is started: a session that starts now is running in its row.
        state.tabs = tabs()
        state.header = header()
        state.groups = SessionRows.of(sessions, model.environment) { [weak self] id in self?.live(id) }
        stageCover?.isHidden = state.stage == .none
        for view in terminals.values where view !== shown {
            view.isHidden = true
        }
        showEditor(for: model.session(sessions), terminal: shown)
        markSeen()
        notice()

        guard let terminal = shown else {
            showShell(for: nil, terminal: nil)
            showLoader(over: nil)
            return
        }
        if terminal.superview !== stage {
            terminal.translatesAutoresizingMaskIntoConstraints = false
            stage.addSubview(terminal, positioned: .below, relativeTo: editor)
            NSLayoutConstraint.activate([
                terminal.leadingAnchor.constraint(equalTo: stage.leadingAnchor, constant: 8),
                terminal.trailingAnchor.constraint(equalTo: stage.trailingAnchor, constant: -8),
                terminal.topAnchor.constraint(equalTo: stage.topAnchor, constant: 8),
                terminal.bottomAnchor.constraint(equalTo: stoppedBar.topAnchor, constant: -8),
            ])
        }
        terminal.isHidden = false
        // After the terminal is in the stage: the loader and the blocks are tied to its place. The
        // blocks come last, so a command line keeps its terminal out of sight under them.
        showLoader(over: terminal as? AikoTerminalView)
        showShell(for: model.sessionId, terminal: terminal)
        focusOpenTerminal()
    }

    /// What stands in place of the terminal: the question before closing first, then the empty
    /// environment, then why the chosen session has no terminal (D-421).
    func stageMessage() -> StageMessage {
        if let stopping {
            let question = StopQuestion.of(stopping.reason)
            return .ask(title: question.title, body: question.body, stop: question.stop, keep: question.keep)
        }
        if let id = closing, let session = sessions.find(id) {
            return .ask(title: Strings.format(Strings.terminalCloseTitle, session.name), body: Strings.terminalCloseBody,
                        stop: Strings.terminalStopAndClose, keep: Strings.terminalKeepOpen)
        }
        guard model.view(sessions) == .terminal, let session = model.session(sessions) else {
            return .noSessions(environment: model.environment)
        }
        if let problem = problems[session.id] {
            return .problem(problem, shell: session.kind == .shell, folder: session.folder)
        }
        return .none
    }

    /// The pills: each environment with the limit closer to its end (D-355) and its waiting sessions.
    func tabs() -> [TabItem] {
        let now = Date()
        return Store.environments().environments.map { environment in
            let badge = limitsOf(environment.name).flatMap { LimitBadge.forSnapshot($0, now) }
            return TabItem(
                name: environment.name,
                selected: environment.name == model.environment,
                waiting: SessionRows.waiting(sessions, environment.name) { [weak self] id in self?.live(id) },
                percent: badge?.percent,
                tone: badge?.tone ?? .unknown)
        }
    }

    /// The open header (D-373, D-376): the active environment first. The words are read only while
    /// the header is open.
    func header() -> [HeaderColumnItem] {
        guard state.headerOpen else { return [] }

        let environments = Store.environments().environments
        let blocks = cardOf().blocks
        let columns = HeaderStrip.columns(
            environments.map { environment in
                HeaderInput(
                    environment: environment.name,
                    limits: limitsOf(environment.name),
                    waiting: SessionRows.waiting(sessions, environment.name) { [weak self] id in self?.live(id) })
            },
            model.environment,
            Date())

        return columns.map { column in
            HeaderColumnItem(
                column: column,
                block: column.active ? blocks.first { $0.name == column.environment } : nil,
                direct: environments.first { $0.name == column.environment }?.directMode ?? false)
        }
    }

    // ---- the terminals ----

    /// The terminal of a session, started the first time it is shown. Nil when the program cannot
    /// start: no Claude Code, or the folder is gone.
    func terminal(for session: TerminalSession) -> LocalProcessTerminalView? {
        if let running = terminals[session.id] {
            return running
        }

        guard FileManager.default.fileExists(atPath: session.folder) else {
            problems[session.id] = .folderGone
            return nil
        }
        guard let environment = Store.environments().environments.first(where: { $0.name == session.environment })
        else { return nil }

        let config = environment.configDirectories.first ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home)
        let executable: String
        let arguments: [String]
        let plan: ClaudeLaunchPlan
        var shell: ShellLaunch?
        switch session.kind {
        case .shell:
            let launch = ShellLaunch.find()
            shell = launch
            executable = launch.executable
            arguments = launch.arguments
            plan = .forShell(.macOS, configDirectory: config, userProfile: Store.home,
                             inheritedVariables: ProcessEnvironment.inheritedNames)
        case .claude:
            guard let claude = ClaudeLauncher.findClaude() else {
                problems[session.id] = .noClaude
                return nil
            }
            executable = claude
            plan = .forRequest(
                .macOS,
                LaunchRequest(
                    configDirectory: config, userProfile: Store.home, environmentName: session.environment,
                    sessionId: session.id, started: session.started, hooksFile: TerminalEvents.hooksFile(),
                    picturesFolder: Store.folders.picturesFolder(session.id),
                    onDisk: Self.conversationOnDisk(config, session.id)),
                inheritedVariables: ProcessEnvironment.inheritedNames)
            arguments = plan.arguments
        }

        let view = AikoTerminalView(frame: stage.bounds)
        view.processDelegate = self
        wire(view, session.id)
        if let shell {
            wireShell(view, session, shell)
        }
        view.nativeBackgroundColor = Theme.nsSurface
        view.nativeForegroundColor = Theme.nsInk
        view.font = Self.terminalFont()
        view.startProcess(
            executable: executable,
            args: arguments,
            environment: ProcessEnvironment.with(plan.variables + (shell?.variables ?? [])),
            execName: nil,
            currentDirectory: session.folder)
        terminals[session.id] = view
        sessions = sessions.markStarted(session.id).used(session.id, now: Self.now())
        save()
        TerminalLog.started(
            environment: session.environment, shell: session.kind == .shell,
            resumed: plan.arguments.contains("--resume"))
        return view
    }

    /// The size of the terminal is the size of the screen the blocks are cut from.
    nonisolated func sizeChanged(source: LocalProcessTerminalView, newCols: Int, newRows: Int) {
        Task { @MainActor [weak self] in
            guard let self, let id = self.terminals.first(where: { $0.value === source })?.key else { return }
            self.shells[id]?.screen.resize(columns: newCols, rows: newRows)
            self.shells[id]?.blocks.refresh()
        }
    }

    nonisolated func setTerminalTitle(source: LocalProcessTerminalView, title: String) {
        Task { @MainActor [weak self] in
            guard let self, let id = self.terminals.first(where: { $0.value === source })?.key else { return }
            self.titleCame(id, title)
        }
    }

    nonisolated func hostCurrentDirectoryUpdate(source: TerminalView, directory: String?) {}

    /// Claude Code ended by itself: its row says so, and Enter brings it back (D-351). A shell that
    /// ended leaves the list.
    nonisolated func processTerminated(source: TerminalView, exitCode: Int32?) {
        Task { @MainActor [weak self] in
            // On the way out every program ends because Aiko ends it: the list stays as it is.
            guard let self, !self.quitting, let id = self.terminals.first(where: { $0.value === source })?.key,
                  let session = self.sessions.find(id) else { return }
            TerminalLog.exited(
                environment: session.environment, shell: session.kind == .shell, exitCode: exitCode.map(Int.init))
            if session.kind == .shell {
                self.closeSession(id)
                return
            }
            self.exitedAt[id] = Self.nowMs
            self.draw()
        }
    }

    /// A stopped Claude Code starts again in the same place, with --resume.
    func resume() {
        guard let session = model.session(sessions), exitedAt.removeValue(forKey: session.id) != nil else { return }
        if let old = terminals.removeValue(forKey: session.id) {
            old.removeFromSuperview()
        }
        draw()
    }

    // ---- doors for --try-terminal ----

    /// A session for a self test, never written to terminal.json; its id.
    @discardableResult
    func startForCheck(in folder: String, kind: SessionKind = .shell) -> String? {
        changingNothing = true
        start(in: folder, kind: kind)
        return model.sessionId
    }

    /// The text of the open terminal, row by row.
    var screenForCheck: String {
        guard let terminal = openTerminal() else { return "" }
        // An empty cell is a zero, not a space.
        return String(decoding: terminal.getTerminal().getBufferAsData(), as: UTF8.self)
            .replacingOccurrences(of: "\u{0}", with: " ")
    }

    func closeForCheck() {
        if let window { _ = windowShouldClose(window) }
    }

    var terminalCountForCheck: Int { terminals.count }

    var settingsOpenForCheck: Bool { settings != nil }

    /// The page of the open settings, or nil when they are closed.
    var settingsPageForCheck: SettingsPage? { settings?.page }

    /// The environment of a session, for a question about it.
    func environmentForCheck(_ id: String) -> String? { sessions.find(id)?.environment }

    /// Whether the session is in the list and closed.
    func closedForCheck(_ id: String) -> Bool { sessions.find(id)?.closed == true }

    /// Whether the loader stands over the terminal now.
    var loaderShownForCheck: Bool { loader.map { !$0.isHidden && $0.superview != nil } ?? false }

    /// Puts the window where a saved place says and tells where it stands, from the top left.
    func placeForCheck(_ saved: WindowPlace?) -> Box? {
        guard let window else { return nil }
        place(window, saved)
        return Screens.box(window.frame)
    }

    var settingsPanelForCheck: String {
        guard let panel = settingsCover else { return "none" }
        return "frame \(Int(panel.frame.width))x\(Int(panel.frame.height)), "
            + "bounds \(Int(panel.bounds.width))x\(Int(panel.bounds.height)), fitting \(Int(panel.fittingSize.height))"
    }

    /// Opens or folds the header without writing the setting.
    func showHeaderForCheck(_ open: Bool) {
        state.headerOpen = open
        state.header = header()
        placeHeader(animated: false)
    }

    /// The columns of the header, the active one first, and the height of its strip.
    var headerForCheck: String {
        let columns = state.header.map { item in
            "\(item.column.environment)\(item.column.active ? " active, \(item.block?.rows.count ?? 0) rows" : "")"
        }
        return "header [\(columns.joined(separator: "; "))], strip \(Int(headerHeight?.constant ?? -1))"
    }

    /// Where the bar and the window buttons stand, in points from the top of the window.
    var barForCheck: String {
        guard let window, let bar, let close = window.standardWindowButton(.closeButton),
              let content = window.contentView else { return "no window" }
        let barFrame = bar.convert(bar.bounds, to: nil)
        let button = close.convert(close.bounds, to: nil)
        let top = content.frame.height
        let rooms = zoomChecks.compactMap { $0() }
        return "zoom \(Int((zoom * 100).rounded())) %, views seeing their room \(rooms.filter { $0 }.count) of \(rooms.count), bar \(Int(top - barFrame.maxY))..\(Int(top - barFrame.minY)), "
            + "close button middle \(Int(top - button.midY)), list \(Int(listWidthConstraint?.constant ?? -1))"
    }

    /// Each row of the list with its state, and what stands in place of the terminal.
    var rowsForCheck: String {
        let rows = state.groups.flatMap(\.rows).map { "\($0.state)" }.joined(separator: " ")
        return "rows [\(rows)], stage \(state.stage)"
    }

    var runningForCheck: Bool { terminals.values.contains { $0.process.running } }

    /// The session leader of the program of a session, and a line typed into it, for --try-terminal.
    func leaderForCheck(_ id: String) -> pid_t { terminals[id]?.process.shellPid ?? 0 }

    func typeForCheck(_ id: String, _ line: String) {
        terminals[id]?.send(txt: line)
    }

    /// What the window asks about now, when the question is on the stage.
    var askingForCheck: StopReason? { state.stage == stageMessage() ? stopping?.reason : nil }

    // ---- small parts ----

    func save() {
        if !changingNothing {
            Store.saveTerminal(sessions)
        }
    }

    /// Whether Claude Code saved the conversation: it writes the file only after the first message,
    /// and a session closed before one has nothing to resume. Unsure means yes, as on Windows.
    static func conversationOnDisk(_ config: String, _ id: String) -> Bool {
        let projects = TranscriptPath.projectsFolder(.macOS, config)
        guard let folders = try? FileManager.default.contentsOfDirectory(atPath: projects) else {
            // No folder yet: nothing saved. A folder that cannot be read: unsure.
            return FileManager.default.fileExists(atPath: projects)
        }
        return folders.contains {
            FileManager.default.fileExists(atPath: (projects as NSString)
                .appendingPathComponent($0).appending("/" + TranscriptPath.fileName(id)))
        }
    }

    /// Seconds, as terminal.json keeps them on both systems: the day of the second line and the
    /// start of a session for --resume read them so.
    static func now() -> Int64 {
        Int64(Date().timeIntervalSince1970)
    }

    /// The room of every screen without the menu bar and the Dock, counted from the top left as the
    /// core counts it (Screens).
    static func screens() -> [ScreenArea] {
        NSScreen.screens.map {
            let box = Screens.box($0.visibleFrame)
            return ScreenArea(left: box.x, top: box.y, width: box.width, height: box.height)
        }
    }

    /// The window remembers where it stood, as on Windows: closed or with Aiko quitting, it opens
    /// there again, on any screen that still has it.
    func rememberPlace() {
        guard let window, window.isVisible else { return }
        let box = Screens.box(window.frame)
        sessions = sessions.placed(WindowPlace(
            left: box.x, top: box.y, width: box.width, height: box.height, maximized: window.isZoomed))
        save()
    }
}
