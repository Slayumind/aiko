import AikoKit
import AppKit

/// --try-terminal: the terminal window with a real command line, run over SSH with nobody at the
/// screen. It opens the window, starts a shell in a temporary folder, writes what it sees, closes
/// the window and quits. Nothing is saved: the sessions of the person stay as they were.
/// AIKO_TRY_SECONDS holds the window longer, so a snapshot can be taken of it.
@MainActor
enum TerminalCheck {
    /// iconShown: whether the menu bar icon is there. It steps aside while the window is open, as
    /// the tray icon of Windows does (D-410).
    static func run(_ host: TerminalHost, iconShown: @escaping @MainActor () -> Bool) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-terminal")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)

        host.show()
        let session = host.startForCheck(in: folder)
        hold(host, ProcessInfo.processInfo.environment["AIKO_TRY_VIEW"])
        let seconds = Double(ProcessInfo.processInfo.environment["AIKO_TRY_SECONDS"] ?? "") ?? 2
        after(seconds) {
            Log.write("self test: terminal window shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                + "menu bar icon=\(iconShown()), terminals=\(host.terminalCountForCheck), running=\(host.runningForCheck)")
            Log.write("self test: terminal window \(host.barForCheck)")
            Log.write("self test: terminal window \(host.rowsForCheck)")
            host.showHeaderForCheck(true)
            Log.write("self test: terminal window \(host.headerForCheck)")
            host.showHeaderForCheck(false)
            keys(host)
            tips()
            copyWhole()
            after(0.5) {
                host.closeForCheck()
                after(0.5) {
                    Log.write("self test: terminal window closed, shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                        + "menu bar icon=\(iconShown()), terminals still running=\(host.runningForCheck)")
                    quitAndEnd(host, session)
                }
            }
        }
    }

    /// Quitting with a program running asks first, and "Don't quit" leaves everything as it was. A
    /// closed session ends with all it started: a sleep run in the background of the command line is
    /// gone with it (D-351).
    private static func quitAndEnd(_ host: TerminalHost, _ session: String?) {
        var answered: Bool?
        host.askToQuit { answered = $0 }
        let asked = host.askingToQuitForCheck
        host.answer(false)
        let kept = answered == false && !host.askingToQuitForCheck && host.runningForCheck

        guard let session else {
            Log.write("self test: terminal window quit: no session to end")
            NSApp.terminate(nil)
            return
        }
        let leader = host.leaderForCheck(session)
        host.typeForCheck(session, "sleep 300 &\r")
        after(0.8) {
            let before = ProcessTree.members(of: leader).count
            host.closeSession(session)
            after(ProcessTree.grace + 0.5) {
                let after = ProcessTree.members(of: leader).count
                Log.write("self test: terminal window quit: asked=\(asked), don't quit kept=\(kept), "
                    + "session processes \(before) before close, \(after) after")
                NSApp.terminate(nil)
            }
        }
    }

    /// The keys of the window: each one opens what the table says, and the window is left as it was.
    /// The search waits up to 150 ms for the conversations before it shows, so it is looked at later.
    private static func keys(_ host: TerminalHost) {
        let folders = host.pressForCheck(17, shift: true)
        let foldersOpen = host.paletteOpen
        host.closePalette()
        let settings = host.pressForCheck(43, shift: false)
        let settingsOpen = host.settingsOpenForCheck
        host.closeSettings()
        let search = host.pressForCheck(40, shift: true)
        after(0.3) {
            let searchOpen = host.paletteOpen
            host.closePalette()
            Log.write("self test: terminal window keys: Cmd+Shift+K search=\(search && searchOpen), "
                + "Cmd+Shift+T folders=\(folders && foldersOpen), Cmd+, settings=\(settings && settingsOpen), "
                + "all closed=\(!host.paletteOpen && !host.settingsOpenForCheck)")
        }
    }

    /// AIKO_TRY_VIEW puts the window in one state for a snapshot: header, settings, search,
    /// folders, the editor (editor-shell, editor-compose, editor-mention, editor-voice) or stopped. Nothing is written: the
    /// header opens without the setting.
    private static func hold(_ host: TerminalHost, _ view: String?) {
        switch view {
        case "header": host.showHeaderForCheck(true)
        case "settings": host.openSettings(at: .general)
        case "search": host.openSessionPalette()
        case "folders": host.pickFolderQuickly()
        case "editor-shell", "editor-compose", "editor-mention": holdEditor(host, view ?? "")
        case "editor-voice": holdVoice(host)
        case "stopped": holdStopped(host)
        case "shell": holdShell(host)
        default: break
        }
    }

    /// A command line with three blocks, one failed and one long, and the buttons of the failed one.
    private static func holdShell(_ host: TerminalHost) {
        guard let id = host.model.sessionId, let pane = host.shells[id] else { return }
        for (at, command) in ["ls -la", "cat missing.txt", "seq 1 60"].enumerated() {
            after(1 + Double(at)) {
                host.editor.setText(command, caret: (command as NSString).length)
                _ = host.editor.press(36, [])
            }
        }
        after(4.5) { pane.blocks.hoverForCheck(1) }
    }

    /// Claude Code that ended by itself, with the bar to continue it: it starts in the folder of
    /// --try-editor and is stopped with two Ctrl+C.
    private static func holdStopped(_ host: TerminalHost) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-editor")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        host.startForCheck(in: folder, kind: .claude)
        after(5) { host.openTerminal()?.send(txt: "\u{03}") }
        after(5.5) { host.openTerminal()?.send(txt: "\u{03}") }
    }

