import Foundation
import Testing

@testable import AikoKit

/// The words of the card, the tooltip and the blocks. One suite, run one test at a time, because
/// the chosen language is one value for the whole app: two tests in different languages at once
/// would read each other's setting.
@Suite(.serialized)
struct CardWordsTests {
    static let now = utc(2026, 9, 12, 12)
    static let utcZone = TimeZone(identifier: "UTC")!

    private func card(
        _ name: String,
        _ percent: Int,
        receivedAt: Date = CardWordsTests.now,
        resetsIn hours: Double = 2
    ) -> CardState {
        CardState.from(
            LimitSnapshot(
                environment: name,
                source: .statusLine,
                receivedAt: receivedAt,
                windows: [LimitWindow(kind: .fiveHour, percent: percent, resetsAt: Self.now.adding(hours: hours))]),
            Self.now)
    }

    // ---- CardText ----

    @Test
    func everyWindowHasItsOwnName() {
        #expect(CardText.windowName(.fiveHour) == "Session · 5 hours")
        #expect(CardText.windowName(.sevenDay) == "Week")
        #expect(CardText.windowName(.modelWeek, "Fable") == "Fable · week")
    }

    @Test
    func theModelRowFallsBackToTheWordModelWhenTheServerDidNotNameOne() {
        #expect(CardText.windowName(.modelWeek) == "model · week")
    }

    @Test
    func thePercentageSaysItIsTheShareAlreadySpent() {
        #expect(CardText.percent(42) == "42% used")
    }

    @Test(arguments: [
        (LimitTone.normal, ""),
        (LimitTone.unknown, ""),
        (LimitTone.caution, "running low"),
        (LimitTone.critical, "almost gone"),
    ])
    func onlyTheTwoLoudTonesHaveAWord(tone: LimitTone, expected: String) {
        #expect(CardText.tone(tone) == expected)
    }

    @Test(arguments: [
        (ResetCountdown(isReset: false, days: 2, hours: 3, minutes: 0, unit: .daysAndHours), "resets in 2d 3h"),
        (ResetCountdown(isReset: false, days: 0, hours: 4, minutes: 7, unit: .hoursAndMinutes), "resets in 4h 7m"),
        (ResetCountdown(isReset: false, days: 0, hours: 0, minutes: 9, unit: .minutes), "resets in 9m"),
        (ResetCountdown(isReset: false, days: 0, hours: 0, minutes: 0, unit: .lessThanMinute), "resets in <1m"),
        (ResetCountdown.reset, "just reset"),
    ])
    func theCountdownIsSaidInTheUnitTheCoreChose(countdown: ResetCountdown, expected: String) {
        #expect(CardText.resets(countdown) == expected)
    }

    @Test(arguments: [
        (PaceEstimate(verdict: .unknown, timeLeft: 0), ""),
        (PaceEstimate(verdict: .lastsPastReset, timeLeft: 3600), "lasts until reset"),
        (PaceEstimate(verdict: .runsOutBeforeReset, timeLeft: 90 * 60), "~1h 30m at this pace"),
        (PaceEstimate(verdict: .runsOutBeforeReset, timeLeft: 25 * 3600), "~1d 1h at this pace"),
        (PaceEstimate(verdict: .runsOutBeforeReset, timeLeft: 45 * 60), "~45m at this pace"),
        (PaceEstimate(verdict: .runsOutBeforeReset, timeLeft: 30), "~<1m at this pace"),
    ])
    func thePaceIsOnlySaidWhenThereIsSomethingToSay(pace: PaceEstimate, expected: String) {
        #expect(CardText.pace(pace) == expected)
    }

    @Test
    func theHeaderSaysHowOldTheNumbersAre() {
        let at = utc(2026, 9, 12, 9, 5)

        #expect(CardText.updated(.none, nil, timeZone: Self.utcZone) == "no data yet")
        #expect(CardText.updated(.live, at, timeZone: Self.utcZone) == "updated 09:05")
        #expect(CardText.updated(.stale, at, timeZone: Self.utcZone) == "as of 09:05")
    }

