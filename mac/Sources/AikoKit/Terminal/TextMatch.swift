import Foundation

/// A piece of a line in a list that is searched: a match is drawn marked.
public struct TextPart: Sendable, Equatable {
    public let text: String
    public let match: Bool

    public init(_ text: String, _ match: Bool) {
        self.text = text
        self.match = match
    }
}

/// How the lists of the terminal match what the person types (D-346): as one piece of text, with
/// no care for the case of letters and for ё against е. Places are UTF-16 units, as in C#.
///
/// The twin of TextMatch in TextMatch.cs.
public enum TextMatch {
    /// The text cut into parts at every match, or nil when nothing matches. An empty query
    /// matches nothing here: the caller decides what an empty query shows.
    public static func parts(_ text: String, _ query: String) -> [TextPart]? {
        let wanted = Array(oneLine(query).utf16)
        guard !wanted.isEmpty else {
            return nil
        }

        let units = Array(text.utf16)
        var parts: [TextPart] = []
        var from = 0
        var at = indexOf(units, wanted, from: 0)
        while at >= 0 {
            if at > from {
                parts.append(TextPart(string(units[from..<at]), false))
            }
            parts.append(TextPart(string(units[at..<(at + wanted.count)]), true))
            from = at + wanted.count
            at = indexOf(units, wanted, from: from)
        }

        guard !parts.isEmpty else {
            return nil
        }

        if from < units.count {
            parts.append(TextPart(string(units[from...]), false))
        }
        return parts
    }

    /// Where the query stands in the text, letter for letter after folding, or -1.
    public static func indexOf(_ text: [UInt16], _ wanted: [UInt16], from: Int) -> Int {
        var at = from
        while at + wanted.count <= text.count {
            if (0..<wanted.count).allSatisfy({ fold(text[at + $0]) == fold(wanted[$0]) }) {
                return at
            }
            at += 1
        }
        return -1
    }

    /// The text on one line: every run of spaces and line breaks is one space.
    public static func oneLine(_ text: String) -> String {
        text.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    /// char.ToLowerInvariant for one UTF-16 unit, then ё as е.
    private static func fold(_ unit: UInt16) -> UInt16 {
        guard let scalar = Unicode.Scalar(unit) else {
            return unit
        }

        let lower = Array(String(scalar).lowercased().utf16)
        let folded = lower.count == 1 ? lower[0] : unit
        return folded == 0x0451 ? 0x0435 : folded
    }

    private static func string<S: Sequence>(_ units: S) -> String where S.Element == UInt16 {
        String(decoding: Array(units), as: UTF16.self)
    }
}

/// A folder the quick pick offers for a new session (D-350): its name with the match marked, and
/// the folder it is in.
public struct FolderHit: Sendable, Equatable {
    public let folder: String
    public let name: [TextPart]
    public let parent: String
}

/// The quick pick of a folder: the folders the sessions of an environment ran in, the latest used
/// first, closed sessions counted. Other folders, such as those of conversations started outside
/// Aiko, may be added with the time they were used.
///
/// The twin of FolderPick in TextMatch.cs.
public enum FolderPick {
    /// How many folders the pick shows at most.
    public static let limit = 20

    public static func find(
        _ sessions: TerminalSessions, environment: String, query: String, others: [(String, Int64)] = []
    ) -> [FolderHit] {
        var used: [String: Int64] = [:]
        var order: [String] = []
        func note(_ folder: String, _ lastUsed: Int64) {
            let place = SessionName.folderPath(folder)
            guard !place.isEmpty else { return }
            let key = place.lowercased()
            if let known = used[key] {
                used[key] = max(known, lastUsed)
                return
            }
            used[key] = lastUsed
            order.append(place)
        }

        for session in sessions.sessions where session.environment == environment {
            note(session.folder, session.lastUsed)
        }
        for (folder, lastUsed) in others {
            note(folder, lastUsed)
        }

        // The order is stable: of two folders used at one moment, the one seen first stays higher.
        let latest = order.enumerated()
            .sorted { left, right in
                let a = used[left.element.lowercased()] ?? 0
                let b = used[right.element.lowercased()] ?? 0
                return a != b ? a > b : left.offset < right.offset
            }
            .map(\.element)

        let wanted = TextMatch.oneLine(query)
        if wanted.isEmpty {
            return latest.prefix(limit).map { hit($0, [TextPart(SessionName.folderName($0), false)]) }
        }

        // A match in the name of the folder comes before a match only in the path above it.
        let byName = latest.compactMap { folder in
            TextMatch.parts(SessionName.folderName(folder), wanted).map { hit(folder, $0) }
        }
        let byPath = latest
            .filter { TextMatch.parts(SessionName.folderName($0), wanted) == nil && TextMatch.parts($0, wanted) != nil }
            .map { hit($0, [TextPart(SessionName.folderName($0), false)]) }

        return Array((byName + byPath).prefix(limit))
    }

    private static func hit(_ folder: String, _ name: [TextPart]) -> FolderHit {
        FolderHit(folder: folder, name: name, parent: SessionName.parentName(folder))
    }
}
