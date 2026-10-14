using System.Text.Json;

namespace Aiko.Core.Terminal;

/// How much Claude Code asks before it acts. The four modes the terminal offers (D-302).
///
/// bypassPermissions and dontAsk exist in Claude Code and are left out on purpose: the first
/// turns every question off, the second refuses in silence.
public enum PermissionMode
{
    Manual,
    AcceptEdits,
    Plan,
    Auto,
}

/// The names of the modes. They are the words of Claude Code itself, the same in every language
/// of the interface, so the line under the input, the flag and the documentation say one thing.
public static class PermissionModes
{
    /// The order Shift+Tab walks through.
    private static readonly PermissionMode[] Order =
        [PermissionMode.Manual, PermissionMode.AcceptEdits, PermissionMode.Plan, PermissionMode.Auto];

    public static IReadOnlyList<PermissionMode> All => Order;

    public static string Name(PermissionMode mode) => mode switch
    {
        PermissionMode.AcceptEdits => "acceptEdits",
        PermissionMode.Plan => "plan",
        PermissionMode.Auto => "auto",
        _ => "manual",
    };

    /// The mode a stream event names. The stream still calls the manual mode "default", the flag
    /// calls it "manual"; both are read. Null for a mode the terminal does not offer.
    public static PermissionMode? FromStream(string? value) => value switch
    {
        "default" or "manual" => PermissionMode.Manual,
        "acceptEdits" => PermissionMode.AcceptEdits,
        "plan" => PermissionMode.Plan,
        "auto" => PermissionMode.Auto,
        _ => null,
    };

    /// The value of --permission-mode. The flag is always passed: without it a new config folder
    /// starts in auto mode. The manual mode goes as "default", the word every version takes.
    public static string FlagValue(PermissionMode mode) => RequestValue(mode);

    /// The value a set_permission_mode request carries. The stream reports the manual mode as
    /// "default", so that is the word sent back.
    public static string RequestValue(PermissionMode mode) =>
        mode == PermissionMode.Manual ? "default" : Name(mode);

    /// The mode a new session starts in: the one Claude Code itself would start in (D-311). settings
    /// are the texts of the settings files, the strongest first: the local settings of the project,
    /// its shared settings, then the settings of the config folder. The first file that names a
    /// default mode decides; a mode the terminal does not offer, and no mode at all, is manual.
    public static PermissionMode Default(IReadOnlyList<string?> settings)
    {
        foreach (var text in settings)
        {
            if (DefaultModeIn(text) is { } named)
            {
                return FromStream(named) ?? PermissionMode.Manual;
            }
        }

        return PermissionMode.Manual;
    }

    private static string? DefaultModeIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("permissions", out var permissions)
                && permissions.ValueKind == JsonValueKind.Object
                && permissions.TryGetProperty("defaultMode", out var mode)
                && mode.ValueKind == JsonValueKind.String
                    ? mode.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static PermissionMode Next(PermissionMode mode) =>
        Order[(Array.IndexOf(Order, mode) + 1) % Order.Length];
}
