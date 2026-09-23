import Foundation
import Testing

@testable import AikoKit

/// The list of addresses Aiko may open, and the promise that nothing else can be opened by
/// accident (D-005).
struct AllowedHostsTests {
    @Test(arguments: [
        "https://api.anthropic.com/api/oauth/usage",
        "https://slayumind.org/api/v1/aiko/version",
        "https://SLAYUMIND.ORG/api/v1/aiko/version",
        "https://api.github.com/repos/Slayumind/aiko/releases",
        "https://github.com/Slayumind/aiko/releases/download/v0.3.0/SHA256SUMS.txt",
        "https://release-assets.githubusercontent.com/github-production-release-asset/1",
        "https://objects.githubusercontent.com/github-production-release-asset/1",
    ])
    func theHostsOfAnUpdateAndOfTheLimitsAreAllowed(address: String) {
        #expect(AllowedHosts.allows(URL(string: address)))
    }

    /// GitHub is here since 0.3, when Aiko began downloading the files of an update itself. The
    /// list is what makes "and nothing else" true, so a host GitHub does not use is still refused.
    @Test(arguments: [
        "https://githubusercontent.com/file",
        "https://raw.githubusercontent.com/Slayumind/aiko/main/README.md",
        "https://codeload.github.com/Slayumind/aiko/zip/main",
    ])
    func aGitHubHostThatIsNotOnTheListIsRefused(address: String) {
        #expect(!AllowedHosts.allows(URL(string: address)))
    }

    /// The suffix trick is how such a list is usually defeated: a host that merely ends with an
    /// allowed name belongs to whoever registered it.
    @Test(arguments: [
        "https://api.anthropic.com.example.net/usage",
        "https://slayumind.org.attacker.tld/api",
        "https://notslayumind.org/api",
        "https://evil.example/slayumind.org",
    ])
    func aHostThatOnlyLooksLikeOneOfThemIsRefused(address: String) {
        #expect(!AllowedHosts.allows(URL(string: address)))
    }

    @Test(arguments: ["http://slayumind.org/api", "ftp://slayumind.org/api"])
    func anythingButHttpsIsRefused(address: String) {
        // The access token travels on one of these connections in direct mode.
        #expect(!AllowedHosts.allows(URL(string: address)))
    }

    @Test
    func nothingAndARelativeAddressAreRefused() {
        #expect(!AllowedHosts.allows(nil))
        #expect(!AllowedHosts.allows(URL(string: "/api/v1/aiko/version")))
    }

    @Test
    func aSubdomainOfAnAllowedHostIsStillNotThatHost() {
        #expect(!AllowedHosts.allows(URL(string: "https://cdn.slayumind.org/file")))
    }

    /// D-005 promised that unwanted hosts are cut off, not merely unused. A guard alone would not
    /// do it: one line makes a session of its own, and that is how both clients on Windows were
    /// written until 0.2.1. This test is what makes the promise hold, so it reads the source.
    @Test
    func everyHttpClientInTheAppIsMadeByTheFactory() throws {
        var made: [String] = []
        let sources = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // AikoKitTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // mac
            .appendingPathComponent("Sources")

        let files = FileManager.default.enumerator(atPath: sources.path)?.allObjects as? [String] ?? []
        for file in files where file.hasSuffix(".swift") && !file.hasSuffix("AikoHttp.swift") {
            let text = try String(contentsOf: sources.appendingPathComponent(file), encoding: .utf8)
            if text.contains("URLSession(") || text.contains("URLSession.shared") {
                made.append(file)
            }
        }

        #expect(made.isEmpty, "URLSession is made outside AikoHttp in: \(made.joined(separator: ", "))")
    }
}
