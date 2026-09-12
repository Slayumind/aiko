using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Reads and writes the two files Aiko keeps for itself: the settings of the app and the list of
/// environments. Both live in %APPDATA%, because they are the user's choices, not a cache.
///
/// Nothing here throws. A settings file that cannot be read means the defaults, and Aiko starts.
static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aiko");

    private static readonly string SettingsFile = Path.Combine(Folder, "settings.json");
    private static readonly string EnvironmentsFile = Path.Combine(Folder, "environments.json");

    public static AppSettings Load() => AppSettings.FromJson(Read(SettingsFile));

    public static void Save(AppSettings settings) => Write(SettingsFile, settings.ToJson());

    public static EnvironmentSettings LoadEnvironments() =>
        EnvironmentSettings.FromJson(Read(EnvironmentsFile));

    public static void SaveEnvironments(EnvironmentSettings environments) =>
        Write(EnvironmentsFile, environments.ToJson());

    private static string Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// Written through a temporary file: a power cut in the middle of a save would otherwise
    /// leave half a file, and half a settings file reads as no settings at all.
    private static void Write(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
