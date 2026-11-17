using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Reads and writes the files Aiko keeps for itself: the settings of the app, the list of
/// environments and the persona. All live in %APPDATA%, because they are the user's choices, not a cache.
///
/// Nothing here throws. A settings file that cannot be read means the defaults, and Aiko starts.
///
/// Every file is read from the disk once per change. The window, the island, the icon and the
/// numbers all ask for the environments on every event of the status line; a copy is kept with the
/// time and the length of the file it came from, and only a file that changed is read again.
static class SettingsStore
{
    private static readonly Dictionary<string, (DateTime Written, long Length, string Text)> Copies = new(StringComparer.OrdinalIgnoreCase);
    private static (string Text, EnvironmentSettings Settings)? LastEnvironments;

    private static readonly string Folder = ThisComputer.Folders.SettingsFolder;
    private static readonly string SettingsFile = ThisComputer.Folders.SettingsFile;
    private static readonly string EnvironmentsFile = ThisComputer.Folders.EnvironmentsFile;
    private static readonly string PersonaFile = ThisComputer.Folders.PersonaFile;
    private static readonly string TerminalFile = ThisComputer.Folders.TerminalFile;

    public static AppSettings Load() => AppSettings.FromJson(Read(SettingsFile));

    public static void Save(AppSettings settings) => Write(SettingsFile, settings.ToJson());

    /// One change to the settings, made to what the file holds now, and the result. A copy read
    /// earlier may be old: the terminal window, the island and the settings window each change their
    /// own fields, and a save of an old copy would put back what another one changed.
    public static AppSettings Change(Func<AppSettings, AppSettings> change)
    {
        var settings = change(Load());
        Save(settings);
        return settings;
    }

    /// The list is read again only when the file changed; the same text gives the same object.
    public static EnvironmentSettings LoadEnvironments()
    {
        var text = Read(EnvironmentsFile);
        lock (Copies)
        {
            if (LastEnvironments is { } known && ReferenceEquals(known.Text, text))
            {
                return known.Settings;
            }

            var settings = EnvironmentSettings.FromJson(text);
            LastEnvironments = (text, settings);
            return settings;
        }
    }

    public static void SaveEnvironments(EnvironmentSettings environments) =>
        Write(EnvironmentsFile, environments.ToJson());

    /// The sessions of the terminal: names, folders and conversation ids, never a conversation.
    public static Aiko.Core.Terminal.TerminalSessions LoadTerminal() =>
        Aiko.Core.Terminal.TerminalSessions.FromJson(Read(TerminalFile));

    public static void SaveTerminal(Aiko.Core.Terminal.TerminalSessions sessions) => Write(TerminalFile, sessions.ToJson());

    public static PersonaSettings LoadPersona() => PersonaSettings.FromJson(Read(PersonaFile));

    public static void SavePersona(PersonaSettings persona) => Write(PersonaFile, persona.ToJson());

    /// The text of the file, or "" for the defaults. What happens to the file is the rule of
    /// StoredFile: a spoiled one is set aside, and one that could not be read is kept from saves.
    private static string Read(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            Readable(path);
            return string.Empty;
        }

        lock (Copies)
        {
            if (Copies.TryGetValue(path, out var copy) && copy.Written == file.LastWriteTimeUtc && copy.Length == file.Length)
            {
                return copy.Text;
            }
        }

        var text = ReadBusy(path);
        switch (StoredFile.Of(exists: true, text))
        {
            case StoredFileState.Unreadable:
                lock (Copies)
                {
                    if (Unread.Add(path))
                    {
                        Log.Write($"{Path.GetFileName(path)} could not be read: it is not written until it can be");
                    }
                }

                return string.Empty;
            case StoredFileState.Spoiled:
                Readable(path);
                SetAside(path);
                return string.Empty;
            default:
                Readable(path);
                lock (Copies)
                {
                    Copies[path] = (file.LastWriteTimeUtc, file.Length, text!);
                }

                return text!;
        }
    }

    /// A file another program holds for a moment (a backup or an antivirus) is tried again a few
    /// times. Null when it still cannot be read.
    private static string? ReadBusy(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                return string.Empty;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// The files that could not be read the last time: a save would put the defaults in place of
    /// the person's choices, so they are not written until a read works again.
    private static readonly HashSet<string> Unread = new(StringComparer.OrdinalIgnoreCase);

    private static void Readable(string path)
    {
        lock (Copies)
        {
            Unread.Remove(path);
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
        lock (Copies)
        {
            if (Unread.Contains(path))
            {
                Log.Write($"{Path.GetFileName(path)} was not saved: it could not be read before");
                return;
            }
        }

        try
        {
            Directory.CreateDirectory(Folder);
            AtomicFile.Replace(path, text);
            lock (Copies)
            {
                Copies.Remove(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
