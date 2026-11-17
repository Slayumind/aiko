using System.IO;
using System.Text;

namespace Aiko.Shared;

static class AtomicFile
{
    /// Write to a temporary file and move it over the old one, so a reader never sees half a file.
    /// While somebody reads the old file, Windows refuses to replace it, so the move is tried again
    /// for a short while. A temporary file that could not take the place is removed, not left in the
    /// folder (AIKO-267).
    public static void Replace(string path, string text)
    {
        const int Attempts = 10;
        var temporary = path + "." + Environment.ProcessId + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporary, path, overwrite: true);
                    return;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < Attempts)
                {
                    Thread.Sleep(20);
                }
            }
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Aiko removes it when it starts.
            }
        }
    }
}
