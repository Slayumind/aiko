using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// The memory server, one line in and one line out. Claude Code is the client, so the shapes it
/// expects are checked here: the echoed id, the tool list, the text of a tool result.
public class MemoryServerTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static MemoryServerReply Ask(string line, MemoryFile? memory = null) =>
        MemoryServer.Answer(line, memory ?? MemoryFile.Empty, Today, "0.4.0");

    private static JsonElement Json(MemoryServerReply reply) => JsonDocument.Parse(reply.Line!).RootElement;

    private static string ToolText(MemoryServerReply reply) =>
        Json(reply).GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

    [Fact]
    public void Initialize_answers_with_the_protocol_the_client_named()
    {
        var reply = Ask("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26"}}""");

        var result = Json(reply).GetProperty("result");
        Assert.Equal("2025-03-26", result.GetProperty("protocolVersion").GetString());
        Assert.Equal("aiko-memory", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal("0.4.0", result.GetProperty("serverInfo").GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Object, result.GetProperty("capabilities").GetProperty("tools").ValueKind);
        Assert.False(reply.Changed);
    }

    [Fact]
    public void Initialize_without_a_protocol_answers_with_ours()
    {
        var reply = Ask("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""");

        Assert.Equal(
            MemoryServer.DefaultProtocol,
            Json(reply).GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Theory]
    [InlineData("7", "7")]
    [InlineData("\"abc\"", "\"abc\"")]
    public void The_id_goes_back_as_it_came(string id, string expected)
    {
        var reply = Ask($$"""{"jsonrpc":"2.0","id":{{id}},"method":"ping"}""");

        Assert.Equal(expected, Json(reply).GetProperty("id").GetRawText());
        Assert.Equal(JsonValueKind.Object, Json(reply).GetProperty("result").ValueKind);
    }

    [Fact]
    public void A_notification_gets_no_answer()
    {
        var reply = Ask("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Null(reply.Line);
        Assert.False(reply.Changed);
    }

    [Fact]
    public void An_unknown_method_is_an_error_not_silence()
    {
        var reply = Ask("""{"jsonrpc":"2.0","id":3,"method":"resources/list"}""");

        Assert.Equal(-32601, Json(reply).GetProperty("error").GetProperty("code").GetInt32());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void A_line_that_is_not_a_message_is_a_parse_error(string line)
    {
        var reply = Ask(line);

        Assert.Equal(-32700, Json(reply).GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, Json(reply).GetProperty("id").ValueKind);
    }

    [Fact]
    public void Every_answer_is_one_line()
    {
        var reply = Ask("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        Assert.DoesNotContain('\n', reply.Line!);
    }

    [Fact]
    public void Both_tools_are_listed_and_always_loaded()
    {
        var tools = Json(Ask("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))
            .GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        Assert.Equal(["remember", "forget"], tools.Select(t => t.GetProperty("name").GetString()));
        Assert.All(tools, t => Assert.True(t.GetProperty("_meta").GetProperty("anthropic/alwaysLoad").GetBoolean()));
        Assert.All(tools, t => Assert.Equal("object", t.GetProperty("inputSchema").GetProperty("type").GetString()));
    }

    [Fact]
    public void Remember_lists_the_four_categories_and_no_scope()
    {
        var properties = Json(Ask("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))
            .GetProperty("result").GetProperty("tools")[0].GetProperty("inputSchema").GetProperty("properties");

        Assert.Equal(
            ["you", "work", "projects", "interests"],
            properties.GetProperty("category").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.False(properties.TryGetProperty("scope", out _));
        Assert.Equal(MemoryRules.MaxKeys, properties.GetProperty("keys").GetProperty("maxItems").GetInt32());
    }

    [Fact]
    public void Remember_saves_a_fact_and_says_so()
    {
        var reply = Ask(
            """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"remember","arguments":{"category":"projects","fact":"Makes Aiko","meaning":"Own product","keys":["Aiko"],"pin":false}}}""");

        var fact = Assert.Single(reply.Memory.Facts);
        Assert.True(reply.Changed);
        Assert.Equal("Saved F1.", ToolText(reply));
        Assert.False(Json(reply).GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("Makes Aiko", fact.Text);
        Assert.Equal("Own product", fact.Meaning);
        Assert.Equal(["Aiko"], fact.Keys);
        Assert.Equal(Today, fact.Seen);
    }

    [Fact]
    public void A_refused_save_changes_nothing_and_the_reply_says_why()
    {
        var tooLong = new string('a', MemoryRules.MaxLength + 1);

        var reply = Ask(
            """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"remember","arguments":{"category":"you","fact":"FACT"}}}"""
                .Replace("FACT", tooLong));

        Assert.False(reply.Changed);
        Assert.Empty(reply.Memory.Facts);
        Assert.Contains("Too long", ToolText(reply));
    }

    [Fact]
    public void Forget_removes_the_fact()
    {
        var memory = new MemoryFile(2, [new MemoryFact("F1", MemoryCategory.You, "Learns Swift") { Seen = Today, Created = Today }], []);

        var reply = Ask(
            """{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"forget","arguments":{"id":"F1"}}}""", memory);

        Assert.True(reply.Changed);
        Assert.Empty(reply.Memory.Facts);
        Assert.Equal("Forgot F1.", ToolText(reply));
    }

    [Fact]
    public void A_reply_keeps_cyrillic_readable()
    {
        var memory = new MemoryFile(2, [new MemoryFact("F1", MemoryCategory.Work, "Говорит «коротко»") { Seen = Today, Created = Today }], []);
        var full = Enumerable.Range(2, MemoryRules.Quota(MemoryCategory.Work) - 1)
            .Select(n => new MemoryFact(MemoryFact.IdFor(n), MemoryCategory.Work, $"habit {n}") { Seen = Today, Created = Today });
        memory = memory with { Facts = [.. memory.Facts, .. full], NextId = 20 };

        var reply = Ask(
            """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"remember","arguments":{"category":"work","fact":"One more habit"}}}""", memory);

        Assert.False(reply.Changed);
        Assert.Contains("Говорит «коротко»", reply.Line);
    }

    [Fact]
    public void An_unknown_tool_is_a_tool_error()
    {
        var reply = Ask("""{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"recall","arguments":{}}}""");

        Assert.True(Json(reply).GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("Unknown tool recall.", ToolText(reply));
        Assert.False(reply.Changed);
    }

    [Fact]
    public void Arguments_of_a_wrong_type_are_read_as_missing()
    {
        var reply = Ask(
            """{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"remember","arguments":{"category":"you","fact":"Learns Swift","keys":"Swift","pin":"yes"}}}""");

        var fact = Assert.Single(reply.Memory.Facts);
        Assert.Empty(fact.Keys);
        Assert.False(fact.Pinned);
    }
}
