import Foundation

/// Aiko's faces, the same seven in every style (D-199, D-215).
public enum AikoFace: Sendable, Equatable, CaseIterable {
    case fresh
    case tired
    case asleep
    case working
    case waiting
    case done
    case error
}

/// A face on the icon and when it came.
public struct FaceMoment: Sendable, Equatable {
    public let face: AikoFace
    public let since: Date

    public init(face: AikoFace, since: Date) {
        self.face = face
        self.since = since
    }
}

/// Which face an event brings and how long it stays (D-199, D-211). The face is a moment, not a
/// state: it replaces the rings for two seconds after an event and goes. Nothing is shown and no
/// timer runs while nothing happens.
public enum TrayMood {
    public static let showFor: TimeInterval = 2

    public static let tiredAt = 90
    public static let asleepAt = 100

    /// A file seen for the first time with an older event in it is history, for example a session
    /// file read when Aiko starts. History brings no face.
    public static let newEventWithin: TimeInterval = 30

    /// The face for a change in one session's file. Null when the activity did not change: a new
    /// tool call in a working session is not a new moment.
    public static func forActivity(
        _ before: ActivityRecord?, _ after: ActivityRecord, _ now: Date
    ) -> AikoFace? {
        if before?.activity == after.activity || now.timeIntervalSince(after.at) > newEventWithin {
            return nil
        }

        switch after.activity {
        case .working: return .working
        case .waiting: return .waiting
        case .done: return .done
        case .error: return .error
        case .outOfLimit: return .asleep
        case .ended: return nil
        }
    }

    /// The face for a limit crossing a line. Null for the first number Aiko sees: that is where the
    /// limit already was, not a change.
    public static func forLimit(_ before: Int?, _ after: Int?) -> AikoFace? {
        guard let was = before, let now = after else {
            return nil
        }

        if was < asleepAt && now >= asleepAt {
            return .asleep
        }

        if was < tiredAt && now >= tiredAt {
            return .tired
        }

        // The window turned over: rested again.
        return was >= tiredAt && now < tiredAt ? .fresh : nil
    }

    /// The fullest window of an environment, with a window past its reset counted as empty.
    public static func highestPercent(_ snapshot: LimitSnapshot, _ now: Date) -> Int? {
        guard snapshot.hasData else { return nil }
        return snapshot.windows.map { snapshot.statusAt(now, $0.kind)?.percent ?? 0 }.max()
    }

    /// What is on the icon after an event. A face that asks for the person, or says something went
    /// wrong, is not pushed away by a calmer one that comes a moment later.
    public static func next(_ showing: FaceMoment?, _ incoming: AikoFace, _ now: Date) -> FaceMoment {
        guard let showing, !isOver(showing, now), weight(incoming) < weight(showing.face) else {
            return FaceMoment(face: incoming, since: now)
        }
        return showing
    }

    public static func isOver(_ moment: FaceMoment, _ now: Date) -> Bool {
        now.timeIntervalSince(moment.since) >= showFor
    }

    /// Faces come only from environments where the persona is on (D-199). The environment file is
    /// not ported yet, so the flags come in as a list.
    public static func hasFace(_ personaPerEnvironment: [Bool]) -> Bool {
        personaPerEnvironment.contains(true)
    }

    private static func weight(_ face: AikoFace) -> Int {
        switch face {
        case .waiting: return 6
        case .error: return 5
        case .asleep: return 4
        case .tired: return 3
        case .done: return 2
        case .working: return 1
        case .fresh: return 0
        }
    }
}
