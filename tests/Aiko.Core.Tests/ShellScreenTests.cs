using System.Diagnostics;
using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The screen of a command line and its blocks, from spec/cases/shell-screen (D-412).
public class ShellScreenTests
{
    [Fact]
    public void Every_recorded_shell_gives_the_same_blocks()
    {
        foreach (var shell in SpecCases.Json("shell-screen").GetProperty("recordings").EnumerateObject())
        {
            var recording = SpecCases.Json("shell-screen", $"{shell.Name}.json");
            var screen = new ShellScreen(recording.GetProperty("columns").GetInt32(), recording.GetProperty("rows").GetInt32());
            screen.Feed(recording.GetProperty("stream").GetString()!, 0);

            var blocks = ShellBlocks.Of(screen);
            var expected = shell.Value.EnumerateArray().ToList();
            Assert.Equal(expected.Count, blocks.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                var row = expected[i];
                var block = blocks[i];
                Assert.Equal(row.GetProperty("command").GetString(), block.Command);
                Assert.Equal(Number(row.GetProperty("exitCode")), block.ExitCode);
                Assert.Equal(row.GetProperty("lines").GetInt32(), block.LineCount);
                if (row.GetProperty("first").GetString() is { } first)
                {
                    Assert.Equal(first, block.LineCount > 0 ? screen.LineText(block.FirstLine) : "");
                }
            }

            Assert.False(screen.FullScreen);
        }
    }

    [Fact]
    public void Every_screen_case_lays_out_the_same()
    {
        foreach (var row in SpecCases.Json("shell-screen").GetProperty("screens").EnumerateArray())
        {
            var screen = new ShellScreen(row.GetProperty("columns").GetInt32(), 10);
            if (row.TryGetProperty("chunks", out var chunks))
            {
                foreach (var chunk in chunks.EnumerateArray())
                {
                    screen.Feed(chunk.GetString()!, 0);
                }
            }
            else
            {
                screen.Feed(row.GetProperty("stream").GetString()!, 0);
            }

            var lines = row.GetProperty("lines").EnumerateArray().Select(line => line.GetString()!).ToList();
            Assert.Equal(lines, Enumerable.Range(0, lines.Count).Select(screen.LineText).ToList());
            if (row.TryGetProperty("wrapped", out var wrapped))
            {
                Assert.Equal(wrapped.EnumerateArray().Select(item => item.GetBoolean()).ToList(), Enumerable.Range(0, lines.Count).Select(screen.Wrapped).ToList());
            }

            if (row.TryGetProperty("text", out var text))
            {
                Assert.Equal(text.GetString(), screen.Text(0, 0, lines.Count - 1, int.MaxValue));
            }

            if (row.TryGetProperty("fullScreen", out var fullScreen))
            {
                Assert.Equal(fullScreen.GetBoolean(), screen.FullScreen);
            }
        }
    }

    [Fact]
    public void Every_run_case_keeps_its_colours()
    {
        foreach (var row in SpecCases.Json("shell-screen").GetProperty("runs").EnumerateArray())
        {
            var screen = new ShellScreen(80, 10);
            screen.Feed(row.GetProperty("stream").GetString()!, 0);

            var expected = row.GetProperty("runs").EnumerateArray()
                .Select(run => (run.GetProperty("text").GetString(), run.GetProperty("foreground").GetInt32(), run.GetProperty("bold").GetBoolean()))
                .ToList();
            Assert.Equal(expected, screen.Runs(0).Select(run => ((string?)run.Text, run.Style.Foreground, run.Style.Bold)).ToList());
        }
    }

    [Fact]
    public void Every_block_case_cuts_the_same()
    {
        foreach (var row in SpecCases.Json("shell-screen").GetProperty("blocks").EnumerateArray())
        {
            var screen = new ShellScreen(80, 10);
            screen.Feed(row.GetProperty("stream").GetString()!, 0);

            var blocks = ShellBlocks.Of(screen);
            var expected = row.GetProperty("blocks").EnumerateArray().ToList();
            Assert.Equal(expected.Count, blocks.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].GetProperty("command").GetString(), blocks[i].Command);
                Assert.Equal(Number(expected[i].GetProperty("exitCode")), blocks[i].ExitCode);
                Assert.Equal(
                    expected[i].GetProperty("lines").EnumerateArray().Select(line => line.GetString()!).ToList(),
                    Enumerable.Range(blocks[i].FirstLine, blocks[i].LineCount).Select(screen.LineText).ToList());
            }
        }
    }

    [Fact]
    public void A_block_knows_how_long_its_command_ran()
    {
        var screen = new ShellScreen(80, 10);
        screen.Feed("\u001b]133;A\u0007$ \u001b]133;B\u0007sleep 2\r\n\u001b]133;C\u0007", 1_000);
        screen.Feed("\u001b]133;D;0\u0007", 3_000);

        var block = Assert.Single(ShellBlocks.Of(screen));
        Assert.Equal(1_000, block.StartedAt);
        Assert.Equal(3_000, block.EndedAt);
    }

    [Fact]
    public void Ten_thousand_lines_are_read_in_well_under_a_second()
    {
        var screen = new ShellScreen(120, 30);
        var watch = Stopwatch.StartNew();
        screen.Feed(string.Concat(Enumerable.Range(1, 10_000).Select(i => $"line {i} with some words in it\r\n")), 0);

        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal("line 10000 with some words in it", screen.LineText(9_999));
    }

    private static int? Number(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();
}
