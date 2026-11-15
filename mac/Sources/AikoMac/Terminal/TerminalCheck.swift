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
        host.startForCheck(in: folder)
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
            after(0.5) {
                host.closeForCheck()
                after(0.5) {
                    Log.write("self test: terminal window closed, shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                        + "menu bar icon=\(iconShown()), terminals still running=\(host.runningForCheck)")
                    NSApp.terminate(nil)
                }
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
    /// folders, or the editor: editor-shell, editor-compose, editor-mention, editor-voice. Nothing is written: the
    /// header opens without the setting.
    private static func hold(_ host: TerminalHost, _ view: String?) {
        switch view {
        case "header": host.showHeaderForCheck(true)
        case "settings": host.openSettings(at: .general)
        case "search": host.openSessionPalette()
        case "folders": host.pickFolderQuickly()
        case "editor-shell", "editor-compose", "editor-mention": holdEditor(host, view ?? "")
        case "editor-voice": holdVoice(host)
        default: break
        }
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
