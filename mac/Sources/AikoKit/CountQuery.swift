import Foundation

/// The query of the daily version check (PRIVACY.md, "Counting"). With counting off it carries the
/// version only. With counting on it adds the system, the identifier of the day, whether the persona
/// is on, and a flag for the first check of a week and of a month: six fields, nothing more.
/// Rows: spec/cases/count-query. The twin of CountQuery.cs.
public enum CountQuery {
    /// Letters, digits and - . _ ~ stay as they are (RFC 3986); everything else is escaped.
    private static let plainCharacters = CharacterSet(
        charactersIn: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~")

    /// day: the identifier of the day (Heartbeat.dailyId), nil when counting is off or the computer
    /// has no steady value to make one from.
    public static func query(
        version: String, system: String, day: String?, persona: Bool, firstOfWeek: Bool, firstOfMonth: Bool
    ) -> String {
        let plain = "?v=\(escape(version))"
        guard let day, !day.isEmpty else {
            return plain
        }

        return plain
            + "&os=\(escape(system))&day=\(escape(day))&p=\(persona ? 1 : 0)"
            + (firstOfWeek ? "&w=1" : "")
            + (firstOfMonth ? "&m=1" : "")
    }

    private static func escape(_ value: String) -> String {
        value.addingPercentEncoding(withAllowedCharacters: plainCharacters) ?? value
    }
}
