using Aiko.Core;

namespace Aiko.Core.Tests;

/// What the Windows build writes and looks for today. These names are on people's disks and in
/// their PATH, so moving the platform details out of the core must keep every one of them.
public class PlatformPinTests
{
    [Fact]
    public void The_shim_and_the_commands_are_exe_files()
    {
        Assert.Equal("claude.exe", CommandLinks.ShimFileName);
        Assert.Equal("aiko-work.exe", CommandLinks.FileNameFor("aiko-work"));
    }

    [Fact]
    public void The_list_of_seen_shim_folders_is_joined_with_a_semicolon()
    {
        Assert.Equal(@"C:\new;C:\old", RealClaude.FormatSeen([@"C:\new", @"C:\old"]));
        Assert.Equal([@"C:\new", @"C:\old"], RealClaude.ParseSeen(@" C:\new ;;C:\old;"));
    }

    [Fact]
    public void The_real_claude_is_an_exe_file()
    {
        Assert.Equal(@"C:\tools\claude.exe", RealClaude.Find(@"C:\tools", [], _ => true));
    }

    [Fact]
    public void A_bridge_without_the_exe_suffix_is_not_ours()
    {
        Assert.False(BridgeCommand.IsAiko(@"""C:\Aiko\Aiko.Bridge"""));
        Assert.False(BridgeCommand.IsAiko(@"""C:\Aiko\Aiko.Bridge.cmd"""));
    }

    [Fact]
    public void The_user_path_keeps_entries_with_spaces_and_joins_with_a_semicolon()
    {
        Assert.Equal(@"C:\aiko\bin; C:\x ;C:\y", UserPathList.AddToFront(@" C:\x ;;C:\y", @"C:\aiko\bin"));
        Assert.True(UserPathList.Contains(@"C:\x;C:\aiko\bin\", @"C:\aiko\bin"));
    }

    [Fact]
    public void The_persona_command_outside_the_install_uses_the_full_path()
    {
        Assert.Equal(
            "\"C:\\Users\\someone\\AppData\\Local\\Other\\Aiko.Bridge.exe\" plugin aiko-persona",
            AikoMarketplace.PersonaCommand(@"C:\Users\someone\AppData\Local\Other\Aiko.Bridge.exe", @"C:\Users\someone\AppData\Local"));
    }

    [Fact]
    public void Aiko_keeps_its_local_files_under_LOCALAPPDATA_Aiko()
    {
        const string local = @"C:\Users\someone\AppData\Local";

        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\marketplace", AikoMarketplace.Folder(local));
        Assert.Equal(
            @"C:\Users\someone\AppData\Local\Aiko\marketplace\.claude-plugin\marketplace.json",
            AikoMarketplace.FilePath(local));
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\activity", ActivityRecord.Folder(local));
    }
}
