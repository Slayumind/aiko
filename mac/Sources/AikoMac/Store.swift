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
        AppSettings.fromJson(read(folders.settingsFile))
    }

    static func environments() -> EnvironmentSettings {
        EnvironmentSettings.fromJson(read(folders.environmentsFile))
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
                signedIn: FileManager.default.fileExists(
                    atPath: ClaudeInstall.credentialsPathIn(.macOS, directory)))
        }

        return found
    }

    /// Which account a Claude Code folder belongs to. Only .claude.json is opened, never the
    /// credentials file, and nothing read here goes into the log.
    private static func account(in directory: String) -> ClaudeAccount {
        for path in ClaudeConfigFolder.accountFileCandidates(.macOS, directory, home) {
            let account = ClaudeAccount.fromClaudeJson(read(path))
            if account.isKnown {
                return account
            }
        }

        return .none
    }

    private static func read(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }

    /// Written through a temporary file: a power cut in the middle of a save would otherwise leave
    /// half a file, and half a settings file reads as no settings at all.
    private static func write(_ path: String, _ text: String) {
        let folder = (path as NSString).deletingLastPathComponent
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)

        let temporary = "\(path).\(ProcessInfo.processInfo.processIdentifier).tmp"
        guard (try? text.write(toFile: temporary, atomically: false, encoding: .utf8)) != nil else {
            return
        }

        if rename(temporary, path) != 0 {
            try? FileManager.default.removeItem(atPath: temporary)
        }
    }
}
