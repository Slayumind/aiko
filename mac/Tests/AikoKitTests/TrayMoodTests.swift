import Foundation
import Testing

@testable import AikoKit

struct TrayMoodTests {
    static let now = utc(2026, 9, 15, 12)

    private func at(_ activity: SessionActivity, _ secondsAgo: Double = 0) -> ActivityRecord {
        ActivityRecord(environment: "claude", activity: activity, at: Self.now.adding(seconds: -secondsAgo))
    }

    // ---- sessions ----

    @Test(arguments: [
        (SessionActivity.working, AikoFace.working),
        (SessionActivity.waiting, AikoFace.waiting),
        (SessionActivity.done, AikoFace.done),
        (SessionActivity.error, AikoFace.error),
        (SessionActivity.outOfLimit, AikoFace.asleep),
    ])
    func aSessionThatChangesWhatItDoesBringsItsFace(activity: SessionActivity, face: AikoFace) {
        #expect(TrayMood.forActivity(nil, at(activity), Self.now) == face)
    }

    @Test
    func theSameActivityAgainBringsNothing() {
        #expect(TrayMood.forActivity(at(.working, 20), at(.working), Self.now) == nil)
        #expect(TrayMood.forActivity(at(.working, 20), at(.done), Self.now) == .done)
    }

    @Test
    func anOldEventFoundInAFileIsHistory() {
        #expect(TrayMood.forActivity(nil, at(.waiting, 600), Self.now) == nil)
    }

    // ---- limits ----

    @Test(arguments: [
        (70, 90, AikoFace.tired),
        (89, 95, AikoFace.tired),
        (95, 100, AikoFace.asleep),
        (80, 100, AikoFace.asleep),
        (100, 0, AikoFace.fresh),
        (92, 3, AikoFace.fresh),
    ])
    func aLimitCrossingALineBringsAFace(before: Int, after: Int, face: AikoFace) {
        #expect(TrayMood.forLimit(before, after) == face)
    }

    @Test(arguments: [
        (nil, 95), (40, 60), (91, 97), (100, 100), (20, 10), (95, nil),
    ] as [(Int?, Int?)])
    func movingInsideABandOrAFirstNumberBringsNothing(before: Int?, after: Int?) {
        #expect(TrayMood.forLimit(before, after) == nil)
    }

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
