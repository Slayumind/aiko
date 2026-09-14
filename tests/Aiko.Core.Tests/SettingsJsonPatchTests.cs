using System.Text.Json.Nodes;

namespace Aiko.Core.Tests;

public class SettingsJsonPatchTests
{
    private const string Bridge = @"""C:\Users\slayu\AppData\Local\Aiko\current\Aiko.Bridge.exe""";

    private const string PlainSettings = """
        {
          "$schema": "https://json.schemastore.org/claude-code-settings.json",
          "model": "opusplan",
          "theme": "dark"
        }
        """;

    private const string SettingsWithOwnStatusLine = """
        {
          "model": "opusplan",
          "statusLine": { "type": "command", "command": "~/.claude/my-status.sh" }
        }
        """;

    [Fact]
    public void Adds_our_status_line_and_keeps_everything_else()
    {
        Assert.True(SettingsJsonPatch.TryAddBridge(PlainSettings, Bridge, out var patched));

        var root = JsonNode.Parse(patched)!.AsObject();
        Assert.Equal("opusplan", root["model"]!.GetValue<string>());
        Assert.Equal("dark", root["theme"]!.GetValue<string>());
        Assert.Equal(Bridge, root["statusLine"]!["command"]!.GetValue<string>());
        Assert.Equal("command", root["statusLine"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Keeps_the_status_line_the_user_already_had()
    {
        SettingsJsonPatch.TryAddBridge(SettingsWithOwnStatusLine, Bridge, out var patched);

        var root = JsonNode.Parse(patched)!.AsObject();
        Assert.Equal(Bridge, root["statusLine"]!["command"]!.GetValue<string>());
        Assert.Equal("~/.claude/my-status.sh", root[SettingsJsonPatch.WrappedKey]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void The_bridge_can_read_the_command_it_has_to_call()
    {
        SettingsJsonPatch.TryAddBridge(SettingsWithOwnStatusLine, Bridge, out var patched);

        Assert.Equal("~/.claude/my-status.sh", SettingsJsonPatch.ReadWrappedCommand(patched));
        Assert.Null(SettingsJsonPatch.ReadWrappedCommand(PlainSettings));
    }

    [Fact]
    public void Adding_twice_changes_nothing()
    {
        SettingsJsonPatch.TryAddBridge(PlainSettings, Bridge, out var once);

        Assert.False(SettingsJsonPatch.TryAddBridge(once, Bridge, out var twice));
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Removing_puts_the_file_back_the_way_it_was()
    {
        SettingsJsonPatch.TryAddBridge(PlainSettings, Bridge, out var patched);

        Assert.True(SettingsJsonPatch.TryRemoveBridge(patched, out var restored));

        var root = JsonNode.Parse(restored)!.AsObject();
        Assert.False(root.ContainsKey("statusLine"));
        Assert.False(root.ContainsKey(SettingsJsonPatch.WrappedKey));
        Assert.Equal("opusplan", root["model"]!.GetValue<string>());
    }

    [Fact]
    public void Removing_gives_the_user_their_own_status_line_back()
    {
        SettingsJsonPatch.TryAddBridge(SettingsWithOwnStatusLine, Bridge, out var patched);

        SettingsJsonPatch.TryRemoveBridge(patched, out var restored);

        var root = JsonNode.Parse(restored)!.AsObject();
        Assert.Equal("~/.claude/my-status.sh", root["statusLine"]!["command"]!.GetValue<string>());
        Assert.False(root.ContainsKey(SettingsJsonPatch.WrappedKey));
    }

    [Fact]
    public void Removing_from_a_file_we_never_touched_changes_nothing()
    {
        Assert.False(SettingsJsonPatch.TryRemoveBridge(PlainSettings, out var restored));
        Assert.Equal(PlainSettings, restored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void A_file_we_cannot_read_is_left_alone(string json)
    {
        Assert.False(SettingsJsonPatch.TryAddBridge(json, Bridge, out var patched));
        Assert.Equal(json, patched);
        Assert.False(SettingsJsonPatch.TryRemoveBridge(json, out var restored));
        Assert.Equal(json, restored);
    }

    [Fact]
    public void An_empty_command_is_refused()
    {
        Assert.False(SettingsJsonPatch.TryAddBridge(PlainSettings, "   ", out var patched));
        Assert.Equal(PlainSettings, patched);
    }
}
