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

    /// The program ended by itself (D-351): Enter brings Claude Code back.
    case stopped
}

/// How long ago a session was used, as the second line of a sleeping row says it.
public enum UsedDay: Sendable, Equatable {
    /// Only the time is shown.
    case today
    case yesterday

    /// The date is shown.
    case earlier
}

/// What a running terminal is doing, as far as Aiko knows it.
public enum TerminalActivity: String, Sendable, Equatable {
    /// It runs and waits for nothing in particular.
    case idle
    case working

    /// Claude Code asks the person something.
    case waiting

    /// A turn ended and the person has not looked yet.
    case done

    /// The program ended by itself.
    case exited
}

/// A terminal that runs for a session, or ran and ended. Line is the last thing it did, in the
/// words of the second line of the row; empty when nothing is known.
public struct LiveTerminal: Sendable, Equatable {
    public let activity: TerminalActivity
    public let line: String

    public init(activity: TerminalActivity, line: String) {
        self.activity = activity
        self.line = line
    }
}

public struct SessionRow: Sendable, Equatable {
    public let id: String
    public let name: String
    public let state: RowState
    public let line: String
    public let lastUsed: Int64
    public var kind: SessionKind = .claude

    public init(id: String, name: String, state: RowState, line: String, lastUsed: Int64) {
        self.id = id
        self.name = name
        self.state = state
        self.line = line
        self.lastUsed = lastUsed
    }
}
public struct RowGroup: Sendable, Equatable {
    public let folder: String
    public let name: String
    public let rows: [SessionRow]

    public init(folder: String, name: String, rows: [SessionRow]) {
        self.folder = folder
        self.name = name
        self.rows = rows
    }
}

/// The list of sessions of one environment as rows, and the count for the tab of the environment.
///
/// The twin of SessionRows.cs.
public enum SessionRows {
    /// live gives the terminal of a session, or nil when none was started.
    public static func of(_ sessions: TerminalSessions, _ environment: String, live: (String) -> LiveTerminal?) -> [RowGroup] {
        sessions.groups(environment).map { group in
            RowGroup(folder: group.folder, name: group.name, rows: group.sessions.map { row($0, live($0.id)) })
        }
    }

    /// How many sessions of the environment wait for the person: the number on its tab.
    public static func waiting(_ sessions: TerminalSessions, _ environment: String, live: (String) -> LiveTerminal?) -> Int {
        sessions.open(environment).filter { live($0.id)?.activity == .waiting }.count
    }

    /// The day a session was used on, counted by the clock of the person: offsetMinutes is how far
    /// that clock is from UTC. A time that is still to come reads as today.
    public static func dayOf(_ usedSeconds: Int64, _ nowSeconds: Int64, offsetMinutes: Int) -> UsedDay {
        let day: Int64 = 86400
        let shift = Int64(offsetMinutes) * 60
        let days = max(0, floorDay(nowSeconds + shift, day) - floorDay(usedSeconds + shift, day))
        switch days {
        case 0: return .today
        case 1: return .yesterday
        default: return .earlier
        }
    }

    private static func floorDay(_ seconds: Int64, _ day: Int64) -> Int64 {
        (seconds - (((seconds % day) + day) % day)) / day
    }

    private static func row(_ session: TerminalSession, _ terminal: LiveTerminal?) -> SessionRow {
        let state: RowState
        switch terminal?.activity {
        case nil: state = .asleep
        case .working: state = .working
        case .waiting: state = .waiting
        case .done: state = .done
        case .exited: state = .stopped
        case .idle: state = .read
        }

        // A sleeping session says when it was used, not what it did last.
        var row = SessionRow(
            id: session.id, name: session.name, state: state,
            line: state == .asleep ? "" : terminal?.line ?? "", lastUsed: session.lastUsed)
        row.kind = session.kind
        return row
    }
}
