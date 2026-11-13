namespace Aiko.Core;

/// What one start of Aiko is, by its arguments (D-419). Only one copy of Aiko runs for a person: a
/// second plain start asks the running copy to open its window and exits, a second start at sign-in
/// exits. Only a command Aiko knows runs beside the running copy on purpose. Checks and snapshots
/// are another program, Aiko.Checks (D-420).
public enum AppStart
{
    /// Started by the person: the Start menu, a pin on the taskbar, a double click. Also any
    /// argument Aiko does not know, such as a folder dropped on its shortcut.
    Plain,

    /// Started by Windows when the person signs in (Startup.Argument).
    AtSignIn,

    /// A command Aiko knows (Commands) or a hook of the installer.
    Other,
}

public static class AppStarts
{
    /// The argument Windows starts Aiko with at sign-in.
    public const string SignInArgument = "--autostart";

    /// Puts the saved environments into effect outside Aiko, the way Finish in the wizard does.
    public const string ApplyEnvironments = "--apply-environments";

    /// Takes back the command folder, the PATH entry and the profile changes.
    public const string UndoEnvironmentSetup = "--undo-environment-setup";

    /// The installer starts Aiko with arguments of its own, and Velopack handles them first.
    private const string InstallerPrefix = "--veloapp-";

    public static AppStart Of(IReadOnlyList<string> args) => args switch
    {
        [SignInArgument] => AppStart.AtSignIn,
        [ApplyEnvironments or UndoEnvironmentSetup, ..] => AppStart.Other,
        [var first, ..] when first.StartsWith(InstallerPrefix, StringComparison.Ordinal) => AppStart.Other,
        _ => AppStart.Plain,
    };

    /// Whether this start stays the only copy.
    public static bool KeepsOneCopy(AppStart start) => start != AppStart.Other;

    /// Whether a second copy asks the running one to open its window. At sign-in it only exits.
    public static bool OpensRunningWindow(AppStart start) => start == AppStart.Plain;
}
