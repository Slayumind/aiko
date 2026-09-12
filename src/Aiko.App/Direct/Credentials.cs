using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Reads the Claude Code credentials file into memory for one request.
///
/// The rules around this file are strict, and they are the reason this class does so little:
/// it is read again before every request and never kept, never copied, never logged and never
/// written back. Aiko does not refresh the token either: a refresh replaces the refresh token and
/// could log Claude Code out.
static class Credentials
{
    private const string FileName = ".credentials.json";

    public static CredentialFile? Read(string configDirectory)
    {
        var path = Path.Combine(configDirectory, FileName);

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return CredentialFile.TryParse(File.ReadAllText(path), out var credential) ? credential : null;
        }
        catch (IOException)
        {
            // Claude Code may be writing the file right now. The next round will read it.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
