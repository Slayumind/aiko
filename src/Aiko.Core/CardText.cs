namespace Aiko.Core;

/// The words of the limits. The rules hand over numbers and say which shape they take; the
/// sentence is built here from the table of words, the same way on both systems.
///
/// The twin of CardText.swift. Rows: spec/cases/card-words.
public static class CardText
{
    public static string WindowName(LimitKind kind, string? modelName = null) => kind switch
    {
        LimitKind.FiveHour => Strings.CardSession,
        LimitKind.SevenDay => Strings.CardWeek,
        _ => Format(Strings.CardModelWeek, modelName ?? "model"),
    };

    /// The share of the limit already gone, said so. A bare "42%" reads as forty two percent left
    /// just as easily as forty two percent spent, and the two are opposite news.
    public static string Percent(int percent) => Format(Strings.CardPercent, percent);

    /// The short name of a window, for a narrow place: "5 h", "wk", or the name of the model alone.
    /// Windows only: the open header of the terminal window has no twin on the Mac yet.
    public static string ShortWindowName(LimitKind kind, string? modelName = null) => kind switch
    {
        LimitKind.FiveHour => Strings.CardShortSession,
        LimitKind.SevenDay => Strings.CardShortWeek,
        _ => modelName ?? WindowName(kind),
    };

    /// A word beside the colour.
    ///
    /// The design system says a status is a mark, a word and a colour, never a colour on its own.
    /// Green and red are one grey to a good many people, and to every screen reader.
    public static string Tone(LimitTone tone) => tone switch
    {
        LimitTone.Caution => Strings.CardToneCaution,
        LimitTone.Critical => Strings.CardToneCritical,
        _ => string.Empty,
    };

    public static string Resets(ResetCountdown countdown) =>
        countdown.IsReset ? Strings.CardJustReset : Format(Strings.CardResets, Left(countdown));

    /// The time left on its own, "1h 6m", for a place where the column already says it is the
    /// time to a reset (the open header of the terminal window).
    public static string Countdown(ResetCountdown countdown) =>
        countdown.IsReset ? Strings.CardJustReset : Left(countdown);

    /// How long the limit lasts at the current pace. An empty answer is honest: early in a window
    /// there is nothing to work it out from, and a confident wrong number is worse than none.
    public static string Pace(PaceEstimate pace) => pace.Verdict switch
    {
        PaceVerdict.LastsPastReset => Strings.CardLastsUntilReset,
        PaceVerdict.RunsOutBeforeReset => Format(Strings.CardAtThisPace, Span(pace.TimeLeft)),
        _ => string.Empty,
    };

    /// The status line only speaks while a session is open, so the numbers always say how old
    /// they are instead of pretending they are live. With Claude Code closed the numbers are not
    /// wrong, only old, and that is the normal state of things most of the day.
    public static string Updated(DataFreshness freshness, DateTimeOffset? updatedAt, TimeZoneInfo? zone = null) => freshness switch
    {
        DataFreshness.None => Strings.CardNoDataYet,
        DataFreshness.Stale => Format(Strings.CardLastSeen, Clock(updatedAt, zone)),
        _ => Format(Strings.CardUpdated, Clock(updatedAt, zone)),
    };

    /// "HH:mm" on the clock of the machine, not of the language: somebody reading Aiko in English
    /// on a Russian computer still wants their own clock.
    public static string Clock(DateTimeOffset? at, TimeZoneInfo? zone = null) =>
        at is { } time ? TimeZoneInfo.ConvertTime(time, zone ?? TimeZoneInfo.Local).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "";

    public static string NoDataNote => Strings.CardNoDataNote;

    /// For an environment where Aiko's line is not in the settings at all. Opening a terminal will
    /// not help there, and telling somebody to do it anyway wastes their time and our credit.
    public static string NoAccessNote => Strings.CardNoAccessNote;

    /// Fills {0}, {1} and so on. Numbers are written without separators, as Strings.format does on
    /// the Mac, so both systems say the same thing.
    internal static string Format(string text, params object[] arguments) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, text, arguments);

    private static string Left(ResetCountdown countdown) => countdown.Unit switch
    {
        CountdownUnit.DaysAndHours => Format(Strings.SpanDaysHours, countdown.Days, countdown.Hours),
        CountdownUnit.HoursAndMinutes => Format(Strings.SpanHoursMinutes, countdown.Hours, countdown.Minutes),
        CountdownUnit.Minutes => Format(Strings.SpanMinutes, countdown.Minutes),
        _ => Strings.SpanUnderMinute,
    };

    private static string Span(TimeSpan left)
    {
        var whole = left < TimeSpan.Zero ? TimeSpan.Zero : left;
        if (whole.TotalDays >= 1)
        {
            return Format(Strings.SpanDaysHours, (int)whole.TotalDays, whole.Hours);
        }

        if (whole.TotalHours >= 1)
        {
            return Format(Strings.SpanHoursMinutes, (int)whole.TotalHours, whole.Minutes);
        }

        return whole.TotalMinutes >= 1
            ? Format(Strings.SpanMinutes, (int)whole.TotalMinutes)
            : Strings.SpanUnderMinute;
    }
}
