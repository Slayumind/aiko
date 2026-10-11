using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// What the bridge prints and writes for the memory. The bridge itself only moves bytes, so the
/// hook shapes Claude Code expects and the "write or not" decisions are all checked here.
public class MemoryBridgeTests
{
    private const string RememberSwift =
        """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"remember","arguments":{"category":"you","fact":"Learns Swift"}}}""";

    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateOnly Yesterday = new(2026, 10, 10);

    private static MemoryFact Fact(int number, string text, params string[] keys) =>
        new(MemoryFact.IdFor(number), MemoryCategory.Projects, text) { Keys = keys, Seen = Yesterday, Created = Yesterday };

    private static string Context(string? hookOutput) =>
        JsonDocument.Parse(hookOutput!).RootElement
            .GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()!;

    private static string PromptInput(string prompt) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["hook_event_name"] = "UserPromptSubmit", ["prompt"] = prompt });

    [Theory]
    [InlineData("rules", MemoryBridgeMode.Rules)]
    [InlineData("facts", MemoryBridgeMode.Facts)]
    [InlineData("prompt", MemoryBridgeMode.Prompt)]
    [InlineData("serve", MemoryBridgeMode.Serve)]
    public void The_second_argument_names_the_job(string argument, MemoryBridgeMode mode)
    {
        Assert.Equal(mode, MemoryBridge.ModeFor(["memory", argument]));
        Assert.Equal(argument, MemoryBridge.ArgumentFor(mode));
    }

    [Theory]
    [InlineData]
    [InlineData("memory")]
    [InlineData("memory", "erase")]
    [InlineData("hook", "rules")]
    public void Anything_else_is_not_a_memory_job(params string[] args)
    {
        Assert.Null(MemoryBridge.ModeFor(args));
    }

    [Fact]
    public void The_rules_go_to_the_model_as_session_start_context()
    {
        var output = MemoryBridge.RulesOutput(Today);

        var hook = JsonDocument.Parse(output).RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("SessionStart", hook.GetProperty("hookEventName").GetString());
        Assert.Equal(MemoryPrompt.Rules(Today), Context(output));
    }

    [Fact]
    public void The_facts_go_in_a_block_of_their_own()
    {
        var memory = new MemoryFile(2, [Fact(1, "Makes Aiko", "Aiko")], []);

        Assert.Equal(MemoryPrompt.Facts(memory), Context(MemoryBridge.FactsOutput(memory.ToJson())));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void No_file_or_a_broken_one_gives_an_empty_list_not_an_error(string? json)
    {
        Assert.Equal(MemoryPrompt.Facts(MemoryFile.Empty), Context(MemoryBridge.FactsOutput(json)));
    }

    [Fact]
    public void A_file_grown_by_hand_still_fits_and_loses_its_longest_facts_first()
    {
        var huge = Fact(1, new string('x', 6_000));
        var big = Fact(2, new string('y', 5_000));
        var small = Fact(3, "Makes Aiko");
        var pinned = Fact(4, new string('z', 3_000)) with { Pinned = true };
        var memory = new MemoryFile(5, [huge, big, small, pinned], []);

        var text = MemoryBridge.FactsThatFit(memory);

        Assert.True(text.Length <= MemoryBridge.HookLimit);
        Assert.DoesNotContain("[F1]", text);
        Assert.Contains("[F2]", text);
        Assert.Contains("[F3]", text);
        Assert.Contains("[F4]", text);
    }

    [Fact]
    public void A_key_in_the_message_refreshes_the_fact_and_the_file_is_written()
    {
        var memory = new MemoryFile(3, [Fact(1, "Makes Aiko", "Aiko"), Fact(2, "Learns Swift", "Swift")], []);

        var step = MemoryBridge.OnPrompt(PromptInput("поправь отступ в Aiko"), memory.ToJson(), Today);

        var written = MemoryFile.FromJson(step.MemoryJson).Memory;
        Assert.Equal(Today, written.Find("F1")!.Seen);
        Assert.Equal(Yesterday, written.Find("F2")!.Seen);
        Assert.Null(step.Output);
    }

    [Fact]
    public void A_message_that_touches_nothing_writes_nothing()
    {
        var memory = new MemoryFile(2, [Fact(1, "Makes Aiko", "Aiko")], []);

        var step = MemoryBridge.OnPrompt(PromptInput("поправь отступ"), memory.ToJson(), Today);

        Assert.Null(step.MemoryJson);
        Assert.Null(step.Output);
    }

    [Fact]
    public void First_person_brings_the_reminder_as_prompt_context()
    {
        var step = MemoryBridge.OnPrompt(PromptInput("мне нравится, когда кнопки без заливки"), null, Today);

        var hook = JsonDocument.Parse(step.Output!).RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("UserPromptSubmit", hook.GetProperty("hookEventName").GetString());
        Assert.Equal(MemoryNudge.Text, Context(step.Output));
        Assert.Null(step.MemoryJson);
    }

    [Fact]
    public void A_broken_file_is_never_written_over_by_a_message()
    {
        var step = MemoryBridge.OnPrompt(PromptInput("я работаю над Aiko"), "not json", Today);

        Assert.Null(step.MemoryJson);
        Assert.NotNull(step.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"hook_event_name":"UserPromptSubmit"}""")]
    public void Hook_input_without_a_prompt_does_nothing(string input)
    {
        var step = MemoryBridge.OnPrompt(input, null, Today);

        Assert.Null(step.Output);
        Assert.Null(step.MemoryJson);
    }

    [Fact]
    public void A_server_call_that_saves_gives_the_file_to_write()
    {
        var step = MemoryBridge.OnServerLine(RememberSwift, null, Today, "0.4.0");

        Assert.Contains("Saved F1.", step.Output);
        Assert.Equal("Learns Swift", MemoryFile.FromJson(step.MemoryJson).Memory.Find("F1")!.Text);
        Assert.False(step.BadFile);
    }

    [Fact]
    public void A_server_call_that_changes_nothing_writes_nothing()
    {
        var step = MemoryBridge.OnServerLine("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", null, Today, "0.4.0");

        Assert.NotNull(step.Output);
        Assert.Null(step.MemoryJson);
    }

    [Fact]
    public void A_save_over_a_broken_file_asks_to_keep_the_old_one_aside()
    {
        var step = MemoryBridge.OnServerLine(RememberSwift, "not json", Today, "0.4.0");

        Assert.NotNull(step.MemoryJson);
        Assert.True(step.BadFile);
    }

    [Fact]
    public void A_failed_write_is_answered_as_a_failed_tool_call()
    {
        var reply = MemoryBridge.WriteFailedReply("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"forget"}}""");

        var result = JsonDocument.Parse(reply!).RootElement;
        Assert.Equal(4, result.GetProperty("id").GetInt32());
        Assert.True(result.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal(
            MemoryBridge.WriteFailed,
            result.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"jsonrpc":"2.0","method":"notifications/initialized"}""")]
    public void A_failed_write_for_something_that_is_not_a_request_has_no_answer(string line)
    {
        Assert.Null(MemoryBridge.WriteFailedReply(line));
    }
}
