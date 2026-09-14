using System.IO;
using System.Text;
using Aiko.Core;

namespace Aiko.App;

/// The person's PowerShell profiles, and the functions in them that switch Claude Code accounts.
///
/// A profile is somebody's own script, so the rules are strict: a copy is kept before the first
/// change, only the functions the person left ticked are turned off, and removing Aiko turns them
/// back on. The file is read and written as bytes: a Windows PowerShell 5.1 profile is often saved
/// in the ANSI code page, and reading it as UTF-8 would spoil every Cyrillic letter in it.
static class PowerShellProfiles
{
    public const string BackupSuffix = ".aiko-backup";

    public sealed record Found(string Path, IReadOnlyList<ProfileFunction> Functions);

    /// Latin-1 maps every byte to one character and back, so text in any encoding survives the trip.
    private static readonly Encoding Bytes = Encoding.Latin1;

    public static IReadOnlyList<string> Candidates()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return
        [
            Path.Combine(documents, "PowerShell", "Microsoft.PowerShell_profile.ps1"),
            Path.Combine(documents, "PowerShell", "profile.ps1"),
            Path.Combine(documents, "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1"),
            Path.Combine(documents, "WindowsPowerShell", "profile.ps1"),
        ];
    }

    public static IReadOnlyList<Found> FindSwitchers()
    {
        var found = new List<Found>();
        foreach (var path in Candidates())
        {
            if (ReadText(path) is { } text && PowerShellProfile.FindClaudeSwitchers(text) is { Count: > 0 } functions)
            {
                found.Add(new Found(path, functions));
            }
        }

        return found;
    }

    public static bool TurnOff(string path, IReadOnlyList<ProfileFunction> functions)
    {
        if (functions.Count == 0 || ReadText(path) is not { } text)
        {
            return false;
        }

        try
        {
            var backup = path + BackupSuffix;
            if (!File.Exists(backup))
            {
                File.Copy(path, backup);
            }

            WriteText(path, PowerShellProfile.TurnOff(text, functions));
            Log.Write($"profile: turned off {functions.Count} functions in {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"profile: could not change {Path.GetFileName(path)} ({e.GetType().Name})");
            return false;
        }
    }

    /// Turns back on everything Aiko turned off, in every profile, and drops the copies.
    public static void TurnOnAll()
    {
        foreach (var path in Candidates())
        {
            try
            {
                if (ReadText(path) is { } text && PowerShellProfile.HasTurnedOff(text))
                {
                    WriteText(path, PowerShellProfile.TurnOn(text));
                    Log.Write($"profile: turned functions back on in {Path.GetFileName(path)}");
                }

                var backup = path + BackupSuffix;
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static string? ReadText(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);

            // UTF-16 is the one encoding the byte trick cannot carry. Such a profile is left alone.
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            {
                return null;
            }

            return Bytes.GetString(bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteText(string path, string text)
    {
        var temporary = path + ".aiko.tmp";
        File.WriteAllBytes(temporary, Bytes.GetBytes(text));
        File.Move(temporary, path, overwrite: true);
    }
}
