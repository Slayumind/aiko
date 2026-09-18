using System.Text.Json.Nodes;
using Aiko.Core;

namespace Aiko.Core.Tests;

public class SessionReminderTests
{
    private const string Home = @"C:\Users\someone";
    private const string Hook = "\"C:\\Program Files\\Aiko\\Aiko.Bridge.exe\" --session-start";

    private static readonly EnvironmentSettings Settings = new([
        new AikoEnvironment("Work", [Home + @"\.claude"]),
        new AikoEnvironment("Personal", [Home + @"\.claude-personal"]) { ProjectFolders = [@"C:\projects\aiko"] },
    ]);

    // ---- the message ----

    [Fact]
    public void A_command_in_a_folder_bound_elsewhere_says_whose_limits_go()
    {
        var message = SessionReminder.MessageFor("command", "Work", @"C:\projects\aiko\src", Settings, russian: true);

        Assert.Equal("Aiko: эта папка относится к Personal, а сессия запущена в Work. Лимиты тратятся из Work.", message);
    }

    [Fact]
    public void English_says_the_same()
    {
        var message = SessionReminder.MessageFor("command", "Work", @"C:\projects\aiko", Settings, russian: false);

        Assert.Equal("Aiko: this folder belongs to Personal, but this session runs in Work, so the limits of Work are used.", message);
    }

    [Theory]
    [InlineData("binding", "Personal", @"C:\projects\aiko")] // started by the binding itself
    [InlineData("command", "Personal", @"C:\projects\aiko")] // the command matches the binding
    [InlineData("command", "Work", @"C:\temp")] // no binding here, so nothing to remind about
    [InlineData(null, null, @"C:\projects\aiko")] // not started through the shim at all
    public void Quiet_when_there_is_nothing_to_say(string? launch, string? running, string folder)
    {
        Assert.Null(SessionReminder.MessageFor(launch, running, folder, Settings, russian: false));
    }

    [Fact]
    public void The_hook_answer_is_a_system_message()
    {
        var output = JsonNode.Parse(SessionReminder.HookOutput("Aiko: «test»"))!.AsObject();

        Assert.Equal("Aiko: «test»", output["systemMessage"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(AikoLanguage.Russian, 0x0409, true)]
    [InlineData(AikoLanguage.English, 0x0419, false)]
    [InlineData(AikoLanguage.System, 0x0419, true)]
    [InlineData(AikoLanguage.System, 0x0409, false)]
    public void Language(AikoLanguage setting, int windows, bool russian)
    {
        Assert.Equal(russian, SessionReminder.IsRussian(setting, windows));
    }

    [Fact]
    public void Reads_the_working_folder_from_hook_input()
    {
        Assert.Equal(@"C:\x", SessionReminder.WorkingDirectoryIn("""{ "session_id": "s", "cwd": "C:\\x" }"""));
        Assert.Null(SessionReminder.WorkingDirectoryIn("not json"));
    }

    // ---- the hook in settings.json ----

    [Fact]
    public void The_hook_is_added_beside_the_persons_own_hooks()
    {
        const string before = """
            {
              "model": "opus",
              "hooks": {
                "SessionStart": [ { "matcher": "startup", "hooks": [ { "type": "command", "command": "echo hi" } ] } ],
                "Stop": [ { "hooks": [ { "type": "command", "command": "notify" } ] } ]
              }
            }
            """;

        Assert.True(SettingsJsonPatch.TryAddSessionHook(Windows, before, Hook, out var after));

        var groups = JsonNode.Parse(after)!["hooks"]!["SessionStart"]!.AsArray();
        Assert.Equal(2, groups.Count);
        Assert.Equal("echo hi", groups[0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Equal(Hook, groups[1]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.NotNull(JsonNode.Parse(after)!["hooks"]!["Stop"]);
        Assert.True(SettingsJsonPatch.HasOurSessionHook(Windows, after));
    }

    [Fact]
    public void Adding_twice_changes_nothing_and_an_old_path_is_replaced()
    {
        Assert.True(SettingsJsonPatch.TryAddSessionHook(Windows, "{}", Hook, out var once));
        Assert.False(SettingsJsonPatch.TryAddSessionHook(Windows, once, Hook, out _));

        const string moved = "\"D:\\Aiko\\Aiko.Bridge.exe\" --session-start";
        Assert.True(SettingsJsonPatch.TryAddSessionHook(Windows, once, moved, out var replaced));

        var commands = JsonNode.Parse(replaced)!["hooks"]!["SessionStart"]!.AsArray();
        Assert.Equal(moved, Assert.Single(commands)!["hooks"]![0]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void Removing_Aiko_takes_the_hook_out_and_leaves_the_file_as_it_was()
    {
        const string before = """{ "hooks": { "SessionStart": [ { "hooks": [ { "type": "command", "command": "echo hi" } ] } ] } }""";

        SettingsJsonPatch.TryAddBridge(Windows, before, "\"C:\\A\\Aiko.Bridge.exe\"", out var withLine);
        SettingsJsonPatch.TryAddSessionHook(Windows, withLine, Hook, out var withBoth);

        Assert.True(SettingsJsonPatch.TryRemoveBridge(Windows, withBoth, out var restored));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before), JsonNode.Parse(restored)));
    }

    [Fact]
    public void A_file_with_only_our_hook_loses_the_empty_hooks_object()
    {
        SettingsJsonPatch.TryAddSessionHook(Windows, """{ "model": "opus" }""", Hook, out var withHook);

        Assert.True(SettingsJsonPatch.TryRemoveBridge(Windows, withHook, out var restored));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{ "model": "opus" }"""), JsonNode.Parse(restored)));
    }

    [Fact]
    public void Hooks_that_are_not_the_expected_shape_are_not_touched()
    {
        Assert.False(SettingsJsonPatch.TryAddSessionHook(Windows, """{ "hooks": "oops" }""", Hook, out _));
        Assert.False(SettingsJsonPatch.TryAddSessionHook(Windows, """{ "hooks": { "SessionStart": {} } }""", Hook, out _));
    }

    [Fact]
    public void The_hook_command_is_the_bridge_with_one_argument()
    {
        Assert.Equal("\"C:\\A\\Aiko.Bridge.exe\" --session-start", BridgeCommand.HookFor(@"C:\A\Aiko.Bridge.exe"));
        Assert.Equal("& \"C:\\A\\Aiko.Bridge.exe\" --session-start", BridgeCommand.HookFor(@"C:\A\Aiko.Bridge.exe", ClaudeShell.PowerShell));
        Assert.True(BridgeCommand.IsAiko(Windows, BridgeCommand.HookFor(@"C:\A\Aiko.Bridge.exe")));
    }
}
