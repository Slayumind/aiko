namespace Aiko.Core;

/// What has to change in Aiko's command folder so it holds exactly the commands of the environments.
///
/// The folder holds the shim under Claude Code's own name, and one link per command: aiko-work,
/// aiko-personal (claude.exe, aiko-work.exe on Windows). Renaming an environment, a command typed by
/// hand or a removed environment leaves a link behind or asks for a new one. Anything else in the
/// folder that is not a command of ours is left alone.
public sealed record CommandLinksPlan(IReadOnlyList<string> ToAdd, IReadOnlyList<string> ToRemove)
{
    public bool IsEmpty => ToAdd.Count == 0 && ToRemove.Count == 0;
}

public static class CommandLinks
{
    public static string ShimFileName(PlatformConventions platform) => platform.ExecutableName(ShimLaunch.ClaudeName);

    public static string FileNameFor(PlatformConventions platform, string command) => platform.ExecutableName(command);

    public static CommandLinksPlan Plan(PlatformConventions platform, EnvironmentSettings settings, IEnumerable<string> filesInFolder)
    {
        var wanted = settings.Environments
            .Select(e => FileNameFor(platform, e.Command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var shim = ShimFileName(platform);
        var present = filesInFolder
            .Select(Path.GetFileName)
            .Where(name => name is not null && name.EndsWith(platform.ExecutableSuffix, StringComparison.OrdinalIgnoreCase))
            .Where(name => !name!.Equals(shim, StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .ToList();

        return new CommandLinksPlan(
            wanted.Where(w => !present.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList(),
            present.Where(p => !wanted.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList());
    }
}
