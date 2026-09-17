import CryptoKit
import Foundation

/// The one number the author of Aiko wants to know: roughly how many people use it on a given day.
///
/// It rides along with the update check, which already asks the same server once a day, so there
/// is one connection to explain instead of two and one switch to turn both off.
///
/// What makes this bearable is that the identifier changes every day. A random value is made once
/// on this computer and never leaves it; what travels is a hash of that value together with the
/// date. Two days of traffic cannot be joined into one person, so the count is of days, not of
/// people followed over time. That is the number that was wanted anyway.
public enum Heartbeat {
    /// Long enough that two installs do not collide in a day, short enough to be useless for
    /// anything else.
    public static let idLength = 16

    public static func dailyId(_ installId: String, _ day: DateOnly) -> String {
        if installId.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return ""
        }

        let material = "\(installId):\(dayText(day))"
        let digest = SHA256.hash(data: Data(material.utf8))
        let hex = digest.map { String(format: "%02x", $0) }.joined()
        return String(hex.prefix(idLength))
    }

    /// A week and a month cannot be counted from daily identifiers: two days never join up, which
    /// is the whole point of them. So the copy answers the question itself. It remembers on this
    /// computer which week and which month it has already reported, and sends one bit with the
    /// daily ping: "this is my first run in that period". The server adds the ones up.
    ///
    /// The cost is that the windows are calendar ones, not the last 7 or 30 days. A rolling window
    /// would need an identifier that outlives it, and that is a name.
    public static func weekKey(_ day: DateOnly) -> String {
        String(format: "%d-W%02d", day.isoWeekYear, day.isoWeekOfYear)
    }

    public static func monthKey(_ day: DateOnly) -> String {
        String(format: "%04d-%02d", day.year, day.month)
    }

    /// Null or an empty value means nothing was reported yet, so this run is the first one.
    public static func firstThisWeek(_ reported: String?, _ day: DateOnly) -> Bool {
        reported != weekKey(day)
    }

    public static func firstThisMonth(_ reported: String?, _ day: DateOnly) -> Bool {
        reported != monthKey(day)
    }

    private static func dayText(_ day: DateOnly) -> String {
        String(format: "%04d-%02d-%02d", day.year, day.month, day.day)
    }
}
