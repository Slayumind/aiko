import Foundation
import Testing

@testable import AikoKit

struct CardLifetimeTests {
    @Test
    func theCardOpensAfterAWholeSecondAndAHalfOnTheIcon() {
        #expect(CardLifetime.hoverDelay == 1.5)
        #expect(CardLifetime.watchInterval == 0.250)
    }

    @Test
    func oneTurnAwayIsNotEnoughToClose() {
        var watch = AwayWatch()

        let closes = watch.turn(pointerIsHome: false)

        #expect(!closes)
        #expect(watch.awayTurns == 1)
    }

    @Test
    func twoTurnsAwayInARowCloseTheCard() {
        var watch = AwayWatch()

        let first = watch.turn(pointerIsHome: false)
        let second = watch.turn(pointerIsHome: false)

        #expect(!first)
        #expect(second)
    }

    @Test
    func comingBackStartsTheCountAgain() {
        var watch = AwayWatch()

        _ = watch.turn(pointerIsHome: false)
        let home = watch.turn(pointerIsHome: true)
        let awayAgain = watch.turn(pointerIsHome: false)

        #expect(!home)
        #expect(!awayAgain)
        #expect(watch.awayTurns == 1)
    }

    @Test
    func motionKeepsTheDurationsOfThePrototype() {
        #expect(Motion.hover == 0.120)
        #expect(Motion.settle == 0.280)
        #expect(Motion.popFrom == 0.94)
        #expect(Motion.popShift == 6)
        #expect(Motion.spring == CubicBezier(0.3, 1.4, 0.5, 1))
        #expect(Motion.standard == CubicBezier(0.2, 0.8, 0.2, 1))
    }

    @Test
    func askedForFewerAnimationsEverythingLandsAtOnce() {
        #expect(Motion.or0(Motion.settle, reduceMotion: true) == 0)
        #expect(Motion.or0(Motion.settle, reduceMotion: false) == Motion.settle)
    }
}
