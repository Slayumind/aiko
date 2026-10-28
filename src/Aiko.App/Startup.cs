using System.IO;
using Microsoft.Win32;

namespace Aiko.App;

/// Starting with Windows. One value under the user's own key: no service, no scheduled task and
/// no administrator rights. Removing Aiko takes the value away again.
static class Startup
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Aiko";

    /// Tells a start at sign-in from a start by the person: only the first may open the terminal
    /// window by the setting (D-350).
    public const string Argument = "--autostart";

    /// Quoted: the path holds spaces, and Windows would otherwise read the first word as the
    /// program and the rest as arguments.
    private static string Command => $"\"{Environment.ProcessPath}\" {Argument}";

    /// An entry of this very program written before the argument existed gets it. An entry of
    /// another copy, such as a build being tried, is left alone: it is not this one's to change.
    public static void Refresh()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(ValueName) is string command && command == $"\"{Environment.ProcessPath}\"")
            {
                Set(true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Read again at the next start.
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is string path && path.Length > 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, Command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write($"could not change the startup entry: {e.GetType().Name}");
        }
    }
}
