import AikoKit
import AppKit

/// --try-shell: a real command line of the person's shell with the marks of Aiko, driven through
/// the editor. It checks the blocks, the exit code, the fold of a long output, the search, Copy and
/// the real terminal for a full-screen program. Explain and the proposed command ask the model, so
/// they are not run here. Nothing is saved.
@MainActor
enum ShellCheck {
    static func run(_ host: TerminalHost) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-shell")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        try? "lantern\n".write(toFile: (folder as NSString).appendingPathComponent("note.txt"), atomically: true, encoding: .utf8)
        host.show()
        guard let id = host.startForCheck(in: folder, kind: .shell) else {
            NSApp.terminate(nil)
            return
        }

        Task { @MainActor in
            guard let pane = host.shells[id] else {
                check("the shell has the marks of Aiko", false, ShellLaunch.find().name)
                finish(host)
                return
            }
            let editor = host.editor
            let blocks = pane.blocks
            @MainActor func run(_ command: String) async {
                editor.setText(command, caret: (command as NSString).length)
                _ = editor.press(36, [])
                _ = await until(10) { ShellBlocks.of(pane.screen).last.map { $0.command == command && !$0.running } == true }
            }
            _ = await until(10) { pane.screen.marks.contains { $0.kind == "A" } }
            check("the shell starts with the marks of Aiko", pane.screen.marks.contains { $0.kind == "B" })

            await run("echo one")
            await run("false")
            await run("seq 1 100")
            let all = ShellBlocks.of(pane.screen)
            check("each command is a block", all.count == 3 && blocks.blockCountForCheck == 3, "\(all.count) blocks")
            check("a failed command has its exit code", all.count > 1 && all[1].failed && all[1].exitCode == 1)
            check("the blocks stand in place of the terminal", !blocks.isHidden && (host.openTerminal()?.alphaValue ?? 1) == 0)
            // echo: command, one line, gap; false: command, gap; seq: command, 20 lines, "more", gap.
            check("a long output folds to its first lines", blocks.rowsForCheck == 3 + 2 + (1 + 20 + 1 + 1),
                  "\(blocks.rowsForCheck) rows")

            _ = host.openFind()
            pane.find.searchForCheck("one")
            check("Cmd+Shift+F finds in the output", !pane.find.isHidden && blocks.matchesForCheck == 1, pane.find.countForCheck)
            pane.find.close()
            check("Esc of the search gives the room back", pane.find.isHidden)

            blocks.pressForCheck(0, Strings.shellCopy)
            check("Copy takes the output of the block", NSPasteboard.general.string(forType: .string)?.contains("one") == true)

            editor.setText("less note.txt", caret: 13)
            _ = editor.press(36, [])
            let full = await until(5) { pane.screen.fullScreen && blocks.isHidden }
            check("a full-screen program shows the real terminal", full && (host.openTerminal()?.alphaValue ?? 0) == 1)
            host.openTerminal()?.send(txt: "q")
            let back = await until(5) { !pane.screen.fullScreen && !blocks.isHidden }
            check("back from it the blocks come again", back)

            host.closeSession(id)
            finish(host)
        }
    }

    private static func finish(_ host: TerminalHost) {
        host.closeForCheck()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { NSApp.terminate(nil) }
    }

    private static func check(_ what: String, _ ok: Bool, _ seen: String = "") {
        Log.write("self test: shell \(ok ? "ok" : "FAIL") \(what)\(seen.isEmpty ? "" : " [\(seen)]")")
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
