import Foundation

/// The few things the core needs to do to a file on disk.
///
/// The core stays free of the file system itself, but the rules about somebody else's settings
/// file — copy it before the first change, never leave half a file behind — are rules, not
/// plumbing, and rules belong where a test can reach them. This is the narrow interface the
/// architecture allows for exactly that. IFileAccess on Windows is the same seam.
public protocol FileAccess {
    func exists(_ path: String) throws -> Bool

    func readAllText(_ path: String) throws -> String

    func writeAllText(_ path: String, _ text: String) throws

    func copy(_ from: String, _ to: String) throws

    /// Replaces the destination if it is already there.
    func move(_ from: String, _ to: String) throws

    func delete(_ path: String) throws
}

/// Why a change to the settings file did not happen. A reason, not a sentence: the words belong
/// to whichever layer is talking to a person, and the core has no language.
public enum PatchProblem: Sendable, Equatable {
    case none

    /// Aiko cannot say where its own bridge program is, so there is no command to write.
    case bridgeUnknown

    /// The file is there and Aiko may not write it.
    case couldNotWrite
}

/// What happened to the Claude Code settings file. The wizard shows this to the user, so a failure
/// has to name what went wrong rather than throw.
public struct PatchOutcome: Sendable, Equatable {
    public let changed: Bool
    public let problem: PatchProblem

    public init(changed: Bool, problem: PatchProblem) {
        self.changed = changed
        self.problem = problem
    }

    public static let nothingToDo = PatchOutcome(changed: false, problem: .none)

    public static let done = PatchOutcome(changed: true, problem: .none)

    public static func failed(_ problem: PatchProblem) -> PatchOutcome {
        PatchOutcome(changed: false, problem: problem)
    }
}

/// Adding and removing Aiko's line in the settings file of one Claude Code environment.
///
/// This is the most dangerous code in the product: it writes to a file that belongs to another
/// program, and removal rewrites one in every Claude Code folder on the machine. It lives here,
/// behind a narrow file interface, so every one of those paths can be tested without a real disk.
///
/// Three rules. Copy the file once, before the first change, because a second copy would save our
/// own edit and lose what the user had. Write through a temporary file, because a half written
/// settings file breaks Claude Code, not just Aiko. And never write at all when the patch changed
/// nothing.
public struct ClaudeSettingsEditor {
    private let files: FileAccess
    private let patch: SettingsJsonPatch

    public init(files: FileAccess, patch: SettingsJsonPatch) {
        self.files = files
        self.patch = patch
    }

    public static let fileName = "settings.json"
    public static let backupName = "settings.json.aiko-backup"

    public static func pathIn(_ configDirectory: String) -> String {
        join(configDirectory, fileName)
    }

    public static func backupPathIn(_ configDirectory: String) -> String {
        join(configDirectory, backupName)
    }

    public func add(_ configDirectory: String, _ command: String) -> PatchOutcome {
        if command.isEmpty {
            return .failed(.bridgeUnknown)
        }

        return change(configDirectory) { patch.addBridge($0, command) }
    }

    /// The session start hook that reminds about folder bindings. Added with the first binding,
    /// and taken out by remove together with the status line.
    public func addSessionHook(_ configDirectory: String, _ hookCommand: String) -> PatchOutcome {
        if hookCommand.isEmpty {
            return .failed(.bridgeUnknown)
        }

        return change(configDirectory) { patch.addSessionHook($0, hookCommand) }
    }

