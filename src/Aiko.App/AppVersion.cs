using System.Reflection;

namespace Aiko.App;

/// The version of the running Aiko, as three numbers.
///
/// Two places ask now, the settings window and the daily update check, and they have to agree:
/// comparing two differently shaped version strings is how a copy decides it is up to date when it
/// is not.
static class AppVersion
{
    public static string Current() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "unknown";
}
