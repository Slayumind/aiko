import AikoKit
import AppKit
import Foundation

/// The terminal behind its window (D-292, D-293, D-304): the sessions of the file, the
/// conversations in memory and what is chosen. Every event of the panel ends in a new screen.
///
/// The window is made when the terminal is opened and is gone when it is closed; with it go the
/// processes of Claude Code. The conversations come back from their files the next time.
///
/// The twin of Terminal/TerminalHost.cs.
@MainActor
final class TerminalHost {
    private let settings: EnvironmentSettings
    private let newProcess: () -> ConversationProcess
    private let limitsOf: (String) -> LimitSnapshot?
    private let panel = TerminalPanelState()
    private let window: TerminalWindow
    private var talks: [String: ConversationRunner] = [:]

    /// What a conversation said before its process started this time, read from the file of
    /// Claude Code (D-303). The process adds what comes after.
    private var histories: [String: Conversation] = [:]

    private var sessions: TerminalSessions
    private var model: TerminalWindowModel
    private var form: SessionForm?
    private var closing: String?
    private var closingWindow = false
    private var agreedToClose = false

    var onClosed: (() -> Void)?

    /// The window came on screen, or left it by being minimized or closed.
    var onInView: ((Bool) -> Void)?

    /// limitsOf gives the latest limits of an environment by its name.
    init(
        settings: EnvironmentSettings,
        sessions: TerminalSessions,
        newProcess: @escaping () -> ConversationProcess,
        limitsOf: @escaping (String) -> LimitSnapshot?
    ) {
        self.settings = settings
        self.sessions = sessions
        self.newProcess = newProcess
        self.limitsOf = limitsOf
        model = TerminalWindowModel.opened(sessions, settings.environments.map(\.name))
        window = TerminalWindow(panel: panel, place: WindowPlace.fit(sessions.window, TerminalWindow.screens()))

        window.mayClose = { [weak self] in self?.mayCloseWindow() ?? true }
        window.onInView = { [weak self] in self?.onInView?($0) }
        window.onClosing = { [weak self] in
            guard let self else { return }
            self.sessions = self.sessions.placed(self.window.place)
            self.save()
        }
        window.onClosed = { [weak self] in
            guard let self else { return }
            for talk in self.talks.values {
                talk.close()
            }
            self.onClosed?()
        }

        panel.tabPicked = { [weak self] name in
            guard let self else { return }
            self.change(self.model.toEnvironment(name, self.sessions))
        }
        panel.rowPicked = { [weak self] in self?.pick($0) }
        panel.rowClosed = { [weak self] in self?.askToClose($0) }
        panel.createAsked = { [weak self] in self?.openForm() }
        panel.folderPicked = { [weak self] in self?.refill(folder: $0, model: nil) }
        panel.otherFolderAsked = { [weak self] in self?.pickOtherFolder() }
        panel.modelPicked = { [weak self] in self?.refill(folder: nil, model: $0) }
        panel.submitted = { [weak self] name, message in self?.create(name, message) }
        panel.formCancelled = { [weak self] in
            guard let self else { return }
            self.change(self.model.withForm(false))
        }
        panel.askAnswered = { [weak self] in self?.answer(stop: $0) }
        panel.signInAsked = { [weak self] in self?.signIn() }
        panel.chosen = { [weak self] requestId, choice in self?.current()?.choose(requestId, choice) }
        panel.input.sent = { [weak self] text, images in
            guard let self, let session = self.model.session(self.sessions) else { return }
            self.send(session.id, text, images: images)
            self.draw()
        }
        panel.input.stopAsked = { [weak self] in self?.current()?.interrupt() }
        panel.input.modeAsked = { [weak self] in self?.nextMode() }
        panel.input.modelAsked = { [weak self] in self?.nextModel() }
        panel.input.chosen = panel.chosen
    }

    func show() {
        draw()
        window.show()
    }

    func activate() {
        window.activate()
    }

    /// Aiko quits: the window closes without asking, as the processes end with Aiko anyway.
    func close() {
        agreedToClose = true
        window.close()
    }

    /// New limits arrived: the percentage in the title follows them.
    func limitsChanged() {
        draw()
    }

    /// For a check: the window as it stands.
    var frameForCheck: NSRect { window.frame }

    var windowNumberForCheck: Int { window.number }

    // MARK: - What the person did

    private func change(_ next: TerminalWindowModel) {
        model = next
        if !next.formOpen {
            form = nil
        }
        draw()
    }

    private func pick(_ id: String) {
        sessions = sessions.opened(id)
        save()
        talks[id]?.markRead()
        change(model.toSession(id, sessions))
    }

    private func openForm() {
        model = model.withForm(true)
        form = formFor(folder: nil, model: nil)
        draw()
    }

    /// The form again after a choice in it: another folder changes the hint and the name.
    private func refill(folder: Int?, model chosen: Int?) {
        guard let form else { return }
        self.form = formFor(folder: folder ?? form.folder, model: chosen ?? form.model, known: form.folders)
        draw()
    }

