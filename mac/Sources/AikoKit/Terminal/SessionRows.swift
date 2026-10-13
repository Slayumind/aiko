import Foundation

/// What the mark of a session in the list says (D-293, D-299).
public enum RowState: Sendable, Equatable {
    case working

    /// A request waits for the person.
    case waiting

    /// The turn ended and the person has not looked yet.
    case done

    /// The turn ended and the person has seen it. The row has no mark.
    case read

    /// No process runs for the session. The second line says when it was used last.
    case asleep

    /// Claude Code ended in the middle of the work.
    case stopped

    case signedOut
}

/// A conversation that is in memory: what it looks like and whether Claude Code runs for it.
public struct LiveTalk: Sendable, Equatable {
    public let glance: TalkGlance
    public let running: Bool

    public init(glance: TalkGlance, running: Bool) {
        self.glance = glance
        self.running = running
    }
}

public struct SessionRow: Sendable, Equatable {
    public let id: String
    public let name: String
    public let state: RowState
    public let glance: TalkGlance?
    public let lastUsed: Int64
}

public struct RowGroup: Sendable, Equatable {
    public let folder: String
    public let name: String
    public let rows: [SessionRow]
}

/// The list of sessions of one environment as rows, and the count for the tab of the environment.
///
/// The twin of SessionRows.cs.
public enum SessionRows {
    /// live gives the conversation of a session that is in memory, or nil.
    public static func of(_ sessions: TerminalSessions, _ environment: String, live: (String) -> LiveTalk?) -> [RowGroup] {
        sessions.groups(environment).map { group in
            RowGroup(folder: group.folder, name: group.name, rows: group.sessions.map { row($0, live($0.id)) })
        }
    }

    /// How many sessions of the environment wait for the person: the number on its tab.
    public static func waiting(_ sessions: TerminalSessions, _ environment: String, live: (String) -> LiveTalk?) -> Int {
        sessions.open(environment).filter { live($0.id)?.glance.activity == .waiting }.count
    }

    private static func row(_ session: TerminalSession, _ talk: LiveTalk?) -> SessionRow {
        let state: RowState
        switch talk?.glance.activity {
        case nil: state = .asleep
        case .working: state = .working
        case .waiting: state = .waiting
        case .done: state = .done
        case .failed: state = .stopped
        case .signedOut: state = .signedOut
        default: state = talk?.running == true ? .read : .asleep
        }

        // A sleeping session says when it was used, not what was said last.
        return SessionRow(
            id: session.id, name: session.name, state: state,
            glance: state == .asleep ? nil : talk?.glance, lastUsed: session.lastUsed)
    }
}
