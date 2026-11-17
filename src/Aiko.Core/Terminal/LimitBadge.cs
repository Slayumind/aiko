namespace Aiko.Core.Terminal;

/// The one percentage in the title bar of the terminal: the limit that is closer to its end (D-296).
public readonly record struct LimitBadge(LimitKind Kind, int Percent, LimitTone Tone)
{
    private static readonly LimitKind[] Kinds = [LimitKind.FiveHour, LimitKind.SevenDay];

    /// Null when nothing is known. When both windows are equally full, the five hour one is named:
    /// it is the one that turns over first.
    public static LimitBadge? For(LimitSnapshot snapshot, DateTimeOffset now)
    {
        LimitStatus? fullest = null;
        foreach (var kind in Kinds)
        {
            if (snapshot.StatusAt(now, kind) is { } status && (fullest is null || status.Percent > fullest.Value.Percent))
            {
                fullest = status;
            }
        }

        return fullest is { } chosen
            ? new LimitBadge(chosen.Kind, chosen.Percent, CardState.ToneFor(chosen.Percent, snapshot.FreshnessAt(now)))
            : null;
    }
}
