using Aiko.Core;

namespace Aiko.Core.Tests;

public class ShimTests
{
    private const string Home = @"C:\Users\someone";

    private static readonly EnvironmentSettings Settings = new([
        new AikoEnvironment("Work", [Home + @"\.claude"]),
        new AikoEnvironment("Personal", [Home + @"\.claude-personal"]) { ProjectFolders = [@"D:\personal"] },
    ]);

    // ---- which environment ----

    [Fact]
    public void Plain_claude_in_a_bound_folder_sets_that_folder()
    {
        var plan = ShimLaunch.Decide(@"C:\aiko\bin\claude.exe", @"D:\personal\app", Settings, Home);

        Assert.Equal("Personal", plan.Environment!.Name);
        Assert.Equal(ConfigVariable.Set, plan.Variable);
        Assert.Equal(Home + @"\.claude-personal", plan.ConfigDirectory);
        Assert.False(plan.Explicit);
    }

    [Fact]
    public void Environment_1_clears_the_variable_instead_of_pointing_it_at_claude()
    {
        var plan = ShimLaunch.Decide("claude", @"C:\temp", Settings, Home);

        Assert.Equal("Work", plan.Environment!.Name);
        Assert.Equal(ConfigVariable.Clear, plan.Variable);
        Assert.Null(plan.ConfigDirectory);
    }

    [Fact]
    public void A_command_wins_over_the_binding_and_says_so()
    {
        var plan = ShimLaunch.Decide("aiko-work.exe", @"D:\personal\app", Settings, Home);

        Assert.Equal("Work", plan.Environment!.Name);
        Assert.True(plan.Explicit);
    }

