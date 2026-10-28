using System.IO;
using System.Text;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The conversations Claude Code keeps in the folders of the environments, for the palette of
/// sessions (D-353). Only the start and the end of each file are read, off the thread of the window,
/// and a file that has not changed since the last time is not read again.
sealed class ConversationShelf
{
    private readonly Dictionary<string, (DateTime Written, long Length, Conversation? Conversation)> _read = [];
    private readonly object _lock = new();

    /// Every conversation of these environments: the name of each, with its folders of Claude Code.
    /// A folder or a file that cannot be read is skipped.
    public Task<IReadOnlyList<Conversation>> LoadAsync(IReadOnlyList<(string Environment, string ConfigDirectory)> folders) =>
        Task.Run(() =>
        {
            var found = new List<Conversation>();
            lock (_lock)
            {
                foreach (var (environment, config) in folders)
                {
                    foreach (var file in Files(config))
                    {
                        if (ReadCached(file, environment) is { } conversation)
                        {
                            found.Add(conversation);
                        }
                    }
                }
            }

            return (IReadOnlyList<Conversation>)found;
        });

    /// The top files of each project folder; the folders inside hold the conversations of subagents.
    private static IEnumerable<string> Files(string config)
    {
        var projects = Path.Combine(config, "projects");
        if (!Directory.Exists(projects))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(projects)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.jsonl", SearchOption.TopDirectoryOnly))
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private Conversation? ReadCached(string path, string environment)
    {
        try
        {
            var info = new FileInfo(path);
            if (_read.TryGetValue(path, out var known) && known.Written == info.LastWriteTimeUtc && known.Length == info.Length)
            {
                return known.Conversation;
            }

            var (head, tail) = Pieces(path, info.Length);
            var conversation = ConversationFile.Read(
                Path.GetFileNameWithoutExtension(path), environment, head, tail,
                new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds());
            _read[path] = (info.LastWriteTimeUtc, info.Length, conversation);
            return conversation;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// The first and the last pieces of a file. Claude Code may be writing it: it is opened for
    /// reading only, sharing everything.
    private static (string Head, string Tail) Pieces(string path, long length)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var head = Piece(stream, 0, (int)Math.Min(length, ConversationFile.HeadBytes));
        var tailLength = (int)Math.Min(length, ConversationFile.TailBytes);
        var tail = Piece(stream, length - tailLength, tailLength);
        return (head, tail);
    }

    private static string Piece(FileStream stream, long from, int count)
    {
        var bytes = new byte[count];
        stream.Seek(from, SeekOrigin.Begin);
        var read = 0;
        while (read < count && stream.Read(bytes, read, count - read) is var got and > 0)
        {
            read += got;
        }

        return Encoding.UTF8.GetString(bytes, 0, read);
    }
}
