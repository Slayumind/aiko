import Foundation
import Testing

@testable import AikoKit

/// What a hook means and what a broken record reads as are tables, and they live in
/// spec/cases/hook-events; both cores read them. What is left here needs a clock or a file name.
struct SessionActivityTests {
    static let session = "5f0c2a8e-1b7d-4c1e-9f3a-2d6b8e4a7c10"

    private func hook(_ name: String, _ extra: String = "") -> String {
        #"{ "session_id": "\#(Self.session)", "hook_event_name": "\#(name)", "cwd": "C:\\secret\\project"\#(extra) }"#
    }


    @Test
    func everyEventInThePluginIsUnderstood() {
        for name in PersonaPlugin.hookEvents {
            let extra = name == "Notification" ? #", "notification_type": "permission_prompt" "# : ""
            #expect(HookEvent.fromJson(hook(name, extra)) != nil)
        }
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