    private func pickOtherFolder() {
        guard let form, let picked = FolderPicker.pick(title: Strings.terminalChooseFolder) else {
            draw()
            return
        }

        let path = SessionName.folderPath(picked)
        var folders = form.folders
        var at = folders.firstIndex { $0.path == path }
        if at == nil {
            folders.insert(FolderChoice(path: path, name: SessionName.folderName(path), source: .recent), at: 0)
            at = 0
        }

        self.form = formFor(folder: at, model: form.model, known: folders)
        draw()
    }

    private func formFor(folder: Int?, model chosen: Int?, known: [FolderChoice]? = nil) -> SessionForm? {
        guard let environment = environment() else { return nil }

        let folders = known ?? NewSessionChoices.folders(sessions, environment, foldersInside: TerminalStore.foldersInside)
        let picked = folders.isEmpty ? -1 : min(max(folder ?? 0, 0), folders.count - 1)
        let path = picked >= 0 ? folders[picked].path : nil
        let defaultModel = NewSessionChoices.models.firstIndex(of: NewSessionChoices.defaultModel(sessions, environment.name)) ?? 0

        return SessionForm(
            environment: environment.name,
            folders: folders,
            folder: picked,
            boundElsewhere: path.flatMap { NewSessionChoices.boundElsewhere(.macOS, $0, environment, settings) },
            model: chosen ?? defaultModel,
            defaultName: path.map { NewSessionChoices.defaultName(sessions, environment.name, $0) } ?? "")
    }

    private func create(_ name: String, _ message: String) {
        guard let form, form.folder >= 0 else { return }

        let id = UUID().uuidString.lowercased()
        let folder = form.folders[form.folder].path
        let config = environment()?.configDirectories.first ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home)
        sessions = sessions
            .add(
                id: id, environment: form.environment, folder: folder, name: name,
                model: NewSessionChoices.models[form.model],
                mode: TerminalStore.defaultMode(folder: folder, configDirectory: config), now: Self.now())
            .opened(id)
        save()

