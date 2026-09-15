using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Reads and writes the files Aiko keeps for itself: the settings of the app, the list of
/// environments and the persona. All live in %APPDATA%, because they are the user's choices, not a cache.
///
/// Nothing here throws. A settings file that cannot be read means the defaults, and Aiko starts.
static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aiko");

    private static readonly string SettingsFile = Path.Combine(Folder, "settings.json");
    private static readonly string EnvironmentsFile = Path.Combine(Folder, "environments.json");

    /// Its own file: the bridge reads the persona to build the plugin, and the shim does not need it.
    private static readonly string PersonaFile = Path.Combine(Folder, "persona.json");

    public static AppSettings Load() => AppSettings.FromJson(Read(SettingsFile));

    public static void Save(AppSettings settings) => Write(SettingsFile, settings.ToJson());

    public static EnvironmentSettings LoadEnvironments() =>
        EnvironmentSettings.FromJson(Read(EnvironmentsFile));

    public static void SaveEnvironments(EnvironmentSettings environments) =>
        Write(EnvironmentsFile, environments.ToJson());

    public static PersonaSettings LoadPersona() => PersonaSettings.FromJson(Read(PersonaFile));

    public static void SavePersona(PersonaSettings persona) => Write(PersonaFile, persona.ToJson());

    private static string Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            var text = File.ReadAllText(path);
            if (text.Length > 0 && !JsonText.IsObject(text))
            {
                SetAside(path);
                return string.Empty;
            }

            return text;
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

    /// A file that will not parse is moved out of the way before Aiko carries on with the
    /// defaults. Left where it is, the next save would write over it, and choices the user made
    /// would be gone with no way to see what they were.
    private static void SetAside(string path)
    {
        try
        {
            var spoiled = path + ".bad";
            File.Move(path, spoiled, overwrite: true);
            Log.Write($"{Path.GetFileName(path)} could not be read and was kept as {Path.GetFileName(spoiled)}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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
