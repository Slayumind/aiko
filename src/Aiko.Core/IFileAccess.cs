namespace Aiko.Core;

/// The few things the core needs to do to a file on disk.
///
/// The core stays free of the file system itself, but the rules about somebody else's settings
/// file — copy it before the first change, never leave half a file behind — are rules, not
/// plumbing, and rules belong where a test can reach them. This is the narrow interface the
/// architecture allows for exactly that.
public interface IFileAccess
{
    bool Exists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string text);

    void Copy(string from, string to);

    /// Replaces the destination if it is already there.
    void Move(string from, string to);

    void Delete(string path);
}
