namespace Aiko.Core.Tests;

public class SnapshotFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void What_the_bridge_writes_the_tray_reads_back()
    {
        var snapshot = new LimitSnapshot(
            "Personal",
            LimitSource.StatusLine,
            Now,
            [new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3))]);

        var again = SnapshotFile.FromJson(SnapshotFile.ToJson(snapshot));

        Assert.Equal("Personal", again.Environment);
        Assert.Equal(LimitSource.StatusLine, again.Source);
        Assert.Equal(Now, again.ReceivedAt);
        Assert.Equal(34, again.Windows.Single().Percent);
        Assert.Equal(Now.AddHours(3), again.Windows.Single().ResetsAt);
    }

    [Fact]
    public void The_model_limit_survives_the_round_trip()
    {
        var snapshot = new LimitSnapshot(
            "Personal",
            LimitSource.DirectMode,
            Now,
            [new LimitWindow(LimitKind.FiveHour, 10, Now.AddHours(1))])
        {
            Model = new ModelLimit("Fable", 3, Now.AddDays(4)),
        };

        var again = SnapshotFile.FromJson(SnapshotFile.ToJson(snapshot));

        Assert.Equal("Fable", again.Model!.Value.ModelName);
        Assert.Equal(3, again.Model.Value.Percent);
        Assert.Equal(LimitSource.DirectMode, again.Source);
    }

    [Fact]
    public void The_file_holds_numbers_only_and_no_traces_of_the_session()
    {
        var snapshot = new LimitSnapshot(
            "Personal",
            LimitSource.StatusLine,
            Now,
            [new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3))]);

        var json = SnapshotFile.ToJson(snapshot);

        Assert.DoesNotContain("session", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transcript", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cwd", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enums_are_written_as_words_so_the_file_stays_readable()
    {
        var json = SnapshotFile.ToJson(new LimitSnapshot(
            "Work",
            LimitSource.DirectMode,
            Now,
            [new LimitWindow(LimitKind.SevenDay, 61, Now.AddDays(4))]));

        Assert.Contains("\"DirectMode\"", json);
        Assert.Contains("\"SevenDay\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("half written {")]
    [InlineData("{}")]
    [InlineData("{ \"environment\": \"Personal\", \"windows\": [] }")]
    public void A_file_being_written_right_now_reads_as_nothing_new(string json)
    {
        var snapshot = SnapshotFile.FromJson(json, "Personal");

        Assert.False(snapshot.HasData);
        Assert.Equal("Personal", snapshot.Environment);
    }
}
