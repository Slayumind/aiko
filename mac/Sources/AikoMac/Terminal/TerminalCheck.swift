import AikoKit
import AppKit

/// --try-terminal: the terminal window with a real command line, run over SSH with nobody at the
/// screen. It opens the window, starts a shell in a temporary folder, writes what it sees, closes
/// the window and quits. Nothing is saved: the sessions of the person stay as they were.
@MainActor
enum TerminalCheck {
    static func run(_ host: TerminalHost) {
        let folder = (NSTemporaryDirectory() as NSString).appendingPathComponent("aiko-try-terminal")
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)

        host.show()
        host.startForCheck(in: folder)
        after(2) {
            Log.write("self test: terminal window shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                + "terminals=\(host.terminalCountForCheck), running=\(host.runningForCheck)")
            host.closeForCheck()
            after(0.5) {
                Log.write("self test: terminal window closed, shown=\(host.isShown), in the Dock=\(NSApp.activationPolicy() == .regular), "
                    + "terminals still running=\(host.runningForCheck)")
                NSApp.terminate(nil)
            }
        }
    }

    private static func after(_ seconds: Double, _ work: @escaping @MainActor () -> Void) {
        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) { MainActor.assumeIsolated(work) }
    }
}
