import AikoKit
import AppKit

/// --try-pulse: what a session does, from the hook to the row, the count and the Dock, with the
/// real bridge of the bundle and no model. The check starts a command line and feeds the bridge the
/// input Claude Code gives a hook, as Claude Code would: in the variable of the session, on stdin.
/// Nothing is saved; the sound is not played.
@MainActor
enum PulseCheck {
    static func run(_ host: TerminalHost, waitingHeard: @escaping @MainActor () -> Int) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-pulse")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        host.show()
        guard let id = host.startForCheck(in: folder, kind: .shell) else {
            NSApp.terminate(nil)
            return
        }

        Task { @MainActor in
            check("the hooks file names the bridge of the app", !TerminalEvents.hooksFile().isEmpty)
            try? await Task.sleep(for: .seconds(1))

            hook(id, #"{"hook_event_name":"PreToolUse","tool_name":"Read","tool_input":{"file_path":"/work/office.mjs"}}"#)
            let reading = await until(3) { host.live(id)?.line.contains("office.mjs") == true }
            check("a hook of a tool becomes the second line of the row", reading, host.live(id)?.line ?? "none")

            // The window is in front, but another session is open: the count and the sound come.
            host.show()
            hook(id, #"{"hook_event_name":"PermissionRequest","tool_name":"Bash","tool_input":{"command":"npm test"}}"#)
            let waiting = await until(3) { host.live(id)?.activity == .waiting }
            check("a permission prompt makes the session wait", waiting, host.live(id)?.line ?? "none")
            check("the count says one waits and the Dock shows 1",
                  host.sessionCounts.waiting == 1 && NSApp.dockTile.badgeLabel == "1", "\(NSApp.dockTile.badgeLabel ?? "no badge")")
            check("the sound of waiting is due once, or not in a session the person looks at", waitingHeard() <= 1, "\(waitingHeard())")

            host.typed(id)
            check("typing into the session answers it", host.live(id)?.activity != .waiting && host.sessionCounts.waiting == 0)

            host.closeSession(id)
            check("a closed session takes its file of events with it",
                  TerminalEvent.fileName(id).map { !FileManager.default.fileExists(atPath: (Store.folders.terminalEventsFolder as NSString).appendingPathComponent($0)) } ?? false)
            check("no badge in the Dock once nothing counts", NSApp.dockTile.badgeLabel == nil)
            host.closeForCheck()
            try? await Task.sleep(for: .milliseconds(500))
            NSApp.terminate(nil)
        }
    }

    /// The bridge of the bundle, run as Claude Code runs a hook of the terminal.
    private static func hook(_ id: String, _ input: String) {
        guard let bridge = BridgePath.current() else { return }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: bridge)
        process.arguments = [TerminalEvent.hookArgument]
        process.environment = [TerminalEvent.sessionVariable: id, "HOME": NSHomeDirectory()]
        let pipe = Pipe()
        process.standardInput = pipe
        try? process.run()
        pipe.fileHandleForWriting.write(Data(input.utf8))
        try? pipe.fileHandleForWriting.close()
        process.waitUntilExit()
    }

    private static func check(_ what: String, _ ok: Bool, _ seen: String = "") {
        Log.write("self test: pulse \(ok ? "ok" : "FAIL") \(what)\(seen.isEmpty ? "" : " [\(seen)]")")
    }

    private static func until(_ seconds: Double, _ done: @MainActor () -> Bool) async -> Bool {
        let end = Date().addingTimeInterval(seconds)
        while Date() < end {
            if done() { return true }
            try? await Task.sleep(for: .milliseconds(100))
        }
        return done()
    }
}
