using Aiko.Core;

namespace Aiko.Core.Tests;

/// The core takes the platform as a description, so its rules must follow whatever description
/// it gets. The system here is made up on purpose: it is neither Windows nor macOS, so a rule
/// that only works for the two we ship fails these cases.
///
/// What each platform names and where it puts things is a table, and tables live in
/// spec/cases/platform-paths; both cores read it. What stays here needs a settings object.
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
    public void Commands_are_links_named_like_the_platform_names_programs()
    {
        var plan = CommandLinks.Plan(Other, new EnvironmentSettings([new AikoEnvironment("Work", ["/w"])]), ["/bin/claude"]);

        Assert.Equal(["aiko-work"], plan.ToAdd);
    }
}
