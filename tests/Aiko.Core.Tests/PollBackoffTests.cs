namespace Aiko.Core.Tests;

public class PollBackoffTests
{
    [Fact]
    public void Starts_at_the_normal_pace()
    {
        Assert.Equal(TimeSpan.FromSeconds(180), PollBackoff.Start.Delay);
        Assert.Equal(0, PollBackoff.Start.FailuresInARow);
    }

    [Fact]
    public void A_failure_doubles_the_wait()
    {
        var backoff = PollBackoff.Start.AfterFailure();

        Assert.Equal(TimeSpan.FromSeconds(360), backoff.Delay);
        Assert.Equal(1, backoff.FailuresInARow);
    }

    [Fact]
    public void The_wait_never_grows_past_the_ceiling()
    {
        var backoff = PollBackoff.Start;
        for (var i = 0; i < 10; i++)
        {
            backoff = backoff.AfterFailure();
        }

        Assert.Equal(TimeSpan.FromSeconds(900), backoff.Delay);
    }

    [Fact]
    public void A_retry_after_header_is_obeyed()
    {
        var backoff = PollBackoff.Start.AfterTooManyRequests(TimeSpan.FromSeconds(600));

        Assert.Equal(TimeSpan.FromSeconds(600), backoff.Delay);
    }

    [Fact]
    public void A_retry_after_shorter_than_the_normal_pace_does_not_speed_us_up()
    {
        var backoff = PollBackoff.Start.AfterTooManyRequests(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(180), backoff.Delay);
    }

    [Fact]
    public void A_retry_after_longer_than_the_ceiling_is_trimmed()
    {
        var backoff = PollBackoff.Start.AfterTooManyRequests(TimeSpan.FromHours(2));

        Assert.Equal(TimeSpan.FromSeconds(900), backoff.Delay);
    }

    [Fact]
    public void A_429_without_a_usable_header_falls_back_to_doubling()
    {
        // This endpoint answers with retry-after: 0 or no header at all, which is why it matters.
        Assert.Equal(TimeSpan.FromSeconds(360), PollBackoff.Start.AfterTooManyRequests(null).Delay);
        Assert.Equal(TimeSpan.FromSeconds(360), PollBackoff.Start.AfterTooManyRequests(TimeSpan.Zero).Delay);
    }

    [Fact]
    public void A_good_answer_puts_the_pace_back()
    {
        var backoff = PollBackoff.Start.AfterFailure().AfterFailure().AfterSuccess();

        Assert.Equal(TimeSpan.FromSeconds(180), backoff.Delay);
        Assert.Equal(0, backoff.FailuresInARow);
    }

    [Fact]
    public void Knows_when_the_next_attempt_is_due()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(now.AddSeconds(180), PollBackoff.Start.NextAttemptAfter(now));
    }
}
