import AikoKit
import AppKit

/// --try-stopped: a program that ends by itself. A command line leaves the list; Claude Code stays
/// with the bar in place of the editor, Enter on the bar starts it again. Claude Code starts in the
/// folder of --try-editor, which that check trusted; nothing goes to the model, nothing is saved.
@MainActor
enum StoppedCheck {
    static func run(_ host: TerminalHost) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-editor")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        host.show()

        Task { @MainActor in
            let shell = host.startForCheck(in: folder, kind: .shell)
            try? await Task.sleep(for: .seconds(1.5))
            host.openTerminal()?.send(txt: "exit\r")
            // A closed session stays in terminal.json marked closed; the list shows only the open ones.
            let gone = await until(5) { shell.map { host.sessions.find($0)?.closed ?? true } ?? false }
            check("a command line that ends leaves the list", gone)

            guard ClaudeLauncher.findClaude() != nil, let id = host.startForCheck(in: folder, kind: .claude) else {
                Log.write("self test: stopped SKIP Claude Code: it is not installed")
                finish(host)
                return
            }
            let ready = await until(30) { host.screenForCheck.contains("shift+tab") }
            check("Claude Code is ready", ready)
            host.openTerminal()?.send(txt: "\u{03}")
            try? await Task.sleep(for: .milliseconds(400))
            host.openTerminal()?.send(txt: "\u{03}")
            let stopped = await until(5) { !host.stoppedBar.isHidden }
            check("Claude Code that ends shows the bar in place of the editor",
                  stopped && host.editor.isHidden && host.live(id)?.activity == .exited && host.sessions.find(id) != nil)
            check("Enter on the bar has the keys", host.stoppedBar.hasKeys)

            host.stoppedBar.resume?()
            let back = await until(30) { host.stoppedBar.isHidden && host.screenForCheck.contains("shift+tab") }
            check("Continue starts Claude Code again in the same session", back && host.live(id)?.activity != .exited)

            host.openTerminal()?.send(txt: "\u{03}")
            try? await Task.sleep(for: .milliseconds(400))
            host.openTerminal()?.send(txt: "\u{03}")
            try? await Task.sleep(for: .milliseconds(500))
            host.closeSession(id)
            finish(host)
        }
    }

    private static func finish(_ host: TerminalHost) {
        host.closeForCheck()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { NSApp.terminate(nil) }
    }

    private static func check(_ what: String, _ ok: Bool) {
        Log.write("self test: stopped \(ok ? "ok" : "FAIL") \(what)")
    }

    private static func until(_ seconds: Double, _ done: @MainActor () -> Bool) async -> Bool {
        let end = Date().addingTimeInterval(seconds)
        while Date() < end {
            if done() { return true }
            try? await Task.sleep(for: .milliseconds(150))
        }
        return done()
    }
}
