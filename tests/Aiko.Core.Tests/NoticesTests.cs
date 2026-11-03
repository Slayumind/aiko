using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Which changes bring a notice and how it reaches the person, from spec/cases/notices (D-375, D-377).
public class NoticesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_session_case_brings_the_same_notice()
    {
        foreach (var row in SpecCases.Json("notices").GetProperty("sessions").EnumerateArray())
        {
            TerminalActivity? before = row.GetProperty("before").ValueKind == JsonValueKind.Null
                ? null
                : Enum.Parse<TerminalActivity>(row.GetProperty("before").GetString()!);
            var now = Enum.Parse<TerminalActivity>(row.GetProperty("now").GetString()!);
            NoticeKind? expected = row.GetProperty("notice").ValueKind == JsonValueKind.Null
                ? null
                : Enum.Parse<NoticeKind>(row.GetProperty("notice").GetString()!);

            Assert.Equal(expected, Notices.ForSession(before, now));
        }
    }

    [Fact]
    public void Every_route_case_goes_the_same_way()
    {
        foreach (var row in SpecCases.Json("notices").GetProperty("routes").EnumerateArray())
        {
            var off = row.TryGetProperty("off", out var list)
                ? list.EnumerateArray().Select(item => item.GetString()!).ToHashSet()
                : [];
            var choice = new NoticeChoice(
                Waiting: !off.Contains("Waiting"),
                Done: !off.Contains("Done"),
                Stopped: !off.Contains("Stopped"),
                Limit: !off.Contains("Limit"),
                Sound: !off.Contains("Sound"));
            var kind = Enum.Parse<NoticeKind>(row.GetProperty("kind").GetString()!);

            Assert.Equal(
                Enum.Parse<NoticeWay>(row.GetProperty("way").GetString()!),
                Notices.Route(kind, choice, row.GetProperty("inFront").GetBoolean(), row.GetProperty("open").GetBoolean()));
        }
    }

    [Fact]
    public void Every_limit_case_crosses_the_same_way()
    {
        static int? Number(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();

        foreach (var row in SpecCases.Json("notices").GetProperty("limits").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("crossed").GetBoolean(),
                Notices.CrossedLimit(Number(row.GetProperty("before")), Number(row.GetProperty("now"))));
        }
    }

    [Fact]
    public void A_limit_watch_says_once_which_window_of_which_environment_crossed()
    {
        var watch = new LimitWatch();

        Assert.Empty(watch.Next([Snapshot("Aiko", fiveHour: 80, week: 40)], Now));
        var crossed = watch.Next([Snapshot("Aiko", fiveHour: 91, week: 40)], Now);
        Assert.Equal([new LimitCrossing("Aiko", LimitKind.FiveHour, 91)], crossed);
        Assert.Empty(watch.Next([Snapshot("Aiko", fiveHour: 95, week: 40)], Now));
    }

    [Fact]
    public void A_limit_watch_keeps_the_environments_apart()
    {
        var watch = new LimitWatch();
        watch.Next([Snapshot("Aiko", fiveHour: 80, week: 40), Snapshot("Work", fiveHour: 10, week: 85)], Now);

        var crossed = watch.Next([Snapshot("Aiko", fiveHour: 80, week: 40), Snapshot("Work", fiveHour: 10, week: 92)], Now);

        Assert.Equal([new LimitCrossing("Work", LimitKind.SevenDay, 92)], crossed);
    }

    [Fact]
    public void A_window_past_its_reset_counts_as_empty_so_the_next_window_can_cross_again()
    {
        var watch = new LimitWatch();
        watch.Next([Snapshot("Aiko", fiveHour: 95, week: 40)], Now);

        // The five hours turned over: the old 95 is past its reset.
        watch.Next([Snapshot("Aiko", fiveHour: 95, week: 40, fiveHourLeft: TimeSpan.FromMinutes(-1))], Now);
        var crossed = watch.Next([Snapshot("Aiko", fiveHour: 91, week: 40)], Now);

        Assert.Equal([new LimitCrossing("Aiko", LimitKind.FiveHour, 91)], crossed);
    }

    [Fact]
    public void Every_notice_and_its_sound_are_on_in_a_new_settings_file()
    {
        Assert.Equal(new NoticeChoice(true, true, true, true, true), NoticeChoice.From(AppSettings.FromJson("{}")));
    }

    private static LimitSnapshot Snapshot(string environment, int fiveHour, int week, TimeSpan? fiveHourLeft = null) =>
        new(environment, LimitSource.StatusLine, Now,
        [
            new LimitWindow(LimitKind.FiveHour, fiveHour, Now + (fiveHourLeft ?? TimeSpan.FromHours(2))),
            new LimitWindow(LimitKind.SevenDay, week, Now + TimeSpan.FromDays(3)),
        ]);
}
