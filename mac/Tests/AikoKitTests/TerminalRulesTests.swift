import Foundation
import Testing

@testable import AikoKit

/// The rules of the terminal window that came to the Mac before the window itself (D-425), from the
/// same case files as the Windows tests: notices, window-zoom, count-query, terminal-keys. The twins
/// of NoticesTests, WindowZoomTests, CountQueryTests, TerminalKeysTests, EnvironmentInitialTests and
/// CountFormTests.
struct TerminalRulesTests {
    private static func activity(_ name: String?) -> TerminalActivity? {
        name.flatMap { TerminalActivity(rawValue: $0.prefix(1).lowercased() + $0.dropFirst()) }
    }

    // ---- notices ----

    @Test
    func everySoundCaseSoundsTheSameWay() {
        for row in SpecCases.json("notices")["sounds"]?.arrayValue ?? [] {
            let sound = !(row["soundOff"]?.boolValue ?? false)
            let sounds = Notices.sounds(
                Self.activity(row["before"]?.stringValue),
                Self.activity(row["now"]?.stringValue)!,
                sound: sound,
                inFocus: row["inFocus"]?.boolValue ?? false,
                open: row["open"]?.boolValue ?? false)
            #expect(sounds == row["sounds"]?.boolValue)
        }
    }

    @Test
    func everyCountCaseCountsAndSignsTheSameWay() {
        for row in SpecCases.json("notices")["counts"]?.arrayValue ?? [] {
            let terminals = (row["activities"]?.arrayValue ?? []).enumerated().map { index, item in
                (id: "\(index)", activity: Self.activity(item.stringValue)!)
            }
            let counts = SessionCounts.of(terminals)
            let first = row["firstWaiting"]?.int64Value.map { "\($0)" }

            #expect(counts == SessionCounts(
                working: Int(row["working"]?.int64Value ?? -1),
                waiting: Int(row["waiting"]?.int64Value ?? -1),
                done: Int(row["done"]?.int64Value ?? -1),
                stopped: Int(row["stopped"]?.int64Value ?? -1),
                firstWaiting: first))
            #expect(counts.badge.kind.rawValue == row["badge"]?.stringValue?.lowercased())
            #expect(counts.badge.text == row["text"]?.stringValue)
        }
    }

    @Test
    func everyStopCaseIsNewsTheSameWay() {
        for row in SpecCases.json("notices")["stops"]?.arrayValue ?? [] {
            #expect(SessionCounts.stoppedIsNews(
                stoppedAt: row["stoppedAt"]?.int64Value ?? 0, seenAt: row["seenAt"]?.int64Value ?? 0)
                == row["news"]?.boolValue)
        }
    }

    @Test
    func noSessionLeavesNothingToShow() {
        #expect(SessionCounts.none.isEmpty)
        #expect(SessionCounts.none.badge == .none)
    }

    // ---- window-zoom ----

    @Test
    func theStepsAndTheDefaultAreTheCases() {
        let cases = SpecCases.json("window-zoom")
        #expect(WindowZoom.steps == (cases["steps"]?.arrayValue ?? []).compactMap { $0.int64Value.map(Int.init) })
        #expect(WindowZoom.read(0) == Int(cases["default"]?.int64Value ?? 0))
    }

    @Test
    func everyZoomCaseGivesTheSameStepAndFont() {
        let cases = SpecCases.json("window-zoom")
        for row in cases["moves"]?.arrayValue ?? [] {
            let from = Int(row["from"]?.int64Value ?? 0)
            #expect(WindowZoom.larger(from) == Int(row["larger"]?.int64Value ?? 0))
            #expect(WindowZoom.smaller(from) == Int(row["smaller"]?.int64Value ?? 0))
        }

        for row in cases["read"]?.arrayValue ?? [] {
            #expect(WindowZoom.read(Int(row["stored"]?.int64Value ?? 0)) == Int(row["zoom"]?.int64Value ?? 0))
        }

        for row in cases["terminalPoints"]?.arrayValue ?? [] {
            #expect(WindowZoom.terminalPoints(Int(row["zoom"]?.int64Value ?? 0)) == Int(row["points"]?.int64Value ?? 0))
        }
    }

    // ---- count-query ----

    @Test
    func theQueryCarriesOnlyWhatPrivacyLists() {
        for row in SpecCases.json("count-query")["rows"]?.arrayValue ?? [] {
            let query = CountQuery.query(
                version: row["version"]?.stringValue ?? "",
                system: row["system"]?.stringValue ?? "",
                day: row["day"]?.stringValue,
                persona: row["persona"]?.boolValue ?? false,
                firstOfWeek: row["week"]?.boolValue ?? false,
                firstOfMonth: row["month"]?.boolValue ?? false)
            #expect(query == row["query"]?.stringValue)
        }
    }

    // ---- terminal-keys ----

    @Test
    func everyKeyOfTheTableDoesWhatTheRowSays() {
        for row in SpecCases.json("terminal-keys")["keys"]?.arrayValue ?? [] {
            let state = row["state"]
            func flag(_ name: String) -> Bool { state?[name]?.boolValue ?? false }

            let action = TerminalKeys.action(
                row["key"]?.stringValue ?? "",
                shift: row["shift"]?.boolValue ?? false,
                TerminalKeyState(
                    settingsOpen: flag("settingsOpen"),
                    claudeSession: flag("claudeSession"),
                    inTerminal: flag("inTerminal"),
                    hasSelection: flag("hasSelection")))
            let expected = row["action"]?.stringValue ?? ""
            #expect(action.rawValue == expected.prefix(1).lowercased() + expected.dropFirst(), "\(row["key"]?.stringValue ?? "")")
        }
    }

    // ---- the letter of an environment and the form of a word ----

    @Test(arguments: [("Aiko", "A"), ("work · Kodland", "W"), ("  личное", "Л"), ("2nd", "2"), ("· ·", ""), ("", "")])
    func anEnvironmentShowsItsFirstLetter(_ name: String, _ letter: String) {
        #expect(EnvironmentInitial.of(name) == letter)
    }

    @Test(arguments: [
        (Int64(1), CountForm.one), (21, .one), (101, .one), (2, .few), (24, .few), (5, .many),
        (11, .many), (12, .many), (14, .many), (111, .many), (212, .many), (0, .many),
    ])
    func theLastDigitsChooseTheForm(_ count: Int64, _ form: CountForm) {
        #expect(CountForm.of(count) == form)
    }
}
