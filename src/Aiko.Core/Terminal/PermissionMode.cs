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

    /// The value of --permission-mode, or null when the flag is left out. The manual mode is what
    /// Claude Code starts in, and its name differs between versions, so it is never passed.
    public static string? FlagValue(PermissionMode mode) =>
        mode == PermissionMode.Manual ? null : Name(mode);

    /// The value a set_permission_mode request carries. The stream reports the manual mode as
    /// "default", so that is the word sent back.
    public static string RequestValue(PermissionMode mode) =>
        mode == PermissionMode.Manual ? "default" : Name(mode);

    public static PermissionMode Next(PermissionMode mode) =>
        Order[(Array.IndexOf(Order, mode) + 1) % Order.Length];
}
