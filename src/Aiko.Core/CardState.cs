namespace Aiko.Core;

public enum LimitTone
{
    /// No numbers at all: Claude Code has not reported yet.
    Unknown,
    Normal,
    Caution,
    Critical,
}

public enum PaceVerdict
{
    /// Nothing to estimate: no data, or the window has just reset.
    Unknown,
    LastsPastReset,
    RunsOutBeforeReset,
}

/// How much is left at the current pace, and until when.
public readonly record struct PaceEstimate(PaceVerdict Verdict, TimeSpan TimeLeft);

/// One row of the card: a window, its colour, its countdown and the estimate.
public readonly record struct CardRow(
    LimitKind Kind,
    int Percent,
    LimitTone Tone,
    bool IsReset,
    ResetCountdown Countdown,
    PaceEstimate Pace,
    /// Only for the weekly limit of a heavy model, and only because the server names it. The card
    /// used to say "Fable" whatever came back, which would be wrong the day that changes.
    string? ModelName = null);

/// What the card and the icon show for one environment. Everything that decides what to show
/// lives here, so the Windows layer only draws and a macOS layer later can reuse it.
public sealed record CardState(
    string Environment,
    DataFreshness Freshness,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<CardRow> Rows)
{
    public const int CautionFrom = 75;
    public const int CriticalFrom = 90;

    /// The windows Claude Code reports, and how long each one lasts. Needed for the pace estimate:
    /// the share of the window already spent is compared with the share of the limit already used.
    public static readonly TimeSpan FiveHourWindow = TimeSpan.FromHours(5);
    public static readonly TimeSpan SevenDayWindow = TimeSpan.FromDays(7);

    /// How long after the last word from the status line a session counts as working. Claude Code
    /// runs the status line as the conversation changes, so an idle or closed session goes quiet;
    /// two minutes rides over a long tool call without calling a closed one "working" for long.
    public static readonly TimeSpan WorkingFor = TimeSpan.FromMinutes(2);

    /// Until when the card may say "working now". Only the status line counts: a direct mode answer
    /// comes from Aiko asking, not from a session doing anything.
    public DateTimeOffset? WorkingUntil { get; init; }

    public bool IsWorkingAt(DateTimeOffset now) => WorkingUntil > now;

    public static DateTimeOffset? WorkingUntilFor(LimitSnapshot snapshot) =>
        snapshot.Source == LimitSource.StatusLine && snapshot.Windows.Count > 0
            ? snapshot.ReceivedAt + WorkingFor
            : null;

    public bool HasData => Rows.Count > 0;

    /// What the ring or the dot shows for this environment: the session window drives the icon,
    /// because it is the one that runs out during a working day.
    ///
    /// Written out on purpose: FirstOrDefault over a struct hands back a zeroed CardRow instead of
    /// nothing, and the icon would then show a made up zero where there is no data at all.
    public CardRow? IconRow
    {
        get
        {
            foreach (var row in Rows)
            {
                if (row.Kind == LimitKind.FiveHour)
                {
                    return row;
                }
            }
            return Rows.Count > 0 ? Rows[0] : null;
        }
    }

    public static LimitTone ToneFor(int percent, DataFreshness freshness)
    {
        if (freshness == DataFreshness.None)
        {
            return LimitTone.Unknown;
        }
        // Stale numbers lose their colour: they are still shown, but they no longer shout.
        if (freshness == DataFreshness.Stale)
        {
            return LimitTone.Unknown;
        }
        return percent >= CriticalFrom ? LimitTone.Critical
            : percent >= CautionFrom ? LimitTone.Caution
            : LimitTone.Normal;
    }

    public static CardState From(LimitSnapshot snapshot, DateTimeOffset now, TimeSpan? staleAfter = null)
    {
        var freshness = snapshot.FreshnessAt(now, staleAfter);
        if (freshness == DataFreshness.None)
        {
            return new CardState(snapshot.Environment, DataFreshness.None, null, []);
        }

        var rows = new List<CardRow>(3);
        AddRow(rows, snapshot, now, freshness, LimitKind.FiveHour, FiveHourWindow);
        AddRow(rows, snapshot, now, freshness, LimitKind.SevenDay, SevenDayWindow);

        if (snapshot.Model is { } model)
        {
            var countdown = ResetCountdown.Between(now, model.ResetsAt);
            rows.Add(new CardRow(
                LimitKind.ModelWeek,
                countdown.IsReset ? 0 : model.Percent,
                ToneFor(model.Percent, freshness),
                countdown.IsReset,
                countdown,
                // The model window length is not reported, so no honest estimate can be made.
                new PaceEstimate(PaceVerdict.Unknown, TimeSpan.Zero),
                model.ModelName));
        }

        return new CardState(snapshot.Environment, freshness, snapshot.ReceivedAt, rows)
        {
            WorkingUntil = WorkingUntilFor(snapshot),
        };
    }

    private static void AddRow(
        List<CardRow> rows,
        LimitSnapshot snapshot,
        DateTimeOffset now,
        DataFreshness freshness,
        LimitKind kind,
        TimeSpan windowLength)
    {
        if (snapshot.StatusAt(now, kind) is not { } status)
        {
            return;
        }

        rows.Add(new CardRow(
            kind,
            status.Percent,
            ToneFor(status.Percent, freshness),
            status.IsReset,
            status.Countdown,
            EstimatePace(status, windowLength, now, snapshot.WindowResetsAt(kind))));
    }

    /// How much of the window has to be gone, and how much of the limit spent, before a guess is
    /// worth making.
    ///
    /// The rule below stretches one measurement across the whole window. Two minutes into a five
    /// hour window, one percent spent reads as "runs out in three hours", stated as confidently as
    /// any other answer. A wrong number said plainly is worse than no number: somebody stops
    /// working because of it. Below either line the card says nothing at all.
    internal const double LeastPercentForPace = 5;

    internal const double LeastShareOfWindowForPace = 0.1;

    /// The simple rule the card explains in words: what has been spent over the part of the window
    /// already gone keeps being spent at the same rate. If the limit would run out after the reset,
    /// the window lasts; otherwise we say how long is left.
    internal static PaceEstimate EstimatePace(
        LimitStatus status,
        TimeSpan windowLength,
        DateTimeOffset now,
        DateTimeOffset? resetsAt)
    {
        if (status.IsReset || resetsAt is not { } reset || status.Percent <= 0)
        {
            return new PaceEstimate(PaceVerdict.Unknown, TimeSpan.Zero);
        }

        var left = reset - now;
        var spent = windowLength - left;
        if (spent <= TimeSpan.Zero)
        {
            return new PaceEstimate(PaceVerdict.Unknown, TimeSpan.Zero);
        }

        if (status.Percent < LeastPercentForPace
            || spent < windowLength * LeastShareOfWindowForPace)
        {
            return new PaceEstimate(PaceVerdict.Unknown, TimeSpan.Zero);
        }

        var percentPerTick = status.Percent / spent.TotalSeconds;
        var secondsToFull = (100 - status.Percent) / percentPerTick;
        var timeToFull = TimeSpan.FromSeconds(secondsToFull);

        return timeToFull >= left
            ? new PaceEstimate(PaceVerdict.LastsPastReset, left)
            : new PaceEstimate(PaceVerdict.RunsOutBeforeReset, timeToFull);
    }
}
