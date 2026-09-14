using Aiko.Core;

namespace Aiko.Core.Tests;

/// Finding Git Bash, and calling somebody else's status line with the shell it was written for.
///
/// The bridge used to run every wrapped command through cmd.exe while Aiko wrote its own line for
/// Git Bash. A bash line put through cmd.exe prints nothing and reports nothing, so the promise
/// that an existing status line keeps working was quietly false.
public class ClaudeShellLookupTests
{
    private const string GitBash = @"C:\Program Files\Git\bin\bash.exe";
    private const string GitExe = @"D:\dev\git\cmd\git.exe";
    private const string BashBesideGit = @"D:\dev\git\cmd\..\bin\bash.exe";

    [Fact]
    public void Git_bash_is_found_where_the_installer_puts_it()
    {
        var places = new[] { new ClaudeShellLookup.Place(GitBash, null) };

        Assert.Equal(GitBash, ClaudeShellLookup.FindGitBash(p => p == GitBash, places));
    }

    [Fact]
    public void Without_Git_there_is_no_bash()
    {
        var places = new[] { new ClaudeShellLookup.Place(GitBash, null) };

        Assert.Null(ClaudeShellLookup.FindGitBash(_ => false, places));
    }

    [Fact]
    public void A_bash_on_the_path_counts_only_with_its_git_beside_it()
    {
        var places = new[] { new ClaudeShellLookup.Place(BashBesideGit, GitExe) };

        // The WSL launcher at System32\bash.exe is exactly this case: a bash with no git. Taking it
        // for Git Bash would write the bash form of the line while Claude Code ran PowerShell.
        Assert.Null(ClaudeShellLookup.FindGitBash(p => p == BashBesideGit, places));
        Assert.Equal(BashBesideGit, ClaudeShellLookup.FindGitBash(_ => true, places));
    }

    [Fact]
    public void The_first_place_that_answers_wins()
    {
        var places = new[]
        {
            new ClaudeShellLookup.Place(@"C:\nope\bash.exe", null),
            new ClaudeShellLookup.Place(GitBash, null),
        };

        Assert.Equal(GitBash, ClaudeShellLookup.FindGitBash(p => p == GitBash, places));
    }

    [Fact]
    public void A_place_that_throws_does_not_stop_the_search()
    {
        var places = new[]
        {
            new ClaudeShellLookup.Place(@"\\gone\share\bash.exe", null),
            new ClaudeShellLookup.Place(GitBash, null),
        };

        var found = ClaudeShellLookup.FindGitBash(
            p => p.StartsWith(@"\\", StringComparison.Ordinal)
                ? throw new IOException("the network share is not there")
                : p == GitBash,
            places);

        Assert.Equal(GitBash, found);
    }

    [Fact]
    public void The_shell_follows_whether_bash_was_found()
    {
        Assert.Equal(ClaudeShell.GitBash, ClaudeShellLookup.ShellFor(GitBash));
        Assert.Equal(ClaudeShell.PowerShell, ClaudeShellLookup.ShellFor(null));
    }

    [Fact]
    public void With_Git_the_wrapped_command_runs_in_bash()
    {
        var (fileName, arguments) = ClaudeShellLookup.CallFor(GitBash, "~/bin/my-line.sh");

        Assert.Equal(GitBash, fileName);
        Assert.Equal(["-c", "~/bin/my-line.sh"], arguments);
    }

    [Fact]
    public void Without_Git_the_wrapped_command_runs_in_PowerShell()
    {
        var (fileName, arguments) = ClaudeShellLookup.CallFor(null, "& my-line.ps1");

        Assert.Equal("powershell.exe", fileName);
        Assert.Contains("-NoProfile", arguments);
        Assert.Contains("-NonInteractive", arguments);
        Assert.Equal("& my-line.ps1", arguments[^1]);
    }

    [Fact]
    public void The_command_is_passed_whole_and_never_taken_apart()
    {
        // Quotes and spaces inside somebody else's command are theirs. The runtime quotes each
        // argument on its own, so nothing here has to escape anything.
        const string awkward = """jq -r '.model.display_name + " ok"' """;

        var (_, arguments) = ClaudeShellLookup.CallFor(GitBash, awkward);

        Assert.Equal(awkward, arguments[^1]);
    }

    [Fact]
    public void The_well_known_places_come_before_the_path()
    {
        var places = ClaudeShellLookup.Places(searchPath: @"D:\dev\git\cmd", windowsFolder: @"C:\Windows").ToList();

        Assert.Equal(4, places.Count);
        Assert.All(places.Take(3), p => Assert.Null(p.Guard));
        Assert.NotNull(places[^1].Guard);
    }

    [Fact]
    public void Folders_inside_Windows_are_skipped()
    {
        var places = ClaudeShellLookup.Places(
            searchPath: @"C:\Windows\System32;D:\dev\git\cmd", windowsFolder: @"C:\Windows").ToList();

        Assert.DoesNotContain(places, p => p.Bash.Contains("System32", StringComparison.OrdinalIgnoreCase));
    }
}
