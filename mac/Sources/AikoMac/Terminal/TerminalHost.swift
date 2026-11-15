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
    var exited: Set<String> = []

    /// Why a session has no terminal, until it is started again (D-421).
    var problems: [String: StartProblem] = [:]

    /// The running session the person asked to close: the question stands until it is answered.
    var closing: String?

    /// Set by a self test: nothing is written to terminal.json.
    var changingNothing = false

    /// The limits of an environment, as the menu bar icon has them: the ring and percentage of its pill.
    let limitsOf: (String) -> LimitSnapshot?

    /// The width of the session list, as TerminalPanel.ListWidth.
    static let listWidth: CGFloat = 252
    var listWidthConstraint: NSLayoutConstraint?
    var holderWatch: NSObjectProtocol?
    var bar: NSView?

    /// The settings while they are open, and their panel over the list and the terminal (D-374).
    var settings: SettingsState?
    var settingsCover: NSView?

    /// The messages in place of the terminal. Hidden while the terminal shows, so it takes no click.
    var stageCover: NSView?

    /// The words of every environment, as the menu bar icon has them: the active card of the header.
    let cardOf: () -> CardModel

    /// The strip of the open header and its height, which grows and folds (D-373).
    var headerHeight: NSLayoutConstraint?

    init(limitsOf: @escaping (String) -> LimitSnapshot?, cardOf: @escaping () -> CardModel) {
        self.limitsOf = limitsOf
        self.cardOf = cardOf
        let environments = Store.environments().environments.map(\.name)
        model = TerminalWindowModel.opened(Store.terminal(), environments)
        super.init()
        state.onEnvironment = { [weak self] name in self?.toEnvironment(name) }
        state.onChoose = { [weak self] id in self?.choose(id) }
        state.onNew = { [weak self] folder, kind in self?.start(in: folder, kind: kind) }
        state.onNewInOtherFolder = { [weak self] in self?.pickFolder() }
        state.onToggleList = { [weak self] in self?.toggleList() }
        state.onToggleHeader = { [weak self] in self?.toggleHeader() }
        state.onSettings = { [weak self] in self?.toggleSettings() }
        wireSessionActions()
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

    func pickFolder() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.allowsMultipleSelection = false
        panel.directoryURL = URL(fileURLWithPath: model.session(sessions)?.folder ?? Store.home)
        guard panel.runModal() == .OK, let url = panel.url else { return }
        start(in: url.path, kind: .claude)
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
        for view in stage.subviews where view !== shown {
            view.isHidden = true
        }

        guard let terminal = shown else { return }
        if terminal.superview !== stage {
            terminal.translatesAutoresizingMaskIntoConstraints = false
            stage.addSubview(terminal)
            NSLayoutConstraint.activate([
                terminal.leadingAnchor.constraint(equalTo: stage.leadingAnchor, constant: 8),
                terminal.trailingAnchor.constraint(equalTo: stage.trailingAnchor, constant: -8),
                terminal.topAnchor.constraint(equalTo: stage.topAnchor, constant: 8),
                terminal.bottomAnchor.constraint(equalTo: stage.bottomAnchor, constant: -8),
            ])
        }
        terminal.isHidden = false
        if settings == nil {
            window?.makeFirstResponder(terminal)
        }
    }

    /// What stands in place of the terminal: the question before closing first, then the empty
    /// environment, then why the chosen session has no terminal (D-421).
    func stageMessage() -> StageMessage {
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

    func live(_ id: String) -> LiveTerminal? {
        guard terminals[id] != nil else { return nil }
        return LiveTerminal(activity: exited.contains(id) ? .exited : .idle, line: "")
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
        switch session.kind {
        case .shell:
            executable = ProcessEnvironment.shell
            arguments = ["-l"]
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
                    sessionId: session.id, started: session.started,
                    picturesFolder: Store.folders.picturesFolder(session.id),
                    onDisk: Self.conversationOnDisk(config, session.id)),
                inheritedVariables: ProcessEnvironment.inheritedNames)
            arguments = plan.arguments
        }

        let view = LocalProcessTerminalView(frame: stage.bounds)
        view.processDelegate = self
        view.nativeBackgroundColor = Theme.nsSurface
        view.nativeForegroundColor = Theme.nsInk
        view.startProcess(
            executable: executable,
            args: arguments,
            environment: ProcessEnvironment.with(plan.variables),
            execName: nil,
            currentDirectory: session.folder)
        terminals[session.id] = view
        sessions = sessions.markStarted(session.id).used(session.id, now: Self.now())
        save()
        Log.write("terminal: \(session.kind == .shell ? "a command line" : "Claude Code") started for \(session.environment)")
        return view
    }

    nonisolated func sizeChanged(source: LocalProcessTerminalView, newCols: Int, newRows: Int) {}

    nonisolated func setTerminalTitle(source: LocalProcessTerminalView, title: String) {}

    nonisolated func hostCurrentDirectoryUpdate(source: TerminalView, directory: String?) {}

    nonisolated func processTerminated(source: TerminalView, exitCode: Int32?) {
        Task { @MainActor [weak self] in
            guard let self, let id = self.terminals.first(where: { $0.value === source })?.key else { return }
            self.exited.insert(id)
            Log.write("terminal: a program ended with \(exitCode.map(String.init) ?? "no code")")
            self.draw()
        }
    }

    // ---- doors for --try-terminal ----

    func startForCheck(in folder: String) {
        changingNothing = true
        start(in: folder, kind: .shell)
    }

    func closeForCheck() {
        if let window { _ = windowShouldClose(window) }
    }

    var terminalCountForCheck: Int { terminals.count }

    var settingsOpenForCheck: Bool { settings != nil }

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
        return "bar \(Int(top - barFrame.maxY))..\(Int(top - barFrame.minY)), "
            + "close button middle \(Int(top - button.midY)), list \(Int(listWidthConstraint?.constant ?? -1))"
    }

    /// Each row of the list with its state, and what stands in place of the terminal.
    var rowsForCheck: String {
        let rows = state.groups.flatMap(\.rows).map { "\($0.state)" }.joined(separator: " ")
        return "rows [\(rows)], stage \(state.stage)"
    }

    var runningForCheck: Bool { terminals.values.contains { $0.process.running } }

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

    static func screens() -> [ScreenArea] {
        NSScreen.screens.map {
            ScreenArea(left: $0.visibleFrame.minX, top: $0.visibleFrame.minY,
                       width: $0.visibleFrame.width, height: $0.visibleFrame.height)
        }
    }
}
