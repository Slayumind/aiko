import AikoKit
import AppKit
import SwiftTerm

/// --try-editor: the editor under a real command line and a real Claude Code, the twin of
/// TerminalEditorCheck.cs. It types through the doors of the editor, reads the screen of the
/// terminal and writes one line per check. Nothing is sent to the model: the text and the picture
/// stop in the line of Claude Code, so no limits are spent. Nothing is saved to terminal.json.
@MainActor
enum EditorCheck {
    static func run(_ host: TerminalHost) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-editor")
        try? FileManager.default.removeItem(atPath: folder)
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        try? "lantern\n".write(toFile: (folder as NSString).appendingPathComponent("note.txt"), atomically: true, encoding: .utf8)
        let picture = (folder as NSString).appendingPathComponent("red.png")
        writeRedPicture(picture)

        host.show()
        host.editor.checkWithoutFocus = true
        Task { @MainActor in
            let shell = await commandLine(host, folder)
            let claude = await claudeCode(host, folder, picture)
            // The sessions of the check go with their pictures; the ones of the person stay.
            let ids = [shell, claude].compactMap { $0 }
            for id in ids {
                host.closeSession(id)
            }
            check("the pictures of a closed session are removed",
                  ids.allSatisfy { !FileManager.default.fileExists(atPath: Store.folders.picturesFolder($0)) })
            host.closeForCheck()
            try? await Task.sleep(for: .milliseconds(500))
            NSApp.terminate(nil)
        }
    }

    // ---- a command line ----

    private static func commandLine(_ host: TerminalHost, _ folder: String) async -> String? {
        let id = host.startForCheck(in: folder, kind: .shell)
        let editor = host.editor
        _ = await until(10) { !host.screenForCheck.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        try? await Task.sleep(for: .seconds(1))
        check("stands under a command line", editor.wanted && !editor.isHidden && editor.frame.height > 20
                && editor.frame.height < 120, editor.layoutForCheck)

        editor.setText("echo editor-ok", caret: 14)
        _ = editor.press(36, [])
        check("a command from the editor runs in the command line",
              await until(10) { host.screenForCheck.components(separatedBy: "editor-ok").count >= 3 })
        _ = editor.press(126, [])
        check("↑ in an empty editor brings the command back", editor.text.string == "echo editor-ok", editor.text.string)

        editor.setText("cat no", caret: 6)
        _ = editor.press(48, [])
        check("Tab completes a path", editor.text.string == "cat note.txt ", editor.text.string)
        _ = editor.press(36, [])
        check("the completed command runs", await until(10) { host.screenForCheck.contains("lantern") })

        editor.setText("two", caret: 3)
        _ = editor.press(36, [.shift])
        check("Shift+Enter makes a new line and sends nothing", editor.text.string == "two\n", editor.text.string.debugDescription)

        // A request with # that Claude Code left without an answer says so under the field.
        editor.setText("# list the files", caret: 16)
        editor.showSuggestion("# list the files", nil)
        let failed = Strings.shellSuggestFailed.replacingOccurrences(of: "**", with: "")
        check("a request with no answer says so", editor.hint.stringValue.hasPrefix(String(failed.prefix(20))),
              editor.hint.stringValue)
        editor.setText("", caret: 0)
        return id
    }

    // ---- Claude Code ----

    private static func claudeCode(_ host: TerminalHost, _ folder: String, _ picture: String) async -> String? {
        guard ClaudeLauncher.findClaude() != nil else {
            Log.write("self test: editor SKIP Claude Code: it is not installed")
            return nil
        }
        let id = host.startForCheck(in: folder, kind: .claude)
        let editor = host.editor
        // A temporary folder asks whether its files are trusted, with "No, exit" chosen. Claude Code
        // takes no keys for a moment after it draws the question, so the arrow waits.
        if await until(15, { host.screenForCheck.contains("Enter to confirm") }) {
            try? await Task.sleep(for: .seconds(3))
            host.openTerminal()?.send(txt: "\u{1B}[B")
            if await until(3, { host.screenForCheck.contains("❯ Yes") }) {
                host.openTerminal()?.send(txt: "\r")
            }
        }
        // The line under the field of Claude Code names Shift+Tab once it is ready for text.
        let ready = await until(30) { host.screenForCheck.contains("shift+tab") }
        check("Claude Code is ready", ready, ready ? "" : lastLines(host.screenForCheck, 12))
        try? await Task.sleep(for: .seconds(1))

        check("the editor is away in a session of Claude Code", !editor.wanted && editor.isHidden)
        _ = host.pressForCheck(5, shift: false)
        check("Cmd+G opens the editor", editor.wanted && editor.composing && !editor.isHidden)

        editor.setText("first line of the editor", caret: 24)
        _ = editor.press(36, [.shift])
        editor.text.insertPlain("second line of the editor")
        editor.textChanged()
        editor.take([picture])
        check("a dropped picture waits above the text", editor.draft.pictures.count == 1 && !editor.pictures.isHidden,
              editor.draft.pictures.first ?? "none")
        _ = editor.press(36, [])
        check("Enter closes the editor and sends nothing", !editor.wanted && editor.isHidden && editor.text.string.isEmpty)
        let shown = await until(5) {
            host.screenForCheck.contains("second line of the editor") && host.screenForCheck.contains("[Image")
        }
        check("the text and the picture stand in the line of Claude Code", shown, lastLines(host.screenForCheck, 25))

        _ = host.pressForCheck(5, shift: false)
        editor.setText("@not", caret: 4)
        let offered = await until(5) { editor.choices.isOpen }
        check("@ offers the files of the project", offered && editor.offered.contains("note.txt"),
              editor.offered.joined(separator: ","))
        _ = editor.press(53, [])
        check("the first Esc closes the list and keeps the editor", !editor.choices.isOpen && editor.wanted)
        _ = editor.press(53, [])
        check("the second Esc closes the editor", !editor.wanted)

        // The voice key with voice input off: the line says so, and the editor opens to show it.
        var off = Store.settings()
        off.voiceInput = false
        host.voice?.use(off)
        editor.pressVoice()
        check("the voice key with voice off opens the editor and says so",
              editor.wanted && !editor.isHidden && editor.shownNotice == .turnedOff, "\(editor.shownNotice)")
        host.voice?.use(Store.settings())
        if editor.wanted { editor.hide() }

        // The line of Claude Code is cleared and the program is left; the pictures of the check go.
        host.openTerminal()?.send(txt: "\u{03}")
        try? await Task.sleep(for: .milliseconds(300))
        host.openTerminal()?.send(txt: "\u{03}")
        try? await Task.sleep(for: .milliseconds(500))
        return id
    }

    // ---- small parts ----

    private static func check(_ what: String, _ ok: Bool, _ seen: String = "") {
        Log.write("self test: editor \(ok ? "ok" : "FAIL") \(what)\(seen.isEmpty ? "" : " [\(seen)]")")
    }

    private static func until(_ seconds: Double, _ done: @MainActor () -> Bool) async -> Bool {
        let end = Date().addingTimeInterval(seconds)
        while Date() < end {
            if done() { return true }
            try? await Task.sleep(for: .milliseconds(150))
        }
        return done()
    }

    /// The last lines of the screen with words on them, for a failed check.
    private static func lastLines(_ screen: String, _ count: Int = 4) -> String {
        screen.split(separator: "\n").map { $0.trimmingCharacters(in: .whitespaces) }.filter { !$0.isEmpty }
            .suffix(count).joined(separator: " | ")
    }

    /// A small red picture, as a person would drop one.
    static func writeRedPicture(_ path: String) {
        guard let space = CGColorSpace(name: CGColorSpace.sRGB),
              let context = CGContext(data: nil, width: 64, height: 64, bitsPerComponent: 8, bytesPerRow: 0, space: space,
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return }
        context.setFillColor(CGColor(red: 0.9, green: 0.1, blue: 0.1, alpha: 1))
        context.fill(CGRect(x: 0, y: 0, width: 64, height: 64))
        guard let image = context.makeImage() else { return }
        _ = TerminalPaste.save(image, folder: (path as NSString).deletingLastPathComponent).map { saved in
            try? FileManager.default.moveItem(atPath: saved, toPath: path)
        }
    }
}
