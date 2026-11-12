using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// What a person can pick in the menu of the tray icon.
enum TrayCommand
{
    None,
    SwapRing,
    Open,
    Settings,
    Exit,
}

/// The menu under a right click on the tray icon. It is the plain menu of Windows: shown, waited
/// for, and gone, with the choice as the answer.
[SupportedOSPlatform("windows10.0.17763")]
static class TrayMenu
{
    private const uint Settings = 1;
    private const uint Exit = 4;
    private const uint Swap = 5;
    private const uint Open = 6;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmNoNotify = 0x0080;
    private const uint TpmReturnCommand = 0x0100;

    /// The anchor is the point Windows sent with the click. otherEnvironment is the one the ring
    /// does not show; with a single environment it is null and the menu leaves the item out rather
    /// than showing one that does nothing.
    public static unsafe TrayCommand Show(HWND owner, IntPtr anchor, string? otherEnvironment)
    {
        var x = (short)(anchor.ToInt64() & 0xFFFF);
        var y = (short)((anchor.ToInt64() >> 16) & 0xFFFF);

        var menu = PInvoke.CreatePopupMenu();

        // Which environment the ring of the icon shows, when there are two. The click opens the
        // window, so the swap needs a home, and here it says which environment it would show.
        if (otherEnvironment is not null)
        {
            fixed (char* swap = string.Format(Strings.MenuShowInRing, otherEnvironment))
            {
                PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, Swap, swap);
            }

            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
        }

        // Open, the settings and quit (D-372): refreshing and the update check are in the window.
        fixed (char* open = Strings.MenuOpen,
                     settings = Strings.Settings,
                     exit = Strings.QuitAiko)
        {
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, Open, open);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, Settings, settings);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, Exit, exit);
        }

        // Without this the menu stays open after a click somewhere else.
        PInvoke.SetForegroundWindow(owner);
        var command = (uint)PInvoke.TrackPopupMenuEx(
            menu, TpmReturnCommand | TpmNoNotify | TpmRightButton, x, y, owner, null).Value;
        PInvoke.DestroyMenu(menu);

        return command switch
        {
            Swap => TrayCommand.SwapRing,
            Open => TrayCommand.Open,
            Settings => TrayCommand.Settings,
            Exit => TrayCommand.Exit,
            _ => TrayCommand.None,
        };
    }
}
