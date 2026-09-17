import CryptoKit
import Foundation

/// What one Claude Code session is doing, as far as its hooks tell (D-206).
public enum SessionActivity: String, Sendable, Equatable, CaseIterable {
    case working = "Working"

    /// A permission prompt or a question: the session waits for the person.
    case waiting = "Waiting"

    case done = "Done"

    case error = "Error"

    /// The answer stopped on a rate limit.
    case outOfLimit = "OutOfLimit"

    /// The session is over. Its file goes away.
    case ended = "Ended"

    /// A file may hold the number instead of the name, the way Enum.TryParse reads one.
    static func named(_ text: String?) -> SessionActivity? {
        guard let text else { return nil }
        if let byName = SessionActivity(rawValue: text) {
            return byName
        }
        guard let number = Int(text.trimmingCharacters(in: .whitespaces)),
              number >= 0, number < allCases.count else {
            return nil
        }
        return allCases[number]
    }
}

/// One hook call turned into an activity. Only the event, the kind of notification or error and the
/// session id are read; the prompt, the tool input and the paths are never looked at.
public struct HookEvent: Sendable, Equatable {
    public let sessionId: String
    public let activity: SessionActivity

    public init(sessionId: String, activity: SessionActivity) {
        self.sessionId = sessionId
        self.activity = activity
    }

    /// The kinds of Notification that mean "waiting for you". An idle reminder after a finished answer
    /// is not one of them: the face would say "waiting" a minute after "done".
    private static let waitingNotifications: Set<String> = [
        "permission_prompt", "elicitation_dialog", "agent_needs_input",
    ]

    /// Null for an event that does not change the face, or for input that is not a hook call.
    public static func fromJson(_ json: String) -> HookEvent? {
        let root = json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? nil
            : JsonNode.parse(json)?.objectValue

        guard let session = root?["session_id"]?.stringValue, !session.isEmpty else {
            return nil
        }

        let activity: SessionActivity?
        switch root?["hook_event_name"]?.stringValue {
        case "UserPromptSubmit", "PostToolUse":
            activity = .working
        case "PermissionRequest":
            activity = .waiting
        case "Notification":
            activity = waitingNotifications.contains(root?["notification_type"]?.stringValue ?? "") ? .waiting : nil
        case "Stop":
            activity = .done
        case "StopFailure":
            activity = root?["error_type"]?.stringValue == "rate_limit" ? .outOfLimit : .error
        case "SessionEnd":
            activity = .ended
        default:
            activity = nil
        }

        guard let known = activity else { return nil }
        return HookEvent(sessionId: session, activity: known)
    }
}

/// The file one session reports its activity into: <app support>/Aiko/activity/<environment>.<session>.json.
/// A folder of its own, because the tray reads every file in environments/ as a limit snapshot.
public struct ActivityRecord: Sendable, Equatable {
    public let environment: String
    public let activity: SessionActivity
    public let at: Date

    /// The offset the file was written with. Two files may say the same moment in two zones, and
    /// the record is written back the way it came, so this is not part of what makes two equal.
    public let offsetSeconds: Int

    public init(environment: String, activity: SessionActivity, at: Date, offsetSeconds: Int = 0) {
        self.environment = environment
        self.activity = activity
        self.at = at
        self.offsetSeconds = offsetSeconds
    }

    public static func == (left: ActivityRecord, right: ActivityRecord) -> Bool {
        left.environment == right.environment && left.activity == right.activity && left.at == right.at
    }

    public static let folderName = "activity"

    /// PostToolUse comes after every tool. The same activity within this time is not written again:
    /// the tray only needs to know the session is still alive, and 10 minutes is when it stops
    /// believing that (D-206).
    public static let rewriteAfter: TimeInterval = 15

    /// Async hooks run side by side, so the last PostToolUse can finish after Stop. A "working" that
    /// lands this soon after an answer ended is that late hook, not a new prompt: nobody types that fast.
    public static let lateHookWindow: TimeInterval = 2

    /// Files left by sessions that never sent SessionEnd are cleared after this.
    public static let keepFor: TimeInterval = 86400

    public static func folder(_ applicationSupport: String) -> String {
        (applicationSupport as NSString).appendingPathComponent("Aiko/\(folderName)")
    }

    /// The session id is hashed: the file name says which session it is without keeping the id.
    public static func fileName(_ environment: String, _ sessionId: String) -> String {
        let digest = SHA256.hash(data: Data(sessionId.utf8))
        let hex = digest.map { String(format: "%02x", $0) }.joined()
        return "\(environment).\(hex.prefix(12)).json"
    }

    /// Whether a new event has to touch the disk, given what the file already says.
    public static func needsWrite(_ written: ActivityRecord?, _ next: SessionActivity, _ now: Date) -> Bool {
        guard let written else {
            return true
        }

        let age = now.timeIntervalSince(written.at)
        if next == .working,
           written.activity == .done || written.activity == .error || written.activity == .outOfLimit,
           age < lateHookWindow {
            return false
        }

        return written.activity != next || age >= rewriteAfter
    }

    public static func isStale(_ written: Date, _ now: Date) -> Bool {
        now.timeIntervalSince(written) > keepFor
    }

    public func toJson() -> String {
        let object = JsonObject([
            ("environment", .string(environment)),
            ("activity", .string(activity.rawValue)),
            ("at", .string(Iso8601.roundTrip(at, offsetSeconds: offsetSeconds))),
        ])
        return JsonNode.object(object).toJsonString(escaping: .relaxed) + "\n"
    }

    /// Null for a file that is half written or not ours.
    public static func fromJson(_ json: String) -> ActivityRecord? {
        guard let root = JsonNode.parse(json)?.objectValue,
              let environment = root["environment"]?.stringValue,
              let activity = SessionActivity.named(root["activity"]?.stringValue),
              let moment = Iso8601.parse(root["at"]?.stringValue) else {
            return nil
        }

        return ActivityRecord(
            environment: environment,
            activity: activity,
            at: moment.date,
            offsetSeconds: moment.offsetSeconds)
    }
}