        self.form = nil
        model = model.withForm(false).toSession(id, sessions)
        if !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            send(id, message)
        }

        draw()
    }

    private func askToClose(_ id: String) {
        if let talk = talks[id], talk.state.isBusy {
            closing = id
            closingWindow = false
            draw()
            return
        }

        closeSession(id)
    }

    private func answer(stop: Bool) {
        let id = closing
        let wholeWindow = closingWindow
        closing = nil
        closingWindow = false

        if stop && wholeWindow {
            agreedToClose = true
            window.close()
        } else if stop, let id {
            closeSession(id)
        } else {
            draw()
        }
    }

    private func closeSession(_ id: String) {
        talks.removeValue(forKey: id)?.close()
        histories.removeValue(forKey: id)
        sessions = sessions.close(id)
        save()
        change(model.after(sessions))
    }

    private func mayCloseWindow() -> Bool {
        if agreedToClose || !talks.values.contains(where: { $0.state.isBusy }) {
            return true
        }

        closing = nil
        closingWindow = true
        draw()
        return false
    }

    private func signIn() {
        guard let environment = environment(), let session = model.session(sessions) else { return }
        _ = ClaudeLauncher.open(
            configFolder: environment.configDirectories.first ?? "", workingDirectory: session.folder)
    }

    // MARK: - The conversations

    private func send(_ id: String, _ message: String, images: [AttachedImage] = []) {
        guard let talk = talkFor(id) else { return }

        let running = talks
            .filter { $0.value.isRunning }
            .map { RunningTalk(id: $0.key, busy: $0.value.state.isBusy, lastUsed: sessions.find($0.key)?.lastUsed ?? 0) }
        if let idle = ProcessCap.toStop(running, starting: id), let stopped = talks.removeValue(forKey: idle) {
            // Its next process starts from the file again, which then holds this run too.
            stopped.close()
            histories.removeValue(forKey: idle)
        }

        sessions = sessions.used(id, now: Self.now()).markStarted(id)
        save()
        talk.send(message, images: images)
    }

    private func talkFor(_ id: String) -> ConversationRunner? {
        if let known = talks[id] {
            return known
        }

        guard let session = sessions.find(id),
              let environment = settings.environments.first(where: { $0.name == session.environment }),
              let claude = ClaudeLauncher.findClaude()
        else {
            return nil
        }

        readHistory(id)
        let folder = FileManager.default.fileExists(atPath: session.folder) ? session.folder : Store.home
        let talk = ConversationRunner(
            .macOS,
            RunnerSetup(
                executable: claude,
                workingDirectory: folder,
                configDirectory: environment.configDirectories.first
                    ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home),
                userProfile: Store.home,
                environmentName: environment.name,
                sessionId: session.id,
                started: session.started,
                mode: session.mode,
                model: session.model,
                inheritedVariables: Array(ProcessInfo.processInfo.environment.keys),
                agentTodos: environment.agentTodos),
            newProcess: newProcess,
            post: { work in DispatchQueue.main.async(execute: work) },
            log: AppTerminalLog())
        // The runner calls this on the main thread, through post.
        talk.changed = { [weak self, weak talk] in
            MainActor.assumeIsolated {
                if let talk { self?.keepModel(id, talk.state) }
                self?.draw()
            }
        }
        talks[id] = talk
        return talk
    }

    /// The full id Claude Code named is kept, so the session opens with it next time. While a
    /// model is asked, the session already keeps that one.
    private func keepModel(_ id: String, _ state: Conversation) {
        guard state.askedModel.isEmpty, !state.model.isEmpty,
              let session = sessions.find(id), session.model != state.model else { return }
        sessions = sessions.setModel(id, state.model)
        save()
    }

    /// The next mode of the session. It is kept for the next start, and a running Claude Code
    /// takes it at once.
    private func nextMode() {
        guard let session = model.session(sessions) else { return }
        let talk = current()
        let next = PermissionModes.next(talk?.state.mode ?? session.mode)
        sessions = sessions.setMode(session.id, next)
        save()
        talk?.setMode(next)
        draw()
    }

    /// The next model of the session. It is kept for the next start, and a running Claude Code
    /// takes it from the next turn.
    private func nextModel() {
        guard let session = model.session(sessions) else { return }
        let talk = current()
        let latest = talk?.state.latestModel ?? ""
        let next = NewSessionChoices.nextModel(latest.isEmpty ? session.model : latest)
        sessions = sessions.setModel(session.id, next)
        save()
        talk?.setModel(next)
        draw()
    }

    private func current() -> ConversationRunner? {
        model.session(sessions).flatMap { talks[$0.id] }
    }

    private func readHistory(_ id: String) {
        guard let session = sessions.find(id), session.started,
              let environment = settings.environments.first(where: { $0.name == session.environment })
        else {
            histories[id] = Conversation.empty(mode: sessions.find(id)?.mode ?? .manual)
            return
        }

        histories[id] = TerminalStore.history(
            configDirectory: environment.configDirectories.first ?? ClaudeConfigFolder.resolve(.macOS, nil, Store.home),
            folder: session.folder,
            sessionId: session.id,
            mode: session.mode)
    }

    private func talk(_ session: TerminalSession?) -> TalkScreen? {
        guard let session else { return nil }

        // The window opens on a session nobody picked yet: its past is read here, once.
        if histories[session.id] == nil {
            readHistory(session.id)
        }
        let past = histories[session.id]?.items ?? []

        guard let talk = talks[session.id] else {
            return TalkScreen(
                session: session.id, items: past, busy: false, hasOpenCard: false, compacting: false, mode: session.mode,
                model: ModelName.label(asked: nil, known: session.model), environment: session.environment)
        }

        let state = talk.state
        return TalkScreen(
            session: session.id,
            items: past + state.items,
            busy: state.isBusy,
            hasOpenCard: state.hasOpenCard,
            compacting: state.compacting,
            mode: state.mode,
            model: ModelName.label(asked: state.askedModel, known: state.model.isEmpty ? session.model : state.model),
            environment: session.environment)
    }

    // MARK: - The screen

    private func environment() -> AikoEnvironment? {
        settings.environments.first { $0.name == model.environment }
    }

    private func live(_ id: String) -> LiveTalk? {
        talks[id].map { LiveTalk(glance: TalkGlance.of($0.state), running: $0.isRunning) }
    }

    private func draw() {
        panel.show(screen())
    }

    private func screen() -> TerminalScreen {
        let session = model.session(sessions)
        let signedOut = session.flatMap { live($0.id) }?.glance.activity == .signedOut

        let ask: CloseAsk? = if closingWindow {
            CloseAsk(title: Strings.terminalCloseWindowTitle, body: Strings.terminalCloseWindowBody)
        } else if let id = closing, let session = sessions.find(id) {
            CloseAsk(title: Strings.format(Strings.terminalCloseTitle, session.name), body: Strings.terminalCloseBody)
        } else {
            nil
        }

        let now = Date()
        return TerminalScreen(
            tabs: settings.environments.map { environment in
                TerminalTab(
                    name: environment.name,
                    selected: environment.name == model.environment,
                    waiting: SessionRows.waiting(sessions, environment.name, live: live))
            },
            badge: badge(now),
            groups: SessionRows.of(sessions, model.environment, live: live),
            environment: model.environment,
            sessionId: session?.id,
            sessionName: session?.name,
            main: model.view(sessions),
            signedOut: signedOut,
            form: form,
            ask: ask,
            talk: signedOut ? nil : talk(session),
            now: now)
    }

    private func badge(_ now: Date) -> TerminalBadge? {
        guard let snapshot = limitsOf(model.environment),
              let badge = LimitBadge.forSnapshot(snapshot, now)
        else {
            return nil
        }
        return TerminalBadge(percent: badge.percent, tone: badge.tone, kind: badge.kind)
    }

    private func save() {
        Store.saveTerminal(sessions)
    }

    private static func now() -> Int64 {
        Int64(Date().timeIntervalSince1970)
    }
}