    /// A recording in the editor of the command line, with AIKO_TRY_WAV playing in place of the
    /// microphone: the red button, the wave, "listening" and the words as they come.
    private static func holdVoice(_ host: TerminalHost) {
        guard let wav = ProcessInfo.processInfo.environment["AIKO_TRY_WAV"], let sound = FileSound.read(wav) else { return }
        Recording.source = { FileSound(sound) }
        var settings = Store.settings()
        settings.voiceInput = true
        settings.voiceEngine = .local
        host.voice?.use(settings)
        after(1) { _ = host.pressForCheck(49, shift: true) }
    }

    /// The editor with the words of the snapshots of Windows (TerminalSnapshot.cs). A session of
    /// Claude Code starts in the folder of --try-editor, which that check has trusted.
    private static func holdEditor(_ host: TerminalHost, _ view: String) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-editor")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        try? "lantern\n".write(toFile: (folder as NSString).appendingPathComponent("note.txt"), atomically: true, encoding: .utf8)
        let picture = (folder as NSString).appendingPathComponent("red.png")
        EditorCheck.writeRedPicture(picture)
        let editor = host.editor
        editor.checkWithoutFocus = true
        if view == "editor-shell" {
            editor.setText("git log --oneline -5", caret: 19)
            editor.take([picture, picture])
            return
        }

        host.startForCheck(in: folder, kind: .claude)
        after(4) {
            _ = host.pressForCheck(5, shift: false)
            if view == "editor-mention" {
                editor.setText("Посмотри @no", caret: 12)
            } else {
                editor.setText("Сначала проверь, что Esc в пустом редакторе доходит до карточки.\nПотом прогони тесты.", caret: 86)
                editor.take([picture])
            }
        }
    }

    /// A paragraph Claude Code wrapped by the width is copied whole (D-371). Claude Code breaks its
    /// text itself, with a line break and an indent under the start of the text, so a terminal of 40
    /// columns with no program gets the rows the way Claude Code prints them; the selection of all
    /// of them comes back as one line. The clipboard of the person is not touched.
    private static func copyWhole() {
        let columns = 40
        let terminal = AikoTerminalView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        terminal.getTerminal().resize(cols: columns, rows: 10)

        let words = (1...24).map { "word\($0)" }
        var rows: [String] = []
        var row = "● "
        for word in words {
            if row.count + word.count + 1 > columns, row.count > 2 {
                rows.append(row)
                row = "  "
            }
            row += (row.hasSuffix(" ") ? "" : " ") + word
        }
        rows.append(row)
        terminal.feed(text: rows.joined(separator: "\r\n") + "\r\n")

        terminal.selectAll(nil)
        let copied = terminal.copiedText()?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        Log.write("self test: terminal window copy: \(rows.count) rows from Claude Code, "
            + "copied whole=\(copied == "● " + words.joined(separator: " "))")
    }

    /// A short tip stands on one line, a long one wraps in 320 points, as on Windows.
    private static func tips() {
        let point = NSPoint(x: 400, y: 400)
        Tips.show(Strings.settings, at: point)
        let short = Tips.sizeForCheck
        Tips.show(String(repeating: Strings.terminalNotStartedBody + " ", count: 3), at: point)
        let long = Tips.sizeForCheck
        Tips.cancel()
        func describe(_ size: NSSize?) -> String { size.map { "\(Int($0.width))x\(Int($0.height))" } ?? "missing" }
        Log.write("self test: terminal window tips: short \(describe(short)), long \(describe(long))")
    }

    private static func after(_ seconds: Double, _ work: @escaping @MainActor () -> Void) {
        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) { MainActor.assumeIsolated(work) }
    }
}
