using System.Text.Json;
using System.Text.Json.Nodes;
using Aiko.Core;
using Aiko.Core.Terminal;

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
                ["memoryFile"] = folders.MemoryFile,
                ["installIdFile"] = folders.InstallIdFile,
                ["reportedPeriodsFile"] = folders.ReportedPeriodsFile,
                ["terminalFile"] = folders.TerminalFile,
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
                ["voiceModelFile"] = folders.VoiceModelFile,
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

    // ---- what Aiko remembers about the person ----

    [Fact]
    public void Every_memory_key_case_answers_the_same()
    {
        var cases = SpecCases.Json("memory-keys");

        Assert.Equal(MemoryRules.MaxKeys, cases.GetProperty("maxKeys").GetInt32());
        Assert.Equal(MemoryRules.MaxKeyTail, cases.GetProperty("maxTail").GetInt32());
        Assert.Equal(MemoryRules.MinStemLength, cases.GetProperty("minStemLength").GetInt32());
        Assert.Equal(MemoryRules.MaxStemTail, cases.GetProperty("maxStemTail").GetInt32());

        foreach (var row in cases.GetProperty("nameLike").EnumerateArray())
        {
            string key = row.GetProperty("key").GetString()!;

            Assert.True(row.GetProperty("nameLike").GetBoolean() == MemoryRules.IsNameLike(key), key);
        }

        foreach (var row in cases.GetProperty("clean").EnumerateArray())
        {
            var cleaned = MemoryRules.CleanKeys(Strings(row.GetProperty("keys")));

            Assert.Equal(Strings(row.GetProperty("kept")), cleaned.Kept);
            Assert.Equal(Strings(row.GetProperty("rejected")), cleaned.Rejected);
        }

        foreach (var row in cases.GetProperty("matches").EnumerateArray())
        {
            string key = row.GetProperty("key").GetString()!;
            string prompt = row.GetProperty("prompt").GetString()!;

            Assert.True(
                row.GetProperty("matches").GetBoolean() == MemoryRules.KeyMatches(key, prompt),
                $"{key} in: {prompt}");
        }
    }

    [Fact]
    public void Every_memory_nudge_case_answers_the_same()
    {
        foreach (var row in SpecCases.Json("memory-nudge").GetProperty("cases").EnumerateArray())
        {
            string prompt = row.GetProperty("prompt").GetString()!;

            Assert.True(row.GetProperty("nudge").GetBoolean() == MemoryNudge.Applies(prompt), prompt);
        }
    }

    [Fact]
    public void Every_memory_limit_case_is_saved_or_refused_the_same()
    {
        var cases = SpecCases.Json("memory-limits");
        var today = new DateOnly(2026, 10, 11);

        Assert.Equal(MemoryRules.MaxLength, cases.GetProperty("maxLength").GetInt32());

        foreach (var row in cases.GetProperty("cases").EnumerateArray())
        {
            string text = Repeated(row, "text");
            string meaning = Repeated(row, "meaning");
            var keys = Strings(row.GetProperty("keys"));
            int length = row.GetProperty("length").GetInt32();
            bool saved = row.GetProperty("saved").GetBoolean();

            Assert.Equal(length, MemoryRules.LengthOf(text, meaning, keys));
            Assert.True(saved == MemoryRules.Fits(length), $"{length}");

            var edit = MemoryEdits.Remember(
                MemoryFile.Empty,
                new MemoryRequest { Category = "you", Text = text, Meaning = meaning, Keys = keys },
                today);

            Assert.True(saved == edit.Changed, $"{length}: {edit.Reply}");
            Assert.Equal(saved ? 1 : 0, edit.Memory.Facts.Count);
        }
    }

    // ---- the terminal: what Claude Code says and what Aiko answers ----

    [Fact]
    public void Every_stream_line_becomes_the_same_event()
    {
        foreach (var row in SpecCases.Json("stream-events").GetProperty("events").EnumerateArray())
        {
            var line = row.TryGetProperty("raw", out var raw) ? raw.GetString()! : row.GetProperty("line").GetRawText();
            var expected = JsonNode.Parse(row.GetProperty("event").GetRawText());

            var described = Described(StreamEvent.FromLine(line));

            Assert.True(
                JsonNode.DeepEquals(expected, described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_host_line_is_written_the_same()
    {
        foreach (var row in SpecCases.Json("host-lines").GetProperty("lines").EnumerateArray())
        {
            var action = row.GetProperty("action");
            var id = action.TryGetProperty("requestId", out var request) ? request.GetString()! : "";
            JsonElement Input() => action.GetProperty("input").Clone();

            var line = action.GetProperty("kind").GetString() switch
            {
                "userMessage" => HostLine.UserMessage(
                    action.GetProperty("text").GetString()!,
                    [.. action.GetProperty("images").EnumerateArray().Select(image => new AttachedImage(
                        image.GetProperty("mediaType").GetString()!,
                        image.GetProperty("base64").GetString()!))]),
                "allow" => HostLine.Allow(id, Input()),
                "allowEdits" => HostLine.AllowEdits(id, Input()),
                "allowAlways" => HostLine.AllowAlways(id, Input(), action.GetProperty("rule").Clone()),
                "deny" => HostLine.Deny(id, action.GetProperty("message").GetString()!),
                "answer" => HostLine.Answer(
                    id,
                    Input(),
                    [.. action.GetProperty("answers").EnumerateArray().Select(answer => KeyValuePair.Create(
                        answer.GetProperty("question").GetString()!,
                        answer.GetProperty("answer").GetString()!))]),
                "interrupt" => HostLine.Interrupt(id),
                "setMode" => HostLine.SetMode(id, Mode(action.GetProperty("mode"))!.Value),
                "setModel" => HostLine.SetModel(id, action.GetProperty("model").GetString()!),
                "initialize" => HostLine.Initialize(id),
                var other => throw new InvalidOperationException($"unknown action {other}"),
            };

            Assert.Equal(row.GetProperty("line").GetString(), line);
        }
    }

    [Fact]
    public void Every_launch_of_a_conversation_is_planned_the_same()
    {
        var cases = SpecCases.Json("launch-plan");
        var home = cases.GetProperty("userProfile").GetString()!;

        foreach (var row in cases.GetProperty("cases").EnumerateArray())
        {
            var request = new LaunchRequest(
                row.GetProperty("configDirectory").GetString()!,
                home,
                row.GetProperty("environment").GetString()!,
                row.GetProperty("sessionId").GetString()!,
                row.GetProperty("started").GetBoolean(),
                Mode(row.GetProperty("mode"))!.Value,
                row.GetProperty("model").GetString())
            {
                AgentTodos = row.TryGetProperty("agentTodos", out var todos) && todos.GetBoolean(),
            };

            var plan = ClaudeLaunchPlan.For(request, Strings(row.GetProperty("inherited")));

            Assert.Equal(Strings(row.GetProperty("arguments")), plan.Arguments);
            Assert.Equal(
                [.. row.GetProperty("variables").EnumerateArray().Select(variable => new VariableChange(
                    variable.GetProperty("name").GetString()!,
                    variable.GetProperty("value").GetString()))],
                plan.Variables);
        }

        foreach (var row in cases.GetProperty("commands").EnumerateArray())
        {
            var plan = ClaudeLaunchPlan.ForCommands(
                row.GetProperty("configDirectory").GetString()!, home, Strings(row.GetProperty("inherited")));

            Assert.Equal(Strings(row.GetProperty("arguments")), plan.Arguments);
            Assert.Equal(
                [.. row.GetProperty("variables").EnumerateArray().Select(variable => new VariableChange(
                    variable.GetProperty("name").GetString()!,
                    variable.GetProperty("value").GetString()))],
                plan.Variables);
        }
    }

    [Fact]
    public void Every_file_mention_offers_the_same_files()
    {
        var cases = SpecCases.Json("file-mention");
        var paths = Strings(cases.GetProperty("paths"));

        foreach (var row in cases.GetProperty("queries").EnumerateArray())
        {
            var text = row.GetProperty("text").GetString()!;
            var found = FileMention.Query(text, row.GetProperty("caret").GetInt32());
            var from = row.GetProperty("from");
            Assert.True(from.ValueKind == JsonValueKind.Null ? found is null : found?.From == from.GetInt32(), text);
            Assert.True(row.GetProperty("query").GetString() == found?.Text, text);
        }

        foreach (var row in cases.GetProperty("matches").EnumerateArray())
        {
            Assert.Equal(Strings(row.GetProperty("files")), FileMention.Matches(paths, row.GetProperty("query").GetString()!));
        }

        foreach (var row in cases.GetProperty("chosen").EnumerateArray())
        {
            var text = row.GetProperty("text").GetString()!;
            var caret = row.GetProperty("caret").GetInt32();
            var (result, at) = FileMention.Chosen(text, FileMention.Query(text, caret)!, caret, row.GetProperty("path").GetString()!);
            Assert.Equal(row.GetProperty("result").GetString(), result);
            Assert.Equal(row.GetProperty("resultCaret").GetInt32(), at);
        }
    }

    [Fact]
    public void Every_shell_command_is_read_and_written_the_same()
    {
        var cases = SpecCases.Json("shell-command");
        static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetString();

        foreach (var row in cases.GetProperty("of").EnumerateArray())
        {
            Assert.Equal(Text(row.GetProperty("command")), ShellCommand.Of(row.GetProperty("text").GetString()!));
        }

        foreach (var row in cases.GetProperty("message").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("text").GetString(),
                ShellCommand.Message(row.GetProperty("command").GetString()!, row.GetProperty("output").GetString()!, row.GetProperty("exitCode").GetInt32()));
        }

        foreach (var row in cases.GetProperty("read").EnumerateArray())
        {
            var read = ShellCommand.Read(row.GetProperty("text").GetString()!);
            Assert.Equal(Text(row.GetProperty("command")), read?.Command);
            Assert.Equal(Text(row.GetProperty("output")), read?.Output);
            Assert.Equal(row.TryGetProperty("exitCode", out var code) ? code.GetInt32() : 0, read?.ExitCode ?? 0);
        }

        foreach (var row in cases.GetProperty("outputs").EnumerateArray())
        {
            Assert.Equal(Text(row.GetProperty("output")), ShellCommand.Output(row.GetProperty("text").GetString()!));
        }
    }

    [Fact]
    public void Every_feed_joins_the_same_steps_and_cuts_the_same_output()
    {
        var cases = SpecCases.Json("feed-entries");

        foreach (var feed in cases.GetProperty("feeds").EnumerateArray())
        {
            List<FeedItem> items = [.. feed.GetProperty("items").EnumerateArray().Select((item, at) => FeedItemIn(item, at + 1))];
            var busy = feed.TryGetProperty("busy", out var flag) && flag.GetBoolean();
            var rows = new JsonArray([.. FeedEntries.Of(items, busy).Select(Described)]);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(feed.GetProperty("rows").GetRawText()), rows),
                $"{feed.GetProperty("name").GetString()}: {rows.ToJsonString()}");
        }

        foreach (var row in cases.GetProperty("outputs").EnumerateArray())
        {
            var count = row.GetProperty("lines").GetInt32();
            var (text, hidden) = LongOutput.Cut(string.Join('\n', Enumerable.Range(1, count).Select(at => $"line {at}")) + "\n");
            Assert.Equal(row.GetProperty("shown").GetInt32(), StepLine.Lines(text).Count);
            Assert.Equal(row.GetProperty("hidden").GetInt32(), hidden);
        }
    }

    private static FeedItem FeedItemIn(JsonElement item, int id)
    {
        if (item.TryGetProperty("other", out _))
        {
            return new FeedItem.Answer(id, "words", false);
        }

        if (item.TryGetProperty("thought", out var thought))
        {
            return new FeedItem.Thought(id, thought.GetString()!);
        }

        if (item.TryGetProperty("waiting", out var waiting))
        {
            return new FeedItem.OwnerMessage(id, waiting.GetString()!, true, []);
        }

        var kind = Enum.Parse<StepKind>(item.GetProperty("kind").GetString()!, ignoreCase: true);
        var hidden = item.TryGetProperty("hidden", out var flag) && flag.GetBoolean();
        var line = new StepLine(kind, "", item.GetProperty("subject").GetString()!, 0, 0, hidden);
        var state = Enum.Parse<StepState>(item.GetProperty("state").GetString()!, ignoreCase: true);
        return new FeedItem.Step(id, $"toolu_{id}", line, state, "", [], "");
    }

    private static JsonObject Described(FeedEntry entry) => entry switch
    {
        FeedEntry.Run run => new JsonObject
        {
            ["run"] = new JsonArray([.. run.Steps.Select(step => (JsonNode)step.Id)]),
            ["kind"] = Camel(run.Kind),
            ["state"] = Camel(run.State),
            ["names"] = new JsonArray([.. run.Subjects().Names.Select(name => (JsonNode)name)]),
            ["more"] = run.Subjects().More,
        },
        FeedEntry.Thought thought => new JsonObject { ["thought"] = entry.Id, ["open"] = thought.Open },
        _ => new JsonObject { ["one"] = entry.Id },
    };

    [Fact]
    public void Every_slash_menu_shows_the_same_commands()
    {
        var cases = SpecCases.Json("slash-menu");
        List<SlashCommand> commands = [.. cases.GetProperty("commands").EnumerateArray().Select(command => new SlashCommand(
            command.GetProperty("name").GetString()!,
            command.GetProperty("description").GetString()!,
            command.GetProperty("argumentHint").GetString()!,
            Strings(command.GetProperty("aliases")),
            command.GetProperty("builtin").GetBoolean()))];

        foreach (var row in cases.GetProperty("queries").EnumerateArray())
        {
            var text = row.GetProperty("text").GetString()!;
            Assert.True(row.GetProperty("query").GetString() == SlashMenu.Query(text, row.GetProperty("caret").GetInt32()), text);
        }

        foreach (var row in cases.GetProperty("matches").EnumerateArray())
        {
            var query = row.GetProperty("query").GetString()!;
            var found = SlashMenu.Matches(commands, query);
            Assert.Equal(Strings(row.GetProperty("names")), [.. found.Select(match => match.Command.Name)]);
            Assert.Equal(
                [.. row.GetProperty("marks").EnumerateArray().Select(mark => (mark[0].GetInt32(), mark[1].GetInt32()))],
                found.Select(match => (match.MatchStart, match.MatchLength)).ToList());
            Assert.True(row.GetProperty("lit").GetBoolean() == SlashMenu.LightsFirst(found), query);
        }

        foreach (var row in cases.GetProperty("chosen").EnumerateArray())
        {
            var command = commands.Single(one => one.Name == row.GetProperty("name").GetString());
            Assert.Equal(row.GetProperty("text").GetString(), SlashMenu.Chosen(command));
        }
    }

    [Fact]
    public void Every_permission_mode_has_the_same_names()
    {
        var cases = SpecCases.Json("permission-mode");

        Assert.Equal(
            Strings(cases.GetProperty("order")),
            PermissionModes.All.Select(PermissionModes.Name));

        foreach (var row in cases.GetProperty("modes").EnumerateArray())
        {
            var mode = Mode(row.GetProperty("mode"))!.Value;

            Assert.Equal(row.GetProperty("name").GetString(), PermissionModes.Name(mode));
            Assert.Equal(row.GetProperty("flag").GetString(), PermissionModes.FlagValue(mode));
            Assert.Equal(row.GetProperty("request").GetString(), PermissionModes.RequestValue(mode));
            Assert.Equal(Mode(row.GetProperty("next")), PermissionModes.Next(mode));
        }

        foreach (var row in cases.GetProperty("fromStream").EnumerateArray())
        {
            Assert.Equal(Mode(row.GetProperty("mode")), PermissionModes.FromStream(row.GetProperty("value").GetString()));
        }

        foreach (var row in cases.GetProperty("defaults").EnumerateArray())
        {
            var settings = row.GetProperty("settings").EnumerateArray()
                .Select(file => file.ValueKind == JsonValueKind.String ? file.GetString() : null)
                .ToList();
            Assert.True(
                Mode(row.GetProperty("mode")) == PermissionModes.Default(settings),
                row.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Every_limit_badge_names_the_same_limit()
    {
        var now = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        foreach (var row in SpecCases.Json("limit-badge").GetProperty("badges").EnumerateArray())
        {
            var snapshot = new LimitSnapshot(
                "claude",
                LimitSource.StatusLine,
                now.AddSeconds(-row.GetProperty("ageSeconds").GetInt32()),
                [.. row.GetProperty("windows").EnumerateArray().Select(window => new LimitWindow(
                    Enum.Parse<LimitKind>(window.GetProperty("kind").GetString()!, ignoreCase: true),
                    window.GetProperty("percent").GetInt32(),
                    now.AddSeconds(window.GetProperty("resetsInSeconds").GetInt32())))]);

            var badge = row.GetProperty("badge");
            LimitBadge? expected = badge.ValueKind == JsonValueKind.Null
                ? null
                : new LimitBadge(
                    Enum.Parse<LimitKind>(badge.GetProperty("kind").GetString()!, ignoreCase: true),
                    badge.GetProperty("percent").GetInt32(),
                    Enum.Parse<LimitTone>(badge.GetProperty("tone").GetString()!, ignoreCase: true));

            Assert.True(expected == LimitBadge.For(snapshot, now), row.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Every_leftover_of_a_bridge_is_found_the_same_way()
    {
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        foreach (var row in SpecCases.Json("snapshot-leftovers").GetProperty("files").EnumerateArray())
        {
            var name = row.GetProperty("name").GetString()!;
            var stale = SnapshotLeftovers.Stale([(name, now.AddSeconds(-row.GetProperty("age").GetInt32()))], now);
            Assert.True(row.GetProperty("stale").GetBoolean() == stale.Contains(name), name);
        }
    }

    [Fact]
    public void Every_model_id_gets_the_same_name()
    {
        foreach (var row in SpecCases.Json("model-name").GetProperty("names").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("name").GetString(), ModelName.Display(row.GetProperty("id").GetString()));
        }
    }

    [Fact]
    public void Every_model_label_waits_the_same_way()
    {
        foreach (var row in SpecCases.Json("model-name").GetProperty("labels").EnumerateArray())
        {
            var label = ModelName.Label(row.GetProperty("asked").GetString(), row.GetProperty("known").GetString());
            Assert.Equal(row.GetProperty("text").GetString(), label.Text);
            Assert.Equal(row.GetProperty("waiting").GetBoolean(), label.Waiting);
        }
    }

    [Fact]
    public void Every_change_of_model_gives_the_same_full_id()
    {
        foreach (var row in SpecCases.Json("model-name").GetProperty("switches").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("id").GetString(), ModelName.FromSwitch(row.GetProperty("text").GetString()));
        }
    }

    [Fact]
    public void Every_markdown_text_is_read_into_the_same_blocks()
    {
        foreach (var row in SpecCases.Json("markdown").GetProperty("texts").EnumerateArray())
        {
            var expected = JsonNode.Parse(row.GetProperty("blocks").GetRawText());

            var described = Described(Markdown.Parse(row.GetProperty("markdown").GetString()));

            Assert.True(
                JsonNode.DeepEquals(expected, described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_edit_shows_the_same_diff()
    {
        foreach (var row in SpecCases.Json("edit-diff").GetProperty("diffs").EnumerateArray())
        {
            var diff = EditDiff.For(row.GetProperty("tool").GetString()!, row.GetProperty("input"));
            var described = new JsonObject
            {
                ["lines"] = diff is null
                    ? null
                    : new JsonArray([.. diff.Lines.Select(line => (JsonNode)new JsonArray(Camel(line.Side), line.Text))]),
                ["more"] = diff?.More,
            };

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("lines").GetRawText()), described["lines"])
                && JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("more").GetRawText()), described["more"]),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_step_gets_the_same_line()
    {
        foreach (var row in SpecCases.Json("step-line").GetProperty("steps").EnumerateArray())
        {
            var described = Described(StepLine.For(row.GetProperty("tool").GetString()!, row.GetProperty("input")));

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("line").GetRawText()), described),
                $"{row.GetProperty("tool").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_request_becomes_the_same_card()
    {
        foreach (var row in SpecCases.Json("request-card").GetProperty("cards").EnumerateArray())
        {
            var card = CardIn(row);
            var name = row.GetProperty("name").GetString();

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("card").GetRawText()), Described(card)),
                $"{name}: {Described(card).ToJsonString()}");

            foreach (var choice in row.GetProperty("choices").EnumerateArray())
            {
                Assert.Equal(choice.GetProperty("line").GetString(), card.LineFor(ChoiceIn(choice)));
            }
        }
    }

    [Fact]
    public void Every_context_gauge_reads_the_same()
    {
        var cases = SpecCases.Json("context-gauge");
        foreach (var row in cases.GetProperty("percent").EnumerateArray())
        {
            var percent = ContextGauge.Percent(row.GetProperty("tokens").GetInt32(), row.GetProperty("window").GetInt32());
            Assert.Equal(row.GetProperty("percent").ValueKind == JsonValueKind.Null ? null : row.GetProperty("percent").GetInt32(), percent);
        }
        foreach (var row in cases.GetProperty("level").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("level").GetString(), Camel(ContextGauge.Level(row.GetProperty("percent").GetInt32())));
        }
        foreach (var row in cases.GetProperty("window").EnumerateArray())
        {
            var windows = row.GetProperty("windows").EnumerateObject().ToDictionary(model => model.Name, model => model.Value.GetInt32());
            Assert.Equal(
                row.GetProperty("window").GetInt32(),
                ContextGauge.WindowFor(row.GetProperty("model").GetString()!, windows, row.GetProperty("known").GetInt32()));
        }
    }

    private static JsonObject WithDeclined(JsonObject step, bool declined)
    {
        if (declined)
        {
            step["declined"] = true;
        }

        return step;
    }

    [Fact]
    public void Every_conversation_ends_in_the_same_state()
    {
        foreach (var row in SpecCases.Json("conversation").GetProperty("scenarios").EnumerateArray())
        {
            var (state, written) = Played(row);
            var name = row.GetProperty("name").GetString();

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("state").GetRawText()), Described(state)),
                $"{name}: {Described(state).ToJsonString()}");
            Assert.True(Strings(row.GetProperty("written")).SequenceEqual(written), $"{name}: written " + string.Join(" || ", written));
        }
    }

    private static RequestCard CardIn(JsonElement row) =>
        RequestCard.From((StreamEvent.Request)StreamEvent.FromLine(row.GetProperty("request").GetRawText()));

    private static CardChoice ChoiceIn(JsonElement choice) => choice.GetProperty("choice").GetString() switch
    {
        "second" => new CardChoice.Second(),
        "deny" => new CardChoice.Deny(choice.GetProperty("message").GetString()!),
        "answers" => new CardChoice.Answers([.. choice.GetProperty("picked").EnumerateArray().Select(Strings)]),
        _ => new CardChoice.Allow(),
    };

    /// A scenario of the conversation cases, step by step.
    private static (Conversation State, List<string> Written) Played(JsonElement scenario)
    {
        var state = Conversation.Empty();
        var written = new List<string>();

        // A command the host is asked to run shows among the lines as "shell: <command>", and one
        // it is asked to stop as "stop shell".
        void Take(Outcome outcome)
        {
            state = outcome.State;
            written.AddRange(outcome.Written);
            if (outcome.StopShell)
            {
                written.Add("stop shell");
            }

            if (outcome.Shell is { } run)
            {
                written.Add("shell: " + run.Command);
            }
        }

        foreach (var step in scenario.GetProperty("steps").EnumerateArray())
        {
            if (step.TryGetProperty("raw", out var raw))
            {
                Take(state.On(StreamEvent.FromLine(raw.GetString())));
            }
            else if (step.TryGetProperty("line", out var line))
            {
                Take(state.On(StreamEvent.FromLine(line.GetRawText())));
            }
            else
            {
                switch (step.GetProperty("do").GetString())
                {
                    case "send":
                        Take(state.Send(
                            step.GetProperty("text").GetString()!,
                            step.TryGetProperty("images", out var images)
                                ? [.. images.EnumerateArray().Select(image => new AttachedImage(
                                    image.GetProperty("mediaType").GetString()!,
                                    image.GetProperty("base64").GetString()!))]
                                : []));
                        break;
                    case "choose":
                        Take(state.Choose(step.GetProperty("requestId").GetString()!, ChoiceIn(step)));
                        break;
                    case "interrupt":
                        Take(state.Interrupt());
                        break;
                    case "setMode":
                        Take(state.SetMode(Mode(step.GetProperty("mode"))!.Value));
                        break;
                    case "markRead":
                        state = state.MarkRead();
                        break;
                    case "exit":
                        state = state.ProcessExited();
                        break;
                    case "shellEnded":
                        var running = state.Items.OfType<FeedItem.Shell>().Last(shell => shell.State == ShellState.Running);
                        Take(state.ShellEnded(running.Id, step.GetProperty("output").GetString()!, step.GetProperty("exitCode").GetInt32()));
                        break;
                    default:
                        throw new InvalidOperationException("unknown step");
                }
            }
        }

        return (state, written);
    }

    private static string Camel<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static JsonObject Described(StepLine line) => new()
    {
        ["kind"] = Camel(line.Kind),
        ["label"] = line.Label,
        ["subject"] = line.Subject,
        ["added"] = line.Added,
        ["removed"] = line.Removed,
        ["hidden"] = line.Hidden,
    };

    private static JsonObject Described(RequestCard card) => new()
    {
        ["kind"] = Camel(card.Kind),
        ["second"] = Camel(card.Second),
        ["rule"] = card.RuleText,
        ["step"] = Described(card.Step),
        ["questions"] = new JsonArray([.. card.Questions.Select(question => new JsonObject
        {
            ["text"] = question.Text,
            ["header"] = question.Header,
            ["multi"] = question.MultiSelect,
            ["options"] = new JsonArray([.. question.Options.Select(Described)]),
        })]),
        ["plan"] = card.Plan,
    };

    private static JsonObject Described(ToolResult result) => WithAnswers(new JsonObject
    {
        ["id"] = result.ToolUseId,
        ["error"] = result.IsError,
        ["text"] = result.Text,
        ["images"] = new JsonArray([.. result.Images.Select(image => new JsonObject
        {
            ["mediaType"] = image.MediaType,
            ["data"] = image.Data,
        })]),
    }, result.Answers);

    /// The answers to questions are named only where there are some, so older cases stay as they are.
    private static JsonObject WithAnswers(JsonObject described, IReadOnlyList<QuestionAnswer> answers)
    {
        if (answers.Count > 0)
        {
            described["answers"] = new JsonArray([.. answers.Select(answer => new JsonObject
            {
                ["header"] = answer.Header,
                ["answer"] = answer.Answer,
            })]);
        }

        return described;
    }

    private static JsonObject Described(QuestionOption option)
    {
        var described = new JsonObject { ["label"] = option.Label, ["description"] = option.Description };
        if (option.Preview.Length > 0)
        {
            described["preview"] = option.Preview;
        }

        return described;
    }

    /// The size of the context is named only where the line has one, so older cases stay as they are.
    private static JsonObject WithContextTokens(JsonObject described, int tokens)
    {
        if (tokens > 0)
        {
            described["contextTokens"] = tokens;
        }

        return described;
    }

    private static JsonObject WithContextWindows(JsonObject described, IReadOnlyDictionary<string, int> windows)
    {
        if (windows.Count > 0)
        {
            var named = new JsonObject();
            foreach (var (model, window) in windows)
            {
                named[model] = window;
            }
            described["contextWindows"] = named;
        }

        return described;
    }

    /// The to-do list and the context are named only where a conversation has them, so older
    /// cases stay as they are.
    private static JsonObject Described(Conversation state)
    {
        var described = DescribedCore(state);
        if (state.Todos.Count > 0)
        {
            described["todos"] = new JsonArray([.. state.Todos.Select(todo => new JsonObject
            {
                ["id"] = todo.Id,
                ["text"] = todo.Text,
                ["doing"] = todo.Doing,
                ["state"] = Camel(todo.State),
            })]);
        }
        if (state.ContextPercent is { } percent)
        {
            described["context"] = new JsonObject
            {
                ["tokens"] = state.ContextTokens,
                ["window"] = state.ContextWindow,
                ["percent"] = percent,
                ["level"] = Camel(ContextGauge.Level(percent)),
            };
        }

        return described;
    }

    private static JsonObject DescribedCore(Conversation state) => new()
    {
        ["activity"] = Camel(state.Activity),
        ["mode"] = PermissionModes.Name(state.Mode),
        ["model"] = state.Model,
        ["session"] = state.SessionId,
        ["tasks"] = state.Tasks,
        ["unread"] = state.Unread,
        ["compacting"] = state.Compacting,
        ["limits"] = state.Limits.Count,
        ["items"] = new JsonArray([.. state.Items.Select(Described)]),
    };

    private static JsonNode Described(FeedItem item) => item switch
    {
        FeedItem.OwnerMessage message => new JsonObject
        {
            ["owner"] = message.Text,
            ["images"] = message.Attachments.Count,
            ["queued"] = message.Queued,
        },
        FeedItem.Answer answer => new JsonObject { ["answer"] = answer.Text, ["streaming"] = answer.Streaming },
        FeedItem.Step step => WithDeclined(WithAnswers(new JsonObject
        {
            ["step"] = Described(step.Line),
            ["state"] = Camel(step.State),
            ["result"] = step.Result,
            ["images"] = step.Images.Count,
            ["progress"] = step.Progress,
        }, step.Answers), step.Declined),
        FeedItem.Card card => new JsonObject
        {
            ["card"] = Camel(card.Request.Kind),
            ["state"] = Camel(card.State),
            ["requestId"] = card.Request.RequestId,
            ["second"] = Camel(card.Request.Second),
        },
        FeedItem.Notice { Size: { } size } notice => new JsonObject
        {
            ["notice"] = Camel(notice.Kind),
            ["text"] = notice.Text,
            ["before"] = size.Before,
            ["after"] = size.After,
        },
        FeedItem.Notice notice => new JsonObject { ["notice"] = Camel(notice.Kind), ["text"] = notice.Text },
        FeedItem.Thought thought => new JsonObject { ["thought"] = thought.Text },
        FeedItem.Shell shell => new JsonObject
        {
            ["shell"] = shell.Command,
            ["state"] = Camel(shell.State),
            ["output"] = shell.Output,
            ["exitCode"] = shell.ExitCode,
        },
        _ => throw new InvalidOperationException("unknown item"),
    };

    [Fact]
    public void Every_conversation_is_run_the_same()
    {
        foreach (var row in SpecCases.Json("conversation-runner").GetProperty("scenarios").EnumerateArray())
        {
            var described = RunDescribed(row);

            foreach (var part in new[] { "starts", "written", "log", "activity", "items", "running" })
            {
                Assert.True(
                    JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty(part).GetRawText()), described[part]),
                    $"{row.GetProperty("name").GetString()}, {part}: {described[part]!.ToJsonString()}");
            }
        }
    }

    /// A scenario of the runner cases, played against a scripted Claude Code.
    private static JsonObject RunDescribed(JsonElement row)
    {
        var talk = TalkScript.Parse(row.GetRawText())!;
        var claude = new ScriptedClaude(talk.Replies);
        var log = new SpokenLog();
        var runner = new ConversationRunner(
            new RunnerSetup(
                "claude", "/work", "/accounts/.claude-work", "/home/someone", "Work", "id-1",
                row.GetProperty("started").GetBoolean(), Mode(row.GetProperty("mode"))!.Value, null, []),
            claude.NewProcess,
            action => action(),
            log);

        talk.Play(runner);

        return new JsonObject
        {
            ["starts"] = new JsonArray([.. claude.Starts.Select(start => (JsonNode)new JsonObject
            {
                ["resumed"] = start.Arguments.Contains("--resume"),
                ["mode"] = start.Arguments[start.Arguments.ToList().IndexOf("--permission-mode") + 1],
            })]),
            ["written"] = new JsonArray([.. claude.Written.Select(line => (JsonNode)line)]),
            ["log"] = new JsonArray([.. log.Lines.Select(line => (JsonNode)line)]),
            ["activity"] = Camel(runner.State.Activity),
            ["items"] = runner.State.Items.Count,
            ["running"] = runner.IsRunning,
        };
    }

    /// The log of the terminal as words, so a case file can list what was said.
    private sealed class SpokenLog : ITerminalLog
    {
        public List<string> Lines { get; } = [];

        public void Started(string environment, bool resumed) =>
            Lines.Add($"started {environment} {(resumed ? "resumed" : "new")}");

        public void StartFailed(string environment) => Lines.Add($"start failed {environment}");

        public void Exited(string environment, int exitCode, int linesRead, int linesWritten) =>
            Lines.Add($"exited {environment} code {exitCode} read {linesRead} written {linesWritten}");

        public void Stopped(string environment, int linesRead, int linesWritten) =>
            Lines.Add($"stopped {environment} read {linesRead} written {linesWritten}");
    }

    [Fact]
    public void Every_session_gets_the_same_name()
    {
        var cases = SpecCases.Json("session-name");

        foreach (var row in cases.GetProperty("defaults").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("name").GetString(),
                SessionName.Default(row.GetProperty("folder").GetString()!, Strings(row.GetProperty("taken"))));
        }

        foreach (var row in cases.GetProperty("names").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("name").GetString(), SessionName.Clean(row.GetProperty("input").GetString()));
        }

        foreach (var row in cases.GetProperty("folders").EnumerateArray())
        {
            var input = row.GetProperty("input").GetString()!;
            Assert.Equal(row.GetProperty("path").GetString(), SessionName.FolderPath(input));
            Assert.Equal(row.GetProperty("name").GetString(), SessionName.FolderName(input));
            Assert.Equal(row.GetProperty("parent").GetString(), SessionName.ParentName(input));
        }

        foreach (var row in cases.GetProperty("fromMessages").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("name").GetString(),
                SessionName.FromMessage(row.GetProperty("message").GetString()!, row.GetProperty("fallback").GetString()!));
        }
    }

    [Fact]
    public void Every_conversation_file_is_found_in_the_same_place()
    {
        foreach (var row in SpecCases.Json("transcript-path").GetProperty("paths").EnumerateArray())
        {
            var folder = row.GetProperty("folder").GetString()!;

            Assert.Equal(row.GetProperty("folderName").GetString(), TranscriptPath.FolderName(folder));
            Assert.Equal(row.GetProperty("cut").GetBoolean(), TranscriptPath.IsCut(folder));
            Assert.Equal(
                row.GetProperty("path").GetString(),
                TranscriptPath.For(PlatformIn(row), row.GetProperty("config").GetString()!, folder, row.GetProperty("id").GetString()!));
        }
    }

    [Fact]
    public void Every_list_of_sessions_changes_the_same()
    {
        foreach (var row in SpecCases.Json("terminal-sessions").GetProperty("scenarios").EnumerateArray())
        {
            var described = SessionsDescribed(row);

            foreach (var part in new[] { "saved", "groups", "toOpen" })
            {
                Assert.True(
                    JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty(part).GetRawText()), described[part]),
                    $"{row.GetProperty("name").GetString()}, {part}: {described[part]?.ToJsonString()}");
            }
        }
    }

    [Fact]
    public void Every_conversation_file_is_read_into_the_same_feed()
    {
        foreach (var row in SpecCases.Json("transcript").GetProperty("transcripts").EnumerateArray())
        {
            var described = TranscriptDescribed(row);

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("state").GetRawText()), described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_form_of_a_new_session_offers_the_same()
    {
        var cases = SpecCases.Json("new-session");
        var settings = new EnvironmentSettings([.. cases.GetProperty("environments").EnumerateArray().Select(EnvironmentIn)]);

        foreach (var row in cases.GetProperty("forms").EnumerateArray())
        {
            var described = FormDescribed(row, settings);

            foreach (var part in new[] { "folders", "names", "elsewhere", "model", "headers" })
            {
                Assert.True(
                    JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty(part).GetRawText()), described[part]),
                    $"{row.GetProperty("name").GetString()}, {part}: {described[part]?.ToJsonString()}");
            }
        }
    }

    /// The file of a case: an object or a list as it is, a text as a broken file, null as no file.
    private static string? FileIn(JsonElement file) => file.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => file.GetString(),
        _ => file.GetRawText(),
    };

    private static JsonObject SessionsDescribed(JsonElement row)
    {
        var sessions = TerminalSessions.FromJson(FileIn(row.GetProperty("file")));

        foreach (var step in row.GetProperty("steps").EnumerateArray())
        {
            string Word(string key) => step.GetProperty(key).GetString()!;

            sessions = step.GetProperty("do").GetString() switch
            {
                "add" => sessions.Add(
                    Word("id"), Word("environment"), Word("folder"), Word("name"),
                    step.GetProperty("model").GetString(), Mode(step.GetProperty("mode"))!.Value, step.GetProperty("now").GetInt64()),
                "rename" => sessions.Rename(Word("id"), Word("name")),
                "renameEnvironment" => sessions.RenameEnvironment(Word("from"), Word("to")),
                "setMode" => sessions.SetMode(Word("id"), Mode(step.GetProperty("mode"))!.Value),
                "setModel" => sessions.SetModel(Word("id"), step.GetProperty("model").GetString()),
                "started" => sessions.MarkStarted(Word("id")),
                "used" => sessions.Used(Word("id"), step.GetProperty("now").GetInt64()),
                "opened" => sessions.Opened(Word("id")),
                "close" => sessions.Close(Word("id")),
                "placed" => sessions.Placed(new WindowPlace(
                    step.GetProperty("left").GetDouble(), step.GetProperty("top").GetDouble(),
                    step.GetProperty("width").GetDouble(), step.GetProperty("height").GetDouble(),
                    step.GetProperty("maximized").GetBoolean())),
                var other => throw new InvalidOperationException($"unknown step {other}"),
            };
        }

        var environment = row.GetProperty("environment").GetString()!;
        return new JsonObject
        {
            ["saved"] = JsonNode.Parse(sessions.ToJson()),
            ["groups"] = new JsonArray([.. sessions.Groups(environment).Select(group => (JsonNode)new JsonObject
            {
                ["folder"] = group.Folder,
                ["name"] = group.Name,
                ["sessions"] = new JsonArray([.. group.Sessions.Select(session => (JsonNode)session.Name)]),
            })]),
            ["toOpen"] = sessions.ToOpen(environment)?.Id,
        };
    }

    private static JsonObject TranscriptDescribed(JsonElement row)
    {
        var lines = row.GetProperty("lines").EnumerateArray()
            .Select(line => line.ValueKind == JsonValueKind.String ? line.GetString()! : line.GetRawText());
        var state = Transcript.Read(lines, PermissionMode.Plan);

        return new JsonObject
        {
            ["activity"] = Camel(state.Activity),
            ["mode"] = PermissionModes.Name(state.Mode),
            ["busy"] = state.IsBusy,
            ["nextId"] = state.NextId,
            ["items"] = new JsonArray([.. state.Items.Select(Described)]),
        };
    }

    private static JsonObject FormDescribed(JsonElement row, EnvironmentSettings settings)
    {
        var sessions = TerminalSessions.FromJson(FileIn(row.GetProperty("sessions")));
        var environment = settings.Environments.First(one => one.Name == row.GetProperty("environment").GetString());
        var inside = row.GetProperty("inside");

        var folders = NewSessionChoices.Folders(
            sessions,
            environment,
            bound => inside.TryGetProperty(bound, out var list) ? Strings(list) : []);

        return new JsonObject
        {
            ["folders"] = new JsonArray([.. folders.Select(choice => (JsonNode)new JsonObject
            {
                ["path"] = choice.Path,
                ["name"] = choice.Name,
                ["source"] = Camel(choice.Source),
            })]),
            ["names"] = new JsonArray([.. Strings(row.GetProperty("nameFor"))
                .Select(folder => (JsonNode)NewSessionChoices.DefaultName(sessions, environment.Name, folder))]),
            ["elsewhere"] = new JsonArray([.. Strings(row.GetProperty("elsewhereFor"))
                .Select(folder => (JsonNode?)NewSessionChoices.BoundElsewhere(folder, environment, settings))]),
            ["model"] = NewSessionChoices.DefaultModel(sessions, environment.Name),
            ["headers"] = new JsonArray([.. NewSessionChoices.Headers(folders).Select(header => (JsonNode)new JsonObject
            {
                ["before"] = header.Before,
                ["group"] = Camel(header.Group),
            })]),
        };
    }

    [Fact]
    public void Every_answer_is_cut_into_the_same_pieces()
    {
        foreach (var row in SpecCases.Json("answer-segments").GetProperty("answers").EnumerateArray())
        {
            var segments = AnswerSegments.Of(Markdown.Parse(row.GetProperty("markdown").GetString()));

            var described = new JsonArray([.. segments.Select(segment => (JsonNode)(segment switch
            {
                AnswerSegment.Prose prose => new JsonObject { ["kind"] = "prose", ["blocks"] = prose.Blocks.Count },
                AnswerSegment.Code code => new JsonObject { ["kind"] = "code", ["language"] = code.Language },
                AnswerSegment.Table table => new JsonObject { ["kind"] = "table", ["rows"] = table.Value.Rows.Count },
                _ => throw new InvalidOperationException("unknown segment"),
            }))]);

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("segments").GetRawText()), described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_selection_copies_the_same_markdown()
    {
        foreach (var row in SpecCases.Json("markdown-copy").GetProperty("selections").EnumerateArray())
        {
            static TextPlace Place(JsonElement place) => new(place[0].GetInt32(), place[1].GetInt32());

            var copied = MarkdownCopy.Selected(
                Markdown.Parse(row.GetProperty("markdown").GetString()),
                Place(row.GetProperty("from")),
                Place(row.GetProperty("to")));

            Assert.True(
                copied == row.GetProperty("copied").GetString(),
                $"{row.GetProperty("name").GetString()}: {copied}");
        }
    }

    [Fact]
    public void Leaves_are_counted_in_the_order_they_are_drawn()
    {
        var blocks = Markdown.Parse("Plain **bold** and `code`.\n\n- item\n\n  ```sh\n  ls\n  ```");
        var paragraph = (MarkdownBlock.Paragraph)blocks[0];
        var list = (MarkdownBlock.List)blocks[1];

        var leaves = MarkdownCopy.Leaves(blocks);

        Assert.Equal(1, leaves[((MarkdownInline.Strong)paragraph.Inlines[1]).Inlines[0]]);
        Assert.Equal(3, leaves[paragraph.Inlines[3]]);
        Assert.Equal(6, leaves[list.Items[0][1]]);
        Assert.Equal(7, leaves.Count);
    }

    [Fact]
    public void Every_file_link_is_found_the_same()
    {
        var cases = SpecCases.Json("file-link");

        foreach (var row in cases.GetProperty("files").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString();
            var line = row.GetProperty("line");
            var expected = path is null
                ? null
                : new FileLink(path, line.ValueKind == JsonValueKind.Null ? null : line.GetInt32());

            Assert.Equal(expected, FileLink.Parse(row.GetProperty("text").GetString()));
        }

        foreach (var row in cases.GetProperty("targets").EnumerateArray())
        {
            LinkTarget expected = row.GetProperty("kind").GetString() switch
            {
                "web" => new LinkTarget.Web(row.GetProperty("url").GetString()!),
                "file" => new LinkTarget.File(new FileLink(
                    row.GetProperty("path").GetString()!,
                    row.GetProperty("line").ValueKind == JsonValueKind.Null ? null : row.GetProperty("line").GetInt32())),
                _ => new LinkTarget.None(),
            };

            Assert.Equal(expected, LinkTarget.Of(row.GetProperty("href").GetString()));
        }
    }

    /// Markdown blocks as the case files write them down.
    private static JsonArray Described(IReadOnlyList<MarkdownBlock> blocks) => new([.. blocks.Select(Described)]);

    private static JsonNode Described(MarkdownBlock block) => block switch
    {
        MarkdownBlock.Paragraph paragraph => new JsonObject { ["type"] = "paragraph", ["inlines"] = Described(paragraph.Inlines) },
        MarkdownBlock.Heading heading => new JsonObject
        {
            ["type"] = "heading",
            ["level"] = heading.Level,
            ["inlines"] = Described(heading.Inlines),
        },
        MarkdownBlock.Code code => new JsonObject { ["type"] = "code", ["language"] = code.Language, ["text"] = code.Text },
        MarkdownBlock.List list => new JsonObject
        {
            ["type"] = "list",
            ["ordered"] = list.Ordered,
            ["start"] = list.Start,
            ["items"] = new JsonArray([.. list.Items.Select(Described)]),
        },
        MarkdownBlock.Quote quote => new JsonObject { ["type"] = "quote", ["blocks"] = Described(quote.Blocks) },
        MarkdownBlock.Table table => new JsonObject
        {
            ["type"] = "table",
            ["columns"] = new JsonArray([.. table.Columns.Select(column => (JsonNode)column.ToString().ToLowerInvariant())]),
            ["header"] = new JsonArray([.. table.Header.Select(Described)]),
            ["rows"] = new JsonArray([.. table.Rows.Select(row => new JsonArray([.. row.Select(Described)]))]),
        },
        _ => new JsonObject { ["type"] = "rule" },
    };

    private static JsonArray Described(IReadOnlyList<MarkdownInline> inlines) => new([.. inlines.Select(Described)]);

    private static JsonNode Described(MarkdownInline inline) => inline switch
    {
        MarkdownInline.Text text => new JsonObject { ["text"] = text.Value },
        MarkdownInline.Code code => new JsonObject { ["code"] = code.Value },
        MarkdownInline.Strong strong => new JsonObject { ["strong"] = Described(strong.Inlines) },
        MarkdownInline.Emphasis emphasis => new JsonObject { ["emphasis"] = Described(emphasis.Inlines) },
        MarkdownInline.Strike strike => new JsonObject { ["strike"] = Described(strike.Inlines) },
        MarkdownInline.Link link => new JsonObject { ["link"] = link.Target, ["inlines"] = Described(link.Inlines) },
        _ => throw new InvalidOperationException("unknown inline"),
    };

    private static PermissionMode? Mode(JsonElement name) =>
        name.ValueKind == JsonValueKind.Null ? null : Enum.Parse<PermissionMode>(name.GetString()!, ignoreCase: true);

    /// A stream event as the case files write it down.
    private static JsonObject Described(StreamEvent streamEvent) => streamEvent switch
    {
        StreamEvent.Ready ready => new JsonObject
        {
            ["kind"] = "ready",
            ["sessionId"] = ready.SessionId,
            ["model"] = ready.Model,
            ["mode"] = ModeName(ready.Mode),
        },
        StreamEvent.Assistant assistant => WithContextTokens(new JsonObject
        {
            ["kind"] = "assistant",
            ["parent"] = assistant.ParentToolUseId,
            ["error"] = assistant.Error,
            ["blocks"] = new JsonArray([.. assistant.Blocks.Select(Described)]),
        }, assistant.ContextTokens),
        StreamEvent.User user => new JsonObject
        {
            ["kind"] = "user",
            ["parent"] = user.ParentToolUseId,
            ["text"] = user.Text,
            ["replay"] = user.IsReplay,
            ["synthetic"] = user.IsSynthetic,
            ["results"] = new JsonArray([.. user.Results.Select(Described)]),
        },
        StreamEvent.TextDelta delta => new JsonObject
        {
            ["kind"] = "textDelta",
            ["parent"] = delta.ParentToolUseId,
            ["text"] = delta.Text,
        },
        StreamEvent.Request request => new JsonObject
        {
            ["kind"] = "request",
            ["requestId"] = request.RequestId,
            ["tool"] = request.ToolName,
            ["toolUseId"] = request.ToolUseId,
            ["input"] = JsonNode.Parse(request.Input.GetRawText()),
            ["description"] = request.Description,
            ["suggestsAcceptEdits"] = request.SuggestsAcceptEdits,
            ["needsAnswer"] = request.NeedsAnswer,
            ["agent"] = request.AgentId,
            ["rule"] = request.RuleText,
        },
        StreamEvent.Commands commands => new JsonObject
        {
            ["kind"] = "commands",
            ["requestId"] = commands.RequestId,
            ["names"] = new JsonArray([.. commands.List.Select(command => (JsonNode)command.Name)]),
            ["first"] = commands.List.Count == 0 ? null : new JsonObject
            {
                ["description"] = commands.List[0].Description,
                ["argumentHint"] = commands.List[0].ArgumentHint,
                ["aliases"] = new JsonArray([.. commands.List[0].Aliases.Select(alias => (JsonNode)alias)]),
                ["builtin"] = commands.List[0].Builtin,
            },
            ["models"] = new JsonArray([.. commands.Models.Select(model => (JsonNode)$"{model.Value}={model.DisplayName}")]),
        },
        StreamEvent.Reply reply => new JsonObject
        {
            ["kind"] = "reply",
            ["requestId"] = reply.RequestId,
            ["ok"] = reply.Ok,
            ["mode"] = ModeName(reply.Mode),
        },
        StreamEvent.Status status => new JsonObject
        {
            ["kind"] = "status",
            ["activity"] = status.Activity,
            ["mode"] = ModeName(status.Mode),
        },
        StreamEvent.Compacted compacted => new JsonObject { ["kind"] = "compacted", ["before"] = compacted.Before, ["after"] = compacted.After },
        StreamEvent.Tasks tasks => new JsonObject { ["kind"] = "tasks", ["count"] = tasks.Count },
        StreamEvent.TaskProgress progress => new JsonObject
        {
            ["kind"] = "taskProgress",
            ["toolUseId"] = progress.ToolUseId,
            ["description"] = progress.Description,
        },
        StreamEvent.TaskStarted started => new JsonObject { ["kind"] = "taskStarted", ["toolUseId"] = started.ToolUseId },
        StreamEvent.TaskEnded ended => new JsonObject { ["kind"] = "taskEnded", ["toolUseId"] = ended.ToolUseId, ["how"] = ended.How },
        StreamEvent.RateLimit limit => new JsonObject
        {
            ["kind"] = "rateLimit",
            ["windows"] = new JsonArray([.. limit.Windows.Select(window => new JsonObject
            {
                ["kind"] = window.Kind == LimitKind.FiveHour ? "fiveHour" : "sevenDay",
                ["percent"] = window.Percent,
                ["resetsAt"] = window.ResetsAt.ToUnixTimeSeconds(),
            })]),
        },
        StreamEvent.Result result => WithContextWindows(new JsonObject
        {
            ["kind"] = "result",
            ["isError"] = result.IsError,
            ["subtype"] = result.Subtype,
            ["terminalReason"] = result.TerminalReason,
            ["text"] = result.Text,
        }, result.ContextWindows),
        _ => new JsonObject { ["kind"] = "ignored" },
    };

    private static JsonNode Described(AssistantBlock block) => block switch
    {
        AssistantBlock.Text text => new JsonObject { ["type"] = "text", ["text"] = text.Value },
        AssistantBlock.ToolUse tool => new JsonObject
        {
            ["type"] = "toolUse",
            ["id"] = tool.Id,
            ["name"] = tool.Name,
            ["input"] = JsonNode.Parse(tool.Input.GetRawText()),
        },
        AssistantBlock.Thinking { Words.Length: > 0 } thinking => new JsonObject { ["type"] = "thinking", ["text"] = thinking.Words },
        _ => new JsonObject { ["type"] = "thinking" },
    };

    private static string? ModeName(PermissionMode? mode) =>
        mode is { } known ? PermissionModes.Name(known) : null;

    private static string Repeated(JsonElement row, string name) =>
        string.Concat(Enumerable.Repeat(row.GetProperty(name).GetString()!, row.GetProperty(name + "Times").GetInt32()));

    [Fact]
    public void Every_conversation_looks_the_same_in_the_list()
    {
        foreach (var row in SpecCases.Json("conversation").GetProperty("scenarios").EnumerateArray())
        {
            var described = GlanceDescribed(TalkGlance.Of(Played(row).State));

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("glance").GetRawText()), described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    [Fact]
    public void Every_list_of_the_terminal_window_has_the_same_rows()
    {
        var cases = SpecCases.Json("terminal-window");
        var sessions = TerminalSessions.FromJson(cases.GetProperty("sessions").GetRawText());

        foreach (var row in cases.GetProperty("rows").EnumerateArray())
        {
            var described = RowsDescribed(row, sessions);

            foreach (var part in new[] { "groups", "waiting" })
            {
                Assert.True(
                    JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty(part).GetRawText()), described[part]),
                    $"{row.GetProperty("name").GetString()}, {part}: {described[part]?.ToJsonString()}");
            }
        }
    }

    [Fact]
    public void Every_fifth_process_stops_the_same_session()
    {
        foreach (var row in SpecCases.Json("terminal-window").GetProperty("caps").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("stop").GetString(), ProcessCap.ToStop(RunningIn(row), row.GetProperty("starting").GetString()!));
        }
    }

    [Fact]
    public void Every_terminal_window_opens_at_the_same_place()
    {
        var cases = SpecCases.Json("terminal-window");

        foreach (var row in cases.GetProperty("places").EnumerateArray())
        {
            var described = PlaceDescribed(row);

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("place").GetRawText()), described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }

        foreach (var row in cases.GetProperty("narrow").EnumerateArray())
        {
            Assert.Equal(row[1].GetBoolean(), WindowPlace.IsNarrow(row[0].GetDouble()));
        }

        foreach (var row in cases.GetProperty("days").EnumerateArray())
        {
            Assert.Equal(row[3].GetString(), Camel(SessionRows.DayOf(row[0].GetInt64(), row[1].GetInt64(), row[2].GetInt32())));
        }
    }

    [Fact]
    public void Every_terminal_window_chooses_the_same()
    {
        var cases = SpecCases.Json("terminal-window");

        foreach (var row in cases.GetProperty("windows").EnumerateArray())
        {
            var described = WindowDescribed(row, TerminalSessions.FromJson(cases.GetProperty("sessions").GetRawText()));

            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("steps").GetRawText()), described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    private static JsonObject GlanceDescribed(TalkGlance glance) => new()
    {
        ["activity"] = Camel(glance.Activity),
        ["line"] = Camel(glance.Line),
        ["kind"] = Camel(glance.Kind),
        ["label"] = glance.Label,
        ["text"] = glance.Text,
    };

    private static List<RunningTalk> RunningIn(JsonElement row) =>
        [.. row.GetProperty("running").EnumerateArray()
            .Select(talk => new RunningTalk(talk[0].GetString()!, talk[1].GetBoolean(), talk[2].GetInt64()))];

    private static JsonArray PlaceDescribed(JsonElement row)
    {
        var saved = row.GetProperty("saved");
        var place = WindowPlace.Fit(
            saved.ValueKind == JsonValueKind.Null
                ? null
                : new WindowPlace(saved[0].GetDouble(), saved[1].GetDouble(), saved[2].GetDouble(), saved[3].GetDouble(), saved[4].GetBoolean()),
            [.. row.GetProperty("screens").EnumerateArray().Select(screen =>
                new ScreenArea(screen[0].GetDouble(), screen[1].GetDouble(), screen[2].GetDouble(), screen[3].GetDouble()))]);

        return new JsonArray(place.Left, place.Top, place.Width, place.Height, place.Maximized);
    }

    private static JsonObject RowsDescribed(JsonElement row, TerminalSessions sessions)
    {
        var environment = row.GetProperty("environment").GetString()!;
        var live = row.GetProperty("live");

        LiveTalk? Live(string id) => live.TryGetProperty(id, out var talk)
            ? new LiveTalk(
                new TalkGlance(
                    Enum.Parse<TalkActivity>(talk.GetProperty("activity").GetString()!, ignoreCase: true),
                    Enum.Parse<GlanceLine>(talk.GetProperty("line").GetString()!, ignoreCase: true),
                    Enum.Parse<StepKind>(talk.GetProperty("kind").GetString()!, ignoreCase: true),
                    talk.GetProperty("label").GetString()!,
                    talk.GetProperty("text").GetString()!),
                talk.GetProperty("running").GetBoolean())
            : null;

        return new JsonObject
        {
            ["groups"] = new JsonArray([.. SessionRows.For(sessions, environment, Live).Select(group => (JsonNode)new JsonObject
            {
                ["name"] = group.Name,
                ["rows"] = new JsonArray([.. group.Rows.Select(one => (JsonNode)new JsonObject
                {
                    ["id"] = one.Id,
                    ["state"] = Camel(one.State),
                    ["line"] = one.Glance is { } glance ? Camel(glance.Line) : null,
                    ["text"] = one.Glance?.Text,
                    ["lastUsed"] = one.LastUsed,
                })]),
            })]),
            ["waiting"] = SessionRows.Waiting(sessions, environment, Live),
        };
    }

    private static JsonArray WindowDescribed(JsonElement row, TerminalSessions sessions)
    {
        var window = TerminalWindowModel.Opened(sessions, Strings(row.GetProperty("environments")));
        var steps = new JsonArray();

        void Note() => steps.Add(new JsonObject
        {
            ["environment"] = window.Environment,
            ["session"] = window.Session(sessions)?.Id,
            ["view"] = Camel(window.View(sessions)),
        });

        Note();
        foreach (var action in row.GetProperty("actions").EnumerateArray())
        {
            var what = action[1].GetString()!;
            switch (action[0].GetString())
            {
                case "environment":
                    window = window.ToEnvironment(what, sessions);
                    break;
                case "session":
                    window = window.ToSession(what, sessions);
                    break;
                case "form":
                    window = window.WithForm(what == "open");
                    break;
                default:
                    sessions = sessions.Close(what);
                    window = window.After(sessions);
                    break;
            }

            Note();
        }

        return steps;
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
