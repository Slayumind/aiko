using System.IO;
using Microsoft.Win32;

namespace Aiko.App;

/// Starting with Windows. One value under the user's own key: no service, no scheduled task and
/// no administrator rights. Removing Aiko takes the value away again.
static class Startup
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Aiko";

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
                // Quoted: the path holds spaces, and Windows would otherwise read the first word
                // as the program and the rest as arguments.
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
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
