import AikoKit
import Foundation

/// The four jobs Claude Code starts the bridge for. What to write and when is decided in
/// `BridgeWork`; this only reads the input, the environment and the files it needs.
enum Bridge {
    static func run(_ arguments: [String]) -> Int32 {
        let mode = BridgeMode.forArguments(arguments)

        // Decided before any input is read: Claude Code sends nothing on stdin for the plugin, so
        // waiting for input that never comes would hang the install. Unlike the status line, a
        // failure here must show as one.
        if mode == .personaPlugin {
            return buildPersonaPlugin()
        }

        let input = readAllInput()
        switch mode {
        case .sessionStart: remindAboutBinding(input)
        case .hook: recordActivity(input)
        default: runStatusLine(input)
        }

        return 0
    }

    /// Read the payload as bytes and drop a byte order mark. The same text goes on to the status
    /// line the user already had, and a mark in front of it would break that one too.
    private static func readAllInput() -> String {
        let data = FileHandle.standardInput.readDataToEndOfFile()
        var text = String(decoding: data, as: UTF8.self)
        if text.hasPrefix("\u{FEFF}") {
            text.removeFirst()
        }
        return text
    }

    // ---- the status line ----

    private static func runStatusLine(_ input: String) {
        let folders = AikoFolders.forThisMac()
        let folder = configDirectory(folders)
        let plan = BridgeWork.statusLine(
            folders: folders,
            configDirectory: folder,
            settingsJson: Disk.readIfThere(ClaudeSettingsEditor.pathIn(folder)),
            input: input,
            now: Date())

        if let snapshot = plan.snapshot {
            try? Disk.writeInOneStep(snapshot)
        }

        if let command = plan.wrappedCommand {
            write(WrappedStatusLine.output(command, input: input))
        }
    }

    // ---- the hooks of the persona plugin ----

    /// The session start hook. It prints a line only when a command overrode a folder binding, and
    /// nothing at all otherwise: an empty hook answer changes nothing in Claude Code.
    private static func remindAboutBinding(_ input: String) {
        let folders = AikoFolders.forThisMac()
        let environment = ProcessInfo.processInfo.environment

        let answer = BridgeWork.sessionStart(
            folders: folders,
            input: input,
            launch: environment[SessionReminder.launchVariable],
            runningEnvironment: environment[SessionReminder.environmentVariable],
            environmentsJson: Disk.readIfThere(folders.environmentsFile),
            settingsJson: Disk.readIfThere(folders.settingsFile),
            systemLanguageTag: Locale.preferredLanguages.first)

        if let answer {
            write(answer)
        }
    }

    /// One small file per session for the face in the tray (D-206).
    private static func recordActivity(_ input: String) {
        let folders = AikoFolders.forThisMac()
        let now = Date()

        let step = BridgeWork.activity(
            folders: folders,
            configDirectory: configDirectory(folders),
            input: input,
            readFile: Disk.readIfThere,
            now: now,
            offsetSeconds: TimeZone.current.secondsFromGMT(for: now))

        switch step {
        case .nothing:
            break
        case .remove(let path):
            try? FileManager.default.removeItem(atPath: path)
        case .write(let file):
            try? Disk.writeInOneStep(file)
        }
    }

    // ---- the persona plugin ----

    /// Writes the plugin folder for the environment this run belongs to and prints its path.
    /// Exit code 1 with a reason on stderr when that is not possible: Claude Code then keeps the
    /// version it already has.
    private static func buildPersonaPlugin() -> Int32 {
        do {
            let folders = AikoFolders.forThisMac()
            guard let bridge = Bundle.main.executablePath else {
                throw Failed("the bridge does not know its own path")
            }

            let build = BridgeWork.personaPlugin(
                folders: folders,
                configDirectory: configDirectory(folders),
                personaJson: Disk.readIfThere(folders.personaFile) ?? "",
                bridgePath: bridge)

            if !FileManager.default.fileExists(atPath: build.folder) {
                try Disk.writeFolderInOneStep(build.folder, build.files)
            }

            clearOldPluginFolders(build.folder)
            write(build.folder + "\n")
            return 0
        } catch {
            FileHandle.standardError.write(
                Data("Aiko could not build the persona plugin: \(error)\n".utf8))
            return 1
        }
    }

    /// Every other version older than an hour. A folder still being copied by another session is
    /// far younger than that (D-201).
    private static func clearOldPluginFolders(_ versionFolder: String) {
        let parent = (versionFolder as NSString).deletingLastPathComponent
        let current = (versionFolder as NSString).lastPathComponent
        let manager = FileManager.default

        let folders = (try? manager.contentsOfDirectory(atPath: parent))?
            .compactMap { name -> (name: String, written: Date)? in
                let attributes = try? manager.attributesOfItem(atPath: (parent as NSString).appendingPathComponent(name))
                guard let written = attributes?[.modificationDate] as? Date else { return nil }
                return (name, written)
            } ?? []

        for stale in PersonaPluginOutput.staleFolders(folders, currentHash: current, now: Date()) {
            // Busy or not ours to delete; the next run tries again.
            try? manager.removeItem(atPath: (parent as NSString).appendingPathComponent(stale))
        }
    }

    // ---- what this run belongs to ----

    /// Without the variable Claude Code works in ~/.claude, so the bridge does too.
    private static func configDirectory(_ folders: AikoFolders) -> String {
        ClaudeConfigFolder.resolve(
            folders.platform,
            ProcessInfo.processInfo.environment[ClaudeConfigFolder.variableName],
            NSHomeDirectory())
    }

    private static func write(_ text: String) {
        FileHandle.standardOutput.write(Data(text.utf8))
    }

    struct Failed: Error, CustomStringConvertible {
        let description: String

        init(_ description: String) {
            self.description = description
        }
    }
}
