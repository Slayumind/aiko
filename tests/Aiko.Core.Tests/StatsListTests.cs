using Aiko.Core;

namespace Aiko.Core.Tests;

/// A yes to the daily count is a yes to the list that was shown. These tests hold the promise that
/// a value added later is not sent on the strength of an older yes.
public class StatsListTests
{
    private static readonly EnvironmentSettings SetUp = new([new AikoEnvironment("Aiko", [@"C:\Users\someone\.claude"])]);

    private static AppSettings App(bool sendStats, int seen) =>
        AppSettings.Default with { SendStats = sendStats, StatsListSeen = seen };

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, 2, true)]
    [InlineData(true, 3, true)]
    [InlineData(false, 2, false)]
    public void The_new_flags_go_only_after_the_list_that_names_them_was_seen(bool sendStats, int seen, bool sends)
    {
        Assert.Equal(sends, StatsList.SendsSkillsAndMemory(App(sendStats, seen)));
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, false)]
    [InlineData(false, 0, false)]
    public void Only_somebody_counted_under_an_older_list_is_asked_to_look_again(bool sendStats, int seen, bool asks)
    {
        Assert.Equal(asks, StatsList.AsksToLookAgain(App(sendStats, seen), SetUp));
    }

    [Fact]
    public void Nobody_is_asked_before_aiko_is_set_up()
    {
        Assert.False(StatsList.AsksToLookAgain(App(sendStats: true, seen: 0), EnvironmentSettings.Empty));
    }

    [Fact]
    public void Seeing_the_list_is_noted_once_and_stops_the_question()
    {
        var before = App(sendStats: true, seen: 0);

        var after = StatsList.Seen(before);

        Assert.Equal(StatsList.Current, after.StatsListSeen);
        Assert.True(after.SendStats);
        Assert.False(StatsList.AsksToLookAgain(after, SetUp));
        Assert.Same(after, StatsList.Seen(after));
    }

    [Fact]
    public void A_settings_file_from_before_the_list_grew_reads_as_not_seen_and_the_number_is_kept()
    {
        var old = AppSettings.FromJson("""{ "schemaVersion": 3, "sendStats": true, "privacyAsked": true }""");

        Assert.Equal(0, old.StatsListSeen);
        Assert.Equal(StatsList.Current, AppSettings.FromJson(StatsList.Seen(old).ToJson()).StatsListSeen);
    }
}
