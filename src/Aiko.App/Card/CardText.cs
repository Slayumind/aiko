using Aiko.Core;

namespace Aiko.App;

/// The words of the card. The core hands over numbers and says which shape they take; the
/// sentence is built here, so neither language lives in the core.
static class CardText
{
    public static string WindowName(LimitKind kind) => kind switch
    {
        LimitKind.FiveHour => "Session · 5 hours",
        LimitKind.SevenDay => "Week",
        _ => "Fable · week",
    };

    /// The share of the limit already gone, said so. A bare "82%" reads as eighty two percent left
    /// just as easily as eighty two percent spent, and the two are opposite news.
    public static string Percent(int percent) => $"{percent}% used";

    /// A word beside the colour.
    ///
    /// The design system says a status is a mark, a word and a colour, never a colour on its own,
    /// and the card was breaking its own rule. Green and red are one grey to a good many people,
    /// and to every screen reader.
    public static string Tone(LimitTone tone) => tone switch
    {
        LimitTone.Caution => "running low",
        LimitTone.Critical => "almost gone",
        _ => string.Empty,
    };

    public static string Resets(ResetCountdown countdown) =>
        countdown.IsReset ? "window just reset" : $"resets in {Left(countdown)}";

    private static string Left(ResetCountdown countdown) => countdown.Unit switch
    {
        CountdownUnit.DaysAndHours => $"{countdown.Days}d {countdown.Hours}h",
        CountdownUnit.HoursAndMinutes => $"{countdown.Hours}h {countdown.Minutes}m",
        CountdownUnit.Minutes => $"{countdown.Minutes}m",
        _ => "<1m",
    };

    /// How long the limit lasts at the current pace. An empty answer is honest: with no spending
    /// yet, or right after a reset, there is nothing to work it out from.
    public static string Pace(PaceEstimate pace) => pace.Verdict switch
    {
        PaceVerdict.LastsPastReset => "lasts until reset",
        PaceVerdict.RunsOutBeforeReset => $"~{Span(pace.TimeLeft)} at this pace",
        _ => string.Empty,
    };

    private static string Span(TimeSpan left)
    {
        if (left.TotalDays >= 1)
        {
            return $"{(int)left.TotalDays}d {left.Hours}h";
        }
        if (left.TotalHours >= 1)
        {
            return $"{(int)left.TotalHours}h {left.Minutes}m";
        }
        return left.TotalMinutes >= 1 ? $"{(int)left.TotalMinutes}m" : "<1m";
    }

    /// The status line only speaks while a session is open, so the card always says how old its
    /// numbers are instead of pretending they are live.
    public static string Updated(DataFreshness freshness, DateTimeOffset? updatedAt) => freshness switch
    {
        DataFreshness.None => "no data yet",
        DataFreshness.Stale => $"data from {updatedAt:HH:mm}",
        _ => $"updated {updatedAt:HH:mm}",
    };

    public const string NoDataNote = "Open Claude Code. The limits show up after the first answer.";
}
