import Foundation

/// What the part of the window to the right of the list shows.
public enum MainView: Sendable, Equatable {
    /// The environment has no session in the list: one sentence and a button.
    case noSessions

    /// The real terminal of the chosen session (D-349).
    case terminal
}

/// What is chosen in the window of the terminal: the tab of an environment and a session of it
/// (D-292, D-293).
///
/// The twin of TerminalWindowModel.cs.
public struct TerminalWindowModel: Sendable, Equatable {
    public let environment: String
    public let sessionId: String?

    public init(environment: String, sessionId: String?) {
        self.environment = environment
        self.sessionId = sessionId
    }

    /// The window as it opens: on the session that was open last, or on the first environment.
    /// environments is never empty.
    public static func opened(_ sessions: TerminalSessions, _ environments: [String]) -> TerminalWindowModel {
        let last = sessions.lastId.flatMap { sessions.find($0) }
        let environment = last.map(\.environment).flatMap { environments.contains($0) ? $0 : nil } ?? environments[0]
        return TerminalWindowModel(environment: environment, sessionId: sessions.toOpen(environment)?.id)
    }

    public func view(_ sessions: TerminalSessions) -> MainView {
        session(sessions) != nil ? .terminal : .noSessions
    }

    /// The chosen session, when it is still in the list of the chosen environment.
    public func session(_ sessions: TerminalSessions) -> TerminalSession? {
        guard let id = sessionId, let session = sessions.find(id), !session.closed, session.environment == environment
        else {
            return nil
        }
        return session
    }

    /// Another tab.
    public func toEnvironment(_ name: String, _ sessions: TerminalSessions) -> TerminalWindowModel {
        name == environment ? self : TerminalWindowModel(environment: name, sessionId: sessions.toOpen(name)?.id)
    }

    /// A click on a row. A session of another environment, a closed one or an unknown one changes nothing.
    public func toSession(_ id: String, _ sessions: TerminalSessions) -> TerminalWindowModel {
        guard let session = sessions.find(id), !session.closed, session.environment == environment else {
            return self
        }
        return TerminalWindowModel(environment: environment, sessionId: id)
    }

    /// After the list has changed: a session was made, closed or the file was read again. The
    /// window stays on its session when it can; otherwise it takes the one the list opens on.
    public func after(_ sessions: TerminalSessions) -> TerminalWindowModel {
        session(sessions) != nil ? self : TerminalWindowModel(environment: environment, sessionId: sessions.toOpen(environment)?.id)
    }
}
