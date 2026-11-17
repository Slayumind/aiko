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
        state.onSignIn = { name in Self.signIn(name) }
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
            quitAfter = nil
            draw()
            return
        }
        closeSession(id)
    }

    func answer(_ stop: Bool) {
        let id = closing
        let quit = quitAfter
        closing = nil
        quitAfter = nil
        if let quit {
            quit(stop)
            if !stop { draw() }
        } else if stop, let id {
            closeSession(id)
        } else {
            draw()
        }
    }

    /// A program still runs in the window.
    var anyRunning: Bool { terminals.values.contains { $0.process.running } }

    /// Aiko is asked to quit while programs still run in the window (D-351): the window comes
    /// forward with the question, and the answer says whether Aiko quits.
    func askToQuit(_ quit: @escaping (Bool) -> Void) {
        quitAfter = quit
        closing = nil
        show()
        draw()
    }

    /// Aiko quits: every program in the window ends with all it started.
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
    static func signIn(_ environment: String) {
        guard let folder = Store.environments().environments.first(where: { $0.name == environment })?
            .configDirectories.first else { return }
        _ = ClaudeLauncher.open(configFolder: folder, workingDirectory: Store.home)
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
