import Foundation

/// What a new version brings, as the General page lists it under "a newer version is out" (D-397).
///
/// The notes of a release on GitHub hold both languages: the English part of CHANGELOG.md, a line
/// that says the Russian part starts, and the part of CHANGELOG.ru.md under the same heading. A
/// point of the notes starts with its title in bold; the list shows the titles only, in order.
/// The twin of ReleaseNotes.cs.
public enum ReleaseNotes {
    /// The line between the two languages in the notes of a release.
    public static let russianMark = "<!-- ru -->"

    /// More than this reads as a changelog, not as "what is new".
    public static let mostTitles = 8

    /// The titles from the JSON GitHub gives for a release, in the language asked for. Nothing when
    /// the answer has no notes.
    public static func fromGitHubJson(_ json: String, russian: Bool) -> [String] {
        guard let body = JsonNode.parse(json)?.objectValue?["body"]?.stringValue else {
            return []
        }
        return titles(body, russian: russian)
    }

    /// The titles of the points in one language. Notes without a Russian part give the English
    /// titles in Russian too: an English list is better than none.
    public static func titles(_ notes: String, russian: Bool) -> [String] {
        let part: Substring
        if let mark = notes.range(of: russianMark) {
            part = russian ? notes[mark.upperBound...] : notes[..<mark.lowerBound]
        } else {
            part = Substring(notes)
        }

        var titles: [String] = []
        for raw in part.split(separator: "\n", omittingEmptySubsequences: false) {
            let line = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            guard line.hasPrefix("- **") else { continue }

            let rest = line.dropFirst(4)
            guard let end = rest.range(of: "**") else { continue }

            var title = rest[..<end.lowerBound].trimmingCharacters(in: .whitespacesAndNewlines)
            while title.hasSuffix(".") { title.removeLast() }
            title = title.trimmingCharacters(in: .whitespacesAndNewlines)
            if !title.isEmpty {
                titles.append(title)
            }

            if titles.count == mostTitles {
                break
            }
        }

        return titles
    }
}
