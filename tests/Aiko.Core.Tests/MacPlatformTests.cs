using Aiko.Core;

namespace Aiko.Core.Tests;

/// What the macOS build writes and looks for (D-243, D-244, D-250). The Swift core repeats these
/// cases, so a change here has to be made twice on purpose rather than once by accident.
public class MacPlatformTests
{
    private static readonly PlatformConventions Mac = PlatformConventions.MacOS;

    private const string Home = "/Users/someone";

    private static readonly AikoFolders Folders = AikoFolders.MacOS(
        Home + "/Library/Application Support", Home + "/Library/Caches");

    [Fact]
    public void A_program_has_no_suffix_and_folders_are_listed_with_a_colon()
    {
        Assert.Equal("claude", Mac.ExecutableName("claude"));
        Assert.Equal(':', Mac.PathListSeparator);
        Assert.Equal('/', Mac.DirectorySeparator);
    }

    [Fact]
    public void A_command_spells_the_real_path_because_there_is_no_variable_for_it()
    {
        // %LOCALAPPDATA% is a cmd.exe idea. A shell on macOS would pass "%LOCALAPPDATA%" through
        // as four plain characters, and Claude Code would run a command that points nowhere.
        Assert.Null(Mac.LocalDataVariable);

        var bridge = "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge";

        Assert.Equal(
            $"\"{bridge}\" plugin aiko-persona",
            AikoMarketplace.PersonaCommand(Mac, bridge, Folders));
    }

    [Fact]
    public void The_status_line_runs_in_a_login_zsh()
    {
        // -l so the line sees the PATH the person's own tools are on; there is no Git Bash to
        // prefer, so this is the only shell the Mac build ever uses.
        var (fileName, arguments) = ClaudeShellLookup.CallFor(Mac, null, "line");

        Assert.Equal(ClaudeShell.Zsh, ClaudeShellLookup.ShellFor(Mac, null));
        Assert.Equal("/bin/zsh", fileName);
        Assert.Equal(["-lc", "line"], arguments);
    }

    [Fact]
    public void Zsh_gets_no_call_operator_because_only_PowerShell_needs_one()
    {
        var bridge = "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge";

        Assert.Equal($"\"{bridge}\"", BridgeCommand.For(bridge, ClaudeShell.Zsh));
        Assert.True(BridgeCommand.IsAiko(Mac, BridgeCommand.For(bridge, ClaudeShell.Zsh)));
        Assert.False(BridgeCommand.IsAiko(Mac, "\"/Applications/Aiko.app/Aiko.Bridge.exe\""));
    }

    [Fact]
    public void The_shim_and_the_commands_are_plain_files_with_no_suffix()
    {
        Assert.Equal("claude", CommandLinks.ShimFileName(Mac));
        Assert.Equal("aiko-work", CommandLinks.FileNameFor(Mac, "aiko-work"));
    }

    [Fact]
    public void The_list_of_seen_shim_folders_is_joined_with_a_colon()
    {
        Assert.Equal("/a/bin:/b/bin", RealClaude.FormatSeen(Mac, ["/a/bin", "/b/bin"]));
        Assert.Equal(["/a/bin", "/b/bin"], RealClaude.ParseSeen(Mac, " /a/bin ::/b/bin:"));
    }

    [Fact]
    public void The_real_claude_is_found_past_the_command_folder()
    {
        var files = new HashSet<string> { "/Users/someone/Library/Caches/Aiko/bin/claude", "/opt/homebrew/bin/claude" };

        Assert.Equal(
            "/opt/homebrew/bin/claude",
            RealClaude.Find(Mac, "/Users/someone/Library/Caches/Aiko/bin:/opt/homebrew/bin", ["/Users/someone/Library/Caches/Aiko/bin"], files.Contains));
    }

    [Fact]
    public void Claude_Code_falls_back_to_the_folder_its_own_installer_uses()
    {
        var native = Home + "/.local/bin/claude";

        Assert.Equal(native, ClaudeInstall.Find(Mac, "/usr/bin", "/tmp/bin", Home, p => p == native));
    }

    [Fact]
    public void A_new_environment_folder_sits_beside_claude_in_the_home_folder()
    {
        Assert.Equal(Home + "/.claude-work", ClaudeInstall.NewConfigFolder(Mac, "Work", Home, _ => false));
        Assert.Equal(Home + "/.claude/.credentials.json", ClaudeInstall.CredentialsPathIn(Mac, Home + "/.claude"));
    }

    [Fact]
    public void The_persons_choices_live_in_Application_Support()
    {
        Assert.Equal(Home + "/Library/Application Support/Aiko", Folders.SettingsFolder);
        Assert.Equal(Home + "/Library/Application Support/Aiko/settings.json", Folders.SettingsFile);
        Assert.Equal(Home + "/Library/Application Support/Aiko/environments.json", Folders.EnvironmentsFile);
        Assert.Equal(Home + "/Library/Application Support/Aiko/persona.json", Folders.PersonaFile);
        Assert.Equal(Home + "/Library/Application Support/Aiko/install-id", Folders.InstallIdFile);
        Assert.Equal(Home + "/Library/Application Support/Aiko/reported.json", Folders.ReportedPeriodsFile);
    }

    [Fact]
    public void What_Aiko_makes_for_itself_lives_in_Caches()
    {
        Assert.Equal(Home + "/Library/Caches/Aiko", Folders.LocalFolder);
        Assert.Equal(Home + "/Library/Caches/Aiko/environments", Folders.SnapshotsFolder);
        Assert.Equal(Home + "/Library/Caches/Aiko/environments/claude-work.json", Folders.SnapshotFile("claude-work"));
        Assert.Equal(Home + "/Library/Caches/Aiko/activity", Folders.ActivityFolder);
        Assert.Equal(Home + "/Library/Caches/Aiko/direct", Folders.DirectCacheFolder);
        Assert.Equal(Home + "/Library/Caches/Aiko/bin", Folders.CommandsFolder);
        Assert.Equal(Home + "/Library/Caches/Aiko/marketplace", Folders.MarketplaceFolder);
        Assert.Equal(
            Home + "/Library/Caches/Aiko/marketplace/.claude-plugin/marketplace.json",
            Folders.MarketplaceFile);
        Assert.Equal(Home + "/Library/Caches/Aiko/plugins", Folders.PersonaPluginsFolder);
        Assert.Equal(Home + "/Library/Caches/Aiko/log.txt", Folders.LogFile);
    }

    [Fact]
    public void Both_systems_name_the_files_under_the_bases_alike()
    {
        // Only the two base folders differ, so a rule written about "the snapshots folder" holds
        // on either system and the two cores keep one layout between them.
        var windows = AikoFolders.Windows(@"C:\Roaming", @"C:\Local");

        Assert.Equal(@"C:\Local\Aiko\environments\claude.json", windows.SnapshotFile("claude"));
        Assert.Equal("/Users/someone/Library/Caches/Aiko/environments/claude.json", Folders.SnapshotFile("claude"));
    }

    [Fact]
    public void A_base_folder_written_with_a_trailing_slash_does_not_double_it()
    {
        Assert.Equal(
            "/Users/someone/Library/Caches/Aiko",
            AikoFolders.MacOS("/x", "/Users/someone/Library/Caches/").LocalFolder);
    }
}
