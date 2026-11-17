namespace Aiko.Core;

/// What one of Aiko's own files turned out to be when it was read.
public enum StoredFileState
{
    /// No file: the defaults, and the first save makes it.
    Missing,

    /// The text is used as it is. An empty file reads as the defaults.
    Read,

    /// Not a JSON object: it is set aside as .bad, and the defaults are used.
    Spoiled,

    /// The file is there but could not be read: busy or no rights. The defaults are used for now,
    /// and nothing is written over the file until it is read again.
    Unreadable,
}

/// The rule for Aiko's own files after a read: settings, environments, sessions, persona. A table
/// in spec/cases/stored-file. The twin of StoredFile.swift.
public static class StoredFile
{
    /// text is null when the read failed.
    public static StoredFileState Of(bool exists, string? text) =>
        !exists ? StoredFileState.Missing
        : text is null ? StoredFileState.Unreadable
        : text.Length > 0 && !JsonText.IsObject(text) ? StoredFileState.Spoiled
        : StoredFileState.Read;

    /// Whether a save may write over the file. A file that could not be read would lose the
    /// person's choices to the defaults.
    public static bool MayWrite(StoredFileState state) => state != StoredFileState.Unreadable;
}
