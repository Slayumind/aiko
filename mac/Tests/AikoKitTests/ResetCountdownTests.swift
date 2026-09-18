import Foundation
import Testing

@testable import AikoKit

struct ResetCountdownTests {
    static let now = utc(2026, 9, 12, 12)

    @Test
    func reportsDaysAndHoursWhenMoreThanADayIsLeft() {
        let countdown = ResetCountdown.between(
            Self.now, Self.now.adding(days: 4).adding(hours: 11).adding(minutes: 30))

        #expect(!countdown.isReset)
        #expect(countdown.unit == .daysAndHours)
        #expect(countdown.days == 4)
        #expect(countdown.hours == 11)
    }

    @Test
    func reportsHoursAndMinutesWithinADay() {
        let countdown = ResetCountdown.between(
            Self.now, Self.now.adding(hours: 3).adding(minutes: 34).adding(seconds: 50))

        #expect(countdown.unit == .hoursAndMinutes)
        #expect(countdown.days == 0)
        #expect(countdown.hours == 3)
        #expect(countdown.minutes == 34)
    }

    @Test
    func roundsDownAndNeverPromisesMoreTime() {
        let countdown = ResetCountdown.between(Self.now, Self.now.adding(minutes: 12).adding(seconds: 59))

        #expect(countdown.unit == .minutes)
        #expect(countdown.minutes == 12)
    }

    @Test
    func reportsLessThanAMinuteWhenAlmostNoTimeIsLeft() {
        let countdown = ResetCountdown.between(Self.now, Self.now.adding(seconds: 59))

        #expect(!countdown.isReset)
        #expect(countdown.unit == .lessThanMinute)
    }

    @Test
    func exactlyOneDayIsStillDaysAndHours() {
        let countdown = ResetCountdown.between(Self.now, Self.now.adding(days: 1))

        #expect(countdown.unit == .daysAndHours)
        #expect(countdown.days == 1)
        #expect(countdown.hours == 0)
    }

    @Test
    func aWindowWhoseTimeHasPassedIsReset() {
        let countdown = ResetCountdown.between(Self.now, Self.now.adding(minutes: -1))

        #expect(countdown.isReset)
    }

    @Test
    func theMomentOfResetCountsAsReset() {
        let countdown = ResetCountdown.between(Self.now, Self.now)

        #expect(countdown.isReset)
    }
}
