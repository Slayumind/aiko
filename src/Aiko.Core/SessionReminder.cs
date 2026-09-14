using System.Text.Json.Nodes;

namespace Aiko.Core;

/// The line Claude Code shows at the start of a session when a command overrode a folder binding.
///
/// An explicit command wins over a binding, and the person may mean it (D-159). But limits are
/// money, so Claude Code says whose limits are being spent. The shim leaves AIKO_LAUNCH and
/// AIKO_ENVIRONMENT for the hook to read.
///
/// These two sentences live here and not in tools/strings.json: the bridge prints them, and the
/// bridge has no resources of the app. The copy was agreed on the artifact (D-163).
public static class SessionReminder
{
    public const string Argument = "--session-start";
    public const string LaunchVariable = "AIKO_LAUNCH";
    public const string EnvironmentVariable = "AIKO_ENVIRONMENT";
    public const string LaunchedByCommand = "command";

    public static string? MessageFor(
        string? launch,
        string? runningEnvironment,
        string workingDirectory,
        EnvironmentSettings settings,
        bool russian)
    {
        if (launch != LaunchedByCommand || string.IsNullOrWhiteSpace(runningEnvironment))
        {
            return null;
        }

        var bound = ProjectBinding.BoundEnvironmentFor(workingDirectory, settings);
        if (bound is null || bound.Name.Equals(runningEnvironment, StringComparison.Ordinal))
        {
            return null;
        }

        return russian
            ? $"Aiko: эта папка относится к {bound.Name}, а сессия запущена в {runningEnvironment}. Лимиты тратятся из {runningEnvironment}."
            : $"Aiko: this folder belongs to {bound.Name}, but this session runs in {runningEnvironment}, so the limits of {runningEnvironment} are used.";
    }

    /// The hook answer Claude Code reads. systemMessage is shown to the person; plain output of a
    /// SessionStart hook would go to the model instead.
    public static string HookOutput(string message) =>
        new JsonObject { ["systemMessage"] = message }.ToJsonString();

    /// Russian when chosen in settings, or when settings follow Windows and Windows speaks Russian.
    /// The bridge gets the language id of Windows, because it runs with invariant globalization.
    public static bool IsRussian(AikoLanguage language, int windowsUiLanguageId) =>
        language == AikoLanguage.Russian
        || (language == AikoLanguage.System && (windowsUiLanguageId & 0x3FF) == 0x19);

    /// The working folder from the JSON Claude Code gives a hook on stdin.
    public static string? WorkingDirectoryIn(string hookInput)
    {
        try
        {
            return JsonNode.Parse(hookInput) is JsonObject root
                && root.TryGetPropertyValue("cwd", out var cwd)
                && cwd is JsonValue value
                && value.TryGetValue<string>(out var text)
                    ? text
                    : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
