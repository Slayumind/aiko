using Aiko.Core;

namespace Aiko.App;

/// Where Aiko's folders are on this Windows computer.
///
/// The core describes the layout; this is the one place in the app that fills it in from Windows.
static class ThisComputer
{
    public static readonly AikoFolders Folders = new(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
}
