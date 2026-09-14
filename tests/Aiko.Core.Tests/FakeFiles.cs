using Aiko.Core;

namespace Aiko.Core.Tests;

/// A file system in a dictionary, so the code that writes to somebody else's settings file can be
/// tested without one.
sealed class FakeFiles : IFileAccess
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    /// Paths that answer every call with an IOException, for the folder Aiko may not touch.
    public HashSet<string> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Writes { get; } = [];

    public FakeFiles With(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    public bool Has(string path) => _files.ContainsKey(path);

    public string Read(string path) => _files[path];

    public bool Exists(string path)
    {
        Refuse(path);
        return _files.ContainsKey(path);
    }

    public string ReadAllText(string path)
    {
        Refuse(path);
        return _files.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException("no such file", path);
    }

    public void WriteAllText(string path, string text)
    {
        Refuse(path);
        Writes.Add(path);
        _files[path] = text;
    }

    public void Copy(string from, string to)
    {
        Refuse(from);
        Refuse(to);
        _files[to] = _files[from];
    }

    public void Move(string from, string to)
    {
        Refuse(from);
        Refuse(to);
        _files[to] = _files[from];
        _files.Remove(from);
    }

    public void Delete(string path)
    {
        Refuse(path);
        _files.Remove(path);
    }

    private void Refuse(string path)
    {
        if (Unreadable.Contains(path))
        {
            throw new IOException("this file is not yours");
        }
    }
}
