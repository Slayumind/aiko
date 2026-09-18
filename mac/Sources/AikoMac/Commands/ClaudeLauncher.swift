import AikoKit
import AppKit

/// Finds Claude Code and opens it in a Terminal window for one account folder.
///
/// Aiko never signs in itself: Anthropic does not allow other programs to offer Claude.ai login
/// (D-157). It opens Claude Code, and Claude Code asks the person to sign in in the browser.
///
/// The twin of Commands/ClaudeLauncher.cs. Windows starts claude.exe and Windows gives it a
/// console; macOS gives a GUI app no terminal at all, so Aiko writes a small script and `open`
/// hands it to Terminal (MacTerminal).
enum ClaudeLauncher {
    /// The PATH a terminal opened right now would get. Windows reads the two registry values;
    /// macOS has no such place, so this is our own PATH plus the folders an installer uses. The
    /// core then falls back to ~/.local/bin, which is where the native installer puts Claude Code.
    private static let extraFolders = ["/usr/local/bin", "/opt/homebrew/bin"]

    static func findClaude() -> String? {
        let path = ClaudeInstall.freshPath(
            .macOS,
            machinePath: ProcessInfo.processInfo.environment["PATH"],
            userPath: extraFolders.joined(separator: ":"),
            expand: { $0 })

        return ClaudeInstall.find(.macOS, freshPath: path, commandFolder: CommandFolder.folder,
                                  userProfile: Store.home, exists: isExecutable)
    }

    /// Opens Claude Code in a Terminal window of its own. The folder of environment 1 is used by
    /// leaving CLAUDE_CONFIG_DIR out, the way a plain claude would, never by pointing the variable
    /// at it.
    @discardableResult
    static func open(configFolder: String, workingDirectory: String) -> Bool {
        guard let claude = findClaude() else {
            Log.write("launch: claude not found")
            return false
        }

        let script = MacTerminal.script(
            claude: claude,
            configFolder: configFolder,
            workingDirectory: FileManager.default.fileExists(atPath: workingDirectory)
                ? workingDirectory : Store.home,
            isDefaultFolder: ClaudeConfigFolder.isDefault(.macOS, configFolder, Store.home))

        let path = (Store.folders.localFolder as NSString).appendingPathComponent(MacTerminal.scriptName)
        do {
            try FileManager.default.createDirectory(
                atPath: Store.folders.localFolder, withIntermediateDirectories: true)
            try script.write(toFile: path, atomically: true, encoding: .utf8)
            try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: path)
        } catch {
            Log.write("launch: could not write the terminal script (\(error.localizedDescription))")
            return false
        }

        let terminal = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.apple.Terminal")
        guard let terminal else {
            Log.write("launch: no Terminal on this Mac")
            return false
        }

        NSWorkspace.shared.open(
            [URL(fileURLWithPath: path)],
            withApplicationAt: terminal,
            configuration: NSWorkspace.OpenConfiguration()) { _, error in
                if let error {
                    Log.write("launch: could not open Terminal (\(error.localizedDescription))")
                }
            }

        Log.write("launch: Claude Code opened for \((configFolder as NSString).lastPathComponent)")
        return true
    }

    private static func isExecutable(_ path: String) -> Bool {
        FileManager.default.isExecutableFile(atPath: path)
    }
}

/// Waits for something that happens outside Aiko: Claude Code getting installed, or somebody
/// finishing the sign-in in the browser. It looks every two seconds and only while the checklist is
/// waiting; the moment the thing is there, or the window closes, the timer is gone.
@MainActor
final class Waiter {
    private var timer: Timer?

    /// Kept on the object rather than captured by the timer: a timer block is @Sendable, and a
    /// plain closure captured inside one is not.
    private let isDone: () -> Bool
    private let done: () -> Void

    private init(_ isDone: @escaping () -> Bool, _ done: @escaping () -> Void) {
        self.isDone = isDone
        self.done = done

        let timer = Timer(timeInterval: 2, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.look() }
        }

        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    private func look() {
        guard isDone() else { return }
        stop()
        done()
    }

    static func forClaude(_ installed: @escaping () -> Void) -> Waiter {
        Waiter({ ClaudeLauncher.findClaude() != nil }, installed)
    }

    /// Only whether the folder has an account, never what is in it. On macOS that is the account
    /// block of .claude.json, because the token itself is in the Keychain (AikoKit.SignedIn).
    static func forSignIn(_ configFolder: String, _ signedIn: @escaping () -> Void) -> Waiter {
        Waiter({ Store.isSignedIn(configFolder) }, signedIn)
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }
}

/// The Claude Code folders on this Mac: ~/.claude and every ~/.claude-something beside it.
/// The twin of ClaudeFolders.cs.
enum ClaudeFolders {
    static func find() -> [ClaudeFolder] {
        let manager = FileManager.default
        let home = Store.home
        let names = (try? manager.contentsOfDirectory(atPath: home)) ?? []

        return names
            .filter { $0 == ClaudeConfigFolder.defaultFolderName
                || $0.hasPrefix(ClaudeConfigFolder.defaultFolderName + "-")
                || $0.hasPrefix(ClaudeConfigFolder.defaultFolderName + "_") }
            .map { name in (name, (home as NSString).appendingPathComponent(name)) }
            .filter { isDirectory($0.1) }
            .map { name, path in
                ClaudeFolder(
                    path,
                    name,
                    hasCredentials: Store.isSignedIn(path),
                    lastUsed: lastUsed(path))
            }
    }

    private static func isDirectory(_ path: String) -> Bool {
        var directory: ObjCBool = false
        return FileManager.default.fileExists(atPath: path, isDirectory: &directory) && directory.boolValue
    }

    /// The account file is written on every session, so its time says when the folder was last in
    /// use. Only the time is read, never what is in it.
    private static func lastUsed(_ folder: String) -> Date? {
        let candidates = ClaudeConfigFolder.accountFileCandidates(.macOS, folder, Store.home) + [folder]
        return candidates
            .compactMap { try? FileManager.default.attributesOfItem(atPath: $0)[.modificationDate] as? Date }
            .max()
    }
}
