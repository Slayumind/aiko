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

    /// The cached answers for the environments that exist right now, and nothing else.
    ///
    /// A file is only believed when its environment is still one of ours. Renaming an environment
    /// used to leave its old file behind, and the card then showed a whole environment that no
    /// longer existed, with numbers that would never change again. The leftovers are deleted here
    /// rather than left to puzzle somebody later.
    public static IReadOnlyDictionary<string, LimitSnapshot> ReadFor(IEnumerable<string> environments)
    {
        var wanted = new HashSet<string>(environments, StringComparer.OrdinalIgnoreCase);
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

                if (snapshot.HasData && wanted.Contains(snapshot.Environment))
                {
                    found[snapshot.Environment] = snapshot;
                }
                else
                {
                    Forget(file);
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

            var path = Path.Combine(Folder, FileNameFor(snapshot.Environment));
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

    private static void Forget(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// A readable part so a person can tell the files apart, and a hash of the whole name so two
    /// environments never land on one file. "Work" and "Work!" used to share one, and one of them
    /// showed the other's numbers.
    private static string FileNameFor(string environment) =>
        $"{Readable(environment)}-{ShortHash(environment)}.json";

    private static string Readable(string environment)
    {
        var kept = new string(environment.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        return kept.Length > 0 ? kept : "environment";
    }

    private static string ShortHash(string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
