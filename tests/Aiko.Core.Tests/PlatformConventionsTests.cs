using Aiko.Core;

namespace Aiko.Core.Tests;

/// The core takes the platform as a description, so its rules must follow whatever description
/// it gets. The system here is made up on purpose: it is neither Windows nor macOS, so a rule
/// that only works for the two we ship fails these cases.
public class PlatformConventionsTests
{
    private static readonly PlatformConventions Other = new()
    {
        ExecutableSuffix = "",
        PathListSeparator = ':',
        DirectorySeparator = '/',
        LocalDataVariable = null,
        FallbackShell = new ShellProgram(ClaudeShell.GitBash, "/bin/sh", ["-c"]),
    };

    [Fact]
    public void Windows_names_programs_with_exe_and_lists_folders_with_a_semicolon()
    {
        Assert.Equal("claude.exe", Windows.ExecutableName("claude"));
        Assert.Equal(';', Windows.PathListSeparator);
        Assert.Equal("%LOCALAPPDATA%", Windows.LocalDataVariable);
    }

    [Fact]
    public void Without_Git_Bash_Windows_falls_back_to_PowerShell()
    {
        var (fileName, arguments) = ClaudeShellLookup.CallFor(Windows, null, "line");

        Assert.Equal(ClaudeShell.PowerShell, ClaudeShellLookup.ShellFor(Windows, null));
        Assert.Equal("powershell.exe", fileName);
        Assert.Equal(["-NoProfile", "-NonInteractive", "-Command", "line"], arguments);
    }

    [Fact]
    public void The_real_claude_is_found_with_the_names_and_separator_of_the_platform()
    {
        var found = RealClaude.Find(Other, "/opt/aiko/bin:/usr/local/bin", ["/opt/aiko/bin"], p => p.EndsWith("claude"));

        Assert.Equal("/usr/local/bin/claude", found);
        Assert.Equal("/a:/b", RealClaude.FormatSeen(Other, ["/a", "/b"]));
    }

    [Fact]
    public void Our_bridge_is_recognised_by_the_program_name_of_the_platform()
    {
        Assert.True(BridgeCommand.IsAiko(Other, "\"/Applications/Aiko.app/Aiko.Bridge\""));
        Assert.False(BridgeCommand.IsAiko(Other, "\"C:\\Aiko\\Aiko.Bridge.exe\""));
    }

    [Fact]
    public void Commands_are_links_named_like_the_platform_names_programs()
    {
        var plan = CommandLinks.Plan(Other, new EnvironmentSettings([new AikoEnvironment("Work", ["/w"])]), ["/bin/claude"]);

        Assert.Equal(["aiko-work"], plan.ToAdd);
    }

    [Fact]
    public void Without_a_variable_for_the_local_folder_the_persona_command_uses_the_full_path()
    {
        var folders = new AikoFolders(Other, "/home/someone/settings", "/home/someone/local");
        var bridge = Other.Join(folders.InstalledAppFolder, "Aiko.Bridge");

        Assert.Equal($"\"{bridge}\" plugin aiko-persona", AikoMarketplace.PersonaCommand(Other, bridge, folders));
    }

    [Fact]
    public void The_fallback_shell_of_the_platform_runs_the_wrapped_line()
    {
        var (fileName, arguments) = ClaudeShellLookup.CallFor(Other, null, "line");

        Assert.Equal("/bin/sh", fileName);
        Assert.Equal(["-c", "line"], arguments);
    }
}
