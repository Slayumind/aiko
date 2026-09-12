using Aiko.Core;

namespace Aiko.App;

/// The words of the card. The core hands over numbers and says which shape they take; the
/// sentence is built here, so neither language lives in the core.
///
/// The words themselves are in Strings.resx, one file per language.
static class CardText
{
    public static string WindowName(LimitKind kind, string? modelName = null) => kind switch
    {
        LimitKind.FiveHour => Strings.CardSession,
        LimitKind.SevenDay => Strings.CardWeek,
        _ => string.Format(Strings.CardModelWeek, modelName ?? "model"),
    };

    /// The share of the limit already gone, said so. A bare "42%" reads as forty two percent left
    /// just as easily as forty two percent spent, and the two are opposite news.
    public static string Percent(int percent) => string.Format(Strings.CardPercentUsed, percent);

    /// A word beside the colour.
    ///
    /// The design system says a status is a mark, a word and a colour, never a colour on its own,
    /// and the card was breaking its own rule. Green and red are one grey to a good many people,
    /// and to every screen reader.
    public static string Tone(LimitTone tone) => tone switch
    {
        LimitTone.Caution => Strings.CardToneCaution,
        LimitTone.Critical => Strings.CardToneCritical,
        _ => string.Empty,
    };

    public static string Resets(ResetCountdown countdown) =>
        countdown.IsReset ? Strings.CardJustReset : string.Format(Strings.CardResets, Left(countdown));

    private static string Left(ResetCountdown countdown) => countdown.Unit switch
    {
        CountdownUnit.DaysAndHours => string.Format(Strings.SpanDaysHours, countdown.Days, countdown.Hours),
        CountdownUnit.HoursAndMinutes => string.Format(Strings.SpanHoursMinutes, countdown.Hours, countdown.Minutes),
        CountdownUnit.Minutes => string.Format(Strings.SpanMinutes, countdown.Minutes),
        _ => Strings.SpanUnderMinute,
    };

    /// How long the limit lasts at the current pace. An empty answer is honest: early in a window
    /// there is nothing to work it out from, and a confident wrong number is worse than none.
    public static string Pace(PaceEstimate pace) => pace.Verdict switch
    {
        PaceVerdict.LastsPastReset => Strings.CardLastsUntilReset,
        PaceVerdict.RunsOutBeforeReset => string.Format(Strings.CardAtThisPace, Span(pace.TimeLeft)),
        _ => string.Empty,
    };

    private static string Span(TimeSpan left)
    {
        if (left.TotalDays >= 1)
        {
            return string.Format(Strings.SpanDaysHours, (int)left.TotalDays, left.Hours);
        }
        if (left.TotalHours >= 1)
        {
            return string.Format(Strings.SpanHoursMinutes, (int)left.TotalHours, left.Minutes);
        }
        return left.TotalMinutes >= 1
            ? string.Format(Strings.SpanMinutes, (int)left.TotalMinutes)
            : Strings.SpanUnderMinute;
    }

    /// The status line only speaks while a session is open, so the card always says how old its
    /// numbers are instead of pretending they are live.
    ///
    /// "Last seen" rather than anything about staleness: with Claude Code closed the numbers are
    /// not wrong, only old, and that is the normal state of things most of the day.
    public static string Updated(DataFreshness freshness, DateTimeOffset? updatedAt) => freshness switch
    {
        DataFreshness.None => Strings.CardNoDataYet,
        DataFreshness.Stale => string.Format(Strings.CardLastSeen, $"{updatedAt:HH:mm}"),
        _ => string.Format(Strings.CardUpdated, $"{updatedAt:HH:mm}"),
    };

    public static string NoDataNote => Strings.CardNoDataNote;

    /// For an environment where Aiko's line is not in the settings at all. Opening a terminal will
    /// not help there, and telling somebody to do it anyway wastes their time and our credit.
    public static string NoAccessNote => Strings.CardNoAccessNote;
}
