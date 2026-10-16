import Foundation

/// The two keys of the field that mean more than a letter. Shift+Return is a new line and never
/// comes here.
public enum TalkKey: String, Sendable, Equatable {
    case enter
    case escape
}

/// What a key does. none leaves the key to the field.
public enum TalkKeyAction: String, Sendable, Equatable {
    case none
    /// The key is taken and nothing happens.
    case swallow
    case send
    case allow
    case deny
    case stop
    case cancelVoice = "cancel-voice"
}

/// What the field knows when a key comes. cardWaits: a card the keys answer is open. empty: no
/// words and no pictures. dictating: the words of a recording are in the field. voiceActive: a
/// recording warms up, listens or finishes.
public struct TalkKeyState: Sendable, Equatable {
    public let cardWaits: Bool
    public let empty: Bool
    public let busy: Bool
    public let dictating: Bool
    public let voiceActive: Bool

    public init(cardWaits: Bool, empty: Bool, busy: Bool, dictating: Bool = false, voiceActive: Bool = false) {
        self.cardWaits = cardWaits
        self.empty = empty
        self.busy = busy
        self.dictating = dictating
        self.voiceActive = voiceActive
    }
}

/// Enter and Esc in the field of a conversation (D-323). The twin of Terminal/TalkKeys.cs.
public enum TalkKeys {
    public static func action(_ key: TalkKey, _ state: TalkKeyState) -> TalkKeyAction {
        switch key {
        case .enter:
            // The text of a recording is never sent by itself (D-313), not even half way.
            if state.dictating { return .swallow }
            if state.cardWaits { return state.empty ? .allow : .swallow }
            return .send
        case .escape:
            if state.voiceActive { return .cancelVoice }
            if state.cardWaits { return .deny }
            return state.busy ? .stop : .none
        }
    }

    /// The card the keys answer: the oldest open card, when it asks for a permission or a plan.
    /// A card of questions has its own keys (AIKO-327), so with one open the keys answer nothing.
    public static func card(_ items: [FeedItem]) -> RequestCard? {
        for item in items {
            guard case .card(let request, .open) = item.content else { continue }
            return request.kind == .question ? nil : request
        }
        return nil
    }
}
