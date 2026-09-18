using Aiko.Core;

namespace Aiko.App;

/// What the core needs to know about this Windows computer: how Windows names programs and lists
/// folders, and where Aiko's folders are.
///
/// The core describes the rules; this is the one place in the app that fills them in from Windows.
static class ThisComputer
{
    public static readonly PlatformConventions Platform = PlatformConventions.Windows;

    public static readonly AikoFolders Folders = AikoFolders.Windows(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static readonly WindowsSystemFolders SystemFolders = new(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows));
}
