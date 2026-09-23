import Foundation

/// The words of the card. The core hands over numbers and says which shape they take; the
/// sentence is built here, so neither language lives in the rules.
///
/// The twin of CardText.cs on Windows. The words themselves are in Strings.swift, made from the
/// same list as the Windows resource files.
public enum CardText {
    public static func windowName(_ kind: LimitKind, _ modelName: String? = nil) -> String {
        switch kind {
        case .fiveHour:
            return Strings.cardSession
        case .sevenDay:
            return Strings.cardWeek
        case .modelWeek:
            return Strings.format(Strings.cardModelWeek, modelName ?? "model")
        }
    }

    /// The share of the limit already gone, said so. A bare "42%" reads as forty two percent left
    /// just as easily as forty two percent spent, and the two are opposite news.
    public static func percent(_ percent: Int) -> String {
        Strings.format(Strings.cardPercent, percent)
    }

    /// A word beside the colour.
    ///
    /// The design system says a status is a mark, a word and a colour, never a colour on its own,
    /// and the card was breaking its own rule. Green and red are one grey to a good many people,
    /// and to every screen reader.
    public static func tone(_ tone: LimitTone) -> String {
        switch tone {
        case .caution:
            return Strings.cardToneCaution
        case .critical:
            return Strings.cardToneCritical
        default:
            return ""
        }
    }

    public static func resets(_ countdown: ResetCountdown) -> String {
        countdown.isReset
            ? Strings.cardJustReset
            : Strings.format(Strings.cardResets, left(countdown))
    }

    private static func left(_ countdown: ResetCountdown) -> String {
        switch countdown.unit {
        case .daysAndHours:
            return Strings.format(Strings.spanDaysHours, countdown.days, countdown.hours)
        case .hoursAndMinutes:
            return Strings.format(Strings.spanHoursMinutes, countdown.hours, countdown.minutes)
        case .minutes:
            return Strings.format(Strings.spanMinutes, countdown.minutes)
        default:
            return Strings.spanUnderMinute
        }
    }

    /// How long the limit lasts at the current pace. An empty answer is honest: early in a window
    /// there is nothing to work it out from, and a confident wrong number is worse than none.
    public static func pace(_ pace: PaceEstimate) -> String {
        switch pace.verdict {
        case .lastsPastReset:
            return Strings.cardLastsUntilReset
        case .runsOutBeforeReset:
            return Strings.format(Strings.cardAtThisPace, span(pace.timeLeft))
        default:
            return ""
        }
    }

    private static func span(_ left: TimeInterval) -> String {
        let whole = max(left, 0)
        let hours = Int(whole / 3600) % 24
        let minutes = Int(whole / 60) % 60

        if whole >= 86400 {
            return Strings.format(Strings.spanDaysHours, Int(whole / 86400), hours)
        }
        if whole >= 3600 {
            return Strings.format(Strings.spanHoursMinutes, hours, minutes)
        }
        return whole >= 60
            ? Strings.format(Strings.spanMinutes, Int(whole / 60))
            : Strings.spanUnderMinute
    }

    /// The status line only speaks while a session is open, so the card always says how old its
    /// numbers are instead of pretending they are live.
    ///
    /// "As of" rather than anything about staleness: with Claude Code closed the numbers are not
    /// wrong, only old, and that is the normal state of things most of the day.
    public static func updated(
        _ freshness: DataFreshness, _ updatedAt: Date?, timeZone: TimeZone = .current
    ) -> String {
        switch freshness {
        case .none:
            return Strings.cardNoDataYet
        case .stale:
            return Strings.format(Strings.cardLastSeen, clock(updatedAt, timeZone))
        default:
            return Strings.format(Strings.cardUpdated, clock(updatedAt, timeZone))
        }
    }

    /// The same "HH:mm" the Windows card shows. The clock is the machine's, not the language's:
    /// somebody reading Aiko in English on a Russian computer still wants their own clock.
    public static func clock(_ date: Date?, _ timeZone: TimeZone = .current) -> String {
        guard let date else { return "" }

        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        let parts = calendar.dateComponents([.hour, .minute], from: date)
        return String(format: "%02d:%02d", parts.hour ?? 0, parts.minute ?? 0)
    }

    public static var noDataNote: String { Strings.cardNoDataNote }

    /// For an environment where Aiko's line is not in the settings at all. Opening a terminal will
    /// not help there, and telling somebody to do it anyway wastes their time and our credit.
    public static var noAccessNote: String { Strings.cardNoAccessNote }
}
