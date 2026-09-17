import Foundation
import Testing

@testable import AikoKit

struct StatusLineReportTests {
    // Shortened copy of a real status line payload captured during the spike.
    static let realPayload = """
        {
          "session_id": "00000000-0000-0000-0000-000000000000",
          "cwd": "C:\\\\work",
          "model": { "id": "claude-opus-5", "display_name": "Opus 5" },
          "rate_limits": {
            "five_hour": { "used_percentage": 28.000000000000004, "resets_at": 1789170600 },
            "seven_day": { "used_percentage": 15, "resets_at": 1789506000 }
          }
        }
        """

    @Test
    func aByteOrderMarkInFrontDoesNotHideTheLimits() {
        // Windows PowerShell 5.1 puts one there when it pipes text into a program, and that is
        // the shell Claude Code uses on a machine without Git. Found in Windows Sandbox: the
        // bridge started, exited 0 and wrote nothing.
        let report = StatusLineReport.fromJson("\u{FEFF}" + Self.realPayload)

        #expect(report.hasData)
        #expect(report.find(.fiveHour)?.percent == 28)
    }

    @Test
    func readsBothWindowsFromARealPayload() {
        let report = StatusLineReport.fromJson(Self.realPayload)

        #expect(report.hasData)
        #expect(report.windows.count == 2)
        #expect(report.find(.fiveHour)?.percent == 28)
        #expect(report.find(.sevenDay)?.percent == 15)
    }

    @Test
    func readsTheResetTimeAsUnixSeconds() {
        let report = StatusLineReport.fromJson(Self.realPayload)

        #expect(report.find(.fiveHour)?.resetsAt == Date(timeIntervalSince1970: 1_789_170_600))
    }

    @Test
    func roundsAPercentageToTheNearestWholeNumber() {
        let report = StatusLineReport.fromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 28.6, "resets_at": 1789170600 } } }
            """)

        #expect(report.find(.fiveHour)?.percent == 29)
    }

    @Test
    func clampsAPercentageAboveAHundred() {
        let report = StatusLineReport.fromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 120, "resets_at": 1789170600 } } }
            """)

        #expect(report.find(.fiveHour)?.percent == 100)
    }

    @Test
    func aWindowWithoutAResetTimeIsSkipped() {
        let report = StatusLineReport.fromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 34 } } }
            """)

        #expect(!report.hasData)
        #expect(report.find(.fiveHour) == nil)
    }

    @Test
    func aPayloadWithoutRateLimitsHasNoData() {
        let report = StatusLineReport.fromJson("""
            { "model": { "display_name": "Opus 5" } }
            """)

        #expect(!report.hasData)
    }

    @Test
    func unknownFieldsDoNotBreakTheParser() {
        let report = StatusLineReport.fromJson("""
            {
              "rate_limits": {
                "five_hour": { "used_percentage": 34, "resets_at": 1789170600, "locked_reason": null },
                "seven_day_omelette": { "used_percentage": 5 }
              }
            }
            """)

        #expect(report.windows.count == 1)
        #expect(report.find(.fiveHour)?.percent == 34)
    }

    @Test(arguments: ["", "   ", "not json at all", "[1, 2, 3]", #"{ "rate_limits": "nonsense" }"#])
    func brokenInputReadsAsNoDataAndNeverThrows(json: String) {
        let report = StatusLineReport.fromJson(json)

        #expect(!report.hasData)
    }
}
