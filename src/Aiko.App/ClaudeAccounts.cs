using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Reads which account a Claude Code folder belongs to. Only .claude.json is opened, never the
/// credentials file, and nothing read here is written to the log.
static class ClaudeAccounts
{
    public static ClaudeAccount Read(string folder)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var path in ClaudeConfigFolder.AccountFileCandidates(folder, home))
        {
            try
            {
                if (File.Exists(path))
                {
                    var account = ClaudeAccount.FromClaudeJson(File.ReadAllText(path));
                    if (account.IsKnown)
                    {
                        return account;
                    }
                }
            }
            catch (IOException)
            {
                // Claude Code may be writing the file right now. The next read will get it.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return ClaudeAccount.None;
    }
}
