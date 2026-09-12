namespace Aiko.Core;

public enum LimitSource
{
    StatusLine,
    DirectMode,
}

public enum DataFreshness
{
    None,
    Live,
    Stale,
}

public readonly record struct LimitStatus(LimitKind Kind, int Percent, bool IsReset, ResetCountdown Countdown);

/// What we know about one environment right now: the windows, where they came from and when.
/// The card and the icon ask this type, not the parsers.
public sealed record LimitSnapshot(
    string Environment,
    LimitSource Source,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<LimitWindow> Windows)
{
    /// The status line only speaks while a session is open, so silence is normal, not a failure.
    /// After this long the numbers are shown greyed out and marked with their time.
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(15);

    public static LimitSnapshot NoData(string environment) =>
        new(environment, LimitSource.StatusLine, DateTimeOffset.MinValue, []);

    public bool HasData => Windows.Count > 0;

    /// The model limit is only known in direct mode: the status line does not carry it.
    public ModelLimit? Model { get; init; }

    public static LimitSnapshot FromStatusLine(string environment, DateTimeOffset receivedAt, StatusLineReport report) =>
        report.HasData
            ? new LimitSnapshot(environment, LimitSource.StatusLine, receivedAt, report.Windows)
            : NoData(environment);

    public static LimitSnapshot FromDirectMode(string environment, DateTimeOffset receivedAt, UsageReport report) =>
        report.HasData
            ? new LimitSnapshot(environment, LimitSource.DirectMode, receivedAt, report.Windows) { Model = report.Model }
            : NoData(environment);

    /// Two sources for one environment: the fresher answer wins. Direct mode is asked rarely,
    /// the status line speaks on every reply, so neither is always ahead.
    public static LimitSnapshot Newer(LimitSnapshot first, LimitSnapshot second)
    {
        if (!first.HasData)
        {
            return second;
        }
        if (!second.HasData)
        {
            return first;
        }
        return second.ReceivedAt > first.ReceivedAt ? second : first;
    }

    public DataFreshness FreshnessAt(DateTimeOffset now, TimeSpan? staleAfter = null)
    {
        if (!HasData)
        {
            return DataFreshness.None;
        }
        return now - ReceivedAt > (staleAfter ?? DefaultStaleAfter) ? DataFreshness.Stale : DataFreshness.Live;
    }

    /// A window whose reset time has passed reads as zero, without asking anyone.
    /// Usage only grows while you work, and working makes Claude Code report again — so old
    /// numbers stay true until the window turns over, and then they are simply gone.
    public LimitStatus? StatusAt(DateTimeOffset now, LimitKind kind)
    {
        foreach (var window in Windows)
        {
            if (window.Kind != kind)
            {
                continue;
            }

            var countdown = ResetCountdown.Between(now, window.ResetsAt);
            return countdown.IsReset
                ? new LimitStatus(kind, 0, true, ResetCountdown.Reset)
                : new LimitStatus(kind, window.Percent, false, countdown);
        }
        return null;
    }
}
