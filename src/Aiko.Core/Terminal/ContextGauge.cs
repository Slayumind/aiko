namespace Aiko.Core.Terminal;

/// How the ring of the context looks: quiet up to 75 %, orange up to 90 %, red above (D-342).
public enum ContextLevel
{
    Quiet,
    Warm,
    Hot,
}

/// How much of its context window the conversation fills.
///
/// The size comes from the stream, with no request of its own: the tokens of the last message of
/// Claude are what the next call sends again, and the window is in the result of the turn. The
/// spike of 2026-10-27 found this within 0.02 % of what /context says (RESEARCH).
public static class ContextGauge
{
    /// Null until both numbers are known.
    public static int? Percent(int tokens, int window) =>
        tokens > 0 && window > 0
            ? (int)Math.Clamp(Math.Round(tokens * 100.0 / window, MidpointRounding.AwayFromZero), 0, 100)
            : null;

    public static ContextLevel Level(int percent) =>
        percent <= 75 ? ContextLevel.Quiet
        : percent <= 90 ? ContextLevel.Warm
        : ContextLevel.Hot;

    /// The window of the model in use. Claude Code names the model with a suffix at times
    /// ("claude-opus-5-5[1m]"), so its name only has to start with the key. When no key fits and
    /// there is just one, that one is the model; otherwise the window stays as it was.
    public static int WindowFor(string model, IReadOnlyDictionary<string, int> windows, int known)
    {
        foreach (var (name, window) in windows)
        {
            if (name.Length > 0 && model.StartsWith(name, StringComparison.Ordinal))
            {
                return window;
            }
        }
        return windows.Count == 1 ? windows.Values.First() : known;
    }
}
