using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// What one Claude Code session is doing, as far as its hooks tell (D-206).
public enum SessionActivity
{
    Working,

    /// A permission prompt or a question: the session waits for the person.
    Waiting,

    Done,

    Error,

    /// The answer stopped on a rate limit.
    OutOfLimit,

    /// The session is over. Its file goes away.
    Ended,
}

/// One hook call turned into an activity. Only the event, the kind of notification or error and the
/// session id are read; the prompt, the tool input and the paths are never looked at.
public sealed record HookEvent(string SessionId, SessionActivity Activity)
{
    /// The kinds of Notification that mean "waiting for you". An idle reminder after a finished answer
    /// is not one of them: the face would say "waiting" a minute after "done".
    private static readonly HashSet<string> WaitingNotifications = ["permission_prompt", "elicitation_dialog", "agent_needs_input"];

    /// Null for an event that does not change the face, or for input that is not a hook call.
    public static HookEvent? FromJson(string json)
    {
        JsonObject? root;
        try
        {
            root = string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (Text(root, "session_id") is not { Length: > 0 } session)
        {
            return null;
        }

        SessionActivity? activity = Text(root, "hook_event_name") switch
        {
            "UserPromptSubmit" or "PostToolUse" => SessionActivity.Working,
            "PermissionRequest" => SessionActivity.Waiting,
            "Notification" when WaitingNotifications.Contains(Text(root, "notification_type") ?? "") => SessionActivity.Waiting,
            "Stop" => SessionActivity.Done,
            "StopFailure" => Text(root, "error_type") == "rate_limit" ? SessionActivity.OutOfLimit : SessionActivity.Error,
            "SessionEnd" => SessionActivity.Ended,
            _ => null,
        };

        return activity is { } known ? new HookEvent(session, known) : null;
    }

    private static string? Text(JsonObject? root, string key) =>
        root?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// The file one session reports its activity into: %LOCALAPPDATA%\Aiko\activity\<environment>.<session>.json.
/// A folder of its own, because the tray reads every file in environments\ as a limit snapshot.
public sealed record ActivityRecord(string Environment, SessionActivity Activity, DateTimeOffset At)
{
    public const string FolderName = "activity";

    /// PostToolUse comes after every tool. The same activity within this time is not written again:
    /// the tray only needs to know the session is still alive, and 10 minutes is when it stops
    /// believing that (D-206).
    public static readonly TimeSpan RewriteAfter = TimeSpan.FromSeconds(15);

    /// Async hooks run side by side, so the last PostToolUse can finish after Stop. A "working" that
    /// lands this soon after an answer ended is that late hook, not a new prompt: nobody types that fast.
    public static readonly TimeSpan LateHookWindow = TimeSpan.FromSeconds(2);

    /// Files left by sessions that never sent SessionEnd are cleared after this.
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(1);

    public static string Folder(string localAppData) => Path.Combine(localAppData, "Aiko", FolderName);

    /// The session id is hashed: the file name says which session it is without keeping the id.
    public static string FileName(string environment, string sessionId) =>
        $"{environment}.{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..12]}.json";

    /// Whether a new event has to touch the disk, given what the file already says.
    public static bool NeedsWrite(ActivityRecord? written, SessionActivity next, DateTimeOffset now)
    {
        if (written is null)
        {
            return true;
        }

        var age = now - written.At;
        if (next == SessionActivity.Working
            && written.Activity is SessionActivity.Done or SessionActivity.Error or SessionActivity.OutOfLimit
            && age < LateHookWindow)
        {
            return false;
        }

        return written.Activity != next || age >= RewriteAfter;
    }

    public static bool IsStale(DateTimeOffset written, DateTimeOffset now) => now - written > KeepFor;

    public string ToJson() =>
        new JsonObject
        {
            ["environment"] = Environment,
            ["activity"] = Activity.ToString(),
            ["at"] = At.ToString("O"),
        }.ToJsonString(Plain) + "\n";

    // The default encoder writes the plus of a time zone as an escape.
    private static readonly JsonSerializerOptions Plain = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// Null for a file that is half written or not ours.
    public static ActivityRecord? FromJson(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root
                || root["environment"]?.GetValue<string>() is not { } environment
                || !Enum.TryParse<SessionActivity>(root["activity"]?.GetValue<string>(), out var activity)
                || !DateTimeOffset.TryParse(root["at"]?.GetValue<string>(), out var at))
            {
                return null;
            }

            return new ActivityRecord(environment, activity, at);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
