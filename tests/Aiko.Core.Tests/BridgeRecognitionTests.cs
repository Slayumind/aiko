using Aiko.Core;

namespace Aiko.Core.Tests;

/// Recognising our own status line again after the install path or the shell has changed.
///
/// Matching the line by exact text looked right and was not. A reinstall into another folder, or
/// Git appearing on a machine that had none, changes the text. The old line was then taken for the
/// user's own, put aside under our wrapped key, and the bridge was asked to run a path that no
/// longer exists after every model answer.
public class BridgeRecognitionTests
{
    private const string Here = @"C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe";
    private const string Elsewhere = @"D:\Programs\Aiko\Aiko.Bridge.exe";

    [Fact]
    public void Our_line_is_ours_whatever_folder_it_points_at()
    {
        Assert.True(BridgeCommand.IsAiko(BridgeCommand.For(Here)));
        Assert.True(BridgeCommand.IsAiko(BridgeCommand.For(Elsewhere)));
    }

    [Fact]
    public void Our_line_is_ours_in_either_shell()
    {
        Assert.True(BridgeCommand.IsAiko(BridgeCommand.For(Here, ClaudeShell.GitBash)));
        Assert.True(BridgeCommand.IsAiko(BridgeCommand.For(Here, ClaudeShell.PowerShell)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("& ")]
    [InlineData("\"")]
    [InlineData("starship prompt")]
    [InlineData(@"""C:\Users\someone\bin\my-status-line.sh""")]
    [InlineData(@"& ""C:\tools\aiko-bridge-helper.exe""")]
    public void Somebody_elses_line_is_not_ours(string? command)
    {
        Assert.False(BridgeCommand.IsAiko(command));
    }

    [Fact]
    public void An_unquoted_line_is_read_too()
    {
        Assert.True(BridgeCommand.IsAiko(@"C:\Aiko\Aiko.Bridge.exe"));
        Assert.True(BridgeCommand.IsAiko(@"C:\Aiko\aiko.bridge.EXE"));
    }

    [Fact]
    public void A_reinstall_elsewhere_replaces_our_line_instead_of_hiding_it()
    {
        SettingsJsonPatch.TryAddBridge("{}", BridgeCommand.For(Here), out var after);

        var moved = SettingsJsonPatch.TryAddBridge(after, BridgeCommand.For(Elsewhere), out var patched);

        Assert.True(moved);
        Assert.Contains(Elsewhere.Replace(@"\", @"\\"), patched);
        // The old path is gone, and nothing of ours was filed away as if the user had written it.
        Assert.DoesNotContain(SettingsJsonPatch.WrappedKey, patched);
    }

    [Fact]
    public void A_shell_change_replaces_our_line_instead_of_hiding_it()
    {
        SettingsJsonPatch.TryAddBridge(
            "{}", BridgeCommand.For(Here, ClaudeShell.PowerShell), out var after);

        var rewritten = SettingsJsonPatch.TryAddBridge(
            after, BridgeCommand.For(Here, ClaudeShell.GitBash), out var patched);

        Assert.True(rewritten);
        Assert.DoesNotContain(SettingsJsonPatch.WrappedKey, patched);
    }

    [Fact]
    public void The_users_own_line_is_still_kept_aside()
    {
        var mine = """{ "statusLine": { "type": "command", "command": "my-line.sh" } }""";

        SettingsJsonPatch.TryAddBridge(mine, BridgeCommand.For(Here), out var patched);

        Assert.Contains(SettingsJsonPatch.WrappedKey, patched);
        Assert.Equal("my-line.sh", SettingsJsonPatch.ReadWrappedCommand(patched));
    }

    [Fact]
    public void A_line_of_ours_left_aside_by_an_older_Aiko_is_thrown_away()
    {
        // What the old exact match produced: our own command filed under the wrapped key. Restoring
        // it on removal would hand the user a status line running a bridge that is gone.
        var damaged = $$"""
            {
              "statusLine": { "type": "command", "command": {{System.Text.Json.JsonSerializer.Serialize(BridgeCommand.For(Here))}} },
              "aikoWrappedStatusLine": { "type": "command", "command": {{System.Text.Json.JsonSerializer.Serialize(BridgeCommand.For(Elsewhere))}} }
            }
            """;

        var repaired = SettingsJsonPatch.TryAddBridge(damaged, BridgeCommand.For(Here), out var patched);

        Assert.True(repaired);
        Assert.DoesNotContain(SettingsJsonPatch.WrappedKey, patched);
        Assert.True(BridgeCommand.IsAiko(
            System.Text.Json.Nodes.JsonNode.Parse(patched)!["statusLine"]!["command"]!.GetValue<string>()));
    }

    [Fact]
    public void Removing_Aiko_does_not_restore_a_line_of_ours()
    {
        var damaged = $$"""
            {
              "statusLine": { "type": "command", "command": {{System.Text.Json.JsonSerializer.Serialize(BridgeCommand.For(Here))}} },
              "aikoWrappedStatusLine": { "type": "command", "command": {{System.Text.Json.JsonSerializer.Serialize(BridgeCommand.For(Elsewhere))}} }
            }
            """;

        var removed = SettingsJsonPatch.TryRemoveBridge(damaged, out var restored);

        Assert.True(removed);
        var root = System.Text.Json.Nodes.JsonNode.Parse(restored)!.AsObject();
        Assert.False(root.ContainsKey("statusLine"));
        Assert.False(root.ContainsKey(SettingsJsonPatch.WrappedKey));
    }

    [Fact]
    public void Removing_Aiko_still_puts_back_the_line_the_user_had()
    {
        var mine = """{ "statusLine": { "type": "command", "command": "my-line.sh" } }""";
        SettingsJsonPatch.TryAddBridge(mine, BridgeCommand.For(Here), out var patched);

        SettingsJsonPatch.TryRemoveBridge(patched, out var restored);

        var root = System.Text.Json.Nodes.JsonNode.Parse(restored)!.AsObject();
        Assert.Equal("my-line.sh", root["statusLine"]!["command"]!.GetValue<string>());
        Assert.False(root.ContainsKey(SettingsJsonPatch.WrappedKey));
    }

    [Fact]
    public void A_command_key_holding_something_other_than_text_is_not_ours()
    {
        var odd = """{ "statusLine": { "type": "command", "command": 42 } }""";

        var added = SettingsJsonPatch.TryAddBridge(odd, BridgeCommand.For(Here), out var patched);

        Assert.True(added);
        // Not ours, so it is kept aside like anyone else's line rather than thrown away.
        Assert.Contains(SettingsJsonPatch.WrappedKey, patched);
    }
}
