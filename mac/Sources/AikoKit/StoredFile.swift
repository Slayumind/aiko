/// What one of Aiko's own files turned out to be when it was read.
public enum StoredFileState: String, Sendable {
    /// No file: the defaults, and the first save makes it.
    case missing = "Missing"

    /// The text is used as it is. An empty file reads as the defaults.
    case read = "Read"

    /// Not a JSON object: it is set aside as .bad, and the defaults are used.
    case spoiled = "Spoiled"

    /// The file is there but could not be read: busy or no rights. The defaults are used for now,
    /// and nothing is written over the file until it is read again.
    case unreadable = "Unreadable"
}

/// The rule for Aiko's own files after a read: settings, environments, sessions, persona. A table
/// in spec/cases/stored-file. The twin of StoredFile.cs.
public enum StoredFile {
    /// text is nil when the read failed. Bytes that are not UTF-8 come with replacement
    /// characters, so they are not a JSON object.
    public static func of(exists: Bool, text: String?) -> StoredFileState {
        guard exists else { return .missing }
        guard let text else { return .unreadable }
        return !text.isEmpty && !JsonText.isObject(text) ? .spoiled : .read
    }

    /// Whether a save may write over the file. A file that could not be read would lose the
    /// person's choices to the defaults.
    public static func mayWrite(_ state: StoredFileState) -> Bool { state != .unreadable }
}
