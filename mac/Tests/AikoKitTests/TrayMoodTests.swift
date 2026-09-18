import Foundation
import Testing

@testable import AikoKit

struct TrayMoodTests {
    static let now = utc(2026, 9, 15, 12)

    private func at(_ activity: SessionActivity, _ secondsAgo: Double = 0) -> ActivityRecord {
        ActivityRecord(environment: "claude", activity: activity, at: Self.now.adding(seconds: -secondsAgo))
    }

    // Which face an event brings is a table, and it lives in spec/cases/tray-mood; both cores read
    // it. What is left here needs a snapshot, a clock or a settings object.






    @Test
    func theFullestWindowCountsAndAWindowPastItsResetIsEmpty() {
        let snapshot = LimitSnapshot(
            environment: "Aiko",
            source: .statusLine,
            receivedAt: Self.now,
            windows: [
                LimitWindow(kind: .fiveHour, percent: 97, resetsAt: Self.now.adding(minutes: -1)),
                LimitWindow(kind: .sevenDay, percent: 41, resetsAt: Self.now.adding(days: 3)),
            ])

        #expect(TrayMood.highestPercent(snapshot, Self.now) == 41)
        #expect(TrayMood.highestPercent(LimitSnapshot.noData("Aiko"), Self.now) == nil)
    }

    // ---- the moment on the icon ----

    @Test
    func aFaceStaysTwoSeconds() {
        let shown = TrayMood.next(nil, .done, Self.now)

        #expect(!TrayMood.isOver(shown, Self.now.adding(seconds: 1.9)))
        #expect(TrayMood.isOver(shown, Self.now.addingTimeInterval(TrayMood.showFor)))
    }

    @Test
    func aCalmerFaceDoesNotPushAwayOneThatAsksForYou() {
        let waiting = TrayMood.next(nil, .waiting, Self.now)

        #expect(TrayMood.next(waiting, .working, Self.now.adding(seconds: 0.5)) == waiting)
        #expect(
            TrayMood.next(waiting, .error, Self.now.adding(seconds: 3))
                == FaceMoment(face: .error, since: Self.now.adding(seconds: 3)))
    }

    @Test
    func anEqualOrStrongerFaceStartsItsOwnTwoSeconds() {
        let done = TrayMood.next(nil, .done, Self.now)

        #expect(
            TrayMood.next(done, .done, Self.now.adding(seconds: 1))
                == FaceMoment(face: .done, since: Self.now.adding(seconds: 1)))
        #expect(
            TrayMood.next(done, .waiting, Self.now.adding(seconds: 1))
                == FaceMoment(face: .waiting, since: Self.now.adding(seconds: 1)))
    }

    @Test
    func afterItsTwoSecondsAnyFaceCanCome() {
        let waiting = TrayMood.next(nil, .waiting, Self.now)

        #expect(TrayMood.next(waiting, .working, Self.now.adding(seconds: 2)).face == .working)
    }

    @Test
    func noPersonaAnywhereMeansNoFace() {
        let off = EnvironmentSettings([AikoEnvironment("Work", [#"C:\x"#])])
        let on = EnvironmentSettings([AikoEnvironment("Work", [#"C:\x"#], persona: true)])

        #expect(!TrayMood.hasFace(off))
        #expect(TrayMood.hasFace(on))
        #expect(!TrayMood.hasFace(.empty))
    }
}
