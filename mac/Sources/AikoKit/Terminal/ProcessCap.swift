import Foundation

/// A conversation with a running Claude Code. Busy covers a turn, a background task and a request
/// that waits for the person.
public struct RunningTalk: Sendable, Equatable {
    public let id: String
    public let busy: Bool
    public let lastUsed: Int64

    public init(id: String, busy: Bool, lastUsed: Int64) {
        self.id = id
        self.busy = busy
        self.lastUsed = lastUsed
    }
}

/// No more than a few Claude Code processes at once (D-304). Each one holds memory, and a session
/// that only sits in the list does not need one: its conversation comes back from its file.
///
/// The twin of ProcessCap.cs.
public enum ProcessCap {
    public static let most = 4

    /// The session to stop before one more process starts, or nil. It is the one that does
    /// nothing and was used longest ago. When every process is busy none is stopped, and the new
    /// one starts over the cap: the work of the person is not ended for a number.
    public static func toStop(_ running: [RunningTalk], starting: String) -> String? {
        let others = running.filter { $0.id != starting }
        if others.count < most {
            return nil
        }

        var oldest: RunningTalk?
        for talk in others where !talk.busy && talk.lastUsed < (oldest?.lastUsed ?? Int64.max) {
            oldest = talk
        }
        return oldest?.id
    }
}
