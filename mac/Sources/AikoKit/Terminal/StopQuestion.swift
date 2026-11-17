/// Why Aiko is about to stop every program in its window (D-351). Each way out asks first, in one
/// place, so none of them can end a session without a word.
public enum StopReason: CaseIterable, Sendable {
    case quit
    case restart
    case remove
    case startOver
    /// One environment goes, and only its sessions stop. Aiko stays.
    case removeEnvironment
}

/// The question the window asks before it stops every session: the title, what stops, and the two
/// buttons. The twin of StopQuestion.cs.
public struct StopQuestion: Equatable, Sendable {
    public let title: String
    public let body: String
    public let stop: String
    public let keep: String

    /// Whether the step takes Aiko or all its environments away, so every session ends with it.
    public static func endsEverySession(_ reason: StopReason) -> Bool { reason != .removeEnvironment }

    public static func of(_ reason: StopReason) -> StopQuestion {
        switch reason {
        case .quit:
            StopQuestion(title: Strings.terminalQuitTitle, body: Strings.terminalQuitBody,
                         stop: Strings.terminalStopAndQuit, keep: Strings.terminalDontQuit)
        case .restart:
            StopQuestion(title: Strings.terminalQuitTitle, body: Strings.terminalRestartBody,
                         stop: Strings.terminalStopAndRestart, keep: Strings.terminalDontRestart)
        case .remove:
            StopQuestion(title: Strings.terminalQuitTitle, body: Strings.terminalRemoveBody,
                         stop: Strings.terminalStopAndRemove, keep: Strings.terminalDontRemove)
        case .removeEnvironment:
            StopQuestion(title: Strings.terminalQuitTitle, body: Strings.terminalRemoveEnvironmentBody,
                         stop: Strings.terminalStopAndRemove, keep: Strings.terminalDontRemove)
        case .startOver:
            StopQuestion(title: Strings.terminalQuitTitle, body: Strings.terminalStartOverBody,
                         stop: Strings.terminalStopAndStartOver, keep: Strings.terminalDontStartOver)
        }
    }
}
