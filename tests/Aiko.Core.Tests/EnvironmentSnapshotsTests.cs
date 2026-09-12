using Aiko.Core;

namespace Aiko.Core.Tests;

public class EnvironmentSnapshotsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);

    private static LimitSnapshot Snapshot(string file, int percent, double minutesAgo) =>
        new(file, LimitSource.StatusLine, Now.AddMinutes(-minutesAgo),
            [new LimitWindow(LimitKind.FiveHour, percent, Now.AddHours(3))]);

    private static EnvironmentSettings Settings(params AikoEnvironment[] environments) =>
        new(environments);

    [Fact]
    public void The_card_shows_the_name_the_person_gave_not_the_folder()
    {
        var settings = Settings(new AikoEnvironment("Личная", [@"C:\Users\someone\.claude-personal"]));
        var files = new Dictionary<string, LimitSnapshot>
        {
            ["claude-personal"] = Snapshot("claude-personal", 42, minutesAgo: 1),
        };

        var combined = EnvironmentSnapshots.Combine(settings, files);

        Assert.Equal("Личная", combined.Single().Environment);
        Assert.Equal(42, combined.Single().Windows.Single().Percent);
    }

    [Fact]
    public void An_environment_over_several_folders_takes_the_freshest_answer()
    {
        var settings = Settings(new AikoEnvironment("Work",
            [@"C:\Users\someone\.claude", @"C:\Users\someone\.claude-work"]));
        var files = new Dictionary<string, LimitSnapshot>
        {
            ["claude"] = Snapshot("claude", 10, minutesAgo: 40),
            ["claude-work"] = Snapshot("claude-work", 77, minutesAgo: 2),
        };

        var combined = EnvironmentSnapshots.Combine(settings, files);

        Assert.Equal(77, combined.Single().Windows.Single().Percent);
    }

    [Fact]
    public void An_environment_with_no_file_yet_is_still_shown()
    {
        var settings = Settings(new AikoEnvironment("Work", [@"C:\Users\someone\.claude-work"]));

        var combined = EnvironmentSnapshots.Combine(settings, new Dictionary<string, LimitSnapshot>());

        Assert.Equal("Work", combined.Single().Environment);
        Assert.False(combined.Single().HasData);
    }

    [Fact]
    public void A_file_that_belongs_to_no_environment_is_left_out()
    {
        var settings = Settings(new AikoEnvironment("Personal", [@"C:\Users\someone\.claude-personal"]));
        var files = new Dictionary<string, LimitSnapshot>
        {
            ["claude-personal"] = Snapshot("claude-personal", 42, minutesAgo: 1),
            ["claude-someone-else"] = Snapshot("claude-someone-else", 99, minutesAgo: 1),
        };

        var combined = EnvironmentSnapshots.Combine(settings, files);

        Assert.Equal(["Personal"], combined.Select(s => s.Environment));
    }

    [Fact]
    public void Before_the_wizard_has_run_everything_that_arrived_is_shown()
    {
        var files = new Dictionary<string, LimitSnapshot>
        {
            ["claude-work"] = Snapshot("claude-work", 82, minutesAgo: 1),
            ["claude-personal"] = Snapshot("claude-personal", 42, minutesAgo: 1),
        };

        var combined = EnvironmentSnapshots.Combine(EnvironmentSettings.Empty, files);

        Assert.Equal(["claude-personal", "claude-work"], combined.Select(s => s.Environment));
    }

    [Fact]
    public void Environments_keep_the_order_of_the_settings()
    {
        var settings = Settings(
            new AikoEnvironment("Work", [@"C:\Users\someone\.claude-work"]),
            new AikoEnvironment("Personal", [@"C:\Users\someone\.claude-personal"]));

        var combined = EnvironmentSnapshots.Combine(settings, new Dictionary<string, LimitSnapshot>());

        Assert.Equal(["Work", "Personal"], combined.Select(s => s.Environment));
    }
}
