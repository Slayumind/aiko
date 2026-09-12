using Aiko.Core;

namespace Aiko.Core.Tests;

public class BridgeCommandTests
{
    [Fact]
    public void The_path_is_quoted_because_it_holds_spaces()
    {
        var command = BridgeCommand.For(@"C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe");

        Assert.Equal(@"""C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe""", command);
    }

    [Fact]
    public void A_path_that_is_already_quoted_is_left_alone()
    {
        var quoted = @"""C:\Program Files\Aiko\Aiko.Bridge.exe""";

        Assert.Equal(quoted, BridgeCommand.For(quoted));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Without_a_path_there_is_no_command(string? path)
    {
        Assert.Equal(string.Empty, BridgeCommand.For(path!));
    }

    [Fact]
    public void An_empty_command_is_refused_by_the_settings_patch()
    {
        // The two belong together: no path means no line in the user's settings file.
        var added = SettingsJsonPatch.TryAddBridge("{}", BridgeCommand.For(""), out var patched);

        Assert.False(added);
        Assert.Equal("{}", patched);
    }

    [Fact]
    public void The_line_we_write_is_recognised_as_ours()
    {
        var command = BridgeCommand.For(@"C:\Aiko\Aiko.Bridge.exe");
        SettingsJsonPatch.TryAddBridge("{}", command, out var patched);

        // Adding it a second time changes nothing: it is already our line.
        Assert.False(SettingsJsonPatch.TryAddBridge(patched, command, out _));
    }
}
