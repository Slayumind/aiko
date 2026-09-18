import Foundation
import Testing

@testable import AikoKit

struct SessionActivityTests {
    static let session = "5f0c2a8e-1b7d-4c1e-9f3a-2d6b8e4a7c10"

    private func hook(_ name: String, _ extra: String = "") -> String {
        #"{ "session_id": "\#(Self.session)", "hook_event_name": "\#(name)", "cwd": "C:\\secret\\project"\#(extra) }"#
    }

    @Test(arguments: [
        ("UserPromptSubmit", "", SessionActivity.working),
        ("PostToolUse", "", SessionActivity.working),
        ("PermissionRequest", "", SessionActivity.waiting),
        ("Notification", #", "notification_type": "permission_prompt" "#, SessionActivity.waiting),
        ("Notification", #", "notification_type": "elicitation_dialog" "#, SessionActivity.waiting),
        ("Notification", #", "notification_type": "agent_needs_input" "#, SessionActivity.waiting),
        ("Stop", "", SessionActivity.done),
        ("StopFailure", #", "error_type": "server_error" "#, SessionActivity.error),
        ("StopFailure", #", "error_type": "rate_limit" "#, SessionActivity.outOfLimit),
        ("SessionEnd", #", "reason": "clear" "#, SessionActivity.ended),
    ])
    func eachEventThePluginListensToMeansOneActivity(name: String, extra: String, expected: SessionActivity) {
        #expect(HookEvent.fromJson(hook(name, extra)) == HookEvent(sessionId: Self.session, activity: expected))
    }

    @Test
    func everyEventInThePluginIsUnderstood() {
        for name in PersonaPlugin.hookEvents {
            let extra = name == "Notification" ? #", "notification_type": "permission_prompt" "# : ""
            #expect(HookEvent.fromJson(hook(name, extra)) != nil)
        }
    }

    @Test(arguments: [
        #"{ "session_id": "x", "hook_event_name": "Notification", "notification_type": "idle_prompt" }"#,
        #"{ "session_id": "x", "hook_event_name": "PreToolUse" }"#,
        #"{ "hook_event_name": "Stop" }"#,
        #"{ "session_id": 7, "hook_event_name": "Stop" }"#,
        "not json",
        "",
    ])
    func anythingElseChangesNothing(json: String) {
        #expect(HookEvent.fromJson(json) == nil)
    }

    @Test
    func theFileNameKeepsTheEnvironmentButNotTheSessionId() {
        let name = ActivityRecord.fileName("claude-work", Self.session)

        #expect(name.hasPrefix("claude-work."))
        #expect(name.hasSuffix(".json"))
        #expect(!name.contains(Self.session))
        #expect(name == ActivityRecord.fileName("claude-work", Self.session))
        #expect(name != ActivityRecord.fileName("claude-work", Self.session + "2"))
    }

    @Test
    func theFileHoldsTheActivityAndTheTimeAndNothingFromTheHook() {
        let record = ActivityRecord(
            environment: "default",
            activity: .waiting,
            at: utc(2026, 9, 15, 9),
            offsetSeconds: 3 * 3600)

        let json = record.toJson()

        #expect(ActivityRecord.fromJson(json) == record)
        #expect(!json.contains("secret"))
        #expect(json.contains("+03:00"))
    }

    @Test(arguments: [
        "",
        #"{ "environment": "default", "activity": "Dancing", "at": "2026-09-15T12:00:00Z" }"#,
        #"{ "environment": "default", "activity": "Done" }"#,
        #"{ "environment": "def"#,
    ])
    func aHalfWrittenOrStrangeFileReadsAsNothing(json: String) {
        #expect(ActivityRecord.fromJson(json) == nil)
    }

    @Test
    func theSameActivityIsNotWrittenAgainForAFewSeconds() {
        let now = utc(2026, 9, 15, 12)
        let working = ActivityRecord(environment: "default", activity: .working, at: now)

        #expect(!ActivityRecord.needsWrite(working, .working, now.adding(seconds: 3)))
        #expect(ActivityRecord.needsWrite(working, .working, now.addingTimeInterval(ActivityRecord.rewriteAfter)))
        #expect(ActivityRecord.needsWrite(working, .waiting, now.adding(seconds: 1)))
        #expect(ActivityRecord.needsWrite(nil, .working, now))
    }

    @Test
    func aLateToolHookRightAfterTheAnswerEndedDoesNotBringBackWorking() {
        let now = utc(2026, 9, 15, 12)
        let done = ActivityRecord(environment: "default", activity: .done, at: now)

        #expect(!ActivityRecord.needsWrite(done, .working, now.adding(seconds: 0.3)))
        #expect(ActivityRecord.needsWrite(done, .working, now.addingTimeInterval(ActivityRecord.lateHookWindow)))
        #expect(
            ActivityRecord.needsWrite(
                ActivityRecord(environment: "default", activity: .waiting, at: now), .working, now.adding(seconds: 0.3)))
    }

    @Test
    func filesOlderThanADayAreStale() {
        let now = utc(2026, 9, 15, 12)

        #expect(!ActivityRecord.isStale(now.adding(hours: -23), now))
        #expect(ActivityRecord.isStale(now.adding(hours: -25), now))
    }
}
