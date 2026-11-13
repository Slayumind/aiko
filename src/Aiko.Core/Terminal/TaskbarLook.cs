namespace Aiko.Core.Terminal;

/// What the middle of the taskbar button of the terminal window shows (D-418).
public enum ButtonCentre
{
    /// The rings of the tray, live.
    Rings,

    /// The face of Aiko in place of the rings.
    Face,

    /// The picture of the pin. Windows draws a pinned button from the pin, not from the window,
    /// so Aiko draws nothing there.
    Pinned,
}

/// What the corner of the button shows. Windows draws the corner over a pinned button too.
public enum ButtonCorner
{
    None,

    /// The sign of the sessions (D-409).
    Badge,

    /// The face of Aiko, while the middle is the picture of the pin.
    Face,
}

/// The picture of the taskbar button: the middle and the corner (D-418). Rows: spec/cases/taskbar-button.
public sealed record ButtonLook(ButtonCentre Centre, ButtonCorner Corner)
{
    /// pinned: Aiko is pinned to the taskbar. face: a face is shown now. badge: the sign of the
    /// sessions. A face takes the middle when it can; on a pinned button it takes the corner for its
    /// two seconds, and the sign comes back after it.
    public static ButtonLook For(bool pinned, bool face, BadgeKind badge)
    {
        var sign = badge == BadgeKind.None ? ButtonCorner.None : ButtonCorner.Badge;
        if (pinned)
        {
            return new ButtonLook(ButtonCentre.Pinned, face ? ButtonCorner.Face : sign);
        }

        return new ButtonLook(face ? ButtonCentre.Face : ButtonCentre.Rings, sign);
    }
}

/// One pin on the taskbar, as Windows keeps it in its shortcut: the app id and the program it starts.
/// Either can be missing.
public sealed record TaskbarPin(string? AppId, string? Target);

/// Which app id the terminal window takes (D-418). Windows 11 draws a taskbar button from the
/// Start menu shortcut of the program when the window has its id, and from the window only when
/// the window has an id no shortcut has. So the window takes an id of its own, unless Aiko is
/// pinned the old way, by its shortcut: then it keeps the id of the shortcut and stands on that
/// pin, and there is one button, not two.
public static class TaskbarIdentity
{
    /// The id of the terminal window. A pin made from its button carries this id.
    public const string Own = "Slayumind.Aiko.Terminal";

    /// The id Velopack gives the Start menu shortcut of Aiko.
    public static string Shortcut => $"velopack.{AikoFolders.InstallFolderName}";

    /// program: the full path of the running Aiko. AppId null: the window keeps the id Windows
    /// gives the program.
    public static (string? AppId, bool Pinned) Choose(IEnumerable<TaskbarPin> pins, string program)
    {
        var all = pins.ToList();
        if (all.Any(pin => Same(pin.AppId, Own)))
        {
            return (Own, true);
        }

        if (all.Any(pin => Same(pin.AppId, Shortcut) || (pin.AppId is null && Same(pin.Target, program))))
        {
            return (null, true);
        }

        return (Own, false);
    }

    /// Whether the list of pins Windows keeps in the registry (Taskband, Favorites) has a pin made
    /// from the button of the window. Such a pin has no shortcut in the folder of pins; the list
    /// names it by the own id, in UTF-16, and drops it when it is unpinned (2026-11-13).
    public static bool ListsOwnPin(ReadOnlySpan<byte> favorites) =>
        favorites.IndexOf(System.Text.Encoding.Unicode.GetBytes(Own)) >= 0;

    private static bool Same(string? one, string other) =>
        one is not null && string.Equals(one, other, StringComparison.OrdinalIgnoreCase);
}
