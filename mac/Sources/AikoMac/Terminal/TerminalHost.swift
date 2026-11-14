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
    private let state = TerminalState()
    private var window: NSWindow?
    private let stage = NSView()
    private var sessions = Store.terminal()
    private var model: TerminalWindowModel
    private var terminals: [String: LocalProcessTerminalView] = [:]
    private var exited: Set<String> = []

    /// Set by a self test: nothing is written to terminal.json.
    private var changingNothing = false

    override init() {
        let environments = Store.environments().environments.map(\.name)
        model = TerminalWindowModel.opened(Store.terminal(), environments)
        super.init()
        state.onEnvironment = { [weak self] name in self?.toEnvironment(name) }
        state.onChoose = { [weak self] id in self?.choose(id) }
        state.onNew = { [weak self] folder, kind in self?.start(in: folder, kind: kind) }
        state.onNewInOtherFolder = { [weak self] in self?.pickFolder() }
    }

    var isShown: Bool { window?.isVisible == true }

    /// The window was shown or closed.
    var onShownChanged: ((Bool) -> Void)?

    /// Opens the window, or brings it forward.
    func show() {
        let window = self.window ?? makeWindow()
        self.window = window
        NSApp.setActivationPolicy(.regular)
        draw()
        window.makeKeyAndOrderFront(nil)
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

    private func toEnvironment(_ name: String) {
        model = model.toEnvironment(name, sessions)
        draw()
    }

    private func choose(_ id: String) {
        model = model.toSession(id, sessions)
        draw()
    }

    private func start(in folder: String, kind: SessionKind) {
        let id = UUID().uuidString.lowercased()
        let name = SessionName.defaultName(folder, taken: sessions.namesIn(model.environment, folder))
        sessions = sessions.add(
            id: id, environment: model.environment, folder: folder, name: name, now: Self.now(), kind: kind)
        save()
        model = model.toSession(id, sessions)
        draw()
    }

    private func pickFolder() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.allowsMultipleSelection = false
        panel.directoryURL = URL(fileURLWithPath: model.session(sessions)?.folder ?? Store.home)
        guard panel.runModal() == .OK, let url = panel.url else { return }
        start(in: url.path, kind: .claude)
    }

    // ---- the window ----

    private func makeWindow() -> NSWindow {
        let place = WindowPlace.fit(nil, Self.screens())
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: place.width, height: place.height),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)
        window.title = "Aiko"
        window.minSize = NSSize(width: WindowPlace.minWidth, height: WindowPlace.minHeight)
        window.backgroundColor = Theme.nsSurface
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()

        let list = NSHostingView(rootView: SessionListView(state: state))
        list.translatesAutoresizingMaskIntoConstraints = false
        stage.translatesAutoresizingMaskIntoConstraints = false
        stage.wantsLayer = true
        stage.layer?.backgroundColor = Theme.nsSurface.cgColor

        let root = NSView()
        root.addSubview(list)
        root.addSubview(stage)
        NSLayoutConstraint.activate([
            list.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            list.topAnchor.constraint(equalTo: root.topAnchor),
            list.bottomAnchor.constraint(equalTo: root.bottomAnchor),
            list.widthAnchor.constraint(equalToConstant: 280),
            stage.leadingAnchor.constraint(equalTo: list.trailingAnchor),
            stage.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            stage.topAnchor.constraint(equalTo: root.topAnchor),
            stage.bottomAnchor.constraint(equalTo: root.bottomAnchor),
        ])
        window.contentView = root
        return window
    }

    /// The list from the core, and the terminal of the chosen session on the stage.
    private func draw() {
        let environments = Store.environments().environments.map(\.name)
        state.environments = environments
        state.environment = model.environment
        state.groups = SessionRows.of(sessions, model.environment) { [weak self] id in self?.live(id) }
        state.chosen = model.sessionId

        for view in stage.subviews {
            view.isHidden = true
        }

        guard let session = model.session(sessions), let terminal = terminal(for: session) else { return }
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
        window?.makeFirstResponder(terminal)
    }

    private func live(_ id: String) -> LiveTerminal? {
        guard terminals[id] != nil else { return nil }
        return LiveTerminal(activity: exited.contains(id) ? .exited : .idle, line: "")
    }

    // ---- the terminals ----

    /// The terminal of a session, started the first time it is shown. Nil when the program cannot
    /// start: no Claude Code, or the folder is gone.
    private func terminal(for session: TerminalSession) -> LocalProcessTerminalView? {
        if let running = terminals[session.id] {
            return running
        }

        guard FileManager.default.fileExists(atPath: session.folder),
              let environment = Store.environments().environments.first(where: { $0.name == session.environment })
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
            guard let claude = ClaudeLauncher.findClaude() else { return nil }
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

    var runningForCheck: Bool { terminals.values.contains { $0.process.running } }

    // ---- small parts ----

    private func save() {
        if !changingNothing {
            Store.saveTerminal(sessions)
        }
    }

    /// Whether Claude Code saved the conversation: it writes the file only after the first message,
    /// and a session closed before one has nothing to resume. Unsure means yes, as on Windows.
    private static func conversationOnDisk(_ config: String, _ id: String) -> Bool {
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

    private static func now() -> Int64 {
        Int64(Date().timeIntervalSince1970 * 1000)
    }

    private static func screens() -> [ScreenArea] {
        NSScreen.screens.map {
            ScreenArea(left: $0.visibleFrame.minX, top: $0.visibleFrame.minY,
                       width: $0.visibleFrame.width, height: $0.visibleFrame.height)
        }
    }
}
