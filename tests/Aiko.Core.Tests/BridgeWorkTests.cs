namespace Aiko.Core.Tests;

/// The decisions of the bridge, moved out of Aiko.Bridge/Program.cs into the core. The twin of
/// BridgeWorkTests.swift, with Windows paths.
public class BridgeWorkTests
{
    private const string ConfigDirectory = @"C:\Users\someone\.claude-work";

    private const string Payload = """
        {
          "session_id": "00000000-0000-0000-0000-000000000000",
          "cwd": "C:\\work",
          "rate_limits": {
            "five_hour": { "used_percentage": 28, "resets_at": 1789170600 },
            "seven_day": { "used_percentage": 15, "resets_at": 1789506000 }
          }
        }
        """;

    private static readonly AikoFolders Folders = AikoFolders.Windows(@"C:\Users\someone\AppData\Roaming", @"C:\Users\someone\AppData\Local");
    private static readonly DateTimeOffset Noon = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    // ---- which job this run is ----

    [Theory]
    [InlineData(new[] { "plugin", "aiko-persona" }, BridgeJob.PersonaPlugin)]
    [InlineData(new[] { "memory", "rules" }, BridgeJob.Memory)]
    [InlineData(new[] { "memory", "serve" }, BridgeJob.Memory)]
    [InlineData(new[] { "--session-start" }, BridgeJob.SessionStart)]
    [InlineData(new[] { "terminal-event" }, BridgeJob.TerminalHook)]
    [InlineData(new[] { "hook" }, BridgeJob.Hook)]
    [InlineData(new string[0], BridgeJob.StatusLine)]
    [InlineData(new[] { "memory" }, BridgeJob.StatusLine)]
    [InlineData(new[] { "memory", "erase" }, BridgeJob.StatusLine)]
    [InlineData(new[] { "something" }, BridgeJob.StatusLine)]
    public void Each_job_is_told_apart_by_its_arguments(string[] arguments, BridgeJob job)
    {
        Assert.Equal(job, BridgeWork.JobFor(arguments));
    }

    // ---- the status line ----

    [Fact]
    public void A_status_line_with_limits_writes_the_snapshot_of_its_environment()
    {
        var plan = BridgeWork.StatusLine(Folders, ConfigDirectory, null, Payload, Noon);

        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\environments\claude-work.json", plan.Snapshot!.Path);
        var written = SnapshotFile.FromJson(plan.Snapshot.Text);
        Assert.Equal(("claude-work", 2), (written.Environment, written.Windows.Count));
    }

    [Fact]
    public void The_same_numbers_are_not_written_again_within_a_minute()
    {
        var first = BridgeWork.StatusLine(Folders, ConfigDirectory, null, Payload, Noon).Snapshot!.Text;

        Assert.Null(BridgeWork.StatusLine(Folders, ConfigDirectory, null, Payload, Noon.AddSeconds(59), first).Snapshot);
        Assert.NotNull(BridgeWork.StatusLine(Folders, ConfigDirectory, null, Payload, Noon.AddSeconds(60), first).Snapshot);
    }

