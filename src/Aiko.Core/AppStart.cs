namespace Aiko.Core;

/// What one start of Aiko is, by its arguments (D-419). Only one copy of Aiko runs for a person: a
/// second plain start asks the running copy to open its window and exits, a second start at sign-in
/// exits. Starts for a check, a snapshot or the installer run beside the running copy on purpose.
public enum AppStart
{
    /// Started by the person: the Start menu, a pin on the taskbar, a double click.
    Plain,

    /// Started by Windows when the person signs in (Startup.Argument).
    AtSignIn,

    /// A check, a snapshot or a hook of the installer.
    Other,
}

public static class AppStarts
{
    /// The argument Windows starts Aiko with at sign-in.
    public const string SignInArgument = "--autostart";

    public static AppStart Of(IReadOnlyList<string> args) => args switch
    {
        [] => AppStart.Plain,
        [SignInArgument] => AppStart.AtSignIn,
        _ => AppStart.Other,
    };

    /// Whether this start stays the only copy.
    public static bool KeepsOneCopy(AppStart start) => start != AppStart.Other;

    /// Whether a second copy asks the running one to open its window. At sign-in it only exits.
    public static bool OpensRunningWindow(AppStart start) => start == AppStart.Plain;
}
