import Foundation
import Testing

@testable import AikoKit

/// A moment in UTC, the way the Windows tests write `new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero)`.
func utc(_ year: Int, _ month: Int, _ day: Int, _ hour: Int = 0, _ minute: Int = 0, _ second: Double = 0) -> Date {
    let days = DateOnly(year, month, day).daysSinceEpoch
    return Date(timeIntervalSince1970: Double(days * 86400 + hour * 3600 + minute * 60) + second)
}

extension Date {
    func adding(seconds: Double) -> Date { addingTimeInterval(seconds) }
    func adding(minutes: Double) -> Date { addingTimeInterval(minutes * 60) }
    func adding(hours: Double) -> Date { addingTimeInterval(hours * 3600) }
    func adding(days: Double) -> Date { addingTimeInterval(days * 86400) }
}

/// The same comparison as Assert.Equal(expected, actual, precision) in xUnit: both sides are
/// rounded to that many places first.
func expectClose(_ expected: Double, _ actual: Double, places: Int, sourceLocation: SourceLocation = #_sourceLocation) {
    let scale = pow(10.0, Double(places))
    let left = (expected * scale).rounded(.toNearestOrEven) / scale
    let right = (actual * scale).rounded(.toNearestOrEven) / scale
    #expect(left == right, "expected \(expected), got \(actual)", sourceLocation: sourceLocation)
}
