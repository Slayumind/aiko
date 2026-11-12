using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// When a session sounds, and the counts and the sign of the tray and the taskbar button, from
/// spec/cases/notices (D-409).
public class NoticesTests
{
    [Fact]
    public void Every_sound_case_sounds_the_same_way()
    {
        foreach (var row in SpecCases.Json("notices").GetProperty("sounds").EnumerateArray())
        {
            TerminalActivity? before = row.GetProperty("before").ValueKind == JsonValueKind.Null
                ? null
                : Enum.Parse<TerminalActivity>(row.GetProperty("before").GetString()!);
            var now = Enum.Parse<TerminalActivity>(row.GetProperty("now").GetString()!);
            var sound = !(row.TryGetProperty("soundOff", out var off) && off.GetBoolean());

            Assert.Equal(
                row.GetProperty("sounds").GetBoolean(),
                Notices.Sounds(before, now, sound, row.GetProperty("inFront").GetBoolean(), row.GetProperty("open").GetBoolean()));
        }
    }

    [Fact]
    public void Every_count_case_counts_and_signs_the_same_way()
    {
        foreach (var row in SpecCases.Json("notices").GetProperty("counts").EnumerateArray())
        {
            var terminals = row.GetProperty("activities").EnumerateArray()
                .Select((item, index) => ($"{index}", Enum.Parse<TerminalActivity>(item.GetString()!)))
                .ToList();
            var first = row.GetProperty("firstWaiting");

            var counts = SessionCounts.Of(terminals);

            Assert.Equal(
                new SessionCounts(
                    row.GetProperty("working").GetInt32(),
                    row.GetProperty("waiting").GetInt32(),
                    row.GetProperty("done").GetInt32(),
                    row.GetProperty("stopped").GetInt32(),
                    first.ValueKind == JsonValueKind.Null ? null : $"{first.GetInt32()}"),
                counts);
            Assert.Equal(Enum.Parse<BadgeKind>(row.GetProperty("badge").GetString()!), counts.Badge.Kind);
            Assert.Equal(row.GetProperty("text").GetString(), counts.Badge.Text);
        }
    }

    [Fact]
    public void Every_stop_case_is_news_the_same_way()
    {
        foreach (var row in SpecCases.Json("notices").GetProperty("stops").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("news").GetBoolean(),
                SessionCounts.StoppedIsNews(row.GetProperty("stoppedAt").GetInt64(), row.GetProperty("seenAt").GetInt64()));
        }
    }

    [Fact]
    public void No_session_leaves_nothing_to_show()
    {
        Assert.True(SessionCounts.None.IsEmpty);
        Assert.Equal(SessionBadge.None, SessionCounts.None.Badge);
    }

    [Fact]
    public void The_sound_is_on_in_a_new_settings_file()
    {
        Assert.True(AppSettings.FromJson("{}").NotifySound);
    }
}