    [Fact]
    public void New_numbers_are_written_at_once()
    {
        var first = BridgeWork.StatusLine(Folders, ConfigDirectory, null, Payload, Noon).Snapshot!.Text;
        var other = Payload.Replace("28", "29", StringComparison.Ordinal);

        Assert.NotEqual(Payload, other);
        Assert.NotNull(BridgeWork.StatusLine(Folders, ConfigDirectory, null, other, Noon.AddSeconds(5), first).Snapshot);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{ "rate_limits": {} }""")]
    public void Input_with_no_limits_writes_nothing(string input)
    {
        var plan = BridgeWork.StatusLine(Folders, ConfigDirectory, null, input, Noon);

        Assert.Equal(new StatusLinePlan(null, null), plan);
    }

    [Fact]
    public void The_line_the_user_already_had_is_handed_back_to_be_run()
    {
        const string Settings = """
            {
              "statusLine": { "type": "command", "command": "C:\\Aiko\\Aiko.Bridge.exe" },
              "aikoWrappedStatusLine": { "type": "command", "command": "my-line.cmd" }
            }
            """;

        Assert.Equal("my-line.cmd", BridgeWork.StatusLine(Folders, ConfigDirectory, Settings, Payload, Noon).WrappedCommand);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{ "aikoWrappedStatusLine": { "type": "command", "command": "  " } }""")]
    public void Nothing_kept_aside_means_nothing_to_run(string settings)
    {
        Assert.Null(BridgeWork.StatusLine(Folders, ConfigDirectory, settings, Payload, Noon).WrappedCommand);
    }

    // ---- the session events behind the face ----

    private static string ActivityPath => Folders.Platform.Join(Folders.ActivityFolder, ActivityRecord.FileName("claude-work", "session-1"));

    private static ActivityStep Activity(string input, string? existing = null, DateTimeOffset? now = null) =>
        BridgeWork.Activity(Folders, ConfigDirectory, input, _ => existing, now ?? Noon);

    private static string Hook(string name) => $$"""{ "hook_event_name": "{{name}}", "session_id": "session-1" }""";

    [Fact]
    public void A_prompt_writes_the_activity_of_its_session()
    {
        var step = Activity(Hook("UserPromptSubmit"));

        Assert.Equal(ActivityPath, step.Write!.Path);
        var record = ActivityRecord.FromJson(step.Write.Text);
        Assert.Equal(("claude-work", SessionActivity.Working), (record!.Environment, record.Activity));
    }

    [Fact]
    public void The_end_of_a_session_takes_its_file_away()
    {
        Assert.Equal(new ActivityStep(ActivityPath, null), Activity(Hook("SessionEnd")));
    }

    [Fact]
    public void The_same_activity_again_does_not_touch_the_disk()
    {
        var written = new ActivityRecord("claude-work", SessionActivity.Working, Noon).ToJson();

        Assert.Equal(ActivityStep.Nothing, Activity(Hook("PostToolUse"), written, Noon.AddSeconds(5)));
    }

    [Theory]
    [InlineData("PreToolUse")]
    [InlineData("SessionStart")]
    [InlineData("")]
    public void An_event_the_face_does_not_care_about_writes_nothing(string name)
    {
        Assert.Equal(ActivityStep.Nothing, Activity(Hook(name)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData(Payload)]
    public void Input_that_is_not_a_hook_call_writes_nothing(string input)
    {
        Assert.Equal(ActivityStep.Nothing, Activity(input));
    }

    // ---- the session start hook ----

    private static readonly EnvironmentSettings Environments = new([
        new AikoEnvironment("Work", [@"C:\Users\someone\.claude"]),
        new AikoEnvironment("Personal", [@"C:\Users\someone\.claude-personal"]) { ProjectFolders = [@"D:\personal"] },
    ]);

    private static string? SessionStart(string? launch, string? running, bool russian = false) =>
        BridgeWork.SessionStart("""{ "cwd": "D:\\personal\\app" }""", launch, running, Environments.ToJson(), "", _ => russian);

    [Fact]
    public void A_command_in_somebody_elses_folder_says_whose_limits_are_spent()
    {
        var answer = SessionStart("command", "Work");

        Assert.Contains("systemMessage", answer);
        Assert.Contains("this folder belongs to Personal", answer);
    }

    [Fact]
    public void A_session_that_matches_its_binding_says_nothing()
    {
        Assert.Null(SessionStart("command", "Personal"));
        Assert.Null(SessionStart("binding", "Work"));
        Assert.Null(SessionStart("command", null));
    }

    [Fact]
    public void Input_without_a_folder_says_nothing()
    {
        Assert.Null(BridgeWork.SessionStart("{}", "command", "Work", Environments.ToJson(), "", _ => false));
    }
}
