import Foundation
import Testing

@testable import AikoKit

struct UsageReportTests {
    // Shortened copy of a real answer captured during the spike, code names included.
    static let realAnswer = """
        {
          "five_hour": { "utilization": 34.0, "resets_at": "2026-09-11T15:00:00.123456+00:00",
                         "limit_dollars": null, "locked_reason": null },
          "seven_day": { "utilization": 61.0, "resets_at": "2026-09-15T03:00:00.000000+00:00" },
          "seven_day_opus": null,
          "tangelo": null,
          "nimbus_quill": { "utilization": 2.0, "resets_at": null },
          "limits": [
            { "kind": "session", "percent": 34.0, "resets_at": "2026-09-11T15:00:00.123456+00:00", "scope": null },
            { "kind": "weekly_all", "percent": 61.0, "resets_at": "2026-09-15T03:00:00.000000+00:00", "scope": null },
            { "kind": "weekly_scoped", "percent": 3.0, "resets_at": "2026-09-15T03:00:00+00:00",
              "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null } }
          ],
          "member_dashboard_available": false
        }
        """

    @Test
    func readsBothWindows() {
        let report = UsageReport.fromJson(Self.realAnswer)

        #expect(report.hasData)
        #expect(report.windows.first { $0.kind == .fiveHour }?.percent == 34)
        #expect(report.windows.first { $0.kind == .sevenDay }?.percent == 61)
    }

    @Test
    func readsTheModelLimitWithItsName() {
        let report = UsageReport.fromJson(Self.realAnswer)

        #expect(report.model != nil)
        #expect(report.model?.modelName == "Fable")
        #expect(report.model?.percent == 3)
    }

    @Test
    func readsAResetTimeWithAndWithoutAFraction() {
        let report = UsageReport.fromJson(Self.realAnswer)

        #expect(report.windows.first { $0.kind == .fiveHour }?.resetsAt == utc(2026, 9, 11, 15, 0, 0.123456))
        #expect(report.model?.resetsAt == utc(2026, 9, 15, 3))
    }

    @Test
    func unknownKeysAndNullsDoNotBreakTheParser() {
        let report = UsageReport.fromJson("""
            {
              "five_hour": { "utilization": 12.0, "resets_at": "2026-09-11T15:00:00+00:00" },
              "juniper_tide": null,
              "omelette_promotional": { "what": "is this" }
            }
            """)

        #expect(report.windows.count == 1)
        #expect(report.model == nil)
    }

    @Test
    func aWindowWithoutAResetTimeIsSkipped() {
        let report = UsageReport.fromJson("""
            { "five_hour": { "utilization": 12.0, "resets_at": null } }
            """)

        #expect(!report.hasData)
    }

    @Test
    func limitsWithoutAScopedWeeklyEntryHaveNoModelLimit() {
        let report = UsageReport.fromJson("""
            {
              "five_hour": { "utilization": 5.0, "resets_at": "2026-09-11T15:00:00+00:00" },
              "limits": [ { "kind": "session", "percent": 5.0, "resets_at": "2026-09-11T15:00:00+00:00" } ]
            }
            """)

        #expect(report.model == nil)
    }

    @Test(arguments: ["", "nonsense", "[]", "{}"])
    func brokenInputReadsAsNoDataAndNeverThrows(json: String) {
        #expect(!UsageReport.fromJson(json).hasData)
    }
}
