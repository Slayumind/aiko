import Foundation

/// When a change of a session plays the sound of "waits" (D-375, D-409). Notices are counts now
/// (SessionCounts): only a session that starts to wait has a sound. Rows: spec/cases/notices.
/// The twin of Notices.cs.
public enum Notices {
    /// before is nil for a terminal seen for the first time. sound: the setting of the sound.
    /// inFocus: the terminal window is on screen and has the keyboard. open: the change is in the
    /// session open in it, which the person already sees.
    public static func sounds(
        _ before: TerminalActivity?, _ now: TerminalActivity, sound: Bool, inFocus: Bool, open: Bool
    ) -> Bool {
        sound && now == .waiting && before != .waiting && !(inFocus && open)
    }
}

/// Which sign the menu bar icon and the Dock icon show (D-409).
public enum BadgeKind: String, Sendable, Equatable {
    case none
    /// The number of the sessions that wait for the person.
    case waiting
    /// The number of the sessions that stopped by themselves and were not opened yet.
    case stopped
    /// A dot: a session ended a turn the person has not seen yet.
    case done
}

/// The one sign on the icon. The text is empty for the dot.
public struct SessionBadge: Sendable, Equatable {
    public let kind: BadgeKind
    public let count: Int

    public static let none = SessionBadge(kind: .none, count: 0)

    /// The highest number with its own text: two digits do not fit a small icon.
    public static let most = 9

    public init(kind: BadgeKind, count: Int) {
        self.kind = kind
        self.count = count
    }

    public var text: String {
        switch kind {
        case .none, .done: return ""
        default: return count > Self.most ? "\(Self.most)+" : "\(count)"
        }
    }
}

/// How many sessions wait, ended a turn, stopped and work (D-364, D-409), and the waiting session a
/// click on the mark of the island opens. A count stays until the person opens the session, like an
/// unread chat in a messenger. Rows: spec/cases/notices. The twin of SessionCounts.cs.
public struct SessionCounts: Sendable, Equatable {
    public let working: Int
    public let waiting: Int
    public let done: Int
    public let stopped: Int
    public let firstWaiting: String?

    public static let none = SessionCounts(working: 0, waiting: 0, done: 0, stopped: 0, firstWaiting: nil)

    public init(working: Int, waiting: Int, done: Int, stopped: Int, firstWaiting: String?) {
        self.working = working
        self.waiting = waiting
        self.done = done
        self.stopped = stopped
        self.firstWaiting = firstWaiting
    }

    public var isEmpty: Bool { working == 0 && waiting == 0 && done == 0 && stopped == 0 }

    /// What waits has to be answered, what stopped has to be started again, and what is done only
    /// has to be read: the sign shows the most urgent of them.
    public var badge: SessionBadge {
        if waiting > 0 { return SessionBadge(kind: .waiting, count: waiting) }
        if stopped > 0 { return SessionBadge(kind: .stopped, count: stopped) }
        if done > 0 { return SessionBadge(kind: .done, count: done) }
        return .none
    }

    /// The running terminals in the order of the list, with what each one does. A stopped session
    /// the person has seen since it stopped comes here as idle (stoppedIsNews).
    public static func of(_ terminals: [(id: String, activity: TerminalActivity)]) -> SessionCounts {
        var working = 0
        var waiting = 0
        var done = 0
        var stopped = 0
        var first: String?
        for (id, activity) in terminals {
            switch activity {
            case .working: working += 1
            case .waiting:
                waiting += 1
                if first == nil { first = id }
            case .done: done += 1
            case .exited: stopped += 1
            default: break
            }
        }

        return SessionCounts(working: working, waiting: waiting, done: done, stopped: stopped, firstWaiting: first)
    }

    /// A stop is news until the person sees the session after it. Both times in Unix milliseconds;
    /// a session seen in the same millisecond counts as seen.
    public static func stoppedIsNews(stoppedAt: Int64, seenAt: Int64) -> Bool {
        stoppedAt > seenAt
    }
}
