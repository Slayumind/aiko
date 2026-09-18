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

    // ---- what a hook says a session is doing ----

    [Fact]
    public void Every_hook_event_means_the_same_activity()
    {
        var cases = SpecCases.Json("hook-events");
        var session = cases.GetProperty("session").GetString();

        foreach (var row in cases.GetProperty("events").EnumerateArray())
        {
            var json = row.GetProperty("json").GetString()!;
            var activity = row.GetProperty("activity").GetString();

            var expected = activity is null
                ? null
                : new HookEvent(session!, Enum.Parse<SessionActivity>(activity));

            Assert.Equal(expected, HookEvent.FromJson(json));
        }

        foreach (var row in cases.GetProperty("badRecords").EnumerateArray())
        {
            Assert.Null(ActivityRecord.FromJson(row.GetString()!));
        }
    }

    // ---- which face an event brings ----

    [Fact]
    public void Every_mood_case_brings_the_same_face()
    {
        var cases = SpecCases.Json("tray-mood");
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        ActivityRecord? RecordIn(JsonElement element) =>
            element.ValueKind == JsonValueKind.Null
                ? null
                : new ActivityRecord(
                    "claude",
                    Enum.Parse<SessionActivity>(element.GetProperty("activity").GetString()!),
                    now.AddSeconds(-element.GetProperty("secondsAgo").GetDouble()));

        foreach (var row in cases.GetProperty("forActivity").EnumerateArray())
        {
            var face = row.GetProperty("face").GetString();

            Assert.Equal(
                face is null ? null : Enum.Parse<AikoFace>(face),
                TrayMood.ForActivity(RecordIn(row.GetProperty("before")), RecordIn(row.GetProperty("now"))!, now));
        }

        foreach (var row in cases.GetProperty("forLimit").EnumerateArray())
        {
            int? Percent(string name) =>
                row.GetProperty(name).ValueKind == JsonValueKind.Null ? null : row.GetProperty(name).GetInt32();

            var face = row.GetProperty("face").GetString();

            Assert.Equal(
                face is null ? null : Enum.Parse<AikoFace>(face),
                TrayMood.ForLimit(Percent("before"), Percent("after")));
        }
    }

    // ---- what a program is called on this system, and every path built from that ----

    [Fact]
    public void Every_platform_answers_its_own_paths()
    {
        foreach (var block in SpecCases.Json("platform-paths").GetProperty("platforms").EnumerateArray())
        {
            var name = block.GetProperty("platform").GetString();
            var platform = PlatformIn(block);

            Assert.Equal(block.GetProperty("executableName").GetString(),
                platform.ExecutableName(block.GetProperty("programName").GetString()!));
            Assert.Equal(block.GetProperty("pathListSeparator").GetString(), platform.PathListSeparator.ToString());
            Assert.Equal(block.GetProperty("directorySeparator").GetString(), platform.DirectorySeparator.ToString());
            Assert.Equal(block.GetProperty("localDataVariable").GetString(), platform.LocalDataVariable);

            var files = block.GetProperty("commandFiles");
            Assert.Equal(files.GetProperty("shim").GetString(), CommandLinks.ShimFileName(platform));
            Assert.Equal(
                files.GetProperty("commandFile").GetString(),
                CommandLinks.FileNameFor(platform, files.GetProperty("command").GetString()!));

            var seen = block.GetProperty("seen");
            Assert.Equal(
                seen.GetProperty("formatted").GetString(),
                RealClaude.FormatSeen(platform, [.. Strings(seen.GetProperty("folders"))]));
            Assert.Equal(
                Strings(seen.GetProperty("parsed")),
                RealClaude.ParseSeen(platform, seen.GetProperty("written").GetString()));

            var real = block.GetProperty("realClaude");
            var onDisk = Strings(real.GetProperty("files")).ToHashSet();
            Assert.Equal(
                real.GetProperty("found").GetString(),
                RealClaude.Find(platform, real.GetProperty("path").GetString(), [.. Strings(real.GetProperty("seen"))], onDisk.Contains));

            foreach (var row in block.GetProperty("bridge").EnumerateArray())
            {
                Assert.True(
                    row.GetProperty("ours").GetBoolean() == BridgeCommand.IsAiko(platform, row.GetProperty("command").GetString()),
                    $"{name}: {row.GetProperty("command").GetString()}");
            }

            var shell = block.GetProperty("shell");
            var (fileName, arguments) = ClaudeShellLookup.CallFor(platform, null, "line");
            Assert.Equal(Enum.Parse<ClaudeShell>(shell.GetProperty("kind").GetString()!), ClaudeShellLookup.ShellFor(platform, null));
            Assert.Equal(shell.GetProperty("fileName").GetString(), fileName);
            Assert.Equal(Strings(shell.GetProperty("arguments")), arguments);

            var install = block.GetProperty("install");
            var installed = Strings(install.GetProperty("files")).ToHashSet();
            Assert.Equal(
                install.GetProperty("found").GetString(),
                ClaudeInstall.Find(
                    platform,
                    install.GetProperty("freshPath").GetString()!,
                    install.GetProperty("commandFolder").GetString()!,
                    install.GetProperty("home").GetString()!,
                    installed.Contains));

            var made = block.GetProperty("newConfigFolder");
            Assert.Equal(
                made.GetProperty("folder").GetString(),
                ClaudeInstall.NewConfigFolder(
                    platform,
                    made.GetProperty("environmentName").GetString()!,
                    made.GetProperty("home").GetString()!,
                    _ => false));

            var credentials = block.GetProperty("credentials");
            Assert.Equal(
                credentials.GetProperty("path").GetString(),
                ClaudeInstall.CredentialsPathIn(platform, credentials.GetProperty("configFolder").GetString()!));

            var userPath = block.GetProperty("userPath");
            var folder = userPath.GetProperty("folder").GetString()!;
            var added = UserPathList.AddToFront(platform, userPath.GetProperty("before").GetString(), folder);
            Assert.Equal(userPath.GetProperty("added").GetString(), added);
            Assert.Equal(userPath.GetProperty("removed").GetString(), UserPathList.Remove(platform, added, folder));
            Assert.True(UserPathList.Contains(platform, userPath.GetProperty("holds").GetString(), folder), $"{name}: holds");

            var forShell = block.GetProperty("bridgeFor");
            Assert.Equal(
                forShell.GetProperty("command").GetString(),
                BridgeCommand.For(
                    forShell.GetProperty("bridge").GetString()!,
                    Enum.Parse<ClaudeShell>(forShell.GetProperty("shell").GetString()!)));

            var persona = block.GetProperty("personaCommand");
            var folders = new AikoFolders(
                platform,
                persona.GetProperty("settingsBase").GetString()!,
                persona.GetProperty("localBase").GetString()!);
            Assert.Equal(
                persona.GetProperty("command").GetString(),
                AikoMarketplace.PersonaCommand(platform, persona.GetProperty("bridge").GetString()!, folders));
        }
    }

    /// The two systems Aiko ships on are taken as they are; a block with conventions of its own
    /// describes a system that is neither, and the core has to follow the description it is given.
    private static PlatformConventions PlatformIn(JsonElement block)
    {
        if (!block.TryGetProperty("conventions", out var made))
        {
            return block.GetProperty("platform").GetString() switch
            {
                "windows" => PlatformConventions.Windows,
                "macos" => PlatformConventions.MacOS,
                var other => throw new InvalidOperationException($"unknown platform {other}"),
            };
        }

        var shell = made.GetProperty("fallbackShell");
        return new PlatformConventions
        {
            ExecutableSuffix = made.GetProperty("executableSuffix").GetString()!,
            PathListSeparator = made.GetProperty("pathListSeparator").GetString()![0],
            DirectorySeparator = made.GetProperty("directorySeparator").GetString()![0],
            LocalDataVariable = made.GetProperty("localDataVariable").GetString(),
            FallbackShell = new ShellProgram(
                Enum.Parse<ClaudeShell>(shell.GetProperty("kind").GetString()!),
                shell.GetProperty("fileName").GetString()!,
                [.. Strings(shell.GetProperty("argumentsBeforeCommand"))]),
        };
    }

    private static List<string> Strings(JsonElement array) =>
        [.. array.EnumerateArray().Select(e => e.GetString()!)];

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
