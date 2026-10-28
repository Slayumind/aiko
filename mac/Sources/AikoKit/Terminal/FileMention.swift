import Foundation

/// Where "@" stands in the field and what was typed after it. Positions are in UTF-16 units, as in C#.
public struct MentionQuery: Sendable, Equatable {
    public let from: Int
    public let text: String
}

/// The files that "@" offers from the folder of the session (AIKO-330, D-342). The twin of
/// FileMention.cs.
///
/// Paths are relative to the folder and use "/". A file whose own name has the typed letters goes
/// first, then a file whose name starts with them, then one in a shallow folder; a long path costs a
/// little. The scores follow Goose (MentionPopover.tsx; research of 2026-10-26).
public enum FileMention {
    /// The most lines the menu shows.
    public static let shown = 10

    /// The "@" word the caret stands in: "@" at the start or after a space, and no space between
    /// it and the caret. Nil when there is none.
    public static func query(_ text: String, caret: Int) -> MentionQuery? {
        let units = Array(text.utf16)
        guard caret >= 1, caret <= units.count else { return nil }
        // "@" in UTF-16.
        guard let at = units[0..<caret].lastIndex(of: 64) else { return nil }
        if at > 0, !isSpace(units[at - 1]) { return nil }
        let typed = Array(units[(at + 1)..<caret])
        if typed.contains(where: isSpace) { return nil }
        return MentionQuery(from: at, text: String(decoding: typed, as: UTF16.self))
    }

    /// The best files for what was typed, at most `shown`. With nothing typed, the shallow files first.
    public static func matches(_ paths: [String], query: String) -> [String] {
        paths
            .compactMap { path in score(path, query).map { (path: path, score: $0) } }
            .sorted { left, right in
                if left.score != right.score { return left.score > right.score }
                let leftLength = left.path.utf16.count, rightLength = right.path.utf16.count
                if leftLength != rightLength { return leftLength < rightLength }
                return left.path.lowercased().utf16.lexicographicallyPrecedes(right.path.lowercased().utf16)
            }
            .prefix(shown)
            .map(\.path)
    }

    /// The field after a file is chosen: the "@" word becomes "@path " and the caret stands after it.
    public static func chosen(_ text: String, query: MentionQuery, caret: Int, path: String) -> (text: String, caret: Int) {
        let units = Array(text.utf16)
        let word = Array(("@" + path + " ").utf16)
        // The whole "@" word goes, also the part of it after the caret.
        var end = caret
        while end < units.count && units[end] != 32 && units[end] != 10 && units[end] != 9 && units[end] != 13 {
            end += 1
        }
        var rest = Array(units[end...])
        if rest.first == 32 { rest.removeFirst() }
        let result = Array(units[..<query.from]) + word + rest
        return (String(decoding: result, as: UTF16.self), query.from + word.count)
    }

    /// The field after files are dropped or pasted into it: their paths at the caret, a path with
    /// a space in quotes, a space before them after a word and one after them, the caret after it.
    /// The caret counts UTF-16 units, as a text field does.
    public static func dropped(_ field: String, caret: Int, paths: [String]) -> (text: String, caret: Int) {
        guard !paths.isEmpty else { return (field, caret) }
        let units = Array(field.utf16)
        let afterWord = caret > 0 && ![32, 9, 10, 13].contains(units[caret - 1])
        let inserted = (afterWord ? " " : "") + paths.map { $0.contains(" ") ? "\"\($0)\"" : $0 }.joined(separator: " ") + " "
        let result = units[..<caret] + Array(inserted.utf16) + units[caret...]
        return (String(decoding: result, as: UTF16.self), caret + inserted.utf16.count)
    }

    /// The file name part of a path, for the menu.
    public static func name(_ path: String) -> String {
        path.split(separator: "/", omittingEmptySubsequences: false).last.map(String.init) ?? path
    }

    private static func isSpace(_ unit: UTF16.CodeUnit) -> Bool {
        Character(Unicode.Scalar(unit) ?? " ").isWhitespace
    }

    private static func score(_ path: String, _ query: String) -> Double? {
        let depth = path.filter { $0 == "/" }.count
        let shallow: Double = depth <= 1 ? 50 : depth == 2 ? 30 : depth == 3 ? 15 : 0
        let length = Double(path.utf16.count)
        if query.isEmpty {
            return shallow - 0.05 * length
        }

        let lowerPath = path.lowercased(), lowerQuery = query.lowercased()
        guard lowerPath.contains(lowerQuery) else { return nil }

        let lowerName = name(path).lowercased()
        var score = 20.0 + shallow - 0.05 * length
        if lowerName.contains(lowerQuery) { score += 25 }
        if lowerName.hasPrefix(lowerQuery) { score += 15 }
        return score
    }
}