    /// Everything of Aiko's in the file: the status line, the hook, the plugins and the marketplace.
    public func remove(_ configDirectory: String) -> PatchOutcome {
        // A copy is only ever made of a file that was already there. No copy means Aiko made this
        // file itself, from nothing, the day it added its line.
        let aikoMadeTheFile = !((try? files.exists(ClaudeSettingsEditor.backupPathIn(configDirectory))) ?? false)

        let outcome = change(configDirectory) { json in
            let withoutBridge = patch.removeBridge(json)
            let withoutPlugins = patch.removePlugins(withoutBridge ?? json)
            return withoutPlugins ?? withoutBridge
        }

        if outcome.changed {
            // The copy was insurance while Aiko was in the file. Once nothing of ours is left,
            // leaving the copy behind would be litter in someone else's folder.
            if !hasAnyAikoEntries(configDirectory) {
                dropBackup(configDirectory)
            }

            // A file Aiko made and that now says nothing is litter too. Windows Sandbox showed it: a
            // folder with no settings.json before Aiko had one holding "{}" after it. Anything the
            // user or Claude Code wrote into it since keeps it alive.
            if aikoMadeTheFile {
                deleteIfEmpty(ClaudeSettingsEditor.pathIn(configDirectory))
            }
        }

        return outcome
    }

    /// After "claude plugin uninstall": the empty keys it leaves. The backup and the file stay.
    public func tidyAfterPluginRemoval(_ configDirectory: String) -> PatchOutcome {
        change(configDirectory) { patch.dropEmptyPluginKeys($0) }
    }

    /// Put our line right again, and only when it is already there.
    ///
    /// Aiko's own path changes on a reinstall, and the shell changes the day another one arrives.
    /// Either leaves a line of ours pointing somewhere useless. This fixes that without ever
    /// installing the line: somebody who answered "not now" in the wizard has to keep that answer,
    /// and a file with no line of ours in it is not ours to write to.
    public func repairIfOurs(_ configDirectory: String, _ command: String) -> PatchOutcome {
        if command.isEmpty {
            return .nothingToDo
        }

        return change(configDirectory) { json in
            guard patch.hasOurLine(json) else { return nil }
            return patch.addBridge(json, command)
        }
    }

    private func hasAnyAikoEntries(_ configDirectory: String) -> Bool {
        do {
            let path = ClaudeSettingsEditor.pathIn(configDirectory)
            return try files.exists(path) && patch.hasAnyAikoEntries(try files.readAllText(path))
        } catch {
            // Unreadable now: keeping the copy costs nothing.
            return true
        }
    }

    private func deleteIfEmpty(_ path: String) {
        do {
            if try files.exists(path), JsonNode.parse(try files.readAllText(path))?.objectValue?.isEmpty == true {
                try files.delete(path)
            }
        } catch {
        }
    }

    private func dropBackup(_ configDirectory: String) {
        do {
            let backup = ClaudeSettingsEditor.backupPathIn(configDirectory)
            if try files.exists(backup) {
                try files.delete(backup)
            }
        } catch {
        }
    }

    private func change(_ configDirectory: String, _ patching: (String) -> String?) -> PatchOutcome {
        let path = ClaudeSettingsEditor.pathIn(configDirectory)

        do {
            // Claude Code writes this file itself, but it may not exist yet on a fresh account.
            let json = try files.exists(path) ? try files.readAllText(path) : "{}"

            guard let patched = patching(json) else {
                // Nothing to change: our line is already right, or there was nothing to take out.
                return .nothingToDo
            }

            try backup(path, configDirectory)
            try writeAtomically(path, patched)
            return .done
        } catch {
            return .failed(.couldNotWrite)
        }
    }

    /// Made once, before the first change. A later copy would save our own edit over the original
    /// and lose what the user had.
    private func backup(_ path: String, _ configDirectory: String) throws {
        guard try files.exists(path) else {
            return
        }

        let backup = ClaudeSettingsEditor.backupPathIn(configDirectory)
        if try !files.exists(backup) {
            try files.copy(path, backup)
        }
    }

    private func writeAtomically(_ path: String, _ text: String) throws {
        let temporary = path + ".aiko.tmp"
        try files.writeAllText(temporary, text)
        try files.move(temporary, path)
    }

    private static func join(_ folder: String, _ name: String) -> String {
        folder.hasSuffix("/") || folder.hasSuffix("\\") ? folder + name : folder + "/" + name
    }
}
