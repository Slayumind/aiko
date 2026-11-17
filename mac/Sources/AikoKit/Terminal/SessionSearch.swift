import Foundation

/// A conversation the palette found (D-347, D-353). The name is cut into parts so the match can be
/// marked. Snippet is the place in the first message that matched, when the name did not. Source
/// is where a conversation outside Aiko was started; nil for a session of Aiko.
///
/// The twin of SearchHit in SessionSearch.cs.
public struct SearchHit: Sendable, Equatable {
    public let id: String
    public let environment: String
    public let folder: String
    public let name: [TextPart]
    public let snippet: [TextPart]
    public let lastUsed: Int64
    public let source: ConversationSource?
}

/// What the palette found, in three groups: sessions in the list, closed ones, and conversations
/// started outside Aiko (D-364).
public struct SearchResult: Sendable, Equatable {
    public let open: [SearchHit]
    public let closed: [SearchHit]
    public let outside: [SearchHit]

    public var count: Int { open.count + closed.count + outside.count }
}

/// The palette of sessions (D-346, D-347, D-353): by the name and by the first message of the
/// person, in every environment. The conversations are every one Claude Code keeps in the folders
/// of the environments; one with the id of a session of Aiko is that session.
///
/// The twin of SessionSearch.cs.
public enum SessionSearch {
    /// How many closed sessions and outside conversations the palette shows at most, each.
    public static let moreLimit = 20

    /// How many of them an empty query shows: the last ones used.
    public static let recent = 5

    /// Characters of the message before the match, and in the whole snippet (UTF-16 units).
    private static let before = 24
    private static let snippetLength = 80

    public static func find(_ sessions: TerminalSessions, _ conversations: [Conversation], query: String) -> SearchResult {
        let wanted = TextMatch.oneLine(query)
        var known: [String: Conversation] = [:]
        var knownOrder: [String] = []
        for conversation in conversations where known[conversation.id] == nil {
            known[conversation.id] = conversation
            knownOrder.append(conversation.id)
        }

        var hits: [(hit: SearchHit, group: Int, byName: Bool, order: Int)] = []

        func consider(
            _ id: String, _ environment: String, _ folder: String, _ name: String, _ lastUsed: Int64,
            _ source: ConversationSource?, _ group: Int
        ) {
            let message = known[id].map { TextMatch.oneLine($0.firstMessage) } ?? ""
            func hit(_ parts: [TextPart]? = nil, _ snippet: [TextPart]? = nil) -> SearchHit {
                SearchHit(
                    id: id, environment: environment, folder: SessionName.folderName(folder),
                    name: parts ?? [TextPart(name, false)], snippet: snippet ?? [], lastUsed: lastUsed, source: source)
            }

            if wanted.isEmpty {
                hits.append((hit(), group, true, hits.count))
            } else if let parts = TextMatch.parts(name, wanted) {
                hits.append((hit(parts), group, true, hits.count))
            } else if let found = snippet(message, wanted) {
                hits.append((hit(nil, found), group, false, hits.count))
            }
        }

        for session in sessions.sessions {
            consider(session.id, session.environment, session.folder, session.name, session.lastUsed, nil, session.closed ? 1 : 0)
        }
        for id in knownOrder where sessions.find(id) == nil {
            if let conversation = known[id] {
                consider(
                    conversation.id, conversation.environment, conversation.folder, conversation.name,
                    conversation.lastUsed, conversation.source, 2)
            }
        }

        // The order is stable: of two used at one moment, the one seen first stays higher.
        func ordered(_ group: Int, _ limit: Int) -> [SearchHit] {
            let sorted = hits.filter { $0.group == group }.sorted { left, right in
                if left.byName != right.byName { return left.byName }
                if left.hit.lastUsed != right.hit.lastUsed { return left.hit.lastUsed > right.hit.lastUsed }
                return left.order < right.order
            }
            return Array(sorted.prefix(limit).map(\.hit))
        }

        let more = wanted.isEmpty ? recent : moreLimit
        return SearchResult(open: ordered(0, Int.max), closed: ordered(1, more), outside: ordered(2, more))
    }

    /// The place of the message around the first match: a few words before it and the rest up to
    /// the length of the snippet, cut on a space, with "…" where the message goes on.
    private static func snippet(_ message: String, _ wanted: String) -> [TextPart]? {
        let units = Array(message.utf16)
        let target = Array(wanted.utf16)
        let at = TextMatch.indexOf(units, target, from: 0)
        guard at >= 0 else {
            return nil
        }

        let space: UInt16 = 0x20
        var start = 0
        if at > before {
            let from = at - before
            let found = units[from...].firstIndex(of: space)
            start = found.map { $0 < at ? $0 + 1 : from } ?? from
        }

        var end = units.count
        if end - start > snippetLength {
            let limit = max(start + snippetLength, at + target.count)
            let last = min(limit, units.count - 1)
            let found = units[...last].lastIndex(of: space)
            end = found.map { $0 > at + target.count ? $0 : min(limit, units.count) } ?? min(limit, units.count)
        }

        var parts = TextMatch.parts(String(decoding: units[start..<end], as: UTF16.self), wanted) ?? []
        if start > 0 {
            parts.insert(TextPart("…", false), at: 0)
        }
        if end < units.count {
            parts.append(TextPart("…", false))
        }
        return joined(parts)
    }

    /// Neighbours that are both plain become one part.
    private static func joined(_ parts: [TextPart]) -> [TextPart] {
        var joined: [TextPart] = []
        for part in parts {
            if let last = joined.last, !last.match, !part.match {
                joined[joined.count - 1] = TextPart(last.text + part.text, false)
            } else {
                joined.append(part)
            }
        }
        return joined
    }
}
