using Aiko.Core;

namespace Aiko.Core.Tests;

public class CommandLinksTests
{
    private static EnvironmentSettings Settings(params AikoEnvironment[] environments) => new(environments);

    [Fact]
    public void An_empty_folder_gets_one_link_per_environment()
    {
        var plan = CommandLinks.Plan(Windows,
            Settings(new AikoEnvironment("Work", [@"C:\a"]), new AikoEnvironment("Personal", [@"C:\b"])),
            [@"C:\bin\claude.exe"]);

        Assert.Equal(["aiko-work.exe", "aiko-personal.exe"], plan.ToAdd);
        Assert.Empty(plan.ToRemove);
    }

    [Fact]
    public void A_rename_swaps_the_old_link_for_a_new_one()
    {
        var plan = CommandLinks.Plan(Windows,
            Settings(new AikoEnvironment("Job", [@"C:\a"])),
            [@"C:\bin\claude.exe", @"C:\bin\aiko-work.exe"]);

        Assert.Equal(["aiko-job.exe"], plan.ToAdd);
        Assert.Equal(["aiko-work.exe"], plan.ToRemove);
    }

    [Fact]
    public void Nothing_to_do_when_the_folder_matches_and_case_does_not_matter()
    {
        var plan = CommandLinks.Plan(Windows,
            Settings(new AikoEnvironment("Work", [@"C:\a"]) { CustomCommand = "cc" }),
            [@"C:\bin\CLAUDE.EXE", @"C:\bin\CC.exe", @"C:\bin\readme.txt", @"C:\bin\claude.exe.old"]);

        Assert.True(plan.IsEmpty);
    }
}
