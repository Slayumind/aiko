import AikoKit
import Foundation

/// What a newer version brings, from the notes of its release on GitHub (D-397). Asked only once a
/// newer version is known, through the same guard of allowed hosts as the files of an update. The
/// request carries nothing about the person. The twin of ReleaseNotesRun.cs.
@MainActor
enum ReleaseNotesRun {
    private static let releases = "https://api.github.com/repos/Slayumind/aiko/releases/tags/v"

    /// The titles already read, by version and language: the page opens many times a day.
    private static var known: [String: [String]] = [:]

    static func titles(for version: String) async -> [String] {
        let russian = Strings.language == .russian
        let key = version + (russian ? "/ru" : "/en")
        if let titles = known[key] {
            return titles
        }

        let path = version.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? version
        guard let answer = await AikoHttp.get(releases + path, timeout: 5) else {
            // No list is the honest answer: the line "a newer version is out" stands without it.
            Log.write("release notes: no answer")
            return []
        }
        guard answer.status == 200 else {
            Log.write("release notes: GitHub answered \(answer.status)")
            return []
        }

        let titles = ReleaseNotes.fromGitHubJson(answer.body, russian: russian)
        known[key] = titles
        return titles
    }
}
