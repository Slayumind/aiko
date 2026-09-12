namespace Aiko.Core;

/// How long to wait before asking the usage API again. Direct mode only: the status line
/// costs nothing and is never polled.
///
/// The endpoint answers 429 often and with no retry-after at all, and Anthropic closed those
/// reports as not planned — so a client that keeps knocking only makes it worse.
public sealed record PollBackoff(TimeSpan Delay, int FailuresInARow)
{
    public static readonly TimeSpan Normal = TimeSpan.FromSeconds(180);
    public static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(900);

    public static readonly PollBackoff Start = new(Normal, 0);

    public PollBackoff AfterSuccess() => Start;

    /// A 429 with a retry-after is obeyed, within reason: never shorter than the normal pace,
    /// never longer than the ceiling. Without a usable header the delay doubles.
    public PollBackoff AfterTooManyRequests(TimeSpan? retryAfter)
    {
        if (retryAfter is { } wait && wait > TimeSpan.Zero)
        {
            return new PollBackoff(Clamp(wait), FailuresInARow + 1);
        }
        return AfterFailure();
    }

    public PollBackoff AfterFailure()
    {
        var failures = FailuresInARow + 1;
        var doubled = TimeSpan.FromTicks(Normal.Ticks * (long)Math.Pow(2, Math.Min(failures, 8)));
        return new PollBackoff(Clamp(doubled), failures);
    }

    public DateTimeOffset NextAttemptAfter(DateTimeOffset now) => now + Delay;

    private static TimeSpan Clamp(TimeSpan value) =>
        value < Normal ? Normal : value > Ceiling ? Ceiling : value;
}
