namespace Aiko.Core.Tests;

public class TrayMoodTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static ActivityRecord At(SessionActivity activity, double secondsAgo = 0) =>
        new("claude", activity, Now.AddSeconds(-secondsAgo));

    // ---- sessions ----

    [Theory]
    [InlineData(SessionActivity.Working, AikoFace.Working)]
    [InlineData(SessionActivity.Waiting, AikoFace.Waiting)]
    [InlineData(SessionActivity.Done, AikoFace.Done)]
    [InlineData(SessionActivity.Error, AikoFace.Error)]
    [InlineData(SessionActivity.OutOfLimit, AikoFace.Asleep)]
    public void A_session_that_changes_what_it_does_brings_its_face(SessionActivity activity, AikoFace face)
    {
        Assert.Equal(face, TrayMood.ForActivity(null, At(activity), Now));
    }

    [Fact]
    public void The_same_activity_again_brings_nothing()
    {
        Assert.Null(TrayMood.ForActivity(At(SessionActivity.Working, 20), At(SessionActivity.Working), Now));
        Assert.Equal(AikoFace.Done, TrayMood.ForActivity(At(SessionActivity.Working, 20), At(SessionActivity.Done), Now));
    }

    [Fact]
    public void An_old_event_found_in_a_file_is_history()
    {
        Assert.Null(TrayMood.ForActivity(null, At(SessionActivity.Waiting, 600), Now));
    }

    // ---- limits ----

    [Theory]
    [InlineData(70, 90, AikoFace.Tired)]
    [InlineData(89, 95, AikoFace.Tired)]
    [InlineData(95, 100, AikoFace.Asleep)]
    [InlineData(80, 100, AikoFace.Asleep)]
    [InlineData(100, 0, AikoFace.Fresh)]
    [InlineData(92, 3, AikoFace.Fresh)]
    public void A_limit_crossing_a_line_brings_a_face(int before, int after, AikoFace face)
    {
        Assert.Equal(face, TrayMood.ForLimit(before, after));
    }

    [Theory]
    [InlineData(null, 95)]
    [InlineData(40, 60)]
    [InlineData(91, 97)]
    [InlineData(100, 100)]
    [InlineData(20, 10)]
    [InlineData(95, null)]
    public void Moving_inside_a_band_or_a_first_number_brings_nothing(int? before, int? after)
    {
        Assert.Null(TrayMood.ForLimit(before, after));
    }

    [Fact]
    public void The_fullest_window_counts_and_a_window_past_its_reset_is_empty()
    {
        var snapshot = new LimitSnapshot("Aiko", LimitSource.StatusLine, Now,
        [
            new LimitWindow(LimitKind.FiveHour, 97, Now.AddMinutes(-1)),
            new LimitWindow(LimitKind.SevenDay, 41, Now.AddDays(3)),
        ]);

        Assert.Equal(41, TrayMood.HighestPercent(snapshot, Now));
        Assert.Null(TrayMood.HighestPercent(LimitSnapshot.NoData("Aiko"), Now));
    }

    // ---- the moment on the icon ----

    [Fact]
    public void A_face_stays_two_seconds()
    {
        var shown = TrayMood.Next(null, AikoFace.Done, Now);

        Assert.False(TrayMood.IsOver(shown, Now.AddSeconds(1.9)));
        Assert.True(TrayMood.IsOver(shown, Now + TrayMood.ShowFor));
    }

    [Fact]
    public void A_calmer_face_does_not_push_away_one_that_asks_for_you()
    {
        var waiting = TrayMood.Next(null, AikoFace.Waiting, Now);

        Assert.Same(waiting, TrayMood.Next(waiting, AikoFace.Working, Now.AddSeconds(0.5)));
        Assert.Equal(new FaceMoment(AikoFace.Error, Now.AddSeconds(3)), TrayMood.Next(waiting, AikoFace.Error, Now.AddSeconds(3)));
    }

    [Fact]
    public void An_equal_or_stronger_face_starts_its_own_two_seconds()
    {
        var done = TrayMood.Next(null, AikoFace.Done, Now);

        Assert.Equal(new FaceMoment(AikoFace.Done, Now.AddSeconds(1)), TrayMood.Next(done, AikoFace.Done, Now.AddSeconds(1)));
        Assert.Equal(new FaceMoment(AikoFace.Waiting, Now.AddSeconds(1)), TrayMood.Next(done, AikoFace.Waiting, Now.AddSeconds(1)));
    }

    [Fact]
    public void After_its_two_seconds_any_face_can_come()
    {
        var waiting = TrayMood.Next(null, AikoFace.Waiting, Now);

        Assert.Equal(AikoFace.Working, TrayMood.Next(waiting, AikoFace.Working, Now.AddSeconds(2)).Face);
    }

    [Fact]
    public void No_persona_anywhere_means_no_face()
    {
        var off = new EnvironmentSettings([new AikoEnvironment("Aiko", ["C:/a/.claude"])]);

        Assert.False(TrayMood.HasFace(off));
        Assert.True(TrayMood.HasFace(new EnvironmentSettings([new AikoEnvironment("Aiko", ["C:/a/.claude"]) { Persona = true }])));
        Assert.False(TrayMood.HasFace(EnvironmentSettings.Empty));
    }
}