    [Fact]
    public void A_custom_command_is_found_by_its_own_name()
    {
        var settings = new EnvironmentSettings([
            Settings.Environments[0],
            Settings.Environments[1] with { CustomCommand = "cc-home" },
        ]);

        Assert.Equal("Personal", ShimLaunch.Decide("CC-HOME", @"C:\", settings, Home).Environment!.Name);
    }

    [Fact]
    public void A_command_nobody_owns_starts_claude_as_it_is()
    {
        Assert.Equal(ShimPlan.PassThrough, ShimLaunch.Decide("aiko-gone", @"C:\", Settings, Home));
    }

    [Fact]
    public void With_no_environments_nothing_changes()
    {
        Assert.Equal(ShimPlan.PassThrough, ShimLaunch.Decide("claude", @"C:\", EnvironmentSettings.Empty, Home));
    }

    // ---- the real claude.exe ----

    [Fact]
    public void The_real_claude_is_found_past_the_shim_folder()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\aiko\bin\claude.exe", @"C:\Users\someone\.local\bin\claude.exe",
        };

        var real = RealClaude.Find(
            @"C:\aiko\bin;C:\Windows;C:\Users\someone\.local\bin",
            [@"C:\aiko\bin\"],
            files.Contains);

        Assert.Equal(@"C:\Users\someone\.local\bin\claude.exe", real);
    }

    [Fact]
    public void Two_shim_folders_never_start_each_other()
    {
        // The spike: an old and a new shim folder in PATH made thousands of processes.
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\old\claude.exe", @"C:\new\claude.exe", @"C:\real\claude.exe",
        };
        var path = @"C:\new;C:\old;C:\real";

        var fromNew = RealClaude.Find(path, [@"C:\new"], files.Contains);
        Assert.Equal(@"C:\old\claude.exe", fromNew);

        // The old shim was started by the new one, so it inherits the list with both in it.
        var fromOld = RealClaude.Find(path, RealClaude.ParseSeen(RealClaude.FormatSeen([@"C:\new", @"C:\old"])), files.Contains);
        Assert.Equal(@"C:\real\claude.exe", fromOld);
    }

    [Fact]
    public void A_chain_that_is_too_long_stops()
    {
        var seen = Enumerable.Range(0, RealClaude.MaxChain + 1).Select(i => $@"C:\s{i}").ToList();

        Assert.Null(RealClaude.Find(@"C:\real", seen, _ => true));
    }

    [Fact]
    public void Quoted_and_empty_path_entries_are_fine()
    {
        Assert.Equal(@"C:\Program Files\x\claude.exe",
            RealClaude.Find(@";""C:\Program Files\x"";;", [], p => p == @"C:\Program Files\x\claude.exe"));
    }

    // ---- the user PATH ----

    [Fact]
    public void The_command_folder_goes_to_the_front_once()
    {
        var value = @"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;C:\aiko\bin\;C:\Users\someone\.local\bin";

        Assert.Equal(
            @"C:\aiko\bin;%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;C:\Users\someone\.local\bin",
            UserPathList.AddToFront(value, @"C:\aiko\bin"));
    }

    [Fact]
    public void Removing_leaves_everything_else_exactly_as_it_was()
    {
        var before = @"C:\One;%TWO%\x;c:\THREE";

        Assert.Equal(before, UserPathList.Remove(UserPathList.AddToFront(before, @"C:\aiko\bin"), @"C:\aiko\bin"));
        Assert.Equal(@"C:\aiko\bin", UserPathList.AddToFront(null, @"C:\aiko\bin"));
    }

    // ---- the PowerShell profile ----

    // The shape of the author's profile, with other folder names.
    private const string Profile =
        "\r\n$WorkRoot = \"$env:USERPROFILE\\Desktop\\work\"\r\n" +
        "$WorkConfigDir = \"$env:USERPROFILE\\.claude\"\r\n\r\n" +
        "function Invoke-ClaudeCLI {\r\n" +
        "    param([string]$ConfigDir, [string[]]$Args)\r\n" +
        "    if ($ConfigDir) { $env:CLAUDE_CONFIG_DIR = $ConfigDir }\r\n" +
        "    $exe = (Get-Command claude -CommandType Application -ErrorAction Stop).Source\r\n" +
        "    & $exe @Args\r\n" +
        "}\r\n\r\n" +
        "function claude {\r\n" +
        "    $cwd = (Get-Location).Path\r\n" +
        "    if ($cwd -like \"$WorkRoot\\*\") { Invoke-ClaudeCLI -ConfigDir $WorkConfigDir -Args $args }\r\n" +
        "    else { Invoke-ClaudeCLI -ConfigDir $env:CLAUDE_CONFIG_DIR -Args $args }\r\n" +
        "}\r\n\r\n" +
        "function prompt { \"PS {0}> \" -f (Get-Location) }\r\n\r\n" +
        "function cw {\r\n" +
        "    Invoke-ClaudeCLI -ConfigDir $WorkConfigDir -Args $args\r\n" +
        "}\r\n";

    [Fact]
    public void Finds_the_helper_the_claude_function_and_a_shortcut_that_calls_the_helper()
    {
        var found = PowerShellProfile.FindClaudeSwitchers(Profile);

        Assert.Equal(["Invoke-ClaudeCLI", "claude", "cw"], found.Select(f => f.Name));
        Assert.Equal((5, 10), (found[0].FirstLine, found[0].LastLine));
    }

    [Fact]
    public void Leaves_unrelated_functions_alone()
    {
        Assert.DoesNotContain(PowerShellProfile.FindClaudeSwitchers(Profile), f => f.Name == "prompt");
    }

    [Fact]
    public void Turning_off_and_on_gives_back_the_same_bytes()
    {
        var found = PowerShellProfile.FindClaudeSwitchers(Profile);

        var off = PowerShellProfile.TurnOff(Profile, found);

        Assert.True(PowerShellProfile.HasTurnedOff(off));
        Assert.Empty(PowerShellProfile.FindClaudeSwitchers(off));
        Assert.Contains("function prompt", off);
        Assert.Equal(Profile, PowerShellProfile.TurnOn(off));
    }

    [Fact]
    public void A_function_on_the_last_line_without_a_line_break_comes_back_the_same()
    {
        const string text = "function claude-work { claude --x }";

        var off = PowerShellProfile.TurnOff(text, PowerShellProfile.FindClaudeSwitchers(text));

        Assert.Equal(text, PowerShellProfile.TurnOn(off));
    }

    [Fact]
    public void Braces_in_strings_and_comments_do_not_confuse_it()
    {
        const string text = """
            # function claude { in a comment }
            function claude-home {
                Write-Host "a { brace" '}'
                <# a } block comment #>
                $env:CLAUDE_CONFIG_DIR = 'x'
            }
            """;

        var found = PowerShellProfile.FindClaudeSwitchers(text);

        Assert.Equal("claude-home", Assert.Single(found).Name);
        Assert.Equal((2, 6), (found[0].FirstLine, found[0].LastLine));
    }

    [Fact]
    public void A_profile_it_cannot_read_is_left_alone()
    {
        Assert.Empty(PowerShellProfile.FindClaudeSwitchers("function claude {\n  \"open string\n}\n"));
        Assert.Empty(PowerShellProfile.FindClaudeSwitchers("function claude {\n"));
    }
}
