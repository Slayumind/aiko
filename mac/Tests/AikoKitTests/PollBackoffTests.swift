import Foundation
import Testing

@testable import AikoKit

struct PollBackoffTests {
    @Test
    func startsAtTheNormalPace() {
        #expect(PollBackoff.start.delay == 180)
        #expect(PollBackoff.start.failuresInARow == 0)
    }

    @Test
    func aFailureDoublesTheWait() {
        let backoff = PollBackoff.start.afterFailure()

        #expect(backoff.delay == 360)
        #expect(backoff.failuresInARow == 1)
    }

    @Test
    func theWaitNeverGrowsPastTheCeiling() {
        var backoff = PollBackoff.start
        for _ in 0..<10 {
            backoff = backoff.afterFailure()
        }

        #expect(backoff.delay == 900)
    }

    @Test
    func aRetryAfterHeaderIsObeyed() {
        let backoff = PollBackoff.start.afterTooManyRequests(600)

        #expect(backoff.delay == 600)
    }

    @Test
    func aRetryAfterShorterThanTheNormalPaceDoesNotSpeedUsUp() {
        let backoff = PollBackoff.start.afterTooManyRequests(5)

        #expect(backoff.delay == 180)
    }

    @Test
    func aRetryAfterLongerThanTheCeilingIsTrimmed() {
        let backoff = PollBackoff.start.afterTooManyRequests(2 * 3600)

        #expect(backoff.delay == 900)
    }

    @Test
    func a429WithoutAUsableHeaderFallsBackToDoubling() {
        // This endpoint answers with retry-after: 0 or no header at all, which is why it matters.
        #expect(PollBackoff.start.afterTooManyRequests(nil).delay == 360)
        #expect(PollBackoff.start.afterTooManyRequests(0).delay == 360)
    }

    @Test
    func aGoodAnswerPutsThePaceBack() {
        let backoff = PollBackoff.start.afterFailure().afterFailure().afterSuccess()

        #expect(backoff.delay == 180)
        #expect(backoff.failuresInARow == 0)
    }

    @Test
    func knowsWhenTheNextAttemptIsDue() {
        let now = utc(2026, 9, 12, 12)

        #expect(PollBackoff.start.nextAttemptAfter(now) == now.adding(seconds: 180))
    }
}
