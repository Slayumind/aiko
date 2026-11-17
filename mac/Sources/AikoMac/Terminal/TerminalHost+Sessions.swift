import AikoKit
import AppKit

/// What the person does to one session from the list: rename it, close it, open its folder, start
/// it again. The twin of the same parts of TerminalHost.cs.
extension TerminalHost {
    /// The page that tells how to install Claude Code, the same as on Windows.
    static let claudeInstallPage = "https://code.claude.com/docs/en/setup"

    func wireSessionActions() {
        state.onRename = { [weak self] id, name in self?.rename(id, name) }
        state.onClose = { [weak self] id in self?.askToClose(id) }
        state.onAnswer = { [weak self] stop in self?.answer(stop) }
        state.onOpenFolder = { folder in Self.openFolder(folder) }
        state.onRetry = { [weak self] in self?.retry() }
        state.onSignIn = { [weak self] name in self?.signIn(name) }
        state.onInstallPage = {
            if let url = URL(string: Self.claudeInstallPage) { NSWorkspace.shared.open(url) }
        }
    }

    /// An empty name changes nothing (TerminalSessions.rename).
    func rename(_ id: String, _ name: String) {
        sessions = sessions.rename(id, name)
        save()
        draw()
    }

    /// A session that still runs asks first; one that does not leaves at once.
    func askToClose(_ id: String) {
        if let terminal = terminals[id], terminal.process.running {
            closing = id
            stopping = nil
            draw()
            return
        }
        closeSession(id)
    }

    func answer(_ stop: Bool) {
        let id = closing
        let asked = stopping
        let page = settingsBeforeAsking
        closing = nil
        stopping = nil
        settingsBeforeAsking = nil
        if let asked {
            asked.answer(stop)
            if !stop {
                if let page { openSettings(at: page) } else { draw() }
            }
        } else if stop, let id {
            closeSession(id)
        } else {
            draw()
        }
    }

    /// A program still runs in the window and the person could lose it. Once Aiko is on its way
    /// out the programs are already being stopped.
    var anyRunning: Bool { !quitting && terminals.values.contains { $0.process.running } }

    /// Aiko is asked to quit while programs still run in the window (D-351): the window comes
    /// forward with the question, and the answer says whether Aiko quits.
    func askToStop(_ reason: StopReason, _ answer: @escaping (Bool) -> Void) {
        stopping = (reason, answer)
        closing = nil
        if let open = settings {
            settingsBeforeAsking = open.page
            closeSettings()
        }
        show()
        draw()
    }

    /// A step of the settings that stops every session: restart for an update, remove Aiko, start
    /// over. While programs run the window asks first; after a yes they end and the step runs.
    /// Start over keeps Aiko, so the settings come back with the checklist from the start.
    func askBefore(_ reason: StopReason, _ step: @escaping () -> Void) {
        guard anyRunning else {
            step()
            return
        }
        askToStop(reason) { [weak self] yes in
            guard let self, yes else { return }
            if reason == .startOver {
                self.endAll()
                step()
                self.openSettings(at: nil)
            } else {
                self.endForQuit()
                step()
            }
        }
    }

    /// An environment goes: only its own running sessions are asked about, and after a yes they
    /// stop and close with it.
    func askToRemoveEnvironment(_ name: String, _ step: @escaping () -> Void) {
        let running = sessions.sessions
            .filter { $0.environment == name && terminals[$0.id]?.process.running == true }
            .map(\.id)
        guard !running.isEmpty else {
            step()
            return
        }
        askToStop(.removeEnvironment) { [weak self] yes in
            guard let self, yes else { return }
            running.forEach(self.closeSession)
            step()
            self.openSettings(at: nil)
        }
    }

    /// Aiko leaves: what ends now stays in the list for the next start.
    func endForQuit() {
        quitting = true
        endAll()
    }

    /// Every program in the window ends with all it started.
    func endAll() {
        let running = terminals.values.filter { $0.process.running }
        ProcessTree.endNow(running.map { $0.process.shellPid })
        for terminal in running {
            terminal.process.terminate()
        }
    }

    func closeSession(_ id: String) {
        if let terminal = terminals.removeValue(forKey: id) {
            if terminal.process.running {
                ProcessTree.endSession(of: terminal.process.shellPid)
                terminal.process.terminate()
            }
            terminal.removeFromSuperview()
        }
        problems[id] = nil
        forgetEditor(id)
        forgetPulse(id)
        if let pane = shells.removeValue(forKey: id) {
            pane.blocks.removeFromSuperview()
            pane.find.removeFromSuperview()
        }
        sessions = sessions.close(id)
        save()
        model = model.after(sessions)
        draw()
    }

    /// The chosen session starts again: the problem is forgotten and the terminal tried anew.
    func retry() {
        if let id = model.sessionId {
            problems[id] = nil
        }
        draw()
    }

    /// "Sign in" in the open header opens Claude Code of that environment, as "Sign in again" in its
    /// settings does: signing in happens there.
    /// When Claude Code does not open, the page of the environment takes over: it has the same
    /// button and says what went wrong under it.
    func signIn(_ environment: String) {
        guard let folder = Store.environments().environments.first(where: { $0.name == environment })?
            .configDirectories.first else { return }
        if !ClaudeLauncher.open(configFolder: folder, workingDirectory: Store.home) {
            openSettings(at: .environment(folder))
        }
    }

    /// The folder of a group in Finder (D-408); false when the folder is gone from the disk.
    static func openFolder(_ folder: String) -> Bool {
        var isFolder: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder, isDirectory: &isFolder), isFolder.boolValue else {
            return false
        }
        NSWorkspace.shared.open(URL(fileURLWithPath: folder))
        return true
    }
}
