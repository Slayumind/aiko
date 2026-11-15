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
            draw()
            return
        }
        closeSession(id)
    }

    func answer(_ stop: Bool) {
        let id = closing
        closing = nil
        if stop, let id {
            closeSession(id)
        } else {
            draw()
        }
    }

    func closeSession(_ id: String) {
        if let terminal = terminals.removeValue(forKey: id) {
            if terminal.process.running {
                terminal.process.terminate()
            }
            terminal.removeFromSuperview()
        }
        exited.remove(id)
        problems[id] = nil
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
