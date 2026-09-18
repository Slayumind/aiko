import Foundation

/// A day on the calendar, without a time and without a zone, like DateOnly on Windows.
public struct DateOnly: Sendable, Equatable, Hashable, Comparable {
    public let year: Int
    public let month: Int
    public let day: Int

    public init(_ year: Int, _ month: Int, _ day: Int) {
        self.year = year
        self.month = month
        self.day = day
    }

    public static func < (left: DateOnly, right: DateOnly) -> Bool {
        (left.year, left.month, left.day) < (right.year, right.month, right.day)
    }

    /// Days since 1970-01-01. Howard Hinnant's civil calendar, so no Calendar and no zone is needed.
    public var daysSinceEpoch: Int {
        let y = year - (month <= 2 ? 1 : 0)
        let era = (y >= 0 ? y : y - 399) / 400
        let yearOfEra = y - era * 400
        let dayOfYear = (153 * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1
        let dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear
        return era * 146097 + dayOfEra - 719468
    }

    public static func fromDaysSinceEpoch(_ days: Int) -> DateOnly {
        let z = days + 719468
        let era = (z >= 0 ? z : z - 146096) / 146097
        let dayOfEra = z - era * 146097
        let yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365
        let year = yearOfEra + era * 400
        let dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100)
        let mp = (5 * dayOfYear + 2) / 153
        let day = dayOfYear - (153 * mp + 2) / 5 + 1
        let month = mp + (mp < 10 ? 3 : -9)
        return DateOnly(year + (month <= 2 ? 1 : 0), month, day)
    }

    /// Monday is 1, Sunday is 7, as ISO counts.
    public var isoWeekday: Int {
        let weekday = (daysSinceEpoch + 4) % 7
        let fromSunday = weekday < 0 ? weekday + 7 : weekday
        return fromSunday == 0 ? 7 : fromSunday
    }

    /// The year an ISO week belongs to: the year that holds the Thursday of that week.
    public var isoWeekYear: Int {
        let thursday = DateOnly.fromDaysSinceEpoch(daysSinceEpoch + (4 - isoWeekday))
        return thursday.year
    }

    public var isoWeekOfYear: Int {
        let thursday = DateOnly.fromDaysSinceEpoch(daysSinceEpoch + (4 - isoWeekday))
        let firstOfYear = DateOnly(thursday.year, 1, 1)
        return (thursday.daysSinceEpoch - firstOfYear.daysSinceEpoch) / 7 + 1
    }
}

/// Reading and writing the times Claude Code and Aiko put into files.
public enum Iso8601 {
    /// A moment and the offset it was written with. Two files may say the same moment in two zones,
    /// and the activity file is written back with the offset it came with.
    public struct Moment: Sendable, Equatable {
        public let date: Date
        public let offsetSeconds: Int

        public init(date: Date, offsetSeconds: Int) {
            self.date = date
            self.offsetSeconds = offsetSeconds
        }
    }

    /// Accepts what the usage API and the activity file hold: a date, a time, an optional fraction
    /// and an optional offset. A time without an offset is read in the local zone, as on Windows.
    public static func parse(_ text: String?) -> Moment? {
        guard let text else { return nil }
        let scalars = Array(text.trimmingCharacters(in: .whitespaces).unicodeScalars)
        guard scalars.count >= 10 else { return nil }

        var at = 0
        func digits(_ count: Int) -> Int? {
            guard at + count <= scalars.count else { return nil }
            var value = 0
            for _ in 0..<count {
                let scalar = scalars[at]
                guard scalar.value >= 48, scalar.value <= 57 else { return nil }
                value = value * 10 + Int(scalar.value - 48)
                at += 1
            }
            return value
        }
        func take(_ character: Unicode.Scalar) -> Bool {
            guard at < scalars.count, scalars[at] == character else { return false }
            at += 1
            return true
        }

        guard let year = digits(4), take("-"), let month = digits(2), take("-"), let day = digits(2) else { return nil }
        guard month >= 1, month <= 12, day >= 1, day <= 31 else { return nil }

        var hour = 0
        var minute = 0
        var second = 0
        var fraction = 0.0
        var offsetSeconds: Int? = nil

        if at < scalars.count {
            guard take("T") || take(" ") else { return nil }
            guard let readHour = digits(2), take(":"), let readMinute = digits(2) else { return nil }
            hour = readHour
            minute = readMinute
            if at < scalars.count && scalars[at] == ":" {
                at += 1
                guard let readSecond = digits(2) else { return nil }
                second = readSecond
            }
            if at < scalars.count && scalars[at] == "." {
                at += 1
                var places = 0
                var value = 0.0
                var scale = 1.0
                while at < scalars.count, scalars[at].value >= 48, scalars[at].value <= 57 {
                    scale /= 10
                    value += Double(scalars[at].value - 48) * scale
                    at += 1
                    places += 1
                }
                guard places > 0 else { return nil }
                fraction = value
            }
            if at < scalars.count {
                if take("Z") || take("z") {
                    offsetSeconds = 0
                } else {
                    let sign: Int
                    if take("+") {
                        sign = 1
                    } else if take("-") {
                        sign = -1
                    } else {
                        return nil
                    }
                    guard let offsetHour = digits(2) else { return nil }
                    var offsetMinute = 0
                    if at < scalars.count {
                        _ = take(":")
                        guard let read = digits(2) else { return nil }
                        offsetMinute = read
                    }
                    offsetSeconds = sign * (offsetHour * 3600 + offsetMinute * 60)
                }
            }
        }

        guard at == scalars.count else { return nil }
        guard hour < 24, minute < 60, second < 60 else { return nil }

        let days = DateOnly(year, month, day).daysSinceEpoch
        let local = Double(days * 86400 + hour * 3600 + minute * 60 + second) + fraction
        let offset = offsetSeconds ?? TimeZone.current.secondsFromGMT(for: Date(timeIntervalSince1970: local))
        return Moment(date: Date(timeIntervalSince1970: local - Double(offset)), offsetSeconds: offset)
    }

    /// The "O" format of Windows: 2026-09-15T12:00:00.0000000+03:00.
    public static func roundTrip(_ date: Date, offsetSeconds: Int) -> String {
        let local = date.timeIntervalSince1970 + Double(offsetSeconds)
        var wholeSeconds = local.rounded(.down)
        var ticks = ((local - wholeSeconds) * 10_000_000).rounded()
        if ticks >= 10_000_000 {
            ticks -= 10_000_000
            wholeSeconds += 1
        }

        let total = Int(wholeSeconds)
        let days = Int((Double(total) / 86400).rounded(.down))
        let secondOfDay = total - days * 86400
        let day = DateOnly.fromDaysSinceEpoch(days)

        let sign = offsetSeconds < 0 ? "-" : "+"
        let offset = abs(offsetSeconds)
        return String(
            format: "%04d-%02d-%02dT%02d:%02d:%02d.%07d%@%02d:%02d",
            day.year, day.month, day.day,
            secondOfDay / 3600, (secondOfDay % 3600) / 60, secondOfDay % 60,
            Int(ticks), sign, offset / 3600, (offset % 3600) / 60)
    }
}
