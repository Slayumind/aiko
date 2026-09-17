import Testing

@testable import AikoKit

/// The daily identifier that goes with the update check.
///
/// The whole promise rests on one property: two days of it cannot be joined back into one person.
/// These tests hold that promise still, because a change here would be the kind that nobody
/// notices and everybody would mind.
struct HeartbeatTests {
    static let install = "6f1c0d4a9b8e4f2a91d37c5e08b64a12"
    static let monday = DateOnly(2026, 9, 7)
    static let tuesday = DateOnly(2026, 9, 8)

    @Test
    func theSameDayGivesTheSameAnswer() {
        #expect(Heartbeat.dailyId(Self.install, Self.monday) == Heartbeat.dailyId(Self.install, Self.monday))
    }

    @Test
    func theNextDayGivesAnotherOne() {
        // Without this the identifier is a name, and the promise in PRIVACY.md is untrue.
        #expect(Heartbeat.dailyId(Self.install, Self.monday) != Heartbeat.dailyId(Self.install, Self.tuesday))
    }

    @Test
    func twoComputersDifferOnTheSameDay() {
        #expect(
            Heartbeat.dailyId(Self.install, Self.monday)
                != Heartbeat.dailyId("0000aaaa1111bbbb2222cccc3333dddd", Self.monday))
    }

    @Test
    func theValueKeptOnTheComputerNeverAppearsInWhatIsSent() {
        let sent = Heartbeat.dailyId(Self.install, Self.monday)

        #expect(!sent.lowercased().contains(Self.install.lowercased()))
        #expect(!sent.lowercased().contains(Self.install.prefix(8).lowercased()))
    }

    @Test
    func itIsShortAndPlainEnoughForAWebAddress() {
        let sent = Heartbeat.dailyId(Self.install, Self.monday)

        #expect(sent.count == Heartbeat.idLength)
        #expect(sent.allSatisfy { $0.isHexDigit && !$0.isUppercase })
    }

    /// The week key rides on ISO weeks, where a week belongs to the year that holds its Thursday.
    /// Around New Year that year is not the year on the calendar, and a naive key would count one
    /// copy twice in the first days of January.
    @Test(arguments: [
        (2026, 1, 1, "2026-W01"),
        (2026, 12, 31, "2026-W53"),
        (2027, 1, 1, "2026-W53"),
        (2026, 9, 16, "2026-W38"),
    ])
    func theWeekKeyFollowsTheIsoWeek(year: Int, month: Int, day: Int, expected: String) {
        #expect(Heartbeat.weekKey(DateOnly(year, month, day)) == expected)
    }

    @Test
    func mondayAndSundayOfOneWeekGiveTheSameKey() {
        #expect(Heartbeat.weekKey(DateOnly(2026, 9, 14)) == Heartbeat.weekKey(DateOnly(2026, 9, 20)))
    }

    @Test
    func theNextMondayStartsAnotherWeek() {
        #expect(Heartbeat.weekKey(DateOnly(2026, 9, 20)) != Heartbeat.weekKey(DateOnly(2026, 9, 21)))
    }

    @Test
    func theMonthKeyIsTheCalendarMonth() {
        #expect(Heartbeat.monthKey(DateOnly(2026, 9, 16)) == "2026-09")
        #expect(Heartbeat.monthKey(DateOnly(2028, 2, 29)) == "2028-02")
        #expect(Heartbeat.monthKey(DateOnly(2026, 9, 30)) != Heartbeat.monthKey(DateOnly(2026, 10, 1)))
    }

    @Test
    func aPeriodIsFirstUntilItHasBeenReported() {
        let day = DateOnly(2026, 9, 16)

        #expect(Heartbeat.firstThisWeek(nil, day))
        #expect(Heartbeat.firstThisWeek("", day))
        #expect(Heartbeat.firstThisMonth(nil, day))

        #expect(!Heartbeat.firstThisWeek(Heartbeat.weekKey(day), day))
        #expect(!Heartbeat.firstThisMonth(Heartbeat.monthKey(day), day))
    }

    @Test
    func lastWeeksAnswerDoesNotCoverThisWeek() {
        // The copy that ran last Monday and again this Monday is two weekly runs, not one.
        let lastWeek = Heartbeat.weekKey(DateOnly(2026, 9, 14))

        #expect(Heartbeat.firstThisWeek(lastWeek, DateOnly(2026, 9, 21)))
    }

    @Test
    func theKeysAreNotIdentifiersAndHoldNoInstallValue() {
        let day = DateOnly(2026, 9, 16)

        #expect(!Heartbeat.weekKey(day).lowercased().contains(Self.install.lowercased()))
        #expect(!Heartbeat.monthKey(day).lowercased().contains(Self.install.lowercased()))
    }

    @Test(arguments: ["", "   "])
    func withoutAValueOnTheComputerNothingIsSent(installId: String) {
        // A fresh random value on every start would count one person many times over, so when the
        // file cannot be written Aiko sends no identifier at all.
        #expect(Heartbeat.dailyId(installId, Self.monday) == "")
    }
}
