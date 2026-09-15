namespace Aiko.Core.Tests;

public class SessionActivityTests
{
    private const string Session = "5f0c2a8e-1b7d-4c1e-9f3a-2d6b8e4a7c10";

    private static string Hook(string name, string extra = "") =>
        $$"""{ "session_id": "{{Session}}", "hook_event_name": "{{name}}", "cwd": "C:\\secret\\project"{{extra}} }""";

    [Theory]
    [InlineData("UserPromptSubmit", "", SessionActivity.Working)]
    [InlineData("PostToolUse", "", SessionActivity.Working)]
    [InlineData("PermissionRequest", "", SessionActivity.Waiting)]
    [InlineData("Notification", """, "notification_type": "permission_prompt" """, SessionActivity.Waiting)]
    [InlineData("Notification", """, "notification_type": "elicitation_dialog" """, SessionActivity.Waiting)]
    [InlineData("Notification", """, "notification_type": "agent_needs_input" """, SessionActivity.Waiting)]
    [InlineData("Stop", "", SessionActivity.Done)]
    [InlineData("StopFailure", """, "error_type": "server_error" """, SessionActivity.Error)]
    [InlineData("StopFailure", """, "error_type": "rate_limit" """, SessionActivity.OutOfLimit)]
    [InlineData("SessionEnd", """, "reason": "clear" """, SessionActivity.Ended)]
    public void Each_event_the_plugin_listens_to_means_one_activity(string name, string extra, SessionActivity expected)
    {
        Assert.Equal(new HookEvent(Session, expected), HookEvent.FromJson(Hook(name, extra)));
    }

    [Fact]
    public void Every_event_in_the_plugin_is_understood()
    {
        foreach (var name in PersonaPlugin.HookEvents)
        {
            var extra = name == "Notification" ? """, "notification_type": "permission_prompt" """ : "";
            Assert.NotNull(HookEvent.FromJson(Hook(name, extra)));
        }
    }

    [Theory]
    [InlineData("""{ "session_id": "x", "hook_event_name": "Notification", "notification_type": "idle_prompt" }""")]
    [InlineData("""{ "session_id": "x", "hook_event_name": "PreToolUse" }""")]
    [InlineData("""{ "hook_event_name": "Stop" }""")]
    [InlineData("""{ "session_id": 7, "hook_event_name": "Stop" }""")]
    [InlineData("not json")]
    [InlineData("")]
    public void Anything_else_changes_nothing(string json)
    {
        Assert.Null(HookEvent.FromJson(json));
    }

    [Fact]
    public void The_file_name_keeps_the_environment_but_not_the_session_id()
    {
        var name = ActivityRecord.FileName("claude-work", Session);

        Assert.StartsWith("claude-work.", name);
        Assert.EndsWith(".json", name);
        Assert.DoesNotContain(Session, name);
        Assert.Equal(name, ActivityRecord.FileName("claude-work", Session));
        Assert.NotEqual(name, ActivityRecord.FileName("claude-work", Session + "2"));
    }

    [Fact]
    public void The_file_holds_the_activity_and_the_time_and_nothing_from_the_hook()
    {
        var record = new ActivityRecord("default", SessionActivity.Waiting, new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.FromHours(3)));

        var json = record.ToJson();

        Assert.Equal(record, ActivityRecord.FromJson(json));
        Assert.DoesNotContain("secret", json);
        Assert.Contains("+03:00", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"environment\": \"default\", \"activity\": \"Dancing\", \"at\": \"2026-09-15T12:00:00Z\" }")]
    [InlineData("{ \"environment\": \"default\", \"activity\": \"Done\" }")]
    [InlineData("{ \"environment\": \"def")]
    public void A_half_written_or_strange_file_reads_as_nothing(string json)
    {
        Assert.Null(ActivityRecord.FromJson(json));
    }

    [Fact]
    public void The_same_activity_is_not_written_again_for_a_few_seconds()
    {
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var working = new ActivityRecord("default", SessionActivity.Working, now);

        Assert.False(ActivityRecord.NeedsWrite(working, SessionActivity.Working, now.AddSeconds(3)));
        Assert.True(ActivityRecord.NeedsWrite(working, SessionActivity.Working, now + ActivityRecord.RewriteAfter));
        Assert.True(ActivityRecord.NeedsWrite(working, SessionActivity.Waiting, now.AddSeconds(1)));
        Assert.True(ActivityRecord.NeedsWrite(null, SessionActivity.Working, now));
    }

    [Fact]
    public void A_late_tool_hook_right_after_the_answer_ended_does_not_bring_back_working()
    {
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var done = new ActivityRecord("default", SessionActivity.Done, now);

        Assert.False(ActivityRecord.NeedsWrite(done, SessionActivity.Working, now.AddMilliseconds(300)));
        Assert.True(ActivityRecord.NeedsWrite(done, SessionActivity.Working, now + ActivityRecord.LateHookWindow));
        Assert.True(ActivityRecord.NeedsWrite(new ActivityRecord("default", SessionActivity.Waiting, now), SessionActivity.Working, now.AddMilliseconds(300)));
    }

    [Fact]
    public void Files_older_than_a_day_are_stale()
    {
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.False(ActivityRecord.IsStale(now.AddHours(-23), now));
        Assert.True(ActivityRecord.IsStale(now.AddHours(-25), now));
    }
}
