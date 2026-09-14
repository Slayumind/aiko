namespace Aiko.Core;

/// When the island opens by itself (D-161): for a moment, when a session limit crosses 75 % or 90 %
/// on the way up. The numbers on the island are then in front of the person without a hover.
public static class IslandReveal
{
    public static readonly TimeSpan For = TimeSpan.FromSeconds(2);

    /// True when a ring went from normal to caution, or from normal or caution to critical, between
    /// two sets of numbers. Going down after a reset is good news and says nothing. Numbers that were
    /// not known before, on the first report or after they went stale, do not count as a crossing:
    /// the island would otherwise open every time Claude Code starts talking again.
    public static bool ToneRose(IReadOnlyList<CardState> before, IReadOnlyList<CardState> after) =>
        after.Any(card =>
            before.FirstOrDefault(old => old.Environment == card.Environment) is { } previous
            && Rank(previous.IconRow?.Tone) is > 0 and var was
            && Rank(card.IconRow?.Tone) > was);

    private static int Rank(LimitTone? tone) => tone switch
    {
        LimitTone.Normal => 1,
        LimitTone.Caution => 2,
        LimitTone.Critical => 3,
        _ => 0,
    };
}
