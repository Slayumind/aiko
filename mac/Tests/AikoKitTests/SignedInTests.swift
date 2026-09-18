import Testing

@testable import AikoKit

struct SignedInTests {
    static let known = ClaudeAccount(
        plan: .pro, rateLimitTier: nil, email: "someone@example.com", organization: nil)

    @Test
    func windowsTellsByTheCredentialsFile() {
        #expect(SignedIn.byFile(.windows))
        #expect(SignedIn.decide(.windows, credentialsFileExists: true, account: .none))
        #expect(!SignedIn.decide(.windows, credentialsFileExists: false, account: Self.known))
    }

    /// macOS keeps the token in the Keychain, so the file is never there and the account block of
    /// .claude.json is the only thing Aiko may read.
    @Test
    func macOSTellsByTheAccountInClaudeJson() {
        #expect(!SignedIn.byFile(.macOS))
        #expect(SignedIn.decide(.macOS, credentialsFileExists: false, account: Self.known))
        #expect(!SignedIn.decide(.macOS, credentialsFileExists: false, account: .none))
    }

    /// A Mac that somehow has the file still answers from the account: one rule per system, not
    /// two that could disagree.
    @Test
    func aFileOnAMacDoesNotChangeTheAnswer() {
        #expect(!SignedIn.decide(.macOS, credentialsFileExists: true, account: .none))
    }
}
