using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// The rules that live in spec/cases, run against the Windows core. The Swift core runs the same
/// files, so a row added here is a row both cores have to answer.
public class SharedCaseTests
{
    // ---- the file one environment reports into ----

    [Fact]
    public void Every_snapshot_name_case_answers_the_same()
    {
        var cases = SpecCases.Json("snapshot-name");

        Assert.Equal(SnapshotName.Default, cases.GetProperty("default").GetString());

        foreach (var row in cases.GetProperty("for").EnumerateArray())
        {
            var directory = row.GetProperty("configDirectory").GetString();

            Assert.Equal(row.GetProperty("name").GetString(), SnapshotName.For(directory));
        }

        foreach (var row in cases.GetProperty("clean").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("name").GetString(),
                SnapshotName.Clean(row.GetProperty("folderName").GetString()!));
        }
    }

    // ---- where Aiko keeps its files ----

    [Fact]
    public void Every_folder_of_both_layouts_is_where_the_cases_say()
    {
        var cases = SpecCases.Json("folder-layout");

        Assert.Equal(AikoFolders.AppFolderName, cases.GetProperty("appFolderName").GetString());
        Assert.Equal(AikoFolders.InstallFolderName, cases.GetProperty("installFolderName").GetString());
        var snapshotName = cases.GetProperty("snapshotName").GetString()!;

        foreach (var platform in cases.GetProperty("platforms").EnumerateArray())
        {
            var settingsBase = platform.GetProperty("settingsBase").GetString()!;
            var localBase = platform.GetProperty("localBase").GetString()!;
            var folders = platform.GetProperty("platform").GetString() switch
            {
                "windows" => AikoFolders.Windows(settingsBase, localBase),
                "macos" => AikoFolders.MacOS(settingsBase, localBase),
                var other => throw new InvalidOperationException($"unknown platform {other}"),
            };

            var built = new Dictionary<string, string>
            {
                ["settingsFolder"] = folders.SettingsFolder,
                ["settingsFile"] = folders.SettingsFile,
                ["environmentsFile"] = folders.EnvironmentsFile,
                ["personaFile"] = folders.PersonaFile,
                ["installIdFile"] = folders.InstallIdFile,
                ["reportedPeriodsFile"] = folders.ReportedPeriodsFile,
                ["localFolder"] = folders.LocalFolder,
                ["snapshotsFolder"] = folders.SnapshotsFolder,
                ["snapshotFile"] = folders.SnapshotFile(snapshotName),
                ["activityFolder"] = folders.ActivityFolder,
                ["directCacheFolder"] = folders.DirectCacheFolder,
                ["commandsFolder"] = folders.CommandsFolder,
                ["marketplaceFolder"] = folders.MarketplaceFolder,
                ["marketplaceFile"] = folders.MarketplaceFile,
                ["personaPluginsFolder"] = folders.PersonaPluginsFolder,
                ["logFile"] = folders.LogFile,
                ["installedAppFolder"] = folders.InstalledAppFolder,
            };

            foreach (var wanted in platform.GetProperty("paths").EnumerateObject())
            {
                Assert.Equal(wanted.Value.GetString(), built[wanted.Name]);
            }
        }
    }

    // ---- the command that starts one environment ----

    [Fact]
    public void Every_launch_command_case_answers_the_same()
    {
        var cases = SpecCases.Json("launch-command");

        Assert.Equal(LaunchCommand.Prefix, cases.GetProperty("prefix").GetString());
        Assert.Equal(LaunchCommand.MaxLength, cases.GetProperty("maxLength").GetInt32());

        foreach (var row in cases.GetProperty("fromEnvironmentName").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("command").GetString(),
                LaunchCommand.FromEnvironmentName(row.GetProperty("name").GetString()!));
        }

        foreach (var row in cases.GetProperty("slug").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("slug").GetString(), LaunchCommand.Slug(row.GetProperty("name").GetString()!));
        }

        foreach (var row in cases.GetProperty("check").EnumerateArray())
        {
            var taken = row.GetProperty("takenByOthers").EnumerateArray().Select(e => e.GetString()!);

            Assert.Equal(
                Enum.Parse<CommandProblem>(row.GetProperty("problem").GetString()!),
                LaunchCommand.Check(row.GetProperty("command").GetString()!, taken));
        }
    }

    // ---- which environment a start of Claude Code belongs to ----

    [Fact]
    public void Every_shim_case_decides_the_same()
    {
        var cases = SpecCases.Json("shim-launch");
        var userProfile = cases.GetProperty("userProfile").GetString()!;

        foreach (var group in cases.GetProperty("groups").EnumerateArray())
        {
            var settings = new EnvironmentSettings(
                [.. group.GetProperty("environments").EnumerateArray().Select(EnvironmentIn)]);

            foreach (var row in group.GetProperty("cases").EnumerateArray())
            {
                var plan = ShimLaunch.Decide(
                    row.GetProperty("invokedAs").GetString()!,
                    row.GetProperty("workingDirectory").GetString()!,
                    settings,
                    userProfile);

                var where = $"{group.GetProperty("name").GetString()}: {row.GetProperty("invokedAs").GetString()}";
                Assert.Equal(row.GetProperty("environment").GetString(), plan.Environment?.Name);
                Assert.Equal(Enum.Parse<ConfigVariable>(row.GetProperty("variable").GetString()!), plan.Variable);
                Assert.Equal(row.GetProperty("configDirectory").GetString(), plan.ConfigDirectory);
                Assert.True(row.GetProperty("explicit").GetBoolean() == plan.Explicit, where);
            }
        }
    }

    private static AikoEnvironment EnvironmentIn(JsonElement element) =>
        new(
            element.GetProperty("name").GetString()!,
            [.. element.GetProperty("configDirectories").EnumerateArray().Select(e => e.GetString()!)])
        {
            CustomCommand = element.TryGetProperty("command", out var command) ? command.GetString() : null,
            ProjectFolders = element.TryGetProperty("projectFolders", out var folders)
                ? [.. folders.EnumerateArray().Select(e => e.GetString()!)]
                : [],
        };
}
