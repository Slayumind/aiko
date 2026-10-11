import AikoKit
import Foundation

/// The jobs Claude Code starts the bridge for. What to write and when is decided in `BridgeWork`
/// and `MemoryBridge`; this only reads the input, the environment and the files it needs.
enum Bridge {
    static func run(_ arguments: [String]) -> Int32 {
        let mode = BridgeMode.forArguments(arguments)

        // Decided before any input is read: Claude Code sends nothing on stdin for the plugin, so
        // waiting for input that never comes would hang the install. Unlike the status line, a
        // failure here must show as one.
        if mode == .personaPlugin {
            return buildPersonaPlugin()
        }

        // The memory server talks over stdin for as long as the session lives. Reading the input
        // to its end, as every other job does, would wait for the session to close before the
        // first answer.
        if mode == .memory(.serve) {
            return serveMemory()
        }

        let input = readAllInput()
        switch mode {
        case .memory(let job): answerMemoryHook(job, input)
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

    // ---- the memory ----

    /// The memory hooks of the persona plugin. The rules and the facts go to the model at the
    /// start of a session; a message of the person refreshes facts by their keys and may bring a
    /// reminder. A hook that cannot read, lock or write prints nothing, like every other hook.
    private static func answerMemoryHook(_ job: MemoryBridgeMode, _ input: String) {
        let path = AikoFolders.forThisMac().memoryFile

        switch job {
        case .rules:
            write(MemoryBridge.rulesOutput(today()))
        case .facts:
            // No file is an empty memory and is still printed. A file that is there and cannot
            // be opened prints nothing: an empty list would tell the model the memory is gone.
            do {
                write(MemoryBridge.factsOutput(try Disk.readMemory(path)))
            } catch {
            }
        case .prompt, .serve:
            let step = try? Disk.withLock(lockFile(path)) {
                let step = MemoryBridge.onPrompt(input, try Disk.readMemory(path), today())
                if let json = step.memoryJson {
                    try Disk.writeMemory(path, json, keepBadFile: step.badFile)
                }
                return step
            }

            if let output = step?.output {
                write(output)
            }
        }
    }

    /// The MCP server with the memory tools: one JSON-RPC message per line, until Claude Code
    /// closes the input. The file is read again for every message, because hooks of other
    /// sessions and the app write it too.
    private static func serveMemory() -> Int32 {
        let version = serverVersion()

        while let line = readLine(strippingNewline: true) {
            let blank = line.allSatisfy { $0.isWhitespace }
            if !blank, let reply = answerServerLine(line, version) {
                write(reply + "\n")
            }
        }

        return 0
    }

    /// A change that could not be written is answered as a failed tool call: the model must not
    /// read "Saved" for a fact that is not in the file.
    private static func answerServerLine(_ line: String, _ version: String) -> String? {
        let path = AikoFolders.forThisMac().memoryFile

        do {
            return try Disk.withLock(lockFile(path)) {
                let step = MemoryBridge.onServerLine(line, try Disk.readMemory(path), today(), version)
                if let json = step.memoryJson {
                    try Disk.writeMemory(path, json, keepBadFile: step.badFile)
                }
                return step.output
            }
        } catch {
            return MemoryBridge.writeFailedReply(line)
        }
    }

    private static func lockFile(_ memoryFile: String) -> String {
        memoryFile + ".lock"
    }

    /// The day on the clock of this Mac, in the calendar the file is written in whatever calendar
    /// the Mac itself is set to.
    private static func today() -> DateOnly {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .current
        let day = calendar.dateComponents([.year, .month, .day], from: Date())
        return DateOnly(day.year ?? 1, day.month ?? 1, day.day ?? 1)
    }

    /// Three numbers from the Info.plist of the app the bridge ships in. A bridge run straight
    /// out of .build has no bundle and says 0.0.0, as the Windows one does without a version.
    private static func serverVersion() -> String {
        let text = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? ""
        let parts = text.split(separator: ".").prefix(3)
        return parts.count == 3 ? parts.joined(separator: ".") : "0.0.0"
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
