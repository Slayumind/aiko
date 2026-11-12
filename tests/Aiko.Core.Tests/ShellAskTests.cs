using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The questions of a command line to Claude Code and the reading of its answers, from
/// spec/cases/shell-ask (D-358, D-360).
public class ShellAskTests
{
    [Fact]
    public void Every_request_case_is_told_apart_the_same_way()
    {
        foreach (var row in SpecCases.Json("shell-ask").GetProperty("requests").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("request").GetBoolean(), ShellAsk.IsRequest(row.GetProperty("text").GetString()!));
        }
    }

    [Fact]
    public void Every_answer_gives_the_same_command()
    {
        foreach (var row in SpecCases.Json("shell-ask").GetProperty("commands").EnumerateArray())
        {
            var expected = row.GetProperty("command").ValueKind == JsonValueKind.Null ? null : row.GetProperty("command").GetString();
            Assert.Equal(expected, ShellAsk.CommandIn(row.GetProperty("answer").GetString()!));
        }
    }

    [Fact]
    public void Every_explanation_wraps_the_same_way()
    {
        foreach (var row in SpecCases.Json("shell-ask").GetProperty("wraps").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("lines").EnumerateArray().Select(line => line.GetString()!).ToList(),
                ShellAsk.Wrap(row.GetProperty("text").GetString()!, row.GetProperty("width").GetInt32()));
        }
    }

    [Fact]
    public void Every_link_case_finds_the_same_link()
    {
        foreach (var row in SpecCases.Json("shell-ask").GetProperty("links").EnumerateArray())
        {
            var expected = row.GetProperty("link").ValueKind == JsonValueKind.Null ? null : row.GetProperty("link").GetString();
            Assert.Equal(expected, ShellLink.At(row.GetProperty("line").GetString()!, row.GetProperty("column").GetInt32()));
        }
    }

    [Fact]
    public void An_explanation_takes_the_end_of_a_long_output()
    {
        var output = new string('a', ShellAsk.MostOutput) + "THE ERROR";

        var question = ShellAsk.Explain("bash", "make", 2, output, "Russian");

        Assert.Contains("THE ERROR", question);
        Assert.Contains("Exit code: 2", question);
        Assert.Contains("Git Bash", question);
        Assert.Contains("In Russian", question);
    }

    [Fact]
    public void A_suggestion_asks_without_the_mark_of_a_request()
    {
        var question = ShellAsk.Suggest("pwsh", @"C:\work", "# find big files");

        Assert.Contains("that does this: find big files", question);
        Assert.Contains("PowerShell 7", question);
    }
}
