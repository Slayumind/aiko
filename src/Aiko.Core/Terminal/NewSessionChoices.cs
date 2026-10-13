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

/// What the form of a new session offers (D-293, D-300): the folders, the models and the name the
/// session gets when the field is left empty.
public static class NewSessionChoices
{
    public const int MostRecent = 5;

    /// The models of the form, as the words Claude Code takes for --model. They are the short
    /// names, which stay the same from one version of a model to the next. The form offers models
    /// only (D-308): a session always starts with one of them.
    public static readonly IReadOnlyList<string> Models = ["opus", "sonnet", "haiku"];

    /// The folders, each once: where sessions of the environment ran last, then every bound
    /// folder followed by the folders inside it. foldersInside gives the full paths of the folders
    /// right inside one; reading the disk is not done here.
    public static IReadOnlyList<FolderChoice> Folders(
        TerminalSessions sessions,
        AikoEnvironment environment,
        Func<string, IReadOnlyList<string>> foldersInside)
    {
        var choices = new List<FolderChoice>();

        void Offer(string path, FolderSource source)
        {
            var folder = SessionName.FolderPath(path);
            if (folder.Length > 0 && choices.All(choice => choice.Path != folder))
            {
                choices.Add(new FolderChoice(folder, SessionName.FolderName(folder), source));
            }
        }

        // OrderByDescending keeps the order of the list for sessions used at the same time.
        var recent = sessions.Sessions
            .Where(session => session.Environment == environment.Name)
            .OrderByDescending(session => session.LastUsed)
            .Select(session => session.Folder)
            .Distinct()
            .Take(MostRecent);

        foreach (var folder in recent)
        {
            Offer(folder, FolderSource.Recent);
        }

        foreach (var bound in environment.ProjectFolders)
        {
            Offer(bound, FolderSource.Bound);
            foreach (var inside in foldersInside(bound))
            {
                Offer(inside, FolderSource.Inside);
            }
        }

        return choices;
    }

    /// The model the form starts with: the one of the session of the environment that was used
    /// last and has a model of the list. With no such session it is the first of the list.
    public static string DefaultModel(TerminalSessions sessions, string environment) =>
        sessions.Sessions
            .Where(session => session.Environment == environment && session.Model is { } model && Models.Contains(model))
            .OrderByDescending(session => session.LastUsed)
            .Select(session => session.Model)
            .FirstOrDefault() ?? Models[0];

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