    @Test
    func theClockIsTwoDigitsAndFollowsTheTimeZone() {
        let at = utc(2026, 9, 12, 9, 5)

        #expect(CardText.clock(at, Self.utcZone) == "09:05")
        #expect(CardText.clock(at, TimeZone(secondsFromGMT: 3 * 3600)!) == "12:05")
        #expect(CardText.clock(nil, Self.utcZone) == "")
    }

    // ---- TrayText ----

    @Test
    func theTooltipNamesEveryEnvironmentAndItsShare() {
        let text = TrayText.tooltip([card("Personal", 42), card("Work", 82)], timeZone: Self.utcZone)

        #expect(text == "Personal 42% · Work 82%")
    }

    @Test
    func anEnvironmentWithNoNumbersGetsADash() {
        let text = TrayText.tooltip([CardState.from(LimitSnapshot.noData("Work"), Self.now)], timeZone: Self.utcZone)

        #expect(text == "Work — · open Claude Code")
    }

    @Test
    func withNothingSetUpTheTooltipIsJustTheName() {
        #expect(TrayText.tooltip([], timeZone: Self.utcZone) == "Aiko")
    }

    @Test
    func staleNumbersAreDatedInTheTooltip() {
        let old = card("Personal", 42, receivedAt: Self.now.adding(hours: -1))
        let text = TrayText.tooltip([old], timeZone: Self.utcZone)

        #expect(text == "Personal 42% · as of 11:00")
    }

    @Test
    func aNewerVersionIsTheLastPart() {
        let text = TrayText.tooltip([card("Personal", 42)], newerVersion: "0.4.0", timeZone: Self.utcZone)

        #expect(text == "Personal 42% · Aiko 0.4.0 is out")
    }

    @Test
    func theTooltipIsCutToWhatWindowsKeeps() {
        let long = (0..<8).map { card("Environment number \($0)", 42) }
        let text = TrayText.tooltip(long, timeZone: Self.utcZone)

        #expect(text.count == TrayText.room)
    }

    // ---- CardModel ----

    @Test
    func theHeaderTakesTheFreshestEnvironment() {
        let old = card("Work", 10, receivedAt: Self.now.adding(hours: -1))
        let model = CardModel.from([old, card("Personal", 42)], Self.now, timeZone: Self.utcZone)

        #expect(model.updated == "updated 12:00")
        #expect(model.blocks.count == 2)
    }

    @Test
    func onlyTheSecondBlockHasALineAboveIt() {
        let model = CardModel.from([card("Personal", 42), card("Work", 10)], Self.now, timeZone: Self.utcZone)

        #expect(!model.blocks[0].showsSeparator)
        #expect(model.blocks[1].showsSeparator)
    }

    @Test
    func aConnectedAccountShowsItsPlanAndAFilledDot() {
        let model = CardModel.from(
            [card("Personal", 42)],
            Self.now,
            accounts: ["Personal": CardAccount(plan: "Max 5x", signedIn: true)],
            timeZone: Self.utcZone)
        let block = model.blocks[0]

        #expect(block.plan == "Max 5x")
        #expect(block.showsPlan)
        #expect(block.state == "working now")
        #expect(block.stateDot == .filled)
        #expect(block.showsHalo)
        #expect(block.openLabel == "Open Claude Code")
    }

    @Test
    func aQuietSessionIsConnectedAndNotWorking() {
        let quiet = card("Personal", 42, receivedAt: Self.now.adding(minutes: -5))
        let model = CardModel.from(
            [quiet],
            Self.now,
            accounts: ["Personal": CardAccount(plan: "Max 5x", signedIn: true)],
            timeZone: Self.utcZone)

        #expect(model.blocks[0].state == "connected")
        #expect(!model.blocks[0].showsHalo)
        #expect(!model.blocks[0].stateIsBright)
    }

