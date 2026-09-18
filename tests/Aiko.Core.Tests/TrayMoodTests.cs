namespace Aiko.Core.Tests;

public class TrayMoodTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static ActivityRecord At(SessionActivity activity, double secondsAgo = 0) =>
        new("claude", activity, Now.AddSeconds(-secondsAgo));

    // Which face an event brings is a table, and it lives in spec/cases/tray-mood; both cores read
    // it. What is left here needs a snapshot, a clock or a settings object.

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
