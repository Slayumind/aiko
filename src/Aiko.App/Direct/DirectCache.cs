using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Keeps the last answer of the usage API on disk, one file per environment.
///
/// Status line numbers already survive a restart: the bridge writes them into files. Direct mode
/// numbers only lived in memory, so after a restart the card forgot the weekly limit of the heavy
/// model and everything for people who work in the IDE panel.
///
/// The file holds numbers and times, nothing else. It is the same format the bridge writes, and a
/// test in the core asserts that no path, session or token can appear in it.
static class DirectCache
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aiko",
        "direct");

    public static IReadOnlyDictionary<string, LimitSnapshot> ReadAll()
    {
        var found = new Dictionary<string, LimitSnapshot>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!Directory.Exists(Folder))
            {
                return found;
            }

            foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
            {
                var snapshot = SnapshotFile.FromJson(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));
                if (snapshot.HasData)
                {
                    found[snapshot.Environment] = snapshot;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return found;
    }

    public static void Write(LimitSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            var path = Path.Combine(Folder, Safe(snapshot.Environment) + ".json");
            var temporary = path + ".tmp";

            File.WriteAllText(temporary, SnapshotFile.ToJson(snapshot));
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// The user names their environments, and a name can hold anything a file name cannot.
    private static string Safe(string environment)
    {
        var kept = new string(environment.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        return kept.Length > 0 ? kept : "environment";
    }
}
