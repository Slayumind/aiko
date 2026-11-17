import AikoKit
import Foundation

/// Reads the files Aiko keeps for itself and the files Claude Code keeps. The twin of
/// SettingsStore.cs and ClaudeAccounts.cs on Windows.
///
/// Nothing here throws. A settings file that cannot be read means the defaults, and Aiko starts.
enum Store {
    static let folders = AikoFolders.forThisMac()

    static var home: String { NSHomeDirectory() }

    static func settings() -> AppSettings {
        AppSettings.fromJson(readOwn(folders.settingsFile))
    }

    /// The settings file is written back whole, so a field this version does not know about would
    /// be lost. Only the island's place is saved from here, and only after the person moved it.
    static func saveSettings(_ settings: AppSettings) {
        write(folders.settingsFile, settings.toJson())
    }

    /// Which face Aiko wears (D-215). Read when a face is about to be drawn: the file is tiny and
    /// the settings window can change it while Aiko runs.
    static func persona() -> PersonaSettings {
        PersonaSettings.fromJson(readOwn(folders.personaFile) ?? "")
    }

    static func savePersona(_ persona: PersonaSettings) {
        write(folders.personaFile, persona.toJson())
    }

    /// The sessions of the terminal: names, folders and conversation ids, never a conversation.
    static func terminal() -> TerminalSessions {
        TerminalSessions.fromJson(readOwn(folders.terminalFile))
    }

    static func saveTerminal(_ sessions: TerminalSessions) {
        write(folders.terminalFile, sessions.toJson())
    }

    static func environments() -> EnvironmentSettings {
        EnvironmentSettings.fromJson(readOwn(folders.environmentsFile))
    }

    static func saveEnvironments(_ environments: EnvironmentSettings) {
        write(folders.environmentsFile, environments.toJson())
    }

    /// Plan and sign-in per environment. Read when the card opens, not on every new number:
    /// .claude.json can be large, and neither the plan nor the sign-in changes between two answers.
    static func accounts(_ environments: EnvironmentSettings) -> [String: CardAccount] {
        var found: [String: CardAccount] = [:]

        for environment in environments.environments {
            guard let directory = environment.configDirectories.first else { continue }

            found[environment.name] = CardAccount(
                plan: account(in: directory).planLabel,
                signedIn: isSignedIn(directory))
        }

        return found
    }

    /// Which account a Claude Code folder belongs to. Only .claude.json is opened, never the
    /// credentials file, and nothing read here goes into the log.
    static func account(in directory: String) -> ClaudeAccount {
        for path in ClaudeConfigFolder.accountFileCandidates(.macOS, directory, home) {
            let account = ClaudeAccount.fromClaudeJson(read(path))
            if account.isKnown {
                return account
            }
        }

        return .none
    }

    /// Whether this folder has an account. macOS keeps the token in the Keychain, so the rule is
    /// not the same as on Windows: see AikoKit.SignedIn.
    static func isSignedIn(_ directory: String) -> Bool {
        SignedIn.decide(
            .macOS,
            credentialsFileExists: FileManager.default.fileExists(
                atPath: ClaudeInstall.credentialsPathIn(.macOS, directory)),
            account: account(in: directory))
    }

    /// A file of Claude Code: read as it is, never moved or guarded.
    private static func read(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }

    /// A file of Aiko, by the rule of StoredFile (D-431): a spoiled one is set aside as .bad, and
    /// one that could not be read is kept from saves until it can be. Nil gives the defaults.
    private static func readOwn(_ path: String) -> String? {
        // The window, the menu bar and the island ask on every status line: a file that did not
        // change since the last read is not read again, as on Windows.
        let stamp = (try? FileManager.default.attributesOfItem(atPath: path)).map {
            Stamp(changed: $0[.modificationDate] as? Date ?? .distantPast, size: ($0[.size] as? NSNumber)?.int64Value ?? -1)
        }
        if let stamp, let copy = guardedCopies({ $0[path] }), copy.stamp == stamp {
            return copy.text
        }

        let exists = stamp != nil || FileManager.default.fileExists(atPath: path)
        let bytes = exists ? FileManager.default.contents(atPath: path) : nil
        let text = bytes.map { String(decoding: $0, as: UTF8.self) }
        let name = (path as NSString).lastPathComponent

        switch StoredFile.of(exists: exists, text: text) {
        case .unreadable:
            if guarded({ $0.insert(path).inserted }) {
                Log.write("\(name) could not be read: it is not written until it can be")
            }
            return nil
        case .spoiled:
            _ = guarded { $0.remove(path) }
            if rename(path, path + ".bad") == 0 {
                Log.write("\(name) could not be read and was kept as \(name).bad")
            }
            return nil
        case .missing, .read:
            _ = guarded { $0.remove(path) }
            if let stamp, let text {
                guardedCopies { $0[path] = (stamp, text) }
            }
            return text
        }
    }

    /// When a file was changed and how long it was, the way a copy is matched to its file.
    private struct Stamp: Equatable {
        let changed: Date
        let size: Int64
    }

    private nonisolated(unsafe) static var copies: [String: (stamp: Stamp, text: String)] = [:]

    private static func guardedCopies<T>(_ use: (inout [String: (stamp: Stamp, text: String)]) -> T) -> T {
        unreadLock.lock()
        defer { unreadLock.unlock() }
        return use(&copies)
    }

    /// The files of Aiko that could not be read the last time. The window, the menu bar and the
    /// bridge watch read from more than one thread, so the set has a lock.
    private nonisolated(unsafe) static var unread: Set<String> = []
    private static let unreadLock = NSLock()

    private static func guarded<T>(_ use: (inout Set<String>) -> T) -> T {
        unreadLock.lock()
        defer { unreadLock.unlock() }
        return use(&unread)
    }

    /// Written through a temporary file: a power cut in the middle of a save would otherwise leave
    /// half a file, and half a settings file reads as no settings at all.
    private static func write(_ path: String, _ text: String) {
        if guarded({ $0.contains(path) }) {
            Log.write("\((path as NSString).lastPathComponent) was not saved: it could not be read before")
            return
        }
        let folder = (path as NSString).deletingLastPathComponent
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)

        let temporary = "\(path).\(ProcessInfo.processInfo.processIdentifier).tmp"
        guard (try? text.write(toFile: temporary, atomically: false, encoding: .utf8)) != nil else {
            return
        }

        if rename(temporary, path) != 0 {
            try? FileManager.default.removeItem(atPath: temporary)
        }
        _ = guardedCopies { $0.removeValue(forKey: path) }
    }
}
