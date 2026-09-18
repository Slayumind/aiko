using Aiko.Core;

namespace Aiko.Core.Tests;

public class ClaudeInstallTests
{
    private const string Home = @"C:\Users\someone";

    [Fact]
    public void Fresh_path_joins_machine_and_user_and_expands_them()
    {
        var path = ClaudeInstall.FreshPath(Windows,
            @"C:\Windows",
            @"%USERPROFILE%\.local\bin",
            text => text.Replace("%USERPROFILE%", Home));

        Assert.Equal(@"C:\Windows;C:\Users\someone\.local\bin", path);
        Assert.Equal(@"C:\Windows", ClaudeInstall.FreshPath(Windows, @"C:\Windows", null, t => t));
    }

    [Fact]
    public void Finds_claude_past_the_command_folder()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\aiko\bin\claude.exe", @"C:\tools\claude.exe",
        };

        Assert.Equal(@"C:\tools\claude.exe", ClaudeInstall.Find(Windows, @"C:\aiko\bin;C:\tools", @"C:\aiko\bin", Home, files.Contains));
    }

    [Fact]
    public void Falls_back_to_the_native_installer_folder()
    {
        var native = Home + @"\.local\bin\claude.exe";

        Assert.Equal(native, ClaudeInstall.Find(Windows, @"C:\Windows", @"C:\aiko\bin", Home, p => p == native));
        Assert.Null(ClaudeInstall.Find(Windows, @"C:\Windows", @"C:\aiko\bin", Home, _ => false));
    }

    [Theory]
    [InlineData("Work", @"C:\Users\someone\.claude-work")]
    [InlineData("Основная", @"C:\Users\someone\.claude-osnovnaya")]
    [InlineData("!!", @"C:\Users\someone\.claude-env")]
    public void A_new_folder_is_named_after_the_environment(string name, string folder)
    {
        Assert.Equal(folder, ClaudeInstall.NewConfigFolder(Windows, name, Home, _ => false));
    }

    [Fact]
    public void A_taken_folder_name_gets_a_number()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Home + @"\.claude-work", Home + @"\.claude-work-2",
        };

        Assert.Equal(Home + @"\.claude-work-3", ClaudeInstall.NewConfigFolder(Windows, "Work", Home, taken.Contains));
    }
}