    @Test
    func anAccountThatIsNotSignedInAsksForASignIn() {
        let model = CardModel.from(
            [CardState.from(LimitSnapshot.noData("Work"), Self.now)],
            Self.now,
            accounts: ["Work": CardAccount(plan: "", signedIn: false)],
            timeZone: Self.utcZone)
        let block = model.blocks[0]

        #expect(block.state == "sign in needed")
        #expect(block.stateDot == .hollow)
        #expect(!block.showsPlan)
        #expect(block.openLabel == "Sign in")
        #expect(block.note == "Sign in so Aiko can see the limits of this environment.")
        #expect(block.showsNote)
    }

    @Test
    func anEnvironmentWeCannotSeeGetsItsOwnNote() {
        let model = CardModel.from(
            [CardState.from(LimitSnapshot.noData("Work"), Self.now)],
            Self.now,
            noAccess: ["Work"],
            timeZone: Self.utcZone)

        #expect(model.blocks[0].note == CardText.noAccessNote)
        #expect(model.blocks[0].note != CardText.noDataNote)
    }

    @Test
    func anEmptyEnvironmentIsToldToOpenClaudeCode() {
        let model = CardModel.from(
            [CardState.from(LimitSnapshot.noData("Work"), Self.now)], Self.now, timeZone: Self.utcZone)

        #expect(model.blocks[0].note == CardText.noDataNote)
        #expect(model.blocks[0].showsNote)
        #expect(!model.blocks[0].showsState)
        #expect(!model.blocks[0].showsOpen)
    }

    @Test
    func aRowCarriesTheBarTheWordsAndTheTone() {
        let row = LimitRow.from(CardRow(
            kind: .fiveHour,
            percent: 42,
            tone: .normal,
            isReset: false,
            countdown: ResetCountdown(isReset: false, days: 0, hours: 2, minutes: 0, unit: .hoursAndMinutes),
            pace: PaceEstimate(verdict: .lastsPastReset, timeLeft: 7200)))

        #expect(row.name == "Session · 5 hours")
        #expect(row.percent == "42% used")
        #expect(row.resets == "resets in 2h 0m")
        #expect(row.pace == "lasts until reset")
        #expect(row.fill == 0.42)
        #expect(!row.paceTakesTone)
    }

    @Test
    func aLoudToneTakesTheSlotFromThePaceAndItsColour() {
        let row = LimitRow.from(CardRow(
            kind: .fiveHour,
            percent: 94,
            tone: .critical,
            isReset: false,
            countdown: ResetCountdown(isReset: false, days: 0, hours: 1, minutes: 0, unit: .hoursAndMinutes),
            pace: PaceEstimate(verdict: .lastsPastReset, timeLeft: 3600)))

        #expect(row.pace == "almost gone")
        #expect(row.paceTakesTone)
        #expect(row.tone == .critical)
    }

    // ---- The language ----

    @Test
    func theSameCardSpeaksRussianWhenAsked() {
        Strings.language = .russian
        defer { Strings.language = .english }

        let model = CardModel.from(
            [card("Personal", 42)],
            Self.now,
            accounts: ["Personal": CardAccount(plan: "Max 5x", signedIn: true)],
            timeZone: Self.utcZone)

        #expect(model.updated == "обновлено в 12:00")
        #expect(model.blocks[0].state == "сейчас работает")
        #expect(model.blocks[0].rows[0].name == "Сессия · 5 часов")
        #expect(model.blocks[0].rows[0].percent == "потрачено 42%")
    }

    @Test
    func aKeyThatIsNotInTheTableAnswersWithItself() {
        #expect(Strings.get("NoSuchKey") == "NoSuchKey")
    }

    @Test
    func theTableHoldsBothLanguagesForEveryKey() {
        #expect(Strings.table.count > 250)
        #expect(Strings.table.allSatisfy { !$0.value.english.isEmpty && !$0.value.russian.isEmpty })
    }

    @Test
    func placeholdersAreFilledInOrder() {
        #expect(Strings.format("{0}d {1}h", 2, 3) == "2d 3h")
        #expect(Strings.format("no placeholder", 1) == "no placeholder")
    }
}
