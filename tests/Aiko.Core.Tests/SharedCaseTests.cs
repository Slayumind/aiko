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
                ["terminalEventsFolder"] = folders.TerminalEventsFolder,
                ["terminalHooksFile"] = folders.TerminalHooksFile,
                ["picturesFolder"] = folders.PicturesFolder(snapshotName),
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
                row.TryGetProperty("hooksFile", out var hooks) ? hooks.GetString()! : "",
                row.TryGetProperty("picturesFolder", out var pictures) ? pictures.GetString()! : "");

            var plan = ClaudeLaunchPlan.For(request, Strings(row.GetProperty("inherited")));

            Assert.Equal(Strings(row.GetProperty("arguments")), plan.Arguments);
            Assert.Equal(
                [.. row.GetProperty("variables").EnumerateArray().Select(variable => new VariableChange(
                    variable.GetProperty("name").GetString()!,
                    variable.GetProperty("value").GetString()))],
                plan.Variables);
        }

        foreach (var row in cases.GetProperty("shells").EnumerateArray())
        {
            var plan = ClaudeLaunchPlan.ForShell(
                row.GetProperty("configDirectory").GetString()!, home, Strings(row.GetProperty("inherited")));

            Assert.Empty(plan.Arguments);
            Assert.Equal(
                [.. row.GetProperty("variables").EnumerateArray().Select(variable => new VariableChange(
                    variable.GetProperty("name").GetString()!,
                    variable.GetProperty("value").GetString()))],
                plan.Variables);
        }
    }

    [Fact]
    public void Every_title_of_a_terminal_is_read_the_same()
    {
        var cases = SpecCases.Json("terminal-title");

        foreach (var row in cases.GetProperty("reads").EnumerateArray())
        {
            var reader = new TitleReader();
            var titles = row.GetProperty("pieces").EnumerateArray().Select(piece => reader.Read(piece.GetString()!)).ToList();
            Assert.True(
                titles.SequenceEqual(row.GetProperty("titles").EnumerateArray().Select(title => title.GetString())),
                row.GetProperty("name").GetString());
        }

        foreach (var row in cases.GetProperty("long").GetProperty("rows").EnumerateArray())
        {
            var length = row.GetProperty("length").GetInt32();
            var title = new TitleReader().Read("\u001b]0;" + new string('x', length) + "\u0007");
            Assert.Equal(row.GetProperty("title").GetBoolean(), title is not null);
        }

        foreach (var row in cases.GetProperty("screen").EnumerateArray())
        {
            Assert.True(
                row.GetProperty("text").GetBoolean() == TerminalScreen.HasText(row.GetProperty("output").GetString()!),
                row.GetProperty("name").GetString());
        }

        foreach (var row in cases.GetProperty("claude").EnumerateArray())
        {
            var parsed = ClaudeTitle.Parse(row.GetProperty("title").GetString());
            var working = row.GetProperty("working");
            Assert.True(
                working.ValueKind == JsonValueKind.Null ? parsed is null : parsed?.Working == working.GetBoolean(),
                row.GetProperty("title").GetString());
            Assert.Equal(row.GetProperty("name").GetString(), parsed?.SessionName);
        }
    }

    [Fact]
    public void Every_hook_of_the_terminal_says_the_same()
    {
        var cases = SpecCases.Json("terminal-events");
        var now = cases.GetProperty("now").GetInt64();

        foreach (var row in cases.GetProperty("hooks").EnumerateArray())
        {
            var input = row.GetProperty("input");
            var found = TerminalEvent.FromHook(input.ValueKind == JsonValueKind.String ? input.GetString() : input.GetRawText(), now);
            var expected = row.GetProperty("event");
            var name = row.GetProperty("name").GetString();

            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.True(found is null, name);
                continue;
            }

            Assert.True(
                found == new TerminalEvent(KindIn(expected), expected.GetProperty("detail").GetString()!, now),
                $"{name}: {found}");
        }

        foreach (var row in cases.GetProperty("files").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("file").GetString(), TerminalEvent.FileName(row.GetProperty("id").GetString()));
        }

        foreach (var row in cases.GetProperty("saved").EnumerateArray())
        {
            var read = TerminalEvent.FromJson(FileIn(row.GetProperty("file")));
            var expected = row.GetProperty("event");
            Assert.Equal(
                expected.ValueKind == JsonValueKind.Null
                    ? null
                    : new TerminalEvent(KindIn(expected), expected.GetProperty("detail").GetString()!, expected.GetProperty("at").GetInt64()),
                read);

            if (read is not null)
            {
                Assert.Equal(read, TerminalEvent.FromJson(read.ToJson()));
            }
        }

        foreach (var row in cases.GetProperty("typed").EnumerateArray())
        {
            Assert.True(
                row.GetProperty("typed").GetBoolean() == TerminalPulse.IsTyped(row.GetProperty("input").GetString()!),
                row.GetProperty("name").GetString());
        }

        foreach (var row in cases.GetProperty("replaces").EnumerateArray())
        {
            var written = row.GetProperty("written");
            var next = new TerminalEvent(Enum.Parse<TerminalEventKind>(row.GetProperty("next").GetString()!, ignoreCase: true), "", 2);
            Assert.True(
                row.GetProperty("replaces").GetBoolean() == next.Replaces(written.ValueKind == JsonValueKind.Null
                    ? null
                    : new TerminalEvent(Enum.Parse<TerminalEventKind>(written.GetString()!, ignoreCase: true), "", 1)),
                $"{written} then {next.Kind}");
        }

        foreach (var row in cases.GetProperty("pulse").EnumerateArray())
        {
            var title = row.GetProperty("title");
            var happened = row.GetProperty("event");
            var activity = TerminalPulse.Of(
                title.ValueKind == JsonValueKind.Null ? null : new ClaudeTitle(title.GetString() == "working", "name"),
                happened.ValueKind == JsonValueKind.Null ? null : new TerminalEvent(KindIn(happened), "", happened.GetProperty("at").GetInt64()),
                row.GetProperty("seenAt").GetInt64(),
                row.GetProperty("answeredAt").GetInt64());

            Assert.True(row.GetProperty("activity").GetString() == Camel(activity), row.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Every_list_matches_and_picks_folders_the_same()
    {
        var cases = SpecCases.Json("text-match");

        static JsonNode? Described(IReadOnlyList<TextPart>? parts) =>
            parts is null ? null : new JsonArray([.. parts.Select(part => (JsonNode)new JsonArray(part.Text, part.Match))]);

        foreach (var row in cases.GetProperty("parts").EnumerateArray())
        {
            var found = Described(TextMatch.Parts(row.GetProperty("text").GetString()!, row.GetProperty("query").GetString()!));
            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("parts").GetRawText()), found),
                $"{row.GetProperty("text").GetString()}: {found?.ToJsonString()}");
        }

        var sessions = TerminalSessions.FromJson(cases.GetProperty("sessions").GetRawText());
        foreach (var row in cases.GetProperty("folders").EnumerateArray())
        {
            var others = row.GetProperty("others").EnumerateArray().Select(other => (other[0].GetString()!, other[1].GetInt64()));
            var hits = FolderPick.Find(sessions, row.GetProperty("environment").GetString()!, row.GetProperty("query").GetString()!, others);
            var described = new JsonArray([.. hits.Select(hit => (JsonNode)new JsonArray(hit.Folder, Described(hit.Name), hit.Parent))]);
            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("hits").GetRawText()), described),
                $"{row.GetProperty("name").GetString()}: {described.ToJsonString()}");
        }
    }

    private static Conversation ConversationIn(JsonElement row) => new(
        row.GetProperty("id").GetString()!,
        row.GetProperty("environment").GetString()!,
        row.GetProperty("folder").GetString()!,
        row.GetProperty("name").GetString()!,
        row.GetProperty("firstMessage").GetString()!,
        row.GetProperty("lastUsed").GetInt64(),
        Enum.Parse<ConversationSource>(row.GetProperty("source").GetString()!, ignoreCase: true));

    [Fact]
    public void Every_conversation_file_is_read_the_same()
    {
        foreach (var row in SpecCases.Json("conversation-file").GetProperty("cases").EnumerateArray())
        {
            var read = ConversationFile.Read("c1", "Aiko", row.GetProperty("head").GetString()!, row.GetProperty("tail").GetString()!, 1000);
            var expected = row.GetProperty("conversation");
            Assert.True(
                expected.ValueKind == JsonValueKind.Null
                    ? read is null
                    : read == new Conversation(
                        "c1", "Aiko", expected.GetProperty("folder").GetString()!, expected.GetProperty("name").GetString()!,
                        expected.GetProperty("firstMessage").GetString()!, 1000,
                        Enum.Parse<ConversationSource>(expected.GetProperty("source").GetString()!, ignoreCase: true)),
                $"{row.GetProperty("name").GetString()}: {read}");
        }
    }

    [Fact]
    public void Every_palette_of_sessions_finds_the_same()
    {
        var cases = SpecCases.Json("session-search");
        var sessions = TerminalSessions.FromJson(cases.GetProperty("file").GetRawText());
        var conversations = cases.GetProperty("conversations").EnumerateArray().Select(ConversationIn).ToList();

        static string Marked(IReadOnlyList<TextPart> parts) =>
            string.Concat(parts.Select(part => part.Match ? $"[{part.Text}]" : part.Text));

        static JsonArray Described(IReadOnlyList<SearchHit> hits) => new([.. hits.Select(hit =>
        {
            var row = new JsonObject
            {
                ["id"] = hit.Id,
                ["environment"] = hit.Environment,
                ["folder"] = hit.Folder,
                ["name"] = Marked(hit.Name),
            };
            if (hit.Snippet.Count > 0)
            {
                row["snippet"] = Marked(hit.Snippet);
            }

            if (hit.Source is { } source)
            {
                row["source"] = Camel(source);
            }

            return (JsonNode)row;
        })]);

        foreach (var row in cases.GetProperty("queries").EnumerateArray())
        {
            var found = SessionSearch.Find(sessions, conversations, row.GetProperty("query").GetString()!);
            foreach (var (part, hits) in new[] { ("open", found.Open), ("closed", found.Closed), ("outside", found.Outside) })
            {
                var described = Described(hits);
                Assert.True(
                    JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty(part).GetRawText()), described),
                    $"{row.GetProperty("name").GetString()}, {part}: {described.ToJsonString()}");
            }
        }
    }

    [Fact]
    public void Every_editor_does_the_same()
    {
        var cases = SpecCases.Json("editor");

        foreach (var row in cases.GetProperty("keys").EnumerateArray())
        {
            var state = row.GetProperty("state");
            var press = EditorKeys.For(
                new EditorState(
                    state[0].GetString() == "shell" ? SessionKind.Shell : SessionKind.Claude,
                    state[1].GetBoolean(), state[2].GetBoolean(), state[3].GetBoolean(), state[4].GetBoolean(),
                    state.GetArrayLength() > 5 && state[5].GetBoolean()),
                Enum.Parse<EditorKey>(row.GetProperty("key").GetString()!, ignoreCase: true),
                row.GetProperty("shift").GetBoolean(),
                row.GetProperty("char").GetString() is { Length: > 0 } typed ? typed[0] : '\0');
            var expected = row.GetProperty("press");
            Assert.True(
                Camel(press.Action) == expected[0].GetString() && press.Sequence == expected[1].GetString() && press.Passing == expected[2].GetBoolean(),
                $"{state.GetRawText()} {row.GetProperty("key").GetString()} {row.GetProperty("char").GetString()}: {press}");
        }

        foreach (var row in cases.GetProperty("sends").EnumerateArray())
        {
            Assert.Equal(
                Strings(row.GetProperty("writes")),
                EditorSend.Writes(row.GetProperty("text").GetString()!, Strings(row.GetProperty("pictures"))));
        }

        foreach (var row in cases.GetProperty("inserts").EnumerateArray())
        {
            Assert.Equal(
                Strings(row.GetProperty("writes")),
                EditorSend.Inserts(row.GetProperty("text").GetString()!, Strings(row.GetProperty("pictures"))));
        }

        var history = cases.GetProperty("history");
        var walk = CommandHistory.Empty;
        foreach (var command in history.GetProperty("sent").EnumerateArray())
        {
            walk = walk.Sent(command.GetString()!);
        }

        foreach (var step in history.GetProperty("steps").EnumerateArray())
        {
            string? shown;
            (walk, shown) = step.GetProperty("do").GetString() == "back"
                ? walk.Back(step.GetProperty("text").GetString()!)
                : walk.Forward();
            Assert.Equal(step.GetProperty("shows").GetString(), shown);
        }

        var completions = cases.GetProperty("completions");
        var tree = completions.GetProperty("tree").EnumerateObject().ToDictionary(
            folder => folder.Name,
            folder => (IReadOnlyList<FolderEntry>)[.. folder.Value.EnumerateArray().Select(entry => new FolderEntry(entry[0].GetString()!, entry[1].GetBoolean()))]);
        foreach (var row in completions.GetProperty("rows").EnumerateArray())
        {
            var done = PathCompletion.Complete(
                row.GetProperty("text").GetString()!, row.GetProperty("caret").GetInt32(), completions.GetProperty("folder").GetString()!,
                row.GetProperty("separator").GetString()![0], folder => tree.GetValueOrDefault(folder) ?? []);
            var expected = row.GetProperty("result");
            Assert.True(
                expected.ValueKind == JsonValueKind.Null
                    ? done is null
                    : done is not null && done.Text == expected[0].GetString() && done.Caret == expected[1].GetInt32()
                        && done.Choices.SequenceEqual(Strings(expected[2])),
                $"{row.GetProperty("name").GetString()}: {done?.Text}|{done?.Caret}|{string.Join(",", done?.Choices ?? [])}");
        }
    }

    [Fact]
    public void Every_copy_out_of_a_terminal_reads_the_same()
    {
        foreach (var row in SpecCases.Json("terminal-copy").GetProperty("rows").EnumerateArray())
        {
            Assert.True(
                row.GetProperty("copied").GetString() == TerminalCopy.Unwrap(row.GetProperty("text").GetString()!, row.GetProperty("columns").GetInt32()),
                $"{row.GetProperty("name").GetString()}: {TerminalCopy.Unwrap(row.GetProperty("text").GetString()!, row.GetProperty("columns").GetInt32())}");
        }
    }

    private static TerminalEventKind KindIn(JsonElement terminalEvent) =>
        Enum.Parse<TerminalEventKind>(terminalEvent.GetProperty("kind").GetString()!, ignoreCase: true);

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

        foreach (var row in cases.GetProperty("lit").EnumerateArray())
        {
            var (start, length) = FileMention.Lit(row.GetProperty("path").GetString()!, row.GetProperty("query").GetString()!);
            Assert.Equal((row.GetProperty("start").GetInt32(), row.GetProperty("length").GetInt32()), (start, length));
        }

        foreach (var row in cases.GetProperty("dropped").EnumerateArray())
        {
            var (dropped, at) = FileMention.Dropped(
                row.GetProperty("field").GetString()!, row.GetProperty("caret").GetInt32(),
                [.. row.GetProperty("paths").EnumerateArray().Select(path => path.GetString()!)]);
            Assert.Equal(row.GetProperty("result").GetString(), dropped);
            Assert.Equal(row.GetProperty("resultCaret").GetInt32(), at);
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
    public void Every_open_header_puts_the_active_environment_first_the_same_way()
    {
        var now = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        static int? Number(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();

        foreach (var row in SpecCases.Json("header-strip").GetProperty("headers").EnumerateArray())
        {
            var inputs = row.GetProperty("environments").EnumerateArray().Select(environment =>
            {
                var windows = environment.GetProperty("windows");
                var limits = windows.ValueKind == JsonValueKind.Null
                    ? null
                    : new LimitSnapshot(
                        environment.GetProperty("name").GetString()!,
                        LimitSource.StatusLine,
                        now,
                        [.. windows.EnumerateArray().Select(window => new LimitWindow(
                            Enum.Parse<LimitKind>(window.GetProperty("kind").GetString()!, ignoreCase: true),
                            window.GetProperty("percent").GetInt32(),
                            now.AddSeconds(window.GetProperty("resetsInSeconds").GetInt32())))]);
                return new HeaderInput(environment.GetProperty("name").GetString()!, limits, environment.GetProperty("waiting").GetInt32());
            }).ToList();

            var expected = row.GetProperty("columns").EnumerateArray().Select(column => new HeaderColumn(
                column.GetProperty("name").GetString()!,
                column.GetProperty("active").GetBoolean(),
                Number(column.GetProperty("fiveHour")),
                Number(column.GetProperty("sevenDay")),
                column.GetProperty("waiting").GetInt32())).ToList();

            Assert.True(
                expected.SequenceEqual(HeaderStrip.Columns(inputs, row.GetProperty("active").GetString()!, now)),
                row.GetProperty("name").GetString());
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

    private static string Camel<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
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
                    step.GetProperty("now").GetInt64(),
                    step.TryGetProperty("kind", out var kind) && kind.GetString() == "shell" ? SessionKind.Shell : SessionKind.Claude),
                "rename" => sessions.Rename(Word("id"), Word("name")),
                "titled" => sessions.Titled(Word("id"), Word("name")),
                "reopen" => sessions.Reopen(Word("id"), step.GetProperty("now").GetInt64()),
                "found" => sessions.Found(ConversationIn(step.GetProperty("conversation")), step.GetProperty("now").GetInt64()),
                "renameEnvironment" => sessions.RenameEnvironment(Word("from"), Word("to")),
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

    /// A stream event as the case files write it down.
    private static string Repeated(JsonElement row, string name) =>
        string.Concat(Enumerable.Repeat(row.GetProperty(name).GetString()!, row.GetProperty(name + "Times").GetInt32()));

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

        LiveTerminal? Live(string id) => live.TryGetProperty(id, out var terminal)
            ? new LiveTerminal(
                Enum.Parse<TerminalActivity>(terminal.GetProperty("activity").GetString()!, ignoreCase: true),
                terminal.GetProperty("line").GetString()!)
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
                    ["line"] = one.Line,
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
                case "next":
                    window = window.ToNeighbour(int.Parse(what, System.Globalization.CultureInfo.InvariantCulture), sessions);
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
