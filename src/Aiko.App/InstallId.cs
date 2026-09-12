using System.IO;

namespace Aiko.App;

/// A random value made once on this computer, kept in a file beside the settings, and never sent
/// anywhere. Only a hash of it with today's date leaves the machine, and only when update checks
/// are switched on.
///
/// Deleting the file gives a fresh one, which is the whole undo anybody needs.
static class InstallId
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aiko",
        "install-id");

    public static string Current()
    {
        try
        {
            if (File.Exists(Path))
            {
                var kept = File.ReadAllText(Path).Trim();
                if (kept.Length > 0)
                {
                    return kept;
                }
            }

            var made = Guid.NewGuid().ToString("n");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, made);
            return made;
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            // Without a file there is no steady value, and a new one every time would count one
            // person many times. Better to send nothing.
            return string.Empty;
        }
    }
}
