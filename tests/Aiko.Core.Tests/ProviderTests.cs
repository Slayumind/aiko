namespace Aiko.Core.Tests;

/// The seam for another agent (D-420): an environment has its list of providers, and a limit window
/// can say how long it lasts and what it is called. Claude says neither, and nothing changes for it.
public class ProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_environment_without_a_list_works_with_Claude()
    {
        var settings = EnvironmentSettings.FromJson("""{ "environments": [ { "name": "Work", "configDirectories": ["C:\\c"] } ] }""");

        Assert.Equal([AgentProvider.Claude], settings.Environments[0].Providers);
    }

    [Fact]
    public void Claude_alone_is_not_written_and_a_name_Aiko_does_not_know_is_left_out()
    {
        var plain = new EnvironmentSettings([new AikoEnvironment("Work", [@"C:\c"])]);
        var unknown = EnvironmentSettings.FromJson("""{ "environments": [ { "name": "Work", "configDirectories": ["C:\\c"], "providers": ["somebody"] } ] }""");

        Assert.DoesNotContain("providers", plain.ToJson());
        Assert.Equal([AgentProvider.Claude], unknown.Environments[0].Providers);
    }

    [Fact]
    public void A_window_with_its_own_length_is_paced_on_that_length()
    {
        // Half of a two hour window is gone and 60% is spent: it runs out before the reset.
        var window = new LimitWindow(LimitKind.FiveHour, 60, Now.AddHours(1), TimeSpan.FromHours(2), "2 hours");
        var snapshot = new LimitSnapshot("other", LimitSource.StatusLine, Now, [window]);

        var row = CardState.From(snapshot, Now).Rows[0];

        Assert.Equal(PaceVerdict.RunsOutBeforeReset, row.Pace.Verdict);
        Assert.Equal("2 hours", LimitRow.From(row).Name);
    }

    [Fact]
    public void A_window_of_Claude_keeps_its_name_and_length()
    {
        var snapshot = new LimitSnapshot("claude", LimitSource.StatusLine, Now, [new LimitWindow(LimitKind.FiveHour, 10, Now.AddHours(4))]);

        var row = CardState.From(snapshot, Now).Rows[0];

        Assert.Null(row.Label);
        Assert.Equal(CardText.WindowName(LimitKind.FiveHour), LimitRow.From(row).Name);
    }

    [Fact]
    public void The_length_and_the_label_survive_the_snapshot_file_and_Claude_files_stay_as_they_were()
    {
        var other = new LimitSnapshot("other", LimitSource.StatusLine, Now, [new LimitWindow(LimitKind.FiveHour, 5, Now.AddHours(1), TimeSpan.FromHours(2), "2 hours")]);
        var claude = new LimitSnapshot("claude", LimitSource.StatusLine, Now, [new LimitWindow(LimitKind.FiveHour, 5, Now.AddHours(1))]);

        var read = SnapshotFile.FromJson(SnapshotFile.ToJson(other)).Windows[0];

        Assert.Equal((TimeSpan.FromHours(2), "2 hours"), (read.Length, read.Label));
        Assert.DoesNotContain("lengthSeconds", SnapshotFile.ToJson(claude));
        Assert.DoesNotContain("label", SnapshotFile.ToJson(claude));
    }
}
