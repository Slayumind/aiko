namespace Aiko.Core.Terminal;

/// Why a folder is offered in the form of a new session.
public enum FolderSource
{
    /// A session ran there not long ago.
    Recent,

    /// The folder is bound to the environment.
    Bound,

    /// The folder lies right inside a bound one.
    Inside,
}

public sealed record FolderChoice(string Path, string Name, FolderSource Source);

/// The two groups of the folder list: where sessions ran, and the folders of the environment.
public enum FolderGroup
{
    Recent,
    Bound,
}

/// A group title in the folder list, drawn above the folder at Before.
public sealed record FolderHeader(int Before, FolderGroup Group);

/// A model as Claude Code names it in its answer to "initialize": the word for --model, and the
/// name to show, like "fable" and "Fable 5.1".
public sealed record ClaudeModel(string Value, string DisplayName);

/// What the form of a new session offers (D-293, D-300): the folders, the models and the name the
/// session gets when the field is left empty.
public static class NewSessionChoices
{
    public const int MostRecent = 5;

    /// The folders offered from inside one bound folder: the ones changed last (D-335). A bound
    /// folder such as a desktop of projects holds dozens, and a list of sixty took a moment to
    /// open; the others are one "choose a folder" away.
    public const int MostInside = 8;

    /// The models of the form until Claude Code named its own, as the words it takes for --model.
    /// They are the short names, which stay the same from one version of a model to the next.
    /// The form offers models only (D-308): a session always starts with one of them.
    public static readonly IReadOnlyList<string> Models = ["opus", "sonnet", "haiku"];

    /// The models the form offers, from the ones Claude Code named: only the short names, in its
    /// order (AIKO-387). A new family of models comes with no change in Aiko. "default" and the
    /// full ids of single versions stay out. When Claude Code named none, the list above.
    public static IReadOnlyList<ClaudeModel> Offered(IReadOnlyList<ClaudeModel> named)
    {
        var shortNames = named.Where(model => model.Value != "default" && model.Value.All(char.IsAsciiLetterLower)).ToList();
        return shortNames.Count > 0
            ? shortNames
            : [.. Models.Select(model => new ClaudeModel(model, char.ToUpperInvariant(model[0]) + model[1..]))];
    }

    /// The folders, each once whatever the case of their letters: where sessions of the
    /// environment ran last, then every bound folder followed by up to MostInside folders inside
    /// it. foldersInside gives the full paths of the folders right inside one, the ones changed
    /// last first; reading the disk is not done here.
    public static IReadOnlyList<FolderChoice> Folders(
        TerminalSessions sessions,
        AikoEnvironment environment,
        Func<string, IReadOnlyList<string>> foldersInside)
    {
        var choices = new List<FolderChoice>();

        bool Offer(string path, FolderSource source)
        {
            var folder = SessionName.FolderPath(path);
            if (folder.Length == 0 || choices.Any(choice => string.Equals(choice.Path, folder, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            choices.Add(new FolderChoice(folder, SessionName.FolderName(folder), source));
            return true;
        }

        // OrderByDescending keeps the order of the list for sessions used at the same time.
        var recent = sessions.Sessions
            .Where(session => session.Environment == environment.Name)
            .OrderByDescending(session => session.LastUsed)
            .Select(session => SessionName.FolderPath(session.Folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MostRecent);

        foreach (var folder in recent)
        {
            Offer(folder, FolderSource.Recent);
        }

        foreach (var bound in environment.ProjectFolders)
        {
            Offer(bound, FolderSource.Bound);
            var offered = 0;
            foreach (var inside in foldersInside(bound))
            {
                if (offered == MostInside)
                {
                    break;
                }

                if (Offer(inside, FolderSource.Inside))
                {
                    offered++;
                }
            }
        }

        return choices;
    }

    /// The group titles of a folder list: one above the first folder of each group. A folder
    /// inside a bound one is in the group of the bound folders.
    public static IReadOnlyList<FolderHeader> Headers(IReadOnlyList<FolderChoice> folders)
    {
        var headers = new List<FolderHeader>();
        for (var at = 0; at < folders.Count; at++)
        {
            var group = folders[at].Source == FolderSource.Recent ? FolderGroup.Recent : FolderGroup.Bound;
            if (headers.Count == 0 || headers[^1].Group != group)
            {
                headers.Add(new FolderHeader(at, group));
            }
        }

        return headers;
    }

    /// The model the form starts with: the one of the session of the environment that was used
    /// last and has a model of the list. With no such session it is the first of the list.
    /// models are the short names offered; without them, the list above.
    public static string DefaultModel(TerminalSessions sessions, string environment, IReadOnlyList<string>? models = null)
    {
        var list = models is { Count: > 0 } ? models : Models;
        return sessions.Sessions
            .Where(session => session.Environment == environment && Family(session.Model, list) is not null)
            .OrderByDescending(session => session.LastUsed)
            .Select(session => Family(session.Model, list))
            .FirstOrDefault() ?? list[0];
    }

    /// The model after this one, for a click on the model under the input: in the order of the
    /// form, and back to the first after the last. A model outside the list goes to the first one.
    public static string NextModel(string? model, IReadOnlyList<string>? models = null)
    {
        var list = models is { Count: > 0 } ? models : Models;
        var at = Family(model, list) is { } family ? list.ToList().IndexOf(family) : -1;
        return list[(at + 1) % list.Count];
    }

    /// The model of the list a short name or a full id like "claude-sonnet-5-5" belongs to.
    private static string? Family(string? model, IReadOnlyList<string> list) =>
        list.FirstOrDefault(name => model?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);

    /// The name of the other environment a folder is bound to, or null. The form says so before
    /// the session is made: Claude Code started there from a console would use that environment.
    public static string? BoundElsewhere(string folder, AikoEnvironment environment, EnvironmentSettings settings) =>
        ProjectBinding.BoundEnvironmentFor(folder, settings) is { } other && other.Name != environment.Name
            ? other.Name
            : null;

    /// The name the session gets when the field is left empty.
    public static string DefaultName(TerminalSessions sessions, string environment, string folder) =>
        SessionName.Default(folder, sessions.NamesIn(environment, folder));
}
