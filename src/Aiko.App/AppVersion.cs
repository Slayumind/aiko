using System.Reflection;

namespace Aiko.App;

/// The version of the running Aiko.
///
/// Two places compare it, the settings window and the daily update check, and they have to agree:
/// comparing two differently shaped version strings is how a copy decides it is up to date when it
/// is not. So Current() is always three numbers, and the commit is only for people to read.
static class AppVersion
{
    public static string Current() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "unknown";

    /// Three numbers and the short commit the build came from, like 0.2.1+42cb34b. Between releases
    /// the version does not change (D-236), so this is what tells two builds apart in a bug report.
    /// The .NET SDK writes the commit into the informational version when it builds from git.
    public static string WithCommit()
    {
        var informational = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var plus = informational?.IndexOf('+') ?? -1;
        if (informational is null || plus < 0)
        {
            return Current();
        }

        var commit = informational[(plus + 1)..];
        return $"{Current()}+{commit[..Math.Min(7, commit.Length)]}";
    }
}
