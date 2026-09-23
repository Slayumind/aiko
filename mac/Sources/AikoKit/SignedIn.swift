import Foundation

/// Whether a Claude Code folder has an account signed in.
///
/// Windows: Claude Code writes `.credentials.json` next to its settings, and the file being there
/// is the answer. macOS: it keeps the same token in the Keychain instead, so that file is never
/// written (checked on macOS 15.7, 2026-09-18, with two signed-in folders and no file in either).
/// Reading the Keychain is not something Aiko will do — it would be storing somebody else's token
/// by another name — so on macOS the answer comes from the account block Aiko already reads for
/// the plan, in `.claude.json`.
///
/// It is a weaker signal: a folder whose token expired still names its account. That is the right
/// way round for what it drives. A green dot that is a little optimistic costs a person one look
/// at Claude Code; a checklist that can never say "connected" would stop them setting Aiko up at
/// all, which is what the first live run on a Mac ran into.
public enum SignedIn {
    /// Whether this system tells by the file at all.
    public static func byFile(_ platform: PlatformConventions) -> Bool {
        platform == .windows
    }

    public static func decide(
        _ platform: PlatformConventions, credentialsFileExists: Bool, account: ClaudeAccount
    ) -> Bool {
        byFile(platform) ? credentialsFileExists : account.isKnown
    }
}
