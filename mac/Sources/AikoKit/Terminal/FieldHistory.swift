import Foundation

/// Where the arrows stand in what was sent from the field (D-341): at is the message shown, or
/// -1 while the field holds what the person is typing; draft is that text, kept while the arrows
/// walk. The twin of Terminal/FieldHistory.cs.
public struct FieldWalk: Sendable, Equatable {
    public static let none = FieldWalk(at: -1, draft: "")

    public var at: Int
    public var draft: String

    public init(at: Int, draft: String) {
        self.at = at
        self.draft = draft
    }

    public var walking: Bool { at >= 0 }
}

/// Up and Down through the messages sent from the field, as in a console: up is older, down is
/// newer, and past the newest the field gets back what was being typed.
public enum FieldHistory {
    /// The walk after one press of an arrow, and the field. A nil field: the arrow is not the
    /// walk's, it moves the caret as usual.
    public static func step(_ sent: [String], _ walk: FieldWalk, field: String, by step: Int) -> (walk: FieldWalk, field: String?) {
        if sent.isEmpty || (!walk.walking && step > 0) {
            return (walk, nil)
        }

        var from = walk.walking ? walk : FieldWalk(at: sent.count, draft: field)
        let next = from.at + step
        if next < 0 {
            // Up on the oldest stays on it, and the arrow is still the walk's.
            return (from, sent[0])
        }
        if next >= sent.count {
            return (.none, from.draft)
        }
        from.at = next
        return (from, sent[next])
    }

    /// What the field holds as its own text: the draft while the arrows show an old message.
    public static func draft(_ walk: FieldWalk, field: String) -> String {
        walk.walking ? walk.draft : field
    }
}
