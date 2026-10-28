using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Lines that the JSON reader of .NET takes but cannot fully decode. They come on the window
/// thread, so a throw there took the whole of Aiko down (review of 2026-10-28). Swift reads such
/// lines with its own parser, so these stay C# tests and not shared cases.
public class StreamSafetyTests
{
    /// Node writes half of a surrogate pair when a string was cut in the middle of an emoji.
    private const string CutEmoji = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_1","content":"done \ud83d"}]},"parent_tool_use_id":null}""";

    [Fact]
    public void A_tool_result_cut_in_the_middle_of_an_emoji_is_read_and_keeps_its_text()
    {
        var read = StreamEvent.FromLine(CutEmoji);

        var user = Assert.IsType<StreamEvent.User>(read);
        Assert.StartsWith("done", Assert.Single(user.Results).Text);
    }

    [Fact]
    public void A_limit_whose_reset_time_is_out_of_range_is_left_out()
    {
        var line = """{"type":"rate_limit_event","rate_limit_info":{"unifiedWindows":{"five_hour":{"utilization":0.5,"resetsAt":99999999999999},"seven_day":{"utilization":0.2,"resetsAt":1790000000}}}}""";

        var read = StreamEvent.FromLine(line);

        Assert.Equal(LimitKind.SevenDay, Assert.Single(Assert.IsType<StreamEvent.RateLimit>(read).Windows).Kind);
    }

    [Fact]
    public void A_transcript_line_cut_in_the_middle_of_an_emoji_does_not_stop_the_reading()
    {
        var line = """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"hi \ud83d"}]},"uuid":"u1"}""";

        var history = Transcript.Read([line], PermissionMode.Manual);

        Assert.StartsWith("hi", Assert.IsType<FeedItem.Answer>(Assert.Single(history.Items)).Text);
    }
}
