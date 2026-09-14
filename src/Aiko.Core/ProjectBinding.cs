namespace Aiko.Core;

/// Which environment Claude Code should start in, for the folder it is started from.
///
/// It works like includeIf in git: binding D:\work covers every folder inside it. When two bound
/// folders contain the working folder, the deeper one wins, so D:\work\personal-side-project can
/// belong to the other environment. A folder bound nowhere gets the default environment.
public static class ProjectBinding
{
    public static AikoEnvironment? EnvironmentFor(
        string workingDirectory,
        EnvironmentSettings settings,
        string userProfile) =>
        BoundEnvironmentFor(workingDirectory, settings) ?? settings.Default(userProfile);

    /// Only a real binding, with no default to fall back on.
    public static AikoEnvironment? BoundEnvironmentFor(string workingDirectory, EnvironmentSettings settings)
    {
        AikoEnvironment? best = null;
        var bestLength = -1;

        foreach (var environment in settings.Environments)
        {
            foreach (var folder in environment.ProjectFolders)
            {
                var root = Normalize(folder);
                if (root.Length > bestLength && Contains(root, Normalize(workingDirectory)))
                {
                    best = environment;
                    bestLength = root.Length;
                }
            }
        }

        return best;
    }

    /// D:\work contains D:\work and D:\work\app, and not D:\workshop.
    private static bool Contains(string root, string path) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || (root.EndsWith(Path.DirectorySeparatorChar) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string path)
    {
        var unified = path.Trim().Replace('/', Path.DirectorySeparatorChar);

        // "D:\" must keep its separator, or it turns into "D:", which means the current folder on D.
        return unified.Length > 3 ? Path.TrimEndingDirectorySeparator(unified) : unified;
    }
}
