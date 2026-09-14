namespace Aiko.Core;

/// What has to change in Aiko's command folder so it holds exactly the commands of the environments.
///
/// The folder holds claude.exe, the shim, and one link per command: aiko-work.exe, aiko-personal.exe.
/// Renaming an environment, a command typed by hand or a removed environment leaves a link behind
/// or asks for a new one. Anything else in the folder that is not a command of ours is left alone.
public sealed record CommandLinksPlan(IReadOnlyList<string> ToAdd, IReadOnlyList<string> ToRemove)
{
    public bool IsEmpty => ToAdd.Count == 0 && ToRemove.Count == 0;
}

public static class CommandLinks
{
    public const string ShimFileName = "claude.exe";

    public static string FileNameFor(string command) => command + ".exe";

    public static CommandLinksPlan Plan(EnvironmentSettings settings, IEnumerable<string> filesInFolder)
    {
        var wanted = settings.Environments
            .Select(e => FileNameFor(e.Command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var present = filesInFolder
            .Select(Path.GetFileName)
            .Where(name => name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(name => !name!.Equals(ShimFileName, StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .ToList();

        return new CommandLinksPlan(
            wanted.Where(w => !present.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList(),
            present.Where(p => !wanted.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList());
    }
}
