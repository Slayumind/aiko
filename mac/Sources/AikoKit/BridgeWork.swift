import Foundation

/// What one run of the bridge was asked to do.
///
/// Claude Code starts the bridge for four different jobs and tells them apart by the arguments.
/// The plugin job is decided before anything is read from stdin: Claude Code sends nothing there,
/// and waiting for input that never comes would hang the install (D-201).
///
/// On Windows the same choice sits inline in Aiko.Bridge/Program.cs.
public enum BridgeMode: Sendable, Equatable {
    /// "plugin aiko-persona": write the persona plugin and print its folder.
    case personaPlugin

    /// "--session-start": the hook that warns when a command overrode a folder binding.
    case sessionStart

    /// "hook": one session event for the face in the tray.
    case hook

    /// No argument we know: the status line.
    case statusLine

    public static func forArguments(_ arguments: [String]) -> BridgeMode {
        if PersonaPluginOutput.isRequest(arguments) {
            return .personaPlugin
        }

        switch arguments.first {
        case SessionReminder.argument: return .sessionStart
        case PersonaPlugin.hookArgument: return .hook
        default: return .statusLine
        }
    }
}

/// A file to write in one step: the bridge writes it beside the old one and moves it over.
public struct FileWrite: Sendable, Equatable {
    public let path: String
    public let text: String

    public init(path: String, text: String) {
        self.path = path
        self.text = text
    }
}

/// What a status line run has to do: a snapshot to write, and the command the user had before.
public struct StatusLinePlan: Sendable, Equatable {
    public let snapshot: FileWrite?

    /// The status line the user already had, kept aside in their settings file. The bridge runs it
    /// and prints its output, so nothing they built is lost.
    public let wrappedCommand: String?

    public init(snapshot: FileWrite?, wrappedCommand: String?) {
        self.snapshot = snapshot
        self.wrappedCommand = wrappedCommand
    }
}

/// What a hook event does to the activity file of its session.
public enum ActivityStep: Sendable, Equatable {
    /// Nothing changed that the face would notice.
    case nothing

    /// The session is over; its file goes away.
    case remove(path: String)

    case write(FileWrite)
}

/// The persona plugin folder to build and the files that go into it.
public struct PersonaPluginBuild: Sendable {
    public let folder: String
    public let files: [(path: String, content: String)]

    public init(folder: String, files: [(path: String, content: String)]) {
        self.folder = folder
        self.files = files
    }
}

/// The decisions of the bridge, with no disk and no processes.
///
/// The executable reads stdin, the environment and the files, and does what these answers say.
/// Every rule about what to write, where, and when to write nothing at all lives here, so a test
/// can reach it.
public enum BridgeWork {
    /// The status line. Never throws and never refuses: anything unexpected reads as "no data",
    /// and the user's own line still runs.
    public static func statusLine(
        folders: AikoFolders,
        configDirectory: String,
        settingsJson: String?,
        input: String,
        now: Date
    ) -> StatusLinePlan {
        let environment = SnapshotName.forConfigDirectory(configDirectory)
        let report = StatusLineReport.fromJson(input)

        var snapshot: FileWrite?
        if report.hasData {
            snapshot = FileWrite(
                path: folders.snapshotFile(environment),
                text: SnapshotFile.toJson(LimitSnapshot.fromStatusLine(environment, now, report)))
        }

        let wrapped = SettingsJsonPatch.readWrappedCommand(settingsJson ?? "")
        let command = (wrapped?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true) ? nil : wrapped
        return StatusLinePlan(snapshot: snapshot, wrappedCommand: command)
    }

    /// One session event. The file of a session is read back first, because the same activity is
    /// not written again every few seconds: PostToolUse comes after every tool (D-206).
    public static func activity(
        folders: AikoFolders,
        configDirectory: String,
        input: String,
        readFile: (String) -> String?,
        now: Date,
        offsetSeconds: Int
    ) -> ActivityStep {
        guard let hook = HookEvent.fromJson(input) else {
            return .nothing
        }

        let environment = SnapshotName.forConfigDirectory(configDirectory)
        let path = folders.platform.join(
            folders.activityFolder, ActivityRecord.fileName(environment, hook.sessionId))

        if hook.activity == .ended {
            return .remove(path: path)
        }

        let written = readFile(path).flatMap(ActivityRecord.fromJson)
        guard ActivityRecord.needsWrite(written, hook.activity, now) else {
            return .nothing
        }

        let record = ActivityRecord(
            environment: environment, activity: hook.activity, at: now, offsetSeconds: offsetSeconds)
        return .write(FileWrite(path: path, text: record.toJson()))
    }

    /// The session start hook. The answer is nil far more often than not: it only speaks when a
    /// command started an environment other than the one this folder is bound to (D-163).
    public static func sessionStart(
        folders: AikoFolders,
        input: String,
        launch: String?,
        runningEnvironment: String?,
        environmentsJson: String?,
        settingsJson: String?,
        systemLanguageTag: String?
    ) -> String? {
        guard let workingDirectory = SessionReminder.workingDirectoryIn(input) else {
            return nil
        }

        let language = AppSettings.fromJson(settingsJson).language
        let message = SessionReminder.messageFor(
            folders.platform,
            launch: launch,
            runningEnvironment: runningEnvironment,
            workingDirectory: workingDirectory,
            settings: EnvironmentSettings.fromJson(environmentsJson),
            russian: SessionReminder.isRussian(language, systemLanguageTag: systemLanguageTag))

        return message.map(SessionReminder.hookOutput)
    }

    /// The persona plugin for this environment. The folder is named after the hash of its content,
    /// so a changed persona lands in a new folder and never half-overwrites the one Claude Code
    /// may be copying right now (D-201).
    public static func personaPlugin(
        folders: AikoFolders,
        configDirectory: String,
        personaJson: String,
        bridgePath: String
    ) -> PersonaPluginBuild {
        let persona = PersonaSettings.fromJson(personaJson)
        let files = PersonaPlugin.files(persona.temperament, bridgePath)
        let folder = PersonaPluginOutput.versionFolder(
            folders, configDirectory, PersonaPlugin.contentHash(files))
        return PersonaPluginBuild(folder: folder, files: files)
    }
}
