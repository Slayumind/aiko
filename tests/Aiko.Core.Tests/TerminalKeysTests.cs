using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class TerminalKeysTests
{
    [Fact]
    public void Every_key_of_the_table_does_what_the_row_says()
    {
        foreach (var row in SpecCases.Json("terminal-keys").GetProperty("keys").EnumerateArray())
        {
            var state = row.GetProperty("state");
            bool Flag(string name) => state.TryGetProperty(name, out var value) && value.GetBoolean();

            var action = TerminalKeys.For(
                row.GetProperty("key").GetString()!,
                row.GetProperty("shift").GetBoolean(),
                new TerminalKeyState(Flag("settingsOpen"), Flag("claudeSession"), Flag("inTerminal"), Flag("hasSelection")));

            Assert.True(
                Enum.Parse<TerminalKeyAction>(row.GetProperty("action").GetString()!) == action,
                $"{row.GetRawText()} gave {action}");
        }
    }
}
