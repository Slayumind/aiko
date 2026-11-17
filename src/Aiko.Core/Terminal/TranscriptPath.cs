using System.Text;

namespace Aiko.Core.Terminal;

/// Where Claude Code keeps the file of a conversation (D-303):
/// &lt;config folder&gt;/projects/&lt;name made of the working folder&gt;/&lt;id&gt;.jsonl.
///
/// The name of the folder is the path with every character that is not an English letter or a
/// digit turned into a dash: C:\Users\x\a b gives C--Users-x-a-b. Checked on Claude Code 2.1.296;
/// it is not documented.
public static class TranscriptPath
{
    public const string ProjectsFolderName = "projects";

    /// A longer name is cut here by Claude Code, which then adds a dash and a few characters of
    /// its own. Those cannot be worked out, so the folder has to be looked for by its start.
    public const int LongestName = 200;

    public static string FolderName(string workingDirectory)
    {
        var name = new StringBuilder(workingDirectory.Length);
        foreach (var unit in workingDirectory)
        {
            name.Append(unit is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' ? unit : '-');
        }

        return name.Length > LongestName ? name.ToString(0, LongestName) : name.ToString();
    }

    /// Whether the folder on disk has more at its end than FolderName gives.
    public static bool IsCut(string workingDirectory) => workingDirectory.Length > LongestName;

    public static string FileName(string sessionId) => sessionId + ".jsonl";

    public static string ProjectsFolder(PlatformConventions platform, string configDirectory) =>
        platform.Join(configDirectory, ProjectsFolderName);

    /// The file of a conversation, when the name of its folder was not cut.
    public static string For(PlatformConventions platform, string configDirectory, string workingDirectory, string sessionId) =>
        platform.Join(configDirectory, ProjectsFolderName, FolderName(workingDirectory), FileName(sessionId));
}
