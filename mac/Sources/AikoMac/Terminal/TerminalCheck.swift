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
