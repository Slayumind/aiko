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
        let loaderAtStart = host.loaderShownForCheck
        hold(host, ProcessInfo.processInfo.environment["AIKO_TRY_VIEW"])
        let seconds = Double(ProcessInfo.processInfo.environment["AIKO_TRY_SECONDS"] ?? "") ?? 2
        after(seconds) {
            Log.write("self test: terminal window shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                + "menu bar icon=\(iconShown()), terminals=\(host.terminalCountForCheck), running=\(host.runningForCheck)")
            Log.write("self test: terminal window \(host.barForCheck)")
            Log.write("self test: terminal window \(host.rowsForCheck)")
            Log.write("self test: terminal window loader: at the start \(loaderAtStart), after the first letter \(host.loaderShownForCheck)")
            host.showHeaderForCheck(true)
            Log.write("self test: terminal window \(host.headerForCheck)")
            host.showHeaderForCheck(false)
            narrow(host)
            places(host)
            keys(host)
            tips()
            copyWhole()
            dragRows(host, folder) {
                host.closeForCheck()
                after(0.5) {
                    Log.write("self test: terminal window closed, shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                        + "menu bar icon=\(iconShown()), terminals still running=\(host.runningForCheck)")
                    quitAndEnd(host, session)
                }
            }
        }
    }

    /// A session dragged down past the two below it lands at the end of its folder (D-400), with
    /// real mouse events; a press that does not move is still a click. The two extra command lines
    /// close again after.
    private static func dragRows(_ host: TerminalHost, _ folder: String, then: @escaping @MainActor () -> Void) {
        // After the keys step has looked at its search: two shells starting at once keep the main
        // thread busy, and the search would open after that step had closed it.
        after(0.6) {
            let first = host.rowOrderForCheck(folder).first
            let second = host.startForCheck(in: folder)
            let third = host.startForCheck(in: folder)
            after(0.8) { dragFirst(host, folder, first, [second, third].compactMap { $0 }, then) }
        }
    }

    private static func dragFirst(
        _ host: TerminalHost, _ folder: String, _ first: String?, _ extras: [String],
        _ then: @escaping @MainActor () -> Void
    ) {
        let before = host.rowOrderForCheck(folder)
        guard let first, before.count == 3, let height = host.state.rowFramesForCheck[first]?.height else {
            Log.write("self test: terminal window drag: FAIL no three rows (\(before.count))")
            then()
            return
        }
        host.dragRowForCheck(first, down: height * 2.2 * host.zoom) {
            let now = host.rowOrderForCheck(folder)
            let landed = now == [before[1], before[2], first]
            Log.write("self test: terminal window drag: the first row lands at the end=\(landed) "
                + "(\(before.map { String($0.prefix(4)) }) -> \(now.map { String($0.prefix(4)) })), "
                + "nothing written=\(host.changingNothing)")
            // A press that does not move is still a click: it opens the row.
            let other = before[1]
            host.dragRowForCheck(other, down: 0) {
                Log.write("self test: terminal window drag: a press without a move opens the row=\(host.state.chosen == other)")
                for extra in extras { host.closeSession(extra) }
                after(0.5) { then() }
            }
        }
    }

    /// Quitting with a program running asks first, and "Don't quit" leaves everything as it was. A
    /// closed session ends with all it started: a sleep run in the background of the command line is
    /// gone with it (D-351).
    private static func quitAndEnd(_ host: TerminalHost, _ session: String?) {
        var answered: Bool?
        host.askToStop(.quit) { answered = $0 }
        let asked = host.askingForCheck == .quit
        host.answer(false)
        let kept = answered == false && host.askingForCheck == nil && host.runningForCheck

        guard let session else {
            Log.write("self test: terminal window quit: no session to end")
            NSApp.terminate(nil)
            return
        }
        // A step of the settings: the settings step aside so the question is seen, nothing runs
        // before the answer, and "no" gives the same page back (D-351).
        host.openSettings(at: .general)
        var ran = 0
        host.askBefore(.remove) { ran += 1 }
        let settingsAsked = host.askingForCheck == .remove && !host.settingsOpenForCheck && ran == 0
        host.answer(false)
        let settingsBack = host.settingsPageForCheck == .general && ran == 0 && host.runningForCheck
        host.closeSettings()

        let leader = host.leaderForCheck(session)
        host.typeForCheck(session, "sleep 300 &\r")
        after(0.8) {
            // Its environment goes: only its sessions are asked about, and a yes closes them with
            // all they started.
            let before = ProcessTree.members(of: leader).count
            var removed = 0
            host.askToRemoveEnvironment(host.environmentForCheck(session) ?? "") { removed += 1 }
            let environmentAsked = host.askingForCheck == .removeEnvironment && removed == 0
            host.answer(true)
            after(ProcessTree.grace + 0.5) {
                let after = ProcessTree.members(of: leader).count
                Log.write("self test: terminal window quit: asked=\(asked), don't quit kept=\(kept), "
                    + "session processes \(before) before close, \(after) after")
                Log.write("self test: stop question: from the settings asked=\(settingsAsked), "
                    + "no gives the page back=\(settingsBack), environment asked=\(environmentAsked), "
                    + "removed once=\(removed == 1), session closed=\(host.closedForCheck(session)), "
                    + "settings back=\(host.settingsOpenForCheck)")
                NSApp.terminate(nil)
            }
        }
    }

    /// A saved place on a screen is where the window opens; a place no screen has any more gives the
    /// middle of the first screen (WindowPlace.fit). The place is not written: the self test saves
    /// nothing.
    private static func places(_ host: TerminalHost) {
        let saved = WindowPlace(left: 120, top: 140, width: 900, height: 600, maximized: false)
        let kept = host.placeForCheck(saved)
        let gone = host.placeForCheck(WindowPlace(left: 99_999, top: 99_999, width: 900, height: 600, maximized: false))
        _ = host.placeForCheck(nil)
        func describe(_ box: Box?) -> String {
            box.map { "\(Int($0.x)),\(Int($0.y)) \(Int($0.width))x\(Int($0.height))" } ?? "none"
        }
        Log.write("self test: terminal window place: saved 120,140 900x600 opens at \(describe(kept)), "
            + "a place off every screen opens at \(describe(gone))")
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

    /// Below 720 points the list leaves its column for the pop-in under its button, and the list
    /// button opens and closes it without touching the setting; wide again, the column is back
    /// (D-376).
    private static func narrow(_ host: TerminalHost) {
        let before = host.windowWidthForCheck
        let hiddenBefore = Store.settings().sessionListHidden
        host.resizeForCheck(width: 640)
        let narrow = host.state.narrow && host.listWidthForCheck == 0
        host.toggleList()
        let opened = host.state.listPopOpen && host.listPopShownForCheck
        host.choose(host.state.chosen ?? "")
        let closedByPick = !host.state.listPopOpen && !host.listPopShownForCheck
        let settingKept = Store.settings().sessionListHidden == hiddenBefore
        host.resizeForCheck(width: max(before, 1000))
        let wide = !host.state.narrow && (host.listWidthForCheck > 0) != hiddenBefore
        host.resizeForCheck(width: before)
        Log.write("self test: terminal window narrow: list leaves its column=\(narrow), the button opens it=\(opened), "
            + "a pick closes it=\(closedByPick), the setting kept=\(settingKept), wide again=\(wide)")
    }

    /// Three command lines in one folder, the first held half way down, for a picture of the drag.
    private static func holdDrag(_ host: TerminalHost) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-terminal")
        _ = host.startForCheck(in: folder)
        _ = host.startForCheck(in: folder)
        after(1) {
            guard let first = host.rowOrderForCheck(folder).first,
                  let height = host.state.rowFramesForCheck[first]?.height else { return }
            host.dragRowForCheck(first, down: height * 1.3 * host.zoom, letGo: false) {}
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
        case "narrow": host.resizeForCheck(width: 640)
        case "drag": holdDrag(host)
        case "update-dot": host.showUpdate(true)
        case "narrow-list":
            host.resizeForCheck(width: 640)
            host.toggleList()
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
